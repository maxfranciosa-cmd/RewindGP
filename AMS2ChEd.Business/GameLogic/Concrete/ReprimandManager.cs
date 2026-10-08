using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Team orders: a SECOND_DRIVER finishing right ahead of their FIRST_DRIVER, or the player
    /// making contact with their teammate, earns a reprimand from the team principal.
    /// Two reprimands mean the team won't retain the driver at the end of the season (see
    /// EndOfSeasonManager), three mean the driver is replaced for the rest of the season.
    /// </summary>
    public class ReprimandManager : IReprimandManager
    {
        public const int REPRIMANDS_FOR_SEASON_END_DROP = 2;
        public const int REPRIMANDS_FOR_RELEASE = 3;

        public IReadOnlyList<ReprimandNewsItem> ProcessRace(
            ISaveGame saveGame,
            GrandPrixResult result,
            IReadOnlyList<HistoricalDriverStandingEntry> preRaceStandings,
            IReadOnlyCollection<string> playerContactDriverIds)
        {
            saveGame.Reprimands ??= new List<Reprimand>();

            var races = saveGame.CurrentSeason.Races.ToList();
            var raceIndex = saveGame.NextGpIndex;
            if (raceIndex < 0 || raceIndex >= races.Count || saveGame.NextGpEntryList == null)
                return new List<ReprimandNewsItem>();

            var raceId = races[raceIndex].RaceId;
            var isSecondHalfOfSeason = raceIndex >= races.Count / 2;
            var teamsDictionary = saveGame.CurrentSeason.Teams.ToDictionary(t => t.TeamId, t => t);
            var raceResults = (result.RaceResults ?? new List<SessionResult>())
                .Where(r => !string.IsNullOrEmpty(r.DriverId))
                .GroupBy(r => r.DriverId)
                .ToDictionary(g => g.Key, g => g.First());

            var newReprimands = new List<(Reprimand Reprimand, string TeammateId)>();

            foreach (var entry in saveGame.NextGpEntryList)
            {
                if (string.IsNullOrEmpty(entry.Driver1Id) || string.IsNullOrEmpty(entry.Driver2Id))
                    continue;

                if (!teamsDictionary.TryGetValue(entry.TeamId, out var team))
                    continue;

                // absences swap drivers within the same seat, so whoever raced in a seat inherits its role
                // (team orders are off when either seat is taken by a substitute though, see IsSubstitute)
                var driver1Role = team.Driver1Contract?.Role ?? ContractRole.EQUAL;
                var driver2Role = team.Driver2Contract?.Role ?? ContractRole.EQUAL;

                string firstDriverId = null;
                string secondDriverId = null;
                if (driver1Role == ContractRole.FIRST_DRIVER && driver2Role == ContractRole.SECOND_DRIVER)
                {
                    firstDriverId = entry.Driver1Id;
                    secondDriverId = entry.Driver2Id;
                }
                else if (driver1Role == ContractRole.SECOND_DRIVER && driver2Role == ContractRole.FIRST_DRIVER)
                {
                    firstDriverId = entry.Driver2Id;
                    secondDriverId = entry.Driver1Id;
                }

                if (firstDriverId != null &&
                    !IsSubstitute(team, firstDriverId) &&
                    !IsSubstitute(team, secondDriverId) &&
                    FinishedRightAheadOfFirstDriver(raceResults, firstDriverId, secondDriverId) &&
                    !IsExemptFromTeamOrders(preRaceStandings, isSecondHalfOfSeason, firstDriverId, secondDriverId))
                {
                    newReprimands.Add((new Reprimand
                    {
                        DriverId = secondDriverId,
                        TeamId = team.TeamId,
                        RaceId = raceId,
                        Reason = ReprimandReason.FINISHED_AHEAD_OF_FIRST_DRIVER
                    }, firstDriverId));
                }

                // contact is only detectable for the player's own car
                var playerId = saveGame.PlayerData?.DriverId;
                if (playerContactDriverIds != null && playerId != null &&
                    !IsSubstitute(team, playerId) &&
                    (entry.Driver1Id == playerId || entry.Driver2Id == playerId))
                {
                    var teammateId = entry.Driver1Id == playerId ? entry.Driver2Id : entry.Driver1Id;
                    if (playerContactDriverIds.Contains(teammateId))
                    {
                        newReprimands.Add((new Reprimand
                        {
                            DriverId = playerId,
                            TeamId = team.TeamId,
                            RaceId = raceId,
                            Reason = ReprimandReason.CONTACT_WITH_TEAMMATE
                        }, teammateId));
                    }
                }
            }

            saveGame.Reprimands.AddRange(newReprimands.Select(r => r.Reprimand));

            var newsItems = new List<ReprimandNewsItem>();
            var leaderId = saveGame.CurrentDriverStandings?.FirstOrDefault(s => s.Position == 1)?.DriverId;

            foreach (var driverReprimands in newReprimands.GroupBy(r => (r.Reprimand.DriverId, r.Reprimand.TeamId)))
            {
                var (driverId, teamId) = driverReprimands.Key;
                var totalCount = GetReprimandCount(saveGame, driverId, teamId);
                var countBefore = totalCount - driverReprimands.Count();
                var index = 0;

                foreach (var (reprimand, teammateId) in driverReprimands)
                {
                    index++;
                    var count = countBefore + index;
                    var isLastForDriver = index == driverReprimands.Count();

                    var item = new ReprimandNewsItem
                    {
                        DriverId = driverId,
                        TeamId = teamId,
                        TeammateId = teammateId,
                        Reason = reprimand.Reason,
                        ReprimandCount = count,
                        IsPlayer = driverId == saveGame.PlayerData?.DriverId,
                        Consequence = ReprimandConsequence.NONE
                    };

                    // consequences are decided once per driver, on their final tally for this race
                    if (isLastForDriver)
                    {
                        if (count >= REPRIMANDS_FOR_RELEASE)
                        {
                            if (driverId == leaderId)
                            {
                                item.Consequence = ReprimandConsequence.SPARED_AS_LEADER;
                            }
                            else
                            {
                                item.Consequence = ReprimandConsequence.RELEASED;
                                item.ReplacementDriverId = ReleaseDriverForTheRestOfTheSeason(saveGame, teamsDictionary[teamId], driverId, raceIndex);
                            }
                        }
                        else if (count >= REPRIMANDS_FOR_SEASON_END_DROP)
                        {
                            item.Consequence = ReprimandConsequence.FINAL_WARNING;
                        }
                    }

                    newsItems.Add(item);
                }
            }

            return newsItems
                .OrderByDescending(i => i.IsPlayer)
                .ToList();
        }

        public int GetReprimandCount(ISaveGame saveGame, string driverId, string teamId)
        {
            return (saveGame.Reprimands ?? new List<Reprimand>())
                .Count(r => r.DriverId == driverId && r.TeamId == teamId);
        }

        // a driver standing in for an absent one has no contract with the team, and is never reprimanded
        private static bool IsSubstitute(ITeamEntry team, string driverId)
        {
            return team.Driver1Contract?.DriverId != driverId && team.Driver2Contract?.DriverId != driverId;
        }

        private static bool IsClassified(SessionResult result)
        {
            return result != null && !result.DNF && !result.DidNotPreQualify && result.Position > 0;
        }

        private static bool FinishedRightAheadOfFirstDriver(Dictionary<string, SessionResult> raceResults, string firstDriverId, string secondDriverId)
        {
            var firstDriverResult = raceResults.GetValueOrDefault(firstDriverId);
            var secondDriverResult = raceResults.GetValueOrDefault(secondDriverId);

            if (!IsClassified(firstDriverResult) || !IsClassified(secondDriverResult))
                return false;

            return secondDriverResult.Position == firstDriverResult.Position - 1;
        }

        // in the second half of the season, a second driver who's already ahead of the first
        // driver in the championship is free to race them
        private static bool IsExemptFromTeamOrders(IReadOnlyList<HistoricalDriverStandingEntry> preRaceStandings, bool isSecondHalfOfSeason, string firstDriverId, string secondDriverId)
        {
            if (!isSecondHalfOfSeason || preRaceStandings == null)
                return false;

            var firstDriverStanding = preRaceStandings.FirstOrDefault(s => s.DriverId == firstDriverId);
            var secondDriverStanding = preRaceStandings.FirstOrDefault(s => s.DriverId == secondDriverId);

            if (secondDriverStanding == null)
                return false;

            if (firstDriverStanding == null)
                return true;

            return secondDriverStanding.Position < firstDriverStanding.Position;
        }

        // replaces the driver in every remaining race of the season, returning the replacement's id
        private string ReleaseDriverForTheRestOfTheSeason(ISaveGame saveGame, ITeamEntry team, string driverId, int raceIndex)
        {
            var remainingRaces = saveGame.CurrentSeason.Races.Skip(raceIndex + 1).ToList();
            var absences = (saveGame.CurrentSeason.Absences ?? Enumerable.Empty<Absence>()).ToList();

            var replacementId = PickReleaseReplacement(saveGame, team, remainingRaces, absences, driverId);

            foreach (var race in remainingRaces)
            {
                if (absences.Any(a => a.RaceId == race.RaceId && a.TeamId == team.TeamId && a.DriverOut == driverId))
                    continue;

                absences.Add(new Absence
                {
                    RaceId = race.RaceId,
                    TeamId = team.TeamId,
                    DriverOut = driverId,
                    DriverIn = replacementId
                });
            }

            saveGame.CurrentSeason.Absences = absences;
            return replacementId;
        }

        // the best unemployed driver the team would accept as a stand-in, who isn't already
        // standing in for someone else in any of the remaining races
        private static string PickReleaseReplacement(ISaveGame saveGame, ITeamEntry team, List<Race> remainingRaces, List<Absence> absences, string releasedDriverId)
        {
            var contractedDriverIds = saveGame.CurrentSeason.Teams
                .SelectMany(t => new[] { t.Driver1Contract?.DriverId, t.Driver2Contract?.DriverId })
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();

            var remainingRaceIds = remainingRaces.Select(r => r.RaceId).ToHashSet();
            var busyDriverIds = absences
                .Where(a => remainingRaceIds.Contains(a.RaceId))
                .SelectMany(a => GetDriversIn(a))
                .ToHashSet();

            var retiredDriverIds = (saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>())
                .Select(d => d.DriverId)
                .ToHashSet();

            var maxReputation = DriverHirer.teamAbsenceSubstitutionMaxReputation.GetValueOrDefault(team.Reputation, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL);

            var candidates = saveGame.Drivers
                .Where(d => d.DriverId != releasedDriverId &&
                            d.DriverId != saveGame.PlayerData?.DriverId &&
                            !contractedDriverIds.Contains(d.DriverId) &&
                            !busyDriverIds.Contains(d.DriverId) &&
                            !retiredDriverIds.Contains(d.DriverId))
                .OrderByDescending(d => d.Reputation)
                .ToList();

            var pick = candidates.FirstOrDefault(d => d.Reputation <= maxReputation) ?? candidates.FirstOrDefault();

            return pick?.DriverId ?? "";
        }

        private static IEnumerable<string> GetDriversIn(Absence absence)
        {
            for (var current = absence; current != null; current = current.ChainedAbsence)
            {
                if (!string.IsNullOrEmpty(current.DriverIn))
                    yield return current.DriverIn;
            }
        }
    }
}
