using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AMS2ChEd.Business.Services
{
    public class AccoladesImportResult
    {
        public Dictionary<string, Accolades> DriverAccolades { get; set; } = new();
        public Dictionary<string, Accolades> TeamAccolades { get; set; } = new();
        public List<string> UnmatchedDriverNames { get; set; } = new();
        public List<string> UnmatchedTeamNames { get; set; } = new();
    }

    /// <summary>
    /// Matches local drivers/teams against Jolpica's records for a season and fetches their
    /// career accolades as of the start of that season (i.e. excluding that season's own results).
    /// </summary>
    public class JolpicaAccoladesImportService
    {
        private const int MaxConcurrentRequests = 4;

        public async Task<AccoladesImportResult> ImportAsync(int seasonYear, List<Ams2DriverData> localDrivers, IEnumerable<ITeamEntry> localTeams)
        {
            var jolpica = new JolpicaF1Service();
            var result = new AccoladesImportResult();
            var throttle = new SemaphoreSlim(MaxConcurrentRequests);

            var seasonDrivers = await jolpica.FetchDriversAsync(seasonYear);
            var seasonConstructors = await jolpica.FetchConstructorsAsync(seasonYear);

            var driverTasks = new List<Task>();
            foreach (var driver in localDrivers)
            {
                var match = seasonDrivers.FirstOrDefault(jd =>
                    string.Equals($"{jd.GivenName} {jd.FamilyName}".Trim(), driver.Name?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    result.UnmatchedDriverNames.Add(driver.Name ?? driver.DriverId);
                    continue;
                }

                driverTasks.Add(FetchDriverAccoladesAsync(jolpica, throttle, driver.DriverId, match.DriverId, seasonYear, result.DriverAccolades));
            }

            var teamTasks = new List<Task>();
            foreach (var team in localTeams)
            {
                var match = seasonConstructors.FirstOrDefault(jc =>
                    string.Equals(jc.Name?.Trim(), team.TeamName?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    result.UnmatchedTeamNames.Add(team.TeamName ?? team.TeamId);
                    continue;
                }

                teamTasks.Add(FetchTeamAccoladesAsync(jolpica, throttle, team.TeamId, match.ConstructorId, seasonYear, result.TeamAccolades));
            }

            await Task.WhenAll(driverTasks.Concat(teamTasks));

            return result;
        }

        private static async Task FetchDriverAccoladesAsync(JolpicaF1Service jolpica, SemaphoreSlim throttle, string localDriverId, string jolpicaDriverId, int seasonYear, Dictionary<string, Accolades> destination)
        {
            await throttle.WaitAsync();
            try
            {
                var accolades = await jolpica.GetDriverCareerAccoladesBeforeSeasonAsync(jolpicaDriverId, seasonYear);
                lock (destination)
                {
                    destination[localDriverId] = accolades;
                }
            }
            finally
            {
                throttle.Release();
            }
        }

        private static async Task FetchTeamAccoladesAsync(JolpicaF1Service jolpica, SemaphoreSlim throttle, string localTeamId, string jolpicaConstructorId, int seasonYear, Dictionary<string, Accolades> destination)
        {
            await throttle.WaitAsync();
            try
            {
                var accolades = await jolpica.GetConstructorCareerAccoladesBeforeSeasonAsync(jolpicaConstructorId, seasonYear);
                lock (destination)
                {
                    destination[localTeamId] = accolades;
                }
            }
            finally
            {
                throttle.Release();
            }
        }
    }
}
