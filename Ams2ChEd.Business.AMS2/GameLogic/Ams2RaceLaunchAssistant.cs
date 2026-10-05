using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Resources;
using Ams2ChEd.Business.AMS2.Settings;
using Ams2ChEd.Business.AMS2.UI;
using Ams2Interop;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Settings.Contracts;
using System.Windows;

namespace Ams2ChEd.Business.AMS2.GameLogic
{
    /// <summary>
    /// Launches AMS2 (if needed), shows an overlay on top of it, and applies race settings via
    /// Ams2Interop.Ams2RaceConfigurator when the player clicks through. Anything it can't resolve
    /// (no track mapping for this Grand Prix, missing hash-catalog entries, the live apply itself
    /// failing) surfaces as an in-overlay error with a "continue manually" escape hatch - callers
    /// should keep their own existing manual-instructions fallback for that case.
    /// </summary>
    public class Ams2RaceLaunchAssistant : IRaceLaunchAssistant
    {
        // How long to keep a freshly-launched overlay hidden before revealing it - gives AMS2 time
        // to actually come to the foreground first, instead of the overlay flashing up over its own
        // loading/menu screens the moment the window is created.
        private static readonly TimeSpan OverlayRevealDelay = TimeSpan.FromSeconds(10);

        // The player's own tyre-wear/fuel-usage settings, held while a shortened race has them
        // scaled up (see ApplyWearScaling). Static because this class is registered transient -
        // the instance that restores them isn't the one that changed them.
        private static readonly object WearLock = new();
        private static WearSettingsSnapshot? _originalWearSettings;

        private readonly IGameInstallSettingsStorage _installSettingsStorage;
        private readonly IRacePreparator _racePreparator;
        private readonly IAms2GrandPrixTrackResolver _trackResolver;
        private readonly IAms2HashCatalogProvider _hashCatalogProvider;
        private readonly IRaceSetupAdvisor _raceSetupAdvisor;
        private readonly IAms2DlcOwnershipChecker _dlcOwnershipChecker;

        public Ams2RaceLaunchAssistant(
            IGameInstallSettingsStorage installSettingsStorage,
            IRacePreparator racePreparator,
            IAms2GrandPrixTrackResolver trackResolver,
            IAms2HashCatalogProvider hashCatalogProvider,
            IRaceSetupAdvisor raceSetupAdvisor,
            IAms2DlcOwnershipChecker dlcOwnershipChecker)
        {
            _installSettingsStorage = installSettingsStorage;
            _racePreparator = racePreparator;
            _trackResolver = trackResolver;
            _hashCatalogProvider = hashCatalogProvider;
            _raceSetupAdvisor = raceSetupAdvisor;
            _dlcOwnershipChecker = dlcOwnershipChecker;
        }

        public async Task<bool> ShowSetupOverlayAsync(RaceLaunchRequest request, object ownerWindow, CancellationToken ct = default)
        {
            var autoLaunch = (_installSettingsStorage as Ams2GameInstallSettingsStorage)?.LoadAutoLaunchGame() ?? true;

            // Show the overlay immediately, in its initial state, rather than only creating it once
            // AMS2's process is confirmed running - otherwise there's nothing on screen at all
            // between the caller's liveries-exported progress window closing and the process wait
            // below (which can take up to a minute, or be unbounded if waiting on the player)
            // resolving. Which initial state depends on whether this app is about to launch AMS2
            // itself or wait for the player to do it - showing "Launching..." when nothing was
            // actually launched would be misleading.
            var overlay = new RaceSetupOverlayWindow();
            if (autoLaunch)
            {
                overlay.ShowLaunching();
            }
            else
            {
                overlay.ShowWaitingForManualLaunch();
            }
            overlay.Show();

            Ams2WindowTracker tracker = null;
            try
            {
                if (!Ams2Launcher.IsRunning())
                {
                    // Resolve DLC ownership (used later by TryAutoConfigureAsync's track resolution)
                    // now, while AMS2 is confirmed NOT running - see Ams2DlcOwnershipChecker's class
                    // doc comment for why doing this once AMS2 is already up is the thing to avoid.
                    // Holds regardless of which branch below runs next.
                    await _dlcOwnershipChecker.WarmUpAsync().ConfigureAwait(true);

                    if (autoLaunch)
                    {
                        Ams2Launcher.Launch();
                        var launched = await Ams2Launcher.WaitForProcessAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(true);
                        if (!launched)
                        {
                            // AMS2 never came up - close the overlay (in the finally below) and let
                            // the caller fall back to its manual-instructions path instead.
                            return false;
                        }
                    }
                    else
                    {
                        // No deadline for a wait on a person to act - race it against the overlay's
                        // Skip link so the player isn't stuck if they change their mind (e.g.
                        // they're loading a different game instead). Uses its OWN linked
                        // CancellationTokenSource so Skip can actually stop the polling loop -
                        // without this, WaitForProcessAsync would keep calling
                        // Process.GetProcessesByName every second in the background indefinitely,
                        // tied only to this method's own `ct`, long after the player moved on.
                        using var manualLaunchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var processWait = Ams2Launcher.WaitForProcessAsync(manualLaunchCts.Token);
                        var userActionWait = overlay.WaitForUserActionAsync();

                        var completed = await Task.WhenAny(processWait, userActionWait).ConfigureAwait(true);
                        if (completed == userActionWait)
                        {
                            // Only Skip can resolve this while still waiting for the process. Stop
                            // the poll - the player isn't launching AMS2 right now.
                            manualLaunchCts.Cancel();
                            await ShowManualInstructionsAsync(request, overlay, ct).ConfigureAwait(true);
                            return false;
                        }

                        if (!await processWait.ConfigureAwait(true))
                        {
                            return false;
                        }
                    }
                }

                // Only start tracking/positioning the overlay once AMS2's process is confirmed
                // running - starting any earlier would make Ams2WindowTracker.UpdatePosition find no
                // process and immediately fire ProcessLost.
                tracker = new Ams2WindowTracker(overlay);
                tracker.ProcessLost += (_, _) => overlay.ShowError(Strings.Ams2RaceLaunchAssistant_ProcessLost);
                tracker.Start();

                // Keep the launching message up a little longer once AMS2's window is found, so the
                // Configure/Skip prompt doesn't pop up over AMS2's own loading/menu transition.
                await Task.Delay(OverlayRevealDelay, ct).ConfigureAwait(true);
                overlay.ShowPrompt();

                var action = await overlay.WaitForUserActionAsync().WaitAsync(ct).ConfigureAwait(true);
                if (action == RaceSetupOverlayAction.Skip)
                {
                    await ShowManualInstructionsAsync(request, overlay, ct).ConfigureAwait(true);
                    return false;
                }

                overlay.ShowWaiting();

                if (await TryAutoConfigureAsync(request, overlay, ct).ConfigureAwait(true))
                {
                    await overlay.WaitForAutoConfigureConfirmedAsync().WaitAsync(ct).ConfigureAwait(true);
                    return true;
                }

                // TryAutoConfigureAsync already put the overlay into its error state; wait for the
                // player to dismiss it ("continue manually" is the only action available there), then
                // show the manual setup instructions on the same overlay before closing.
                await overlay.WaitForUserActionAsync().WaitAsync(ct).ConfigureAwait(true);
                await ShowManualInstructionsAsync(request, overlay, ct).ConfigureAwait(true);
                return false;
            }
            finally
            {
                tracker?.Dispose();
                overlay.Close();
                tracker?.RestoreGameFocus();
            }
        }

        /// <summary>
        /// Computes the same car/livery/opponents/difficulty info the auto-configure path resolves
        /// and shows it as "set it up yourself" instructions on the overlay itself, so the player
        /// never has to leave the game window for a separate instructions dialog.
        /// </summary>
        private async Task ShowManualInstructionsAsync(RaceLaunchRequest request, RaceSetupOverlayWindow overlay, CancellationToken ct)
        {
            var carName = _raceSetupAdvisor.GetCarDisplayName(request.Season, request.PlayerTeamId, request.PlayerDriverSlot);
            var usesPerformanceScalars = _raceSetupAdvisor.SeasonUsesPerformanceScalars(request.Season);
            var suggestedDifficulty = _raceSetupAdvisor.GetSuggestedAiDifficulty(
                request.Season, request.PlayerTeamId, request.PlayerDriverSlot, request.IsPreQuali ? request.EntryList : null);

            var playerEntry = request.EntryList.FirstOrDefault(e =>
                e.Driver1Id == request.PlayerDriverId || e.Driver2Id == request.PlayerDriverId);
            var playerNumber = playerEntry?.Driver1Id == request.PlayerDriverId
                ? playerEntry.Driver1Number
                : playerEntry?.Driver2Number ?? 0;
            var teamName = request.Season.Teams.FirstOrDefault(t => t.TeamId == request.PlayerTeamId)?.TeamName;
            var playerName = request.Drivers.FirstOrDefault(d => d.DriverId == request.PlayerDriverId)?.Name;
            var liveryName = $"#{playerNumber} {teamName} - {playerName}";
            var opponentsNumber = request.EntryList.DriverCount() - 1;

            await overlay.WaitForManualInstructionsDismissedAsync(
                carName, liveryName, opponentsNumber, suggestedDifficulty, usesPerformanceScalars, request.IsPreQuali)
                .WaitAsync(ct).ConfigureAwait(true);
        }

        public async Task ShowReturnOverlayAsync(object ownerWindow)
        {
            if (!Ams2Launcher.IsRunning())
            {
                // AMS2 isn't actually running (it may have already been closed, or crashed, before
                // this session-finished notification got processed) - there's nothing to show an
                // "in front of the game" overlay over, and doing so anyway leaves a stray topmost
                // window on screen that never goes away on its own.
                return;
            }

            await RestoreWearSettingsAsync().ConfigureAwait(true);

            var overlay = new RaceReturnOverlayWindow(ownerWindow as Window);
            var tracker = new Ams2WindowTracker(overlay);
            tracker.ProcessLost += (_, _) => overlay.DismissWithoutReturning();
            overlay.Closed += (_, _) => tracker.Dispose();

            overlay.Show();
            overlay.Visibility = Visibility.Hidden;
            tracker.Start();

            var dismissed = overlay.WaitForDismissedAsync();

            // Same reveal delay as the setup overlay - avoid flashing up over AMS2's own
            // post-session screens the instant the session-finished event comes in. If the overlay
            // gets dismissed on its own during the wait (e.g. ProcessLost firing), don't bother
            // revealing it at all.
            await Task.WhenAny(dismissed, Task.Delay(OverlayRevealDelay)).ConfigureAwait(true);
            if (!dismissed.IsCompleted)
            {
                overlay.Visibility = Visibility.Visible;
            }

            await dismissed.ConfigureAwait(true);
        }

        private async Task<bool> TryAutoConfigureAsync(RaceLaunchRequest request, RaceSetupOverlayWindow overlay, CancellationToken ct)
        {
            var race = request.Season.Races.FirstOrDefault(r => r.RaceId == request.RaceId);
            if (race == null)
            {
                overlay.ShowError(Strings.Ams2RaceLaunchAssistant_RaceNotFound);
                return false;
            }

            var seasonYear = request.Season.OriginalYear ?? request.Season.Year;
            var trackResolution = _trackResolver.ResolveTrack(race.RaceName, race.RaceShortName, seasonYear);
            if (trackResolution == null)
            {
                overlay.ShowError(string.Format(Strings.Ams2RaceLaunchAssistant_TrackNotConfigured_Format, race.RaceName));
                return false;
            }

            if (!_hashCatalogProvider.TrackHashes.ContainsKey(trackResolution.TrackId))
            {
                overlay.ShowError(string.Format(Strings.Ams2RaceLaunchAssistant_TrackNotInCatalog_Format, trackResolution.TrackId));
                return false;
            }

            var carSelection = (_racePreparator as Ams2RacePreparator)?.GetPlayerCarSelection(
                request.RaceId, request.EntryList, request.Drivers, request.Season, request.PlayerDriverId);
            if (carSelection == null)
            {
                overlay.ShowError(Strings.Ams2RaceLaunchAssistant_CarSelectionFailed);
                return false;
            }

            if (!_hashCatalogProvider.CarHashes.ContainsKey(carSelection.Value.CarModel))
            {
                overlay.ShowError(string.Format(Strings.Ams2RaceLaunchAssistant_CarNotInCatalog_Format, carSelection.Value.CarModel));
                return false;
            }

            var raceLength = (_installSettingsStorage as Ams2GameInstallSettingsStorage)?.LoadRaceLength() ?? Ams2RaceLength.Default;
            var sessionRules = Ams2SessionRulesBuilder.BuildSessionRules(race, raceLength, trackResolution.DefaultNumberOfLaps);
            var opponents = Ams2SessionRulesBuilder.BuildOpponentsConfig(request.EntryList.DriverCount() - 1);
            // Same for both Pre-Quali and the Actual Race - see BuildQualifyingConfig's doc
            // comment. Practice is intentionally NOT configured at all here (left null) - whatever
            // the player already has selected for Practice is left exactly as-is.
            var qualifying = Ams2SessionRulesBuilder.BuildQualifyingConfig();

            using var configurator = new Ams2RaceConfigurator(_hashCatalogProvider.CarHashes, _hashCatalogProvider.TrackHashes, LogToFile);
            if (!await configurator.AttachAsync(ct).ConfigureAwait(true))
            {
                overlay.ShowError(Strings.Ams2RaceLaunchAssistant_AttachFailed);
                return false;
            }

            var result = await configurator.ApplyRaceConfigAsync(
                carSelection.Value.LiveryNumber,
                carSelection.Value.CarModel,
                trackResolution.TrackId,
                opponents,
                sessionRules,
                practice: null,
                qualifying: qualifying,
                ct: ct).ConfigureAwait(true);

            if (!result.Success)
            {
                overlay.ShowError(Strings.Ams2RaceLaunchAssistant_ApplyFailed);
                return false;
            }

            // Pre-Quali is a qualifying-only session - nothing to scale there.
            if (!request.IsPreQuali)
            {
                ApplyWearScaling(configurator, raceLength);
            }

            return true;
        }

        /// <summary>
        /// Scales AMS2's own tyre-wear/fuel-usage settings to match a shortened race (see
        /// Ams2SessionRulesBuilder.GetWearMultiplier). Best-effort: a failure here just leaves the
        /// player's own settings in place, it never fails the race setup. Undone by
        /// RestoreWearSettingsAsync once the race is over.
        /// </summary>
        private static void ApplyWearScaling(Ams2RaceConfigurator configurator, Ams2RaceLength raceLength)
        {
            if (Ams2SessionRulesBuilder.GetWearMultiplier(raceLength) is not int multiplier)
            {
                return;
            }

            if (!configurator.TryApplyWearMultipliers(multiplier, multiplier, out var original))
            {
                LogToFile($"wear scaling x{multiplier} NOT applied");
                return;
            }

            lock (WearLock)
            {
                // Keep the first snapshot if one is already pending (e.g. the race was set up
                // twice without finishing) - a later one would just be our own scaled values.
                _originalWearSettings ??= original;
            }
        }

        /// <summary>Puts the player's own tyre-wear/fuel-usage settings back, if ApplyWearScaling changed them.</summary>
        private static async Task RestoreWearSettingsAsync()
        {
            WearSettingsSnapshot original;
            lock (WearLock)
            {
                if (_originalWearSettings is not WearSettingsSnapshot pending)
                {
                    return;
                }
                original = pending;
            }

            try
            {
                using var configurator = new Ams2RaceConfigurator(
                    new Dictionary<string, int>(), new Dictionary<string, int>(), LogToFile);
                if (!await configurator.AttachAsync().ConfigureAwait(true) || !configurator.TryRestoreWearSettings(original))
                {
                    LogToFile("wear settings NOT restored - will retry after the next session");
                    return;
                }
            }
            catch (Exception ex)
            {
                LogToFile($"wear settings restore failed: {ex.Message}");
                return;
            }

            lock (WearLock)
            {
                _originalWearSettings = null;
            }
        }

        /// <summary>
        /// Ams2RaceConfigurator/VmResolver/SessionVmResolver's diagnostic sink - there's no
        /// general-purpose logging infrastructure elsewhere in the app, so this writes a plain
        /// append-only file so a failed live-apply can be diagnosed after the fact instead of
        /// blind. Best-effort only: a logging failure must never break race setup.
        /// </summary>
        private static void LogToFile(string message)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RewindGP", "ams2interop.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.AppendAllText(path, $"{DateTime.Now:O} {message}{Environment.NewLine}");
            }
            catch
            {
                // best-effort diagnostic logging only
            }
        }
    }
}
