using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    public enum DriverDatabaseStatus
    {
        // holds a seat in the current season
        RACING,
        // in the save's drivers pool without a seat
        FREE_AGENT,
        // has retired
        RETIRED
    }

    public class DriverDatabaseEntry
    {
        public string DriverId { get; set; }
        public string FullName { get; set; }
        public string FirstName { get; set; }
        public string Surname { get; set; }
        public string Nationality { get; set; }
        // null when the year of birth isn't known
        public int? Age { get; set; }
        public string PictureUrl { get; set; }
        public GenericAvatar Avatar { get; set; }
        public DriverDatabaseStatus Status { get; set; }
        public bool IsPlayer { get; set; }

        // the seat, for a driver who is racing; null otherwise
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public string TeamShortName { get; set; }
        public string TeamColor { get; set; }
        public int? CarNumber { get; set; }
    }

    /// <summary>
    /// Every driver of a save, whether racing this season, without a seat or retired, with his
    /// first name and surname apart (see <see cref="ShortNameExtensions"/>), sorted by surname.
    /// </summary>
    public static class DriverDatabase
    {
        public static List<DriverDatabaseEntry> Build(ISaveGame saveGame)
        {
            if (saveGame == null)
                return new List<DriverDatabaseEntry>();

            int seasonYear = saveGame.CurrentSeason?.Year ?? 0;
            string playerId = saveGame.PlayerData?.DriverId;

            // the seat of every driver under contract this season (the first one, should a driver hold two)
            var seats = new Dictionary<string, (ITeamEntry Team, int DriverNumber)>();
            foreach (var team in saveGame.CurrentSeason?.Teams ?? Enumerable.Empty<ITeamEntry>())
            {
                foreach (var contract in new[] { team.Driver1Contract, team.Driver2Contract })
                {
                    if (!string.IsNullOrEmpty(contract?.DriverId))
                        seats.TryAdd(contract.DriverId, (team, contract.DriverNumber));
                }
            }

            var entries = new List<DriverDatabaseEntry>();
            var seen = new HashSet<string>();

            // a driver both in the pool and among the retired is counted where the pool puts him
            foreach (var driver in saveGame.Drivers ?? Enumerable.Empty<IDriverData>())
            {
                if (string.IsNullOrEmpty(driver?.DriverId) || !seen.Add(driver.DriverId))
                    continue;

                var entry = ToEntry(driver, seasonYear, playerId);
                if (seats.TryGetValue(driver.DriverId, out var seat))
                {
                    entry.Status = DriverDatabaseStatus.RACING;
                    entry.TeamId = seat.Team.TeamId;
                    entry.TeamName = seat.Team.TeamName;
                    entry.TeamShortName = seat.Team.ShortTeamName();
                    entry.TeamColor = seat.Team.Color;
                    entry.CarNumber = seat.DriverNumber > -1 ? seat.DriverNumber : null;
                }
                else
                {
                    entry.Status = DriverDatabaseStatus.FREE_AGENT;
                }
                entries.Add(entry);
            }

            foreach (var driver in saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>())
            {
                if (string.IsNullOrEmpty(driver?.DriverId) || !seen.Add(driver.DriverId))
                    continue;

                var entry = ToEntry(driver, seasonYear, playerId);
                entry.Status = DriverDatabaseStatus.RETIRED;
                entries.Add(entry);
            }

            return entries
                .OrderBy(e => e.Surname, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(e => e.FirstName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static DriverDatabaseEntry ToEntry(IDriverData driver, int seasonYear, string playerId)
        {
            return new DriverDatabaseEntry
            {
                DriverId = driver.DriverId,
                FullName = driver.Name,
                FirstName = driver.FirstName() ?? string.Empty,
                Surname = driver.Surname() ?? string.Empty,
                Nationality = driver.Nationality,
                Age = driver.YearOfBirth > 0 && seasonYear > 0 ? seasonYear - driver.YearOfBirth : null,
                PictureUrl = driver.PictureUrl,
                Avatar = driver.GenericAvatar,
                IsPlayer = !string.IsNullOrEmpty(playerId) && driver.DriverId == playerId
            };
        }
    }
}
