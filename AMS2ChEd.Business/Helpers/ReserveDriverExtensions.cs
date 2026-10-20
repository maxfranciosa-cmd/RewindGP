using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

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
    /// Every substitute pick (season-start absence reconciliation, mid-season releases, the
    /// substitute covering a player who leaves for another team's absence) starts from this, so a
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

        // the team's own reserve, when they aren't already standing in somewhere in these absences
        public static string AvailableReserve(ITeamEntry team, IEnumerable<Absence> absencesOfTheRace)
        {
            var reserveId = team.ReserveDriverId();
            if (reserveId == null)
                return null;

            return DriversIn(absencesOfTheRace).Contains(reserveId) ? null : reserveId;
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
