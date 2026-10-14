using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    public enum WinMilestoneKind
    {
        // the driver's win count now equals the legend's
        LEVEL,
        // the driver's win count has just gone past the legend's
        PASSED,
        // the driver has just gone past everyone and holds the all-time record alone
        RECORD
    }

    public class WinMilestone
    {
        public WinMilestoneKind Kind { get; set; }
        public HallOfFameEntry Legend { get; set; }
        public int Wins { get; set; }
    }

    public enum TitleComparisonKind
    {
        // the driver's title count now equals the legends'
        JOINS,
        // nobody else has the driver's title count, but the legends had one fewer
        PASSES,
        // the driver has just gone past everyone and holds the all-time record alone
        RECORD,
        // the driver already held the all-time record alone and has extended it
        EXTENDS_RECORD
    }

    public class TitleComparison
    {
        public TitleComparisonKind Kind { get; set; }
        // one or two legends, oldest era first; empty for EXTENDS_RECORD
        public List<HallOfFameEntry> Legends { get; set; } = new();
        public int Titles { get; set; }
    }

    /// <summary>
    /// Compares a driver's tally with the greats of the sport, as history stands in the save
    /// (the same table <see cref="HallOfFameCalculator"/> builds): who they have just drawn
    /// level with or gone past. Feeds the legends paragraphs of the post-race and championship
    /// articles.
    /// </summary>
    public static class LegendsCalculator
    {
        // below this many wins a driver drawing level with someone isn't news yet
        public const int MinWinsForMilestone = 5;

        // a driver counts as a legend worth naming with at least one title or this many wins
        public const int MinWinsForLegend = 10;

        // the legends paragraph of the championship article only starts from the second title
        public const int MinTitlesForComparison = 2;

        /// <summary>
        /// The legend whose win count <paramref name="driverId"/> has just matched or gone past,
        /// with their latest win already counted in <paramref name="hallOfFame"/>. Null when
        /// there's nobody worth naming.
        /// </summary>
        public static WinMilestone FindWinMilestone(IReadOnlyList<HallOfFameEntry> hallOfFame, string driverId)
        {
            var driver = hallOfFame?.FirstOrDefault(e => e.DriverId == driverId);
            if (driver == null || driver.Wins < MinWinsForMilestone)
                return null;

            int wins = driver.Wins;
            var others = hallOfFame.Where(e => e.DriverId != driverId).ToList();

            // a new all-time record trumps any single name: everyone else is now behind
            int bestOfTheRest = others.Count > 0 ? others.Max(e => e.Wins) : 0;
            if (bestOfTheRest == wins - 1)
            {
                return new WinMilestone
                {
                    Kind = WinMilestoneKind.RECORD,
                    Legend = MostFamous(others.Where(e => e.Wins == bestOfTheRest)),
                    Wins = wins
                };
            }

            var legends = others.Where(IsLegend).ToList();

            var level = MostFamous(legends.Where(e => e.Wins == wins));
            var passed = MostFamous(legends.Where(e => e.Wins == wins - 1));

            if (level == null && passed == null)
                return null;

            // name whoever is the greater figure; ties go to drawing level, the bigger moment
            if (level != null && (passed == null || CompareFame(level, passed) >= 0))
                return new WinMilestone { Kind = WinMilestoneKind.LEVEL, Legend = level, Wins = wins };

            return new WinMilestone { Kind = WinMilestoneKind.PASSED, Legend = passed, Wins = wins };
        }

        /// <summary>
        /// The legends a freshly-crowned champion now sits alongside, or has just gone past.
        /// <paramref name="championshipYear"/> is the title just won, counted whether or not the
        /// save's history already holds it. Null for a first title, or when there's nobody to name.
        /// </summary>
        public static TitleComparison FindTitleComparison(IReadOnlyList<HallOfFameEntry> hallOfFame, string driverId, int championshipYear)
        {
            var driver = hallOfFame?.FirstOrDefault(e => e.DriverId == driverId);
            var titleYears = (driver?.ChampionshipYears ?? new List<int>())
                .Union(new[] { championshipYear })
                .ToList();
            int titles = titleYears.Count;

            if (titles < MinTitlesForComparison)
                return null;

            var others = (hallOfFame ?? new List<HallOfFameEntry>())
                .Where(e => e.DriverId != driverId && e.Championships > 0)
                .ToList();

            int bestOfTheRest = others.Count > 0 ? others.Max(e => e.Championships) : 0;

            var equals = others.Where(e => e.Championships == titles).ToList();
            if (equals.Count > 0)
                return new TitleComparison { Kind = TitleComparisonKind.JOINS, Legends = PickAcrossEras(equals), Titles = titles };

            var oneBehind = others.Where(e => e.Championships == titles - 1).ToList();
            if (oneBehind.Count > 0)
            {
                var kind = bestOfTheRest == titles - 1 ? TitleComparisonKind.RECORD : TitleComparisonKind.PASSES;
                return new TitleComparison { Kind = kind, Legends = PickAcrossEras(oneBehind), Titles = titles };
            }

            if (titles > bestOfTheRest)
                return new TitleComparison { Kind = TitleComparisonKind.EXTENDS_RECORD, Titles = titles };

            return null;
        }

        private static bool IsLegend(HallOfFameEntry entry) =>
            entry.Championships > 0 || entry.Wins >= MinWinsForLegend;

        private static HallOfFameEntry MostFamous(IEnumerable<HallOfFameEntry> entries) =>
            entries
                .OrderByDescending(e => e.Championships)
                .ThenByDescending(e => e.Wins)
                .ThenBy(e => e.DriverId, StringComparer.Ordinal)
                .FirstOrDefault();

        private static int CompareFame(HallOfFameEntry a, HallOfFameEntry b)
        {
            int byTitles = a.Championships.CompareTo(b.Championships);
            return byTitles != 0 ? byTitles : a.Wins.CompareTo(b.Wins);
        }

        /// <summary>
        /// Up to two legends from different eras: the one whose last title came earliest and the
        /// one whose last title came latest ("joins Jim Clark and Fernando Alonso").
        /// </summary>
        private static List<HallOfFameEntry> PickAcrossEras(List<HallOfFameEntry> candidates)
        {
            var byEra = candidates
                .OrderBy(e => e.ChampionshipYears.DefaultIfEmpty(0).Max())
                .ThenBy(e => e.DriverId, StringComparer.Ordinal)
                .ToList();

            if (byEra.Count == 1)
                return new List<HallOfFameEntry> { byEra[0] };

            return new List<HallOfFameEntry> { byEra.First(), byEra.Last() };
        }
    }
}
