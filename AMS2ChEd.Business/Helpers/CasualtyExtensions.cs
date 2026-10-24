using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.Helpers
{
    /// <summary>
    /// Who is out of action after a crash (the save's Casualties): a dead driver for good, an
    /// injured one for the races of the current season they miss.
    /// </summary>
    public static class CasualtyExtensions
    {
        public static bool IsDeceased(this ISaveGame saveGame, string driverId)
        {
            return !string.IsNullOrEmpty(driverId) &&
                   (saveGame?.Casualties ?? Enumerable.Empty<Casualty>()).Any(c => c.Kind == CasualtyKind.DECEASED && c.DriverId == driverId);
        }

        public static bool IsPlayerDeceased(this ISaveGame saveGame) => saveGame.IsDeceased(saveGame?.PlayerData?.DriverId);

        public static HashSet<string> DeceasedDriverIds(this ISaveGame saveGame)
        {
            return (saveGame?.Casualties ?? Enumerable.Empty<Casualty>())
                .Where(c => c.Kind == CasualtyKind.DECEASED)
                .Select(c => c.DriverId)
                .ToHashSet();
        }

        // dead, or recovering at that race of the current season
        public static bool IsOutOfAction(this ISaveGame saveGame, string driverId, int raceId)
        {
            return saveGame.OutOfActionDriverIds(new[] { raceId }).Contains(driverId);
        }

        // the drivers who can't race at any of these races of the current season
        public static HashSet<string> OutOfActionDriverIds(this ISaveGame saveGame, IEnumerable<int> raceIds)
        {
            var year = saveGame?.CurrentSeason?.Year;
            var races = (raceIds ?? Enumerable.Empty<int>()).ToHashSet();

            return (saveGame?.Casualties ?? Enumerable.Empty<Casualty>())
                .Where(c => c.Kind == CasualtyKind.DECEASED ||
                            (c.Year == year && (c.RaceIdsOut ?? new List<int>()).Any(races.Contains)))
                .Select(c => c.DriverId)
                .ToHashSet();
        }
    }
}
