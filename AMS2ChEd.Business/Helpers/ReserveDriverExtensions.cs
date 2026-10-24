using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Business.Helpers
{
    /// <summary>
    /// Who is a team's reserve. A reserve is a driver id in a team's ReserveContract: they hold no
    /// race seat (so PlayerData.TeamId stays null for a player who is a reserve), and only ever
    /// stand in for their own team.
    /// </summary>
    public static class ReserveDriverExtensions
    {
        // null when the team runs no reserve
        public static string ReserveDriverId(this ITeamEntry team)
        {
            var driverId = team?.ReserveContract?.DriverId;
            return string.IsNullOrEmpty(driverId) ? null : driverId;
        }

        public static HashSet<string> ReserveDriverIds(this ISeason season)
        {
            return (season?.Teams ?? Enumerable.Empty<ITeamEntry>())
                .Select(t => t.ReserveDriverId())
                .Where(id => id != null)
                .ToHashSet();
        }

        // the team this driver is the reserve of, null when they're nobody's reserve
        public static ITeamEntry ReserveTeamOf(this ISeason season, string driverId)
        {
            if (string.IsNullOrEmpty(driverId))
                return null;

            return season?.Teams?.FirstOrDefault(t => t.ReserveDriverId() == driverId);
        }

        // the drivers holding a race seat (driver 1 or 2) at any team
        public static HashSet<string> RaceDriverIds(this ISeason season)
        {
            return (season?.Teams ?? Enumerable.Empty<ITeamEntry>())
                .SelectMany(t => new[] { t.Driver1Contract?.DriverId, t.Driver2Contract?.DriverId })
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();
        }
    }

    /// <summary>
    /// The one place that decides who can't be picked as a stand-in for another team's driver.
    /// Every substitute pick (season-start absence reconciliation, mid-season releases and injuries,
    /// the substitute covering a player who leaves for another team's absence) starts from this, so a
    /// driver unavailable here is never handed someone else's seat.
    /// </summary>
    public static class SubstitutePool
    {
        // drivers with a race seat, and every team's reserve (who only covers their own team)
        public static HashSet<string> UnavailableDriverIds(ISeason season)
        {
            var result = season.RaceDriverIds();
            result.UnionWith(season.ReserveDriverIds());
            return result;
        }

        // the same during the season, at these races: also every driver who is dead, or injured at
        // any of them (CasualtyExtensions)
        public static HashSet<string> UnavailableDriverIds(ISaveGame saveGame, IEnumerable<int> raceIds)
        {
            var result = UnavailableDriverIds(saveGame.CurrentSeason);
            result.UnionWith(saveGame.OutOfActionDriverIds(raceIds));
            return result;
        }

        // the team's own reserve, when they aren't already standing in somewhere in these absences
        public static string AvailableReserve(ITeamEntry team, IEnumerable<Absence> absencesOfTheRace)
        {
            var reserveId = team.ReserveDriverId();
            if (reserveId == null)
                return null;

            return DriversIn(absencesOfTheRace).Contains(reserveId) ? null : reserveId;
        }

        // the same, also out of action at none of these races
        public static string AvailableReserve(ISaveGame saveGame, ITeamEntry team, IEnumerable<Absence> absencesOfTheRaces, IEnumerable<int> raceIds)
        {
            var reserveId = AvailableReserve(team, absencesOfTheRaces);
            return reserveId == null || saveGame.OutOfActionDriverIds(raceIds).Contains(reserveId) ? null : reserveId;
        }

        /// <summary>
        /// Who stands in for a driver missing these races (a release, an injury, a death): the team's
        /// reserve when they're free for all of them, the player included when they're the reserve;
        /// otherwise the best driver without a seat the team would accept as a stand-in, who isn't
        /// standing in elsewhere at any of them. Never the player otherwise: they apply for the seat
        /// like for any absence. Empty when there's nobody.
        /// </summary>
        public static string PickStandIn(ISaveGame saveGame, ITeamEntry team, IReadOnlyCollection<int> raceIds, IEnumerable<Absence> absences, string outDriverId)
        {
            var absencesOfTheRaces = (absences ?? Enumerable.Empty<Absence>()).Where(a => raceIds.Contains(a.RaceId)).ToList();

            var reserveId = AvailableReserve(saveGame, team, absencesOfTheRaces, raceIds);
            if (reserveId != null && reserveId != outDriverId)
                return reserveId;

            var unavailable = UnavailableDriverIds(saveGame, raceIds);
            unavailable.UnionWith(DriversIn(absencesOfTheRaces));
            unavailable.UnionWith((saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>()).Select(d => d.DriverId));
            unavailable.Add(outDriverId);
            if (saveGame.PlayerData?.DriverId != null)
                unavailable.Add(saveGame.PlayerData.DriverId);

            var maxReputation = DriverHirer.teamAbsenceSubstitutionMaxReputation.GetValueOrDefault(team.Reputation, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL);

            var candidates = saveGame.Drivers
                .Where(d => !unavailable.Contains(d.DriverId))
                .OrderByDescending(d => d.Reputation)
                .ToList();

            var pick = candidates.FirstOrDefault(d => d.Reputation <= maxReputation) ?? candidates.FirstOrDefault();

            return pick?.DriverId ?? "";
        }

        // every driver standing in, chained absences included
        public static HashSet<string> DriversIn(IEnumerable<Absence> absences)
        {
            var result = new HashSet<string>();
            foreach (var absence in absences ?? Enumerable.Empty<Absence>())
            {
                for (var current = absence; current != null; current = current.ChainedAbsence)
                {
                    if (!string.IsNullOrEmpty(current.DriverIn))
                        result.Add(current.DriverIn);
                }
            }
            return result;
        }
    }
}
