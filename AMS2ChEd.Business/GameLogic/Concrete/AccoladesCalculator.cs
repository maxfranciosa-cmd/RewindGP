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
        /// True only when the season pack shipped a real accolades.json baseline entry for this
        /// specific driver/team id (even if that entry is all zeros) - false when there's no entry at
        /// all, meaning any count returned here only reflects what happened since <see cref="StartYear"/>,
        /// not a trustworthy career total.
        /// </summary>
        public bool HasBaseline { get; set; }

        public int StartYear { get; set; }
    }

    /// <summary>
    /// Combines a save's frozen pre-save-start accolades baseline (<see cref="ISaveGame.AccoladesAtStart"/>)
    /// with a scan of the save's own race/season history, the same "baseline + count" pattern
    /// DriverAccoladesWindow/ConstructorAccoladesWindow already used independently, now shared so
    /// news-article generation can use identical numbers.
    /// </summary>
    public static class AccoladesCalculator
    {
        /// <summary>
        /// The year this save began: the earliest completed season on record, or the current season's
        /// year if none has been completed yet. Same derivation GameEngine.LoadGame already uses for
        /// its own backward-compat AccoladesAtStart fallback.
        /// </summary>
        public static int GetStartYear(ISaveGame saveGame) =>
            saveGame.HistoricalDriverStandings?.Any() == true
                ? saveGame.HistoricalDriverStandings.Min(s => s.Year)
                : saveGame.CurrentSeason.Year;

        public static AccoladeSummary GetDriverAccolades(ISaveGame saveGame, string driverId, int? justClinchedChampionshipYear = null)
        {
            bool hasBaseline = saveGame.AccoladesAtStart?.DriverAccolades?.ContainsKey(driverId) == true;
            var baseAccolades = saveGame.AccoladesAtStart?.DriverAccolades?.GetValueOrDefault(driverId) ?? new Accolades();

            var allRaceResults = saveGame.GrandPrixResults.SelectMany(gp => gp.RaceResults ?? new List<SessionResult>());
            var allQualiResults = saveGame.GrandPrixResults.SelectMany(gp => gp.QualifyingResults ?? new List<SessionResult>());

            var championshipYears = (baseAccolades.Championships ?? new List<int>())
                .Union(saveGame.HistoricalDriverStandings
                    .Where(s => s.Standing.Any(e => e.DriverId == driverId && e.Position == 1))
                    .Select(s => s.Year))
                .ToList();

            if (justClinchedChampionshipYear.HasValue && !championshipYears.Contains(justClinchedChampionshipYear.Value))
                championshipYears.Add(justClinchedChampionshipYear.Value);

            return new AccoladeSummary
            {
                Wins = baseAccolades.Wins + allRaceResults.Count(r => r.DriverId == driverId && r.Position == 1),
                Podiums = baseAccolades.Podiums + allRaceResults.Count(r => r.DriverId == driverId && r.Position >= 1 && r.Position <= 3),
                PolePositions = baseAccolades.PolePositions + allQualiResults.Count(r => r.DriverId == driverId && r.Position == 1),
                ChampionshipYears = championshipYears.OrderBy(y => y).ToList(),
                HasBaseline = hasBaseline,
                StartYear = GetStartYear(saveGame)
            };
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

        public static AccoladeSummary GetTeamAccolades(ISaveGame saveGame, string teamId, int? justClinchedChampionshipYear = null)
        {
            bool hasBaseline = saveGame.AccoladesAtStart?.TeamsAccolades?.ContainsKey(teamId) == true;
            var baseAccolades = saveGame.AccoladesAtStart?.TeamsAccolades?.GetValueOrDefault(teamId) ?? new Accolades();

            var allRaceResults = saveGame.GrandPrixResults.SelectMany(gp => gp.RaceResults ?? new List<SessionResult>());
            var allQualiResults = saveGame.GrandPrixResults.SelectMany(gp => gp.QualifyingResults ?? new List<SessionResult>());

            var championshipYears = (baseAccolades.Championships ?? new List<int>())
                .Union(saveGame.HistoricalConstructorStandings
                    .Where(s => s.Standing.Any(e => e.TeamId == teamId && e.Position == 1))
                    .Select(s => s.Year))
                .ToList();

            if (justClinchedChampionshipYear.HasValue && !championshipYears.Contains(justClinchedChampionshipYear.Value))
                championshipYears.Add(justClinchedChampionshipYear.Value);

            return new AccoladeSummary
            {
                Wins = baseAccolades.Wins + allRaceResults.Count(r => r.TeamId == teamId && r.Position == 1),
                Podiums = baseAccolades.Podiums + allRaceResults.Count(r => r.TeamId == teamId && r.Position >= 1 && r.Position <= 3),
                PolePositions = baseAccolades.PolePositions + allQualiResults.Count(r => r.TeamId == teamId && r.Position == 1),
                ChampionshipYears = championshipYears.OrderBy(y => y).ToList(),
                HasBaseline = hasBaseline,
                StartYear = GetStartYear(saveGame)
            };
        }
    }
}
