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
    }
}
