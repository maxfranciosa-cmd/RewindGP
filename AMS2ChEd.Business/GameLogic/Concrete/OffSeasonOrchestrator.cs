using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using System.Globalization;
using static AMS2ChEd.Business.Services.OffSeasonMovements;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    public class OffSeasonOrchestrator : IOffSeasonOrchestrator
    {
        private readonly IGameDataFactory _dataFactory;
        private readonly GameLogicFactory _gameLogicFactory;

        public OffSeasonOrchestrator(IGameDataFactory dataFactory, GameLogicFactory gameLogicFactory)
        {
            _dataFactory = dataFactory;
            _gameLogicFactory = gameLogicFactory;
        }

        public async Task RunAsync(ISaveGame saveGame, IOffSeasonUiCallbacks uiCallbacks)
        {
            int nextSeasonYear = saveGame.CurrentSeason.Year + 1;

            // STEP 1: Show championship celebration newspaper
            await uiCallbacks.ShowChampionshipCelebrationAsync(saveGame);

            await _gameLogicFactory.SeasonUpdaterOrchestrator.PrepareSeasonAsync(nextSeasonYear);

            var isNextSeasonAvailable = _dataFactory.SeasonLoader.GetAvailableSeasons().Contains(nextSeasonYear.ToString());

            if (!isNextSeasonAvailable)
            {
                await uiCallbacks.ShowSeasonUnavailableWarningAsync(nextSeasonYear);
            }

            var originalNewSeason = isNextSeasonAvailable
                ? LoadNewSeason(nextSeasonYear)
                : DuplicateCurrentSeasonAsNextSeason(saveGame.CurrentSeason, nextSeasonYear);

            // Load driver ratings database
            var newDriversSeason = isNextSeasonAvailable
                ? _dataFactory.DriversLoader.LoadDriversBase(nextSeasonYear)
                : new Dictionary<string, IDriverData>();

            // load the new season's data
            _gameLogicFactory.EndOfSeasonManager.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, newDriversSeason);

            // Execute team drops
            var dropResults = _gameLogicFactory.EndOfSeasonManager
                .ExecuteTeamDrops(saveGame, originalNewSeason)
                .ToList();

            // STEP 2: Show player contract letter
            var playerDropResult = GetPlayerDropResult(saveGame, dropResults);
            var playerReputation = GetPlayerCurrentReputation(saveGame);

            bool playerAcceptedContract = false;

            // if the player is employed by a team
            if (!string.IsNullOrEmpty(saveGame.PlayerData.TeamId))
            {
                playerAcceptedContract = await uiCallbacks.ShowContractLetterAsync(saveGame, originalNewSeason.Teams, playerDropResult, playerReputation);
            }

            // Update drop results if player rejected contract
            if (!playerAcceptedContract && !playerDropResult.IsDropped())
            {
                UpdateDropResultsForPlayerRejection(saveGame, dropResults);
            }

            // STEP 3: Generate potential team picks and driver proposals

            var newSeasonTeamEntries = originalNewSeason.Teams;
            var previouslyRetiredDriverIds = (saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>())
                .Select(d => d.DriverId)
                .ToHashSet();

            var ballots = _gameLogicFactory.EndOfSeasonManager
                .TeamPicksPotentialReplacementsDrivers(nextSeasonYear, saveGame, newSeasonTeamEntries, dropResults)
                .ToList();

            // STEP 3.5: Show a retirement send-off article for any driver who newly retired this off-season.
            // saveGame.CurrentSeason is still the season that just finished at this point, so their
            // last team can still be looked up from it before StartNewSeason (STEP 8) replaces it.
            var newlyRetiredDrivers = (saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>())
                .Where(d => !previouslyRetiredDriverIds.Contains(d.DriverId))
                .ToList();

            foreach (var retiredDriver in newlyRetiredDrivers)
            {
                var lastTeam = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => t.Driver1Contract.DriverId == retiredDriver.DriverId || t.Driver2Contract.DriverId == retiredDriver.DriverId);

                await uiCallbacks.ShowRetirementNewsAsync(saveGame, retiredDriver, lastTeam?.TeamId);
            }

            // STEP 4: If player needs to apply, show team selection window
            IEnumerable<TeamHiringBallot> finalBallots = ballots;

            if (!playerAcceptedContract)
            {
                var newPlayerReputation = saveGame.Drivers.First(d => d.DriverId == saveGame.PlayerData.DriverId).Reputation;
                var updatedBallots = await uiCallbacks.ShowTeamApplicationAsync(saveGame, ballots, dropResults, newPlayerReputation, originalNewSeason.Teams);
                finalBallots = updatedBallots ?? ballots;
            }

            // STEP 5: Generate new season with hirings
            var actualNewSeason = _gameLogicFactory.EndOfSeasonManager
                .GenerateNewSeasonWithNewHirings(saveGame, originalNewSeason, finalBallots);

            // STEP 6: Show final roster newspaper
            await uiCallbacks.ShowNewSeasonRosterAsync(saveGame, actualNewSeason);

            // STEP 7: if the player STILL hasn't got a team, ask if he'd like to create an absence
            if (string.IsNullOrEmpty(saveGame.PlayerData.TeamId))
            {
                bool createFictionalAbsence = await uiCallbacks.AskCreateFictionalAbsenceAsync();

                if (createFictionalAbsence)
                {
                    // if there are no absences at the first GP of the season
                    var firstRaceId = actualNewSeason.Races.First().RaceId;
                    if (!actualNewSeason.Absences.Any(a => a.RaceId == firstRaceId))
                    {
                        // create a new random absence in a midfield (or lower) team
                        var possibleTeams = actualNewSeason
                                        .Teams
                                        .Where(t => t.Reputation <= TeamReputation.MIDFIELD)
                                        .ToList();

                        var selectedTeam = possibleTeams.ElementAt(Random.Shared.Next(possibleTeams.Count));

                        var driverOut = selectedTeam.PickRandomDriverFromTheTeam();

                        actualNewSeason.Absences = actualNewSeason.Absences.Concat(new[]
                        {
                            new Absence
                            {
                                DriverOut = driverOut.DriverId,
                                RaceId = firstRaceId,
                                DriverIn = saveGame.PlayerData.DriverId,
                                TeamId = selectedTeam.TeamId,
                            }
                        });
                    }
                }
            }

            // STEP 8: Start new season
            _gameLogicFactory.EndOfSeasonManager.StartNewSeason(saveGame, actualNewSeason);

            // Update player team ID if changed
            UpdatePlayerTeamId(saveGame, actualNewSeason);

            // Save the game
            string saveName = $"{saveGame.PlayerData.Name}_{saveGame.CurrentSeason.Year}".Replace(" ", "_");
            _dataFactory.GameStorage.SaveGame(saveGame, saveName);
        }

        private ISeason DuplicateCurrentSeasonAsNextSeason(ISeason currentSeason, int nextSeasonYear)
        {
            var result = LoadNewSeason(currentSeason.OriginalYear ?? currentSeason.Year).DeepClone();

            result.OriginalYear = currentSeason.OriginalYear ?? currentSeason.Year;
            result.Year = nextSeasonYear;

            foreach (var race in result.Races)
            {
                var raceDate = DateTime.ParseExact(race.RaceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                race.RaceDate = GetClosestSundayNextYear(raceDate).ToString("yyyy-MM-dd");
            }

            return result;
        }

        private DateTime GetClosestSundayNextYear(DateTime date)
        {
            var d = date.AddYears(1);
            int next = ((int)DayOfWeek.Sunday - (int)d.DayOfWeek + 7) % 7;
            int prev = next == 0 ? 0 : 7 - next;
            return d.AddDays(next <= prev ? next : -prev);
        }

        // Get player's drop status from results
        private DriverFirerOutcome GetPlayerDropResult(ISaveGame saveGame, List<DropTeamResult> dropResults)
        {
            var playerTeam = saveGame.CurrentSeason.Teams.FirstOrDefault(t =>
                t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            if (playerTeam == null)
                return DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED;

            var teamDropResult = dropResults.FirstOrDefault(d => d.TeamId == playerTeam.TeamId);
            if (teamDropResult == null)
                return DriverFirerOutcome.NOT_DROPPED;

            // Check which driver the player is
            if (playerTeam.Driver1Contract.DriverId == saveGame.PlayerData.DriverId)
                return teamDropResult.DropDriver1;
            else
                return teamDropResult.DropDriver2;
        }

        // Get player's current reputation
        private DriverReputation GetPlayerCurrentReputation(ISaveGame saveGame)
        {
            var playerDriver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == saveGame.PlayerData.DriverId);
            if (playerDriver != null)
            {
                return playerDriver.Reputation;
            }
            return DriverReputation.PRIME_MIDFIELD;
        }

        // Update drop results if player rejects
        private void UpdateDropResultsForPlayerRejection(ISaveGame saveGame, List<DropTeamResult> dropResults)
        {
            var playerTeam = saveGame.CurrentSeason.Teams.FirstOrDefault(t =>
                t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            if (playerTeam == null) return;

            var teamDropResult = dropResults.FirstOrDefault(d => d.TeamId == playerTeam.TeamId);
            if (teamDropResult == null) return;

            if (playerTeam.Driver1Contract.DriverId == saveGame.PlayerData.DriverId)
                teamDropResult.DropDriver1 = DriverFirerOutcome.DROPPED_PLAYER_REJECTING;
            else
                teamDropResult.DropDriver2 = DriverFirerOutcome.DROPPED_PLAYER_REJECTING;
        }

        // Load teams for next season
        private ISeason LoadNewSeason(int seasonYear)
        {
            return _dataFactory.SeasonLoader.LoadBaseSeason(seasonYear);
        }

        // Update player's team ID after moves
        private void UpdatePlayerTeamId(ISaveGame saveGame, ISeason newSeason)
        {
            var playerTeam = newSeason.Teams.FirstOrDefault(t =>
                t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            if (playerTeam != null)
            {
                saveGame.PlayerData.TeamId = playerTeam.TeamId;
            }
        }
    }
}
