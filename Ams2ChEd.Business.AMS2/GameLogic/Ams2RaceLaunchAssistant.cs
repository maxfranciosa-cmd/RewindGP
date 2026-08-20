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
            // Show the overlay immediately, in its "launching" state, rather than only creating it
            // once AMS2's process is confirmed running - otherwise there's nothing on screen at all
            // between the caller's liveries-exported progress window closing and the process-launch
            // wait below (which can take up to a minute) resolving.
            var overlay = new RaceSetupOverlayWindow();
            overlay.ShowLaunching();
            overlay.Show();

            Ams2WindowTracker tracker = null;
            try
            {
                if (!Ams2Launcher.IsRunning())
                {
                    // Resolve DLC ownership (used later by TryAutoConfigureAsync's track resolution)
                    // now, while AMS2 is confirmed NOT running - see Ams2DlcOwnershipChecker's class
                    // doc comment for why doing this once AMS2 is already up is the thing to avoid.
                    await _dlcOwnershipChecker.WarmUpAsync().ConfigureAwait(true);

                    Ams2Launcher.Launch();
                    var launched = await Ams2Launcher.WaitForProcessAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(true);
                    if (!launched)
                    {
                        // AMS2 never came up - close the overlay (in the finally below) and let the
                        // caller fall back to its manual-instructions path instead.
                        return false;
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

            using var configurator = new Ams2RaceConfigurator(_hashCatalogProvider.CarHashes, _hashCatalogProvider.TrackHashes);
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

            return true;
        }

    }
}
