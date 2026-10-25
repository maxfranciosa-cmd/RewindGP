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

            // the save takes on the new season's data as it is now (or none, when it is unavailable
            // and the current one is reused), so data that turns up later still reaches it
            saveGame.SeasonDataStamp = _dataFactory.SeasonLoader.GetSeasonDataStamp(nextSeasonYear);

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

            // a player whose driver died in a crash gets no letters and applies nowhere: the save
            // carries on with every race simulated
            var playerDeceased = saveGame.IsPlayerDeceased();

            // if the player is employed by a team
            if (!playerDeceased && !string.IsNullOrEmpty(saveGame.PlayerData.TeamId))
            {
                playerAcceptedContract = await uiCallbacks.ShowContractLetterAsync(saveGame, originalNewSeason.Teams, playerDropResult, playerReputation);
            }

            // Update drop results if player rejected contract
            if (!playerAcceptedContract && !playerDropResult.IsDropped())
            {
                UpdateDropResultsForPlayerRejection(saveGame, dropResults);
            }

            // STEP 2.5: which teams run a reserve next season (reserve contracts last one season, so
            // it's decided again every winter), and the letter to a player who was a team's reserve:
            // a promotion to a race seat, staying on, or the market. Settled before the teams' picks
            var reservePlan = new ReservePlan
            {
                TeamsWantingReserve = EndOfSeasonManager.RollReserveWishes(originalNewSeason.Teams, Random.Shared)
            };

            var playerReserveTeam = playerDeceased ? null : saveGame.CurrentSeason.ReserveTeamOf(saveGame.PlayerData.DriverId);
            if (playerReserveTeam != null && originalNewSeason.Teams.Any(t => t.TeamId == playerReserveTeam.TeamId))
            {
                var reserveLetter = new ReserveLetter
                {
                    TeamId = playerReserveTeam.TeamId,
                    Promotion = _gameLogicFactory.EndOfSeasonManager.GetReservePromotion(saveGame, originalNewSeason.Teams, dropResults, playerReserveTeam.TeamId, playerReputation),
                    TeamWantsReserve = reservePlan.TeamsWantingReserve.Contains(playerReserveTeam.TeamId)
                };

                var decision = await uiCallbacks.ShowReserveLetterAsync(saveGame, originalNewSeason.Teams, reserveLetter, playerReputation);

                if (decision == ReserveLetterDecision.AcceptedPromotion && reserveLetter.Promotion?.IsOffered == true)
                {
                    // the seat is his: no applications needed
                    reservePlan.PlayerPromotion = reserveLetter.Promotion;
                    playerAcceptedContract = true;
                }
                else if (decision == ReserveLetterDecision.StaysReserve && reserveLetter.TeamWantsReserve)
                {
                    // he can still apply for race seats: winning one drops the reserve contract
                    reservePlan.PlayerStaysReserveAtTeamId = playerReserveTeam.TeamId;
                }
            }

            // STEP 3: Generate potential team picks and driver proposals

            var newSeasonTeamEntries = originalNewSeason.Teams;
            var previouslyRetiredDriverIds = (saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>())
                .Select(d => d.DriverId)
                .ToHashSet();

            var ballots = _gameLogicFactory.EndOfSeasonManager
                .TeamPicksPotentialReplacementsDrivers(nextSeasonYear, saveGame, newSeasonTeamEntries, dropResults, reservePlan)
                .ToList();

            // STEP 3.5: Show a retirement send-off article for any driver who newly retired this off-season.
            // saveGame.CurrentSeason is still the season that just finished at this point, so their
            // last team can still be looked up from it before StartNewSeason (STEP 8) replaces it.
            // Not for a driver who died in a crash: the race report was their article
            var newlyRetiredDrivers = (saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>())
                .Where(d => !previouslyRetiredDriverIds.Contains(d.DriverId) && !saveGame.IsDeceased(d.DriverId))
                .ToList();

            foreach (var retiredDriver in newlyRetiredDrivers)
            {
                var lastTeam = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => t.Driver1Contract.DriverId == retiredDriver.DriverId || t.Driver2Contract.DriverId == retiredDriver.DriverId);

                await uiCallbacks.ShowRetirementNewsAsync(saveGame, retiredDriver, lastTeam?.TeamId);
            }

            // STEP 4: If player needs to apply, show team selection window
            IEnumerable<TeamHiringBallot> finalBallots = ballots;

            if (!playerAcceptedContract && !playerDeceased)
            {
                var newPlayerReputation = saveGame.Drivers.First(d => d.DriverId == saveGame.PlayerData.DriverId).Reputation;
                var updatedBallots = await uiCallbacks.ShowTeamApplicationAsync(saveGame, ballots, dropResults, newPlayerReputation, originalNewSeason.Teams);
                finalBallots = updatedBallots ?? ballots;
            }

            // STEP 5: Generate new season with hirings
            var actualNewSeason = _gameLogicFactory.EndOfSeasonManager
                .GenerateNewSeasonWithNewHirings(saveGame, originalNewSeason, finalBallots);

            // STEP 5.5: Show a letter for each team the player applied to (rejections first,
            // then the acceptance if any), using the already-resolved actualNewSeason as the
            // source of truth so the letters never contradict the roster reveal in STEP 6.
            if (!playerAcceptedContract && !playerDeceased)
            {
                var teamApplicationResults = BuildTeamApplicationResults(saveGame, finalBallots, actualNewSeason);
                var reputationForLetters = saveGame.Drivers.First(d => d.DriverId == saveGame.PlayerData.DriverId).Reputation;

                foreach (var result in teamApplicationResults.Where(r => !r.PlayerHired).Concat(teamApplicationResults.Where(r => r.PlayerHired)))
                {
                    await uiCallbacks.ShowTeamApplicationResultAsync(saveGame, actualNewSeason.Teams, result, reputationForLetters);
                }
            }

            // STEP 6: Show final roster newspaper
            await uiCallbacks.ShowNewSeasonRosterAsync(saveGame, actualNewSeason);

            // STEP 7: Start new season
            _gameLogicFactory.EndOfSeasonManager.StartNewSeason(saveGame, actualNewSeason);

            // Update player team ID if changed. A pay driver still without a seat is offered rides
            // when each race's entry list is drawn up
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

        // Resolve one TeamApplicationResult per ballot the player applied to, reading the
        // outcome straight from the already-generated actualNewSeason (never recomputing the
        // ballot ourselves - FinalBallotResults uses its own Random() and could disagree).
        public static List<TeamApplicationResult> BuildTeamApplicationResults(ISaveGame saveGame, IEnumerable<TeamHiringBallot> finalBallots, ISeason actualNewSeason)
        {
            var results = new List<TeamApplicationResult>();
            var playerDriverId = saveGame.PlayerData.DriverId;
            var seatedDriverIds = actualNewSeason.RaceDriverIds();
            seatedDriverIds.UnionWith(actualNewSeason.ReserveDriverIds());

            foreach (var ballot in finalBallots)
            {
                // a seat agreed with the player beforehand (a promotion, staying on as reserve) was
                // never an application: no letter
                if (ballot.OriginalTeamHiring.Guaranteed) continue;

                bool playerApplied = ballot.OriginalTeamHiring.DriverId == playerDriverId
                    || (ballot.Candidates?.Any(c => c.DriverId == playerDriverId) ?? false);

                if (!playerApplied) continue;

                var teamId = ballot.OriginalTeamHiring.TeamId;
                var role = ballot.OriginalTeamHiring.Role;
                var isReserve = ballot.OriginalTeamHiring.IsReserve;

                var newTeamEntry = actualNewSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
                if (newTeamEntry == null) continue;

                var finalContract = isReserve
                    ? newTeamEntry.ReserveContract
                    : ballot.OriginalTeamHiring.GetSlot() == 1
                        ? newTeamEntry.Driver1Contract
                        : newTeamEntry.Driver2Contract;
                var teammateContract = isReserve
                    ? null
                    : ballot.OriginalTeamHiring.GetSlot() == 1
                        ? newTeamEntry.Driver2Contract
                        : newTeamEntry.Driver1Contract;
                var finalDriverId = finalContract?.DriverId;

                // roles get reassigned by reputation once the hiring's done, so report the
                // role the seat actually ended up with rather than the one advertised
                if (finalContract?.Role == ContractRole.FIRST_DRIVER)
                    role = DriverRole.FIRST_DRIVER;
                else if (finalContract?.Role == ContractRole.SECOND_DRIVER)
                    role = DriverRole.SECOND_DRIVER;

                bool playerHired = finalDriverId == playerDriverId;

                string otherDriverId;
                DriverReputation otherDriverReputation;
                bool otherDriverSignedElsewhere = false;

                // ballots are settled top teams first, so the team's pick may have signed for a
                // stronger team before this one was decided: the player never faced them
                var pickTeam = playerHired && !string.IsNullOrEmpty(ballot.OriginalTeamHiring.DriverId) && ballot.OriginalTeamHiring.DriverId != playerDriverId
                    ? actualNewSeason.Teams.FirstOrDefault(t =>
                        t.Driver1Contract?.DriverId == ballot.OriginalTeamHiring.DriverId ||
                        t.Driver2Contract?.DriverId == ballot.OriginalTeamHiring.DriverId ||
                        t.ReserveDriverId() == ballot.OriginalTeamHiring.DriverId)
                    : null;

                if (pickTeam != null)
                {
                    // at this team, they took its other seat: nobody to name
                    otherDriverSignedElsewhere = pickTeam.TeamId != teamId;
                    otherDriverId = otherDriverSignedElsewhere ? ballot.OriginalTeamHiring.DriverId : null;
                    otherDriverReputation = ballot.OriginalTeamHiring.DriverReputation;
                }
                else if (playerHired)
                {
                    if (ballot.OriginalTeamHiring.DriverId != playerDriverId)
                    {
                        // the player beat the team's original pick head-to-head
                        // (OffSeasonMovements.FinalBallotResults), so that pick is who they beat.
                        otherDriverId = ballot.OriginalTeamHiring.DriverId;
                        otherDriverReputation = ballot.OriginalTeamHiring.DriverReputation;
                    }
                    else
                    {
                        // The team had already provisionally picked the player themselves before
                        // they ever applied (EndOfSeasonManager.PickPotentialNewDrivers can do this
                        // for a currently-unemployed player, ahead of TeamApplicationWindow even
                        // running) - there's no head-to-head opponent to name. Fall back to the
                        // strongest other candidate on the ballot, if any, purely for letter flavor -
                        // one still without a seat, as one who signed elsewhere never lost to the player.
                        var bestOtherCandidate = ballot.Candidates?
                            .Where(c => c.DriverId != playerDriverId && !seatedDriverIds.Contains(c.DriverId))
                            .OrderByDescending(c => c.DriverReputation)
                            .FirstOrDefault();

                        otherDriverId = bestOtherCandidate?.DriverId;
                        otherDriverReputation = bestOtherCandidate?.DriverReputation ?? default;
                    }
                }
                else
                {
                    otherDriverId = finalDriverId;
                    otherDriverReputation = saveGame.Drivers.FirstOrDefault(d => d.DriverId == finalDriverId)?.Reputation
                        ?? ballot.OriginalTeamHiring.DriverReputation;
                }

                results.Add(new TeamApplicationResult
                {
                    TeamId = teamId,
                    Role = role,
                    AdvertisedRole = ballot.OriginalTeamHiring.Role,
                    TeammateId = string.IsNullOrEmpty(teammateContract?.DriverId) ? null : teammateContract.DriverId,
                    IsReserve = isReserve,
                    PlayerHired = playerHired,
                    OtherDriverId = otherDriverId,
                    OtherDriverReputation = otherDriverReputation,
                    OtherDriverSignedElsewhere = otherDriverSignedElsewhere,
                    OtherDriverWasAtTeamBefore = WasDriverAtTeamBefore(saveGame, teamId, otherDriverId, isReserve)
                });
            }

            return results;
        }

        // Was this driver already on this team last season (e.g. a contract-expired driver who
        // ended up renewing)? Checked against saveGame.CurrentSeason, which is still last
        // season's data at this point (StartNewSeason in STEP 8 hasn't run yet).
        private static bool WasDriverAtTeamBefore(ISaveGame saveGame, string teamId, string driverId, bool isReserve)
        {
            if (string.IsNullOrEmpty(driverId)) return false;

            var previousTeam = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            if (previousTeam == null) return false;

            // for a reserve seat, the incumbent is last season's reserve
            if (isReserve)
                return previousTeam.ReserveDriverId() == driverId;

            return previousTeam.Driver1Contract?.DriverId == driverId || previousTeam.Driver2Contract?.DriverId == driverId;
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
