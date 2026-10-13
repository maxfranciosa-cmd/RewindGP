using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    public enum HallOfFameStatus
    {
        // has a place in the save's current drivers pool
        ACTIVE,
        // raced in this save and has since retired
        RETIRED,
        // made his name before the save began and never raced in it
        HISTORIC
    }

    public class HallOfFameEntry
    {
        public string DriverId { get; set; }
        public string Name { get; set; }
        public string Nationality { get; set; }
        public int Wins { get; set; }
        public List<int> ChampionshipYears { get; set; } = new();
        public int Championships => ChampionshipYears.Count;
        public HallOfFameStatus Status { get; set; }
        public bool IsPlayer { get; set; }
    }

    /// <summary>
    /// The all-time record of every driver as history stands in a save: what really happened
    /// before the save began, then whatever happened in the save - the same numbers
    /// <see cref="AccoladesCalculator"/> gives everywhere else, for the drivers of the save and
    /// for the champions of the past who never raced in it.
    /// </summary>
    public static class HallOfFameCalculator
    {
        public static List<HallOfFameEntry> Build(ISaveGame saveGame, AllTimeRecords allTimeRecords)
        {
            var activeDrivers = ToDictionary(saveGame.Drivers);
            var retiredDrivers = ToDictionary(saveGame.RetiredDrivers);
            var allTime = (allTimeRecords?.Drivers ?? new List<AllTimeDriverRecord>())
                .Where(r => !string.IsNullOrEmpty(r.DriverId))
                .GroupBy(r => r.DriverId)
                .ToDictionary(g => g.Key, g => g.First());
            string playerId = saveGame.PlayerData?.DriverId;

            var driverIds = activeDrivers.Keys
                .Concat(retiredDrivers.Keys)
                .Concat(allTime.Keys)
                .Concat(string.IsNullOrEmpty(playerId) ? Enumerable.Empty<string>() : new[] { playerId })
                .Distinct()
                .ToList();

            var entries = new List<HallOfFameEntry>();
            foreach (var driverId in driverIds)
            {
                var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, allTimeRecords, driverId);
                int wins = accolades.Wins;
                var championshipYears = accolades.ChampionshipYears;

                if (wins == 0 && championshipYears.Count == 0)
                    continue;

                var record = allTime.GetValueOrDefault(driverId);
                var driver = activeDrivers.GetValueOrDefault(driverId) ?? retiredDrivers.GetValueOrDefault(driverId);
                bool isPlayer = driverId == playerId;

                entries.Add(new HallOfFameEntry
                {
                    DriverId = driverId,
                    Name = driver?.Name ?? (isPlayer ? saveGame.PlayerData.Name : null) ?? record?.Name ?? driverId,
                    Nationality = driver?.Nationality ?? (isPlayer ? saveGame.PlayerData.Nationality : null) ?? record?.Nationality,
                    Wins = wins,
                    ChampionshipYears = championshipYears,
                    Status = activeDrivers.ContainsKey(driverId) ? HallOfFameStatus.ACTIVE
                        : retiredDrivers.ContainsKey(driverId) ? HallOfFameStatus.RETIRED
                        : HallOfFameStatus.HISTORIC,
                    IsPlayer = isPlayer
                });
            }

            return entries;
        }

        private static Dictionary<string, IDriverData> ToDictionary(IEnumerable<IDriverData> drivers)
        {
            return (drivers ?? Enumerable.Empty<IDriverData>())
                .Where(d => !string.IsNullOrEmpty(d.DriverId))
                .GroupBy(d => d.DriverId)
                .ToDictionary(g => g.Key, g => g.First());
        }
    }
}
