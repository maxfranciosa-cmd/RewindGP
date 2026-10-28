using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Applies a season's "best N races count" drop-scores rule for the drivers'
    /// championship. Constructors always count every race and don't use this.
    /// </summary>
    public static class ChampionshipScoringCalculator
    {
        /// <summary>
        /// The specific GrandPrixResult objects (by reference, from whatever
        /// <paramref name="allResults"/> sequence is passed in) whose points shouldn't
        /// count toward this driver's championship total.
        /// </summary>
        public static HashSet<GrandPrixResult> GetDiscardedResults(string driverId, ISeason season, IEnumerable<GrandPrixResult> allResults)
        {
            if (season.RacesToCountTowardsChampionship == null) return new HashSet<GrandPrixResult>();

            var participations = allResults
                .Select(r => new { Result = r, Points = r.RaceResults.FirstOrDefault(sr => sr.DriverId == driverId)?.Points })
                .Where(x => x.Points.HasValue)
                .ToList();

            int discardCount = participations.Count - season.RacesToCountTowardsChampionship.Value;
            if (discardCount <= 0) return new HashSet<GrandPrixResult>();

            return participations
                .OrderBy(x => x.Points)
                .Take(discardCount)
                .Select(x => x.Result)
                .ToHashSet();
        }

        public static double CalculateDriverSeasonPoints(string driverId, ISeason season, IEnumerable<GrandPrixResult> allResults)
        {
            var discarded = GetDiscardedResults(driverId, season, allResults);
            return allResults
                .Where(r => !discarded.Contains(r))
                .Sum(r => r.RaceResults.Where(sr => sr.DriverId == driverId).Sum(sr => sr.Points));
        }
        
        public static bool HasDriverChampionshipClinched(ISaveGame game)
        {
            if (game.NextGpIndex >= game.CurrentSeason.Races.Count() - 1) return true;

            var first2DriversWithTheirTallies = game.CurrentDriverStandings
                                    .Take(2)
                                    .OrderBy(d => d.Position)
                                    .Select(d => new { d.DriverId, d.PositionsTally, d.Points })
                                    .ToDictionary(d => d.DriverId, d => d);

            var firstDriverId = first2DriversWithTheirTallies.First().Key;
            var secondDriverId = first2DriversWithTheirTallies.Last().Key;

            var resultsFromFirstTwoDrivers = game.GrandPrixResults
                                                    .Where(r => r.Year == game.CurrentSeason.Year && r.RaceResults.Any(sr => first2DriversWithTheirTallies.Select(d => d.DriverId).Contains(sr.DriverId)))
                                                    .SelectMany(r => r.RaceResults)
                                                    .ToDictionary(r => r.DriverId, r => r.Points);

            var firstDriverResults = resultsFromFirstTwoDrivers[firstDriverId];
            var secondDriverResults = resultsFromFirstTwoDrivers[secondDriverId];
            var remainingRaces = game.CurrentSeason.Races.Skip(game.NextGpIndex + 1);

            // add the maximum amount of points for the driver in second place
            foreach(var race in remainingRaces)
            {
                var racePointsSystem = race.PointsSystem ?? game.CurrentSeason.PointsSystem;
                // add the maximum points for this race to the second place driver's total
                var maximumPoints = racePointsSystem.Values.Max() + (race.PointsForFastestLap ?? 0);

                resultsFromFirstTwoDrivers[secondDriverId].Add(maximumPoints);
                resultsFromFirstTwoDrivers[firstDriverId].Add(0);

                // set the position as first for the second place driver, and add the maximum position without points to the first place driver's in their respective position tally
                var maxPositionWithoutPoints = racePointsSystem.Keys.Select(k => int.Parse(k)).Min();

                first2DriversWithTheirTallies[secondDriverId].PositionsTally.AddPosition(1);
                first2DriversWithTheirTallies[firstDriverId].PositionsTally.AddPosition(maxPositionWithoutPoints);
            }

            // order the results by points descending and take the top N results (where N is the number of races to count towards the championship)
            var totalPointsForFirstPlaceDriver = resultsFromFirstTwoDrivers[firstDriverId]
                                                        .Take(Math.Max(game.CurrentSeason.RacesToCountTowardsChampionship ?? 0,  game.CurrentSeason.Races.Count()))
                                                        .Sum();
            var totalPointsForSecondPlaceDriver = resultsFromFirstTwoDrivers[secondDriverId]
                                                        .Take(Math.Max(game.CurrentSeason.RacesToCountTowardsChampionship ?? 0,  game.CurrentSeason.Races.Count()))
                                                        .Sum();

            // if the first place driver is still ahead, return true. if are less, return false;
            if (totalPointsForFirstPlaceDriver > totalPointsForSecondPlaceDriver) return true;
            if (totalPointsForFirstPlaceDriver < totalPointsForSecondPlaceDriver) return false;

            // if they are tied, if the first place driver's championship tally is greater, return true, otherwise return false
            return first2DriversWithTheirTallies[firstDriverId].PositionsTally > first2DriversWithTheirTallies[secondDriverId].PositionsTally;
        }
    }
}
