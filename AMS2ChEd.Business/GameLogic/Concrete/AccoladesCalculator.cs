using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    public class AccoladeSummary
    {
        public int Wins { get; set; }
        public int Podiums { get; set; }
        public int PolePositions { get; set; }
        public List<int> ChampionshipYears { get; set; } = new();
        public int Championships => ChampionshipYears.Count;

        /// <summary>
        /// True when the all-time records cover every season before <see cref="StartYear"/>, so the
        /// counts here are career totals (a driver or team the records don't list really had
        /// nothing before the save). False when the records are missing or stop short of the
        /// save's first season, meaning the counts only reflect what happened since
        /// <see cref="StartYear"/>.
        /// </summary>
        public bool HasBaseline { get; set; }

        public int StartYear { get; set; }
    }

    /// <summary>
    /// A driver's or team's record as history stands in a save: what really happened before the
    /// save began (the all-time records shipped with the app, up to the season before the save's
    /// first) plus a scan of the save's own race/season history. Shared by the accolades windows,
    /// the hall of fame and news-article generation so they all show identical numbers.
    /// </summary>
    public static class AccoladesCalculator
    {
        /// <summary>
        /// The year this save began: the earliest completed season on record, or the current season's
        /// year if none has been completed yet.
        /// </summary>
        public static int GetStartYear(ISaveGame saveGame) =>
            saveGame.HistoricalDriverStandings?.Any() == true
                ? saveGame.HistoricalDriverStandings.Min(s => s.Year)
                : saveGame.CurrentSeason.Year;

        public static AccoladeSummary GetDriverAccolades(ISaveGame saveGame, AllTimeRecords allTimeRecords, string driverId, int? justClinchedChampionshipYear = null)
        {
            var record = allTimeRecords?.Drivers?.FirstOrDefault(d => d.DriverId == driverId);

            var allRaceResults = saveGame.GrandPrixResults.SelectMany(gp => gp.RaceResults ?? new List<SessionResult>()).Where(r => r.DriverId == driverId);
            var allQualiResults = saveGame.GrandPrixResults.SelectMany(gp => gp.QualifyingResults ?? new List<SessionResult>()).Where(r => r.DriverId == driverId);

            var championshipYearsInSave = saveGame.HistoricalDriverStandings
                .Where(s => s.Standing.Any(e => e.DriverId == driverId && e.Position == 1))
                .Select(s => s.Year);

            return BuildSummary(saveGame, allTimeRecords, record, allRaceResults, allQualiResults, championshipYearsInSave, justClinchedChampionshipYear);
        }

        /// <summary>
        /// How many of the most recent races (walking backwards, inclusive of the latest result in
        /// saveGame.GrandPrixResults) this driver won consecutively. Spans season boundaries
        /// deliberately - a win streak is conventionally about consecutive races, not consecutive
        /// races within one season. Any race the driver didn't win (including one they didn't enter)
        /// breaks the streak.
        /// </summary>
        public static int GetDriverWinStreak(ISaveGame saveGame, string driverId)
        {
            int streak = 0;
            foreach (var gp in saveGame.GrandPrixResults.Reverse())
            {
                var result = (gp.RaceResults ?? new List<SessionResult>()).FirstOrDefault(r => r.DriverId == driverId);
                if (result == null || result.Position != 1)
                    break;
                streak++;
            }
            return streak;
        }

        public static AccoladeSummary GetTeamAccolades(ISaveGame saveGame, AllTimeRecords allTimeRecords, string teamId, int? justClinchedChampionshipYear = null)
        {
            var record = allTimeRecords?.Teams?.FirstOrDefault(t => t.TeamId == teamId);

            var allRaceResults = saveGame.GrandPrixResults.SelectMany(gp => gp.RaceResults ?? new List<SessionResult>()).Where(r => r.TeamId == teamId);
            var allQualiResults = saveGame.GrandPrixResults.SelectMany(gp => gp.QualifyingResults ?? new List<SessionResult>()).Where(r => r.TeamId == teamId);

            var championshipYearsInSave = saveGame.HistoricalConstructorStandings
                .Where(s => s.Standing.Any(e => e.TeamId == teamId && e.Position == 1))
                .Select(s => s.Year);

            return BuildSummary(saveGame, allTimeRecords, record, allRaceResults, allQualiResults, championshipYearsInSave, justClinchedChampionshipYear);
        }

        private static AccoladeSummary BuildSummary(
            ISaveGame saveGame,
            AllTimeRecords allTimeRecords,
            AllTimeRecord record,
            IEnumerable<SessionResult> raceResults,
            IEnumerable<SessionResult> qualiResults,
            IEnumerable<int> championshipYearsInSave,
            int? justClinchedChampionshipYear)
        {
            int startYear = GetStartYear(saveGame);

            // from the save's first season on, history is whatever happened in the save
            var championshipYears = (record?.Championships ?? new List<int>())
                .Where(year => year < startYear)
                .Union(championshipYearsInSave)
                .ToList();

            if (justClinchedChampionshipYear.HasValue && !championshipYears.Contains(justClinchedChampionshipYear.Value))
                championshipYears.Add(justClinchedChampionshipYear.Value);

            return new AccoladeSummary
            {
                Wins = SumBefore(record?.WinsByYear, startYear) + raceResults.Count(r => r.Position == 1),
                Podiums = SumBefore(record?.PodiumsByYear, startYear) + raceResults.Count(r => r.Position >= 1 && r.Position <= 3),
                PolePositions = SumBefore(record?.PolesByYear, startYear) + qualiResults.Count(r => r.Position == 1),
                ChampionshipYears = championshipYears.OrderBy(y => y).ToList(),
                HasBaseline = allTimeRecords != null && allTimeRecords.UpTo >= startYear - 1,
                StartYear = startYear
            };
        }

        private static int SumBefore(Dictionary<int, int> countByYear, int startYear) =>
            countByYear?.Where(c => c.Key < startYear).Sum(c => c.Value) ?? 0;
    }
}
