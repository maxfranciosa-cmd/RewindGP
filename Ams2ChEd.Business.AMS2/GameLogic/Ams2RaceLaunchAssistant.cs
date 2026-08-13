using Ams2ChEd.Business.AMS2.Helpers;
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
        private readonly IGameInstallSettingsStorage _installSettingsStorage;
        private readonly IRacePreparator _racePreparator;
        private readonly IAms2GrandPrixTrackResolver _trackResolver;
        private readonly IAms2HashCatalogProvider _hashCatalogProvider;
        private readonly IRaceSetupAdvisor _raceSetupAdvisor;

        public Ams2RaceLaunchAssistant(
            IGameInstallSettingsStorage installSettingsStorage,
            IRacePreparator racePreparator,
            IAms2GrandPrixTrackResolver trackResolver,
            IAms2HashCatalogProvider hashCatalogProvider,
            IRaceSetupAdvisor raceSetupAdvisor)
        {
            _installSettingsStorage = installSettingsStorage;
            _racePreparator = racePreparator;
            _trackResolver = trackResolver;
            _hashCatalogProvider = hashCatalogProvider;
            _raceSetupAdvisor = raceSetupAdvisor;
        }

        public async Task<bool> ShowSetupOverlayAsync(RaceLaunchRequest request, object ownerWindow, CancellationToken ct = default)
        {
            if (!Ams2Launcher.IsRunning())
            {
                Ams2Launcher.Launch();
                var launched = await Ams2Launcher.WaitForProcessAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(true);
                if (!launched)
                {
                    // AMS2 never came up - don't show the overlay at all, let the caller fall back
                    // to its manual-instructions path instead.
                    return false;
                }
            }

            var overlay = new RaceSetupOverlayWindow();
            var tracker = new Ams2WindowTracker(overlay);
            tracker.ProcessLost += (_, _) => overlay.ShowError("AMS2 isn't running anymore.");

            overlay.Show();
            tracker.Start();

            try
            {
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
                tracker.Dispose();
                overlay.Close();
                tracker.RestoreGameFocus();
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
            tracker.Start();

            await overlay.WaitForDismissedAsync().ConfigureAwait(true);
        }

        private async Task<bool> TryAutoConfigureAsync(RaceLaunchRequest request, RaceSetupOverlayWindow overlay, CancellationToken ct)
        {
            var race = request.Season.Races.FirstOrDefault(r => r.RaceId == request.RaceId);
            if (race == null)
            {
                overlay.ShowError("Couldn't find this race in the current season.");
                return false;
            }

            var seasonYear = request.Season.OriginalYear ?? request.Season.Year;
            var trackResolution = _trackResolver.ResolveTrack(race.RaceName, race.RaceShortName, seasonYear);
            if (trackResolution == null)
            {
                overlay.ShowError($"No track is configured yet for \"{race.RaceName}\".");
                return false;
            }

            if (!_hashCatalogProvider.TrackHashes.ContainsKey(trackResolution.TrackId))
            {
                overlay.ShowError($"Track \"{trackResolution.TrackId}\" isn't in the track catalog yet.");
                return false;
            }

            var carSelection = (_racePreparator as Ams2RacePreparator)?.GetPlayerCarSelection(
                request.RaceId, request.EntryList, request.Drivers, request.Season, request.PlayerDriverId);
            if (carSelection == null)
            {
                overlay.ShowError("Couldn't resolve your car/livery for this race.");
                return false;
            }

            if (!_hashCatalogProvider.CarHashes.ContainsKey(carSelection.Value.CarModel))
            {
                overlay.ShowError($"Car \"{carSelection.Value.CarModel}\" isn't in the car catalog yet.");
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
                overlay.ShowError("Couldn't attach to AMS2 - make sure it's running and you're on the Custom Race screen.");
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
                overlay.ShowError("Some race settings couldn't be applied automatically - please double check them in-game.");
                return false;
            }

            return true;
        }

    }
}
