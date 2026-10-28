
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.Diagnostics;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Applies best-N-races-count scoring and determines whether
    /// a driver or constructor has mathematically clinched a championship.
    /// </summary>
    public static class ChampionshipScoringCalculator
    {
        /// <summary>
        /// Returns the race results discarded under the season's best-N rule.
        /// </summary>
        public static HashSet<GrandPrixResult> GetDiscardedResults(
            string driverId,
            ISeason season,
            IEnumerable<GrandPrixResult> allResults)
        {
            if (season.RacesToCountTowardsChampionship == null)
                return new HashSet<GrandPrixResult>();

            var results = allResults.ToList();

            var participations = results
                .Select(r => new
                {
                    Result = r,
                    Points = r.RaceResults
                        .Where(sr => sr.DriverId == driverId)
                        .Sum(sr => (double?)sr.Points)
                })
                .Where(x => x.Points.HasValue)
                .ToList();

            int countedRaces = season.RacesToCountTowardsChampionship.Value;
            int discardCount = participations.Count - countedRaces;

            if (discardCount <= 0)
                return new HashSet<GrandPrixResult>();

            return participations
                .OrderBy(x => x.Points)
                .Take(discardCount)
                .Select(x => x.Result)
                .ToHashSet();
        }

        /// <summary>
        /// Calculates a driver's championship points, applying the best-N rule.
        /// </summary>
        public static double CalculateDriverSeasonPoints(
            string driverId,
            ISeason season,
            IEnumerable<GrandPrixResult> allResults)
        {
            var results = allResults.ToList();
            var discarded = GetDiscardedResults(driverId, season, results);

            return results
                .Where(r => !discarded.Contains(r))
                .Sum(r => r.RaceResults
                    .Where(sr => sr.DriverId == driverId)
                    .Sum(sr => sr.Points));
        }

        /// <summary>
        /// Returns true if the constructor title is already mathematically decided.
        /// If atThisExactRace is true, returns true only if the title became
        /// mathematically decided at the specified race.
        /// </summary>
        public static bool HasConstructorChampionshipClinched(
            ISaveGame game,
            out bool clinchedThisExactRace,
            int? raceIndex = null)
        {
            clinchedThisExactRace = false;

            int currentRaceIndex = raceIndex ?? game.NextGpIndex;

            bool clinchedNow =
                IsConstructorChampionshipClinchedAtRace(game, currentRaceIndex);

            if (!clinchedNow) return false;

            clinchedThisExactRace = currentRaceIndex > 0 &&
                !IsConstructorChampionshipClinchedAtRace(game, currentRaceIndex - 1) &&
                clinchedNow;

            return true;
        }

        private static bool IsConstructorChampionshipClinchedAtRace(
            ISaveGame game,
            int raceIndex)
        {
            var season = game.CurrentSeason;
            var races = season.Races.ToList();

            if (raceIndex >= races.Count - 1)
                return true;

            var completedResults = GetCompletedResults(game, raceIndex);

            var teamIds = game.CurrentConstructorStandings
                .Select(s => s.TeamId)
                .Distinct()
                .ToList();

            if (teamIds.Count <= 1)
                return true;

            var standings = teamIds
                .Select(teamId => new
                {
                    TeamId = teamId,
                    Points = CalculateConstructorPoints(
                        teamId, season, completedResults)
                })
                .OrderByDescending(s => s.Points)
                .ToList();

            var leader = standings.First();
            var remainingRaces = races.Skip(raceIndex + 1).ToList();

            foreach (var rival in standings.Skip(1))
            {
                double rivalMaximumFinalPoints =
                        rival.Points + CalculateMaximumConstructorPoints(
                            rival.TeamId,
                            season,
                            completedResults,
                            remainingRaces);

                if (rivalMaximumFinalPoints > leader.Points)
                    return false;

                if (rivalMaximumFinalPoints == leader.Points)
                {
                    var rivalMaximumTally = CalculateMaximumTeamPositionsTally(
                        rival.TeamId,
                        season,
                        completedResults,
                        remainingRaces);

                    var leaderTally = CalculateTeamPositionsTally(
                        leader.TeamId,
                        completedResults);

                    if (rivalMaximumTally >= leaderTally)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true if the driver title is already mathematically decided.
        /// If atThisExactRace is true, returns true only if the title became
        /// mathematically decided at the specified race.
        /// </summary>
        public static bool HasDriverChampionshipClinched(
            ISaveGame game,
            out bool clinchedThisExactRace,
            int? raceIndex = null)
        {
            clinchedThisExactRace = false;

            bool clinchedNow =
                IsDriverChampionshipClinchedAtRace(game, raceIndex ?? game.NextGpIndex);

            if (!clinchedNow) return false;

            bool clinchedBefore = ((raceIndex ?? game.NextGpIndex) > 0) && IsDriverChampionshipClinchedAtRace(game, (raceIndex ?? game.NextGpIndex) - 1);

            clinchedThisExactRace = !clinchedBefore;

            return true;
        }

        private static bool IsDriverChampionshipClinchedAtRace(
            ISaveGame game,
            int raceIndex)
        {
            var season = game.CurrentSeason;
            var races = season.Races.ToList();

            if (raceIndex >= races.Count - 1)
                return true;

            var completedResults = GetCompletedResults(game, raceIndex);

            var driverIds = game.CurrentDriverStandings
                .Select(s => s.DriverId)
                .Distinct()
                .ToList();

            if (driverIds.Count <= 1)
                return true;

            var standings = driverIds
                .Select(driverId => new
                {
                    DriverId = driverId,
                    Points = CalculateDriverSeasonPoints(
                        driverId, season, completedResults)
                })
                .OrderByDescending(s => s.Points)
                .ToList();

            var leader = standings.First();
            var remainingRaces = races.Skip(raceIndex + 1).ToList();

            foreach (var rival in standings.Skip(1))
            {
                double rivalMaximum = CalculateMaximumDriverPoints(
                    rival.DriverId,
                    season,
                    completedResults,
                    remainingRaces);

                if (rivalMaximum > leader.Points)
                    return false;

                if (rivalMaximum == leader.Points)
                {
                    var rivalMaximumTally = CalculateMaximumDriverPositionsTally(
                        rival.DriverId,
                        season,
                        completedResults,
                        remainingRaces);

                    var leaderTally = CalculateDriverPositionsTally(
                        leader.DriverId,
                        completedResults);

                    if (rivalMaximumTally >= leaderTally)
                        return false;
                }
            }

            return true;
        }

        private static List<GrandPrixResult> GetCompletedResults(
            ISaveGame game,
            int raceIndex)
        {
            return game.GrandPrixResults
                .Where(r => r.Year == game.CurrentSeason.Year)
                .Take(raceIndex + 1)
                .ToList();
        }

        private static double CalculateMaximumDriverPoints(
            string driverId,
            ISeason season,
            List<GrandPrixResult> completedResults,
            List<Race> remainingRaces)
        {
            var points = completedResults
                .SelectMany(r => r.RaceResults)
                .Where(sr => sr.DriverId == driverId)
                .Select(sr => (double)sr.Points)
                .ToList();

            foreach (var race in remainingRaces)
            {
                var pointsSystem =
                    race.PointsSystem ?? season.PointsSystem;

                points.Add(pointsSystem.Values.Max()
                    + (race.PointsForFastestLap ?? 0));
            }

            return GetBestNPoints(points, season);
        }

        private static double CalculateConstructorPoints(
            string teamId,
            ISeason season,
            List<GrandPrixResult> results)
        {
            return results
                 .SelectMany(r => r.RaceResults)
                 .Where(r => r.TeamId == teamId)
                 .Sum(r => r.Points);
        }

        private static double CalculateMaximumConstructorPoints(
            string teamId,
            ISeason season,
            List<GrandPrixResult> completedResults,
            List<Race> remainingRaces)
        {
            double maximumPoints = 0;

            foreach (var race in remainingRaces)
            {
                var pointsSystem = race.PointsSystem ?? season.PointsSystem;

                maximumPoints += pointsSystem.Values
                    .OrderByDescending(points => points)
                    .Take(2)
                    .Sum();

                maximumPoints += race.PointsForFastestLap ?? 0;
            }

            return maximumPoints;
        }

        private static double GetBestNPoints(
            List<double> points,
            ISeason season)
        {
            int countedRaces = season.RacesToCountTowardsChampionship
                ?? points.Count;

            return points
                .OrderByDescending(p => p)
                .Take(countedRaces)
                .Sum();
        }

        private static PositionsTally CalculateDriverPositionsTally(string driverId,List<GrandPrixResult> completedResults)
        {
            var tally = new PositionsTally();

            foreach (var result in completedResults)
            {
                foreach (var sr in result.RaceResults
                             .Where(sr => sr.DriverId == driverId))
                {
                    tally.AddPosition(sr.Position);
                }
            }

            return tally;
        }

        private static PositionsTally CalculateTeamPositionsTally(
            string teamId,
            List<GrandPrixResult> completedResults)
        {
            var tally = new PositionsTally();

            foreach (var result in completedResults)
            {
                foreach (var sr in result.RaceResults
                             .Where(sr => sr.TeamId == teamId))
                {
                    tally.AddPosition(sr.Position);
                }
            }

            return tally;
        }

        private static PositionsTally CalculateMaximumDriverPositionsTally(
            string driverId,
            ISeason season,
            List<GrandPrixResult> completedResults,
            List<Race> remainingRaces)
        {
            var tally = CalculateDriverPositionsTally(
                driverId, completedResults);

            // Assume the rival finishes first in every remaining race.
            foreach (var race in remainingRaces)
            {
                tally.AddPosition(1);
            }

            return tally;
        }

        private static PositionsTally CalculateMaximumTeamPositionsTally(
            string teamId,
            ISeason season,
            List<GrandPrixResult> completedResults,
            List<Race> remainingRaces)
        {
            var tally = CalculateTeamPositionsTally(
                teamId, completedResults);

            // Assume the rival's two drivers finish first and second
            // in every remaining race.
            foreach (var race in remainingRaces)
            {
                tally.AddPosition(1);
                tally.AddPosition(2);
            }

            return tally;
        }
    }
}
