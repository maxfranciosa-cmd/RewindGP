using AMS2ChEd.Business.Settings;

namespace AMS2ChEd.Business.Services.CrashDetection
{
    public enum CrashOutcomeKind
    {
        Unhurt,
        Injured,
        Fatal
    }

    public class CrashOutcome
    {
        public CrashOutcomeKind Kind { get; set; }

        // Injured: the races they miss, already capped at the races left in the season (0 after the last one)
        public int RacesOut { get; set; }
    }

    /// <summary>
    /// What a crash does to the driver (crash-incidents-design.md, section 2): most walk away, some
    /// are injured for a few races, very rarely one dies. Only a Severe crash is a risk at all, and
    /// the odds fall with each era's safety milestones. With fatalities off (or for the player's own
    /// driver without their own option) the fatal outcome becomes a long injury instead. A crash
    /// the driver drove on from is a third as likely to injure, for a shorter spell, and never kills.
    /// </summary>
    public static class CrashOutcomeRoller
    {
        // injury lengths, in races, and how often each happens
        private static readonly (int Races, double Weight)[] InjuryLengths =
        {
            (1, 0.40), (2, 0.25), (3, 0.15), (4, 0.10), (5, 0.04), (6, 0.03), (7, 0.02), (8, 0.01)
        };

        // after driving on: the era's injury chance times this, and one of these lengths
        public const double DroveOnInjuryFactor = 1.0 / 3;
        private static readonly (int Races, double Weight)[] DroveOnInjuryLengths =
        {
            (1, 0.60), (2, 0.30), (3, 0.10)
        };

        // the injury a fatal crash becomes when nobody can die
        public const int LongInjuryMinRaces = 6;
        public const int LongInjuryMaxRaces = 10;

        /// <summary>
        /// A Severe crash's chances, by the season's year: to be injured, and to be killed.
        /// Very high before the 1960s safety campaigns, lower with the 1970s barriers and deformable
        /// structures, far lower after the 1981 carbon survival cell, very low after the post-1994
        /// reforms, near zero with HANS (2003) and the halo (2018).
        /// </summary>
        public static (double Injury, double Fatal) Odds(int year) => year switch
        {
            < 1960 => (0.55, 0.15),
            < 1970 => (0.50, 0.10),
            < 1981 => (0.40, 0.05),
            < 1995 => (0.30, 0.015),
            < 2003 => (0.20, 0.004),
            < 2018 => (0.12, 0.0015),
            _ => (0.08, 0.0005)
        };

        public static CrashOutcome Roll(CrashSeverity severity, int year, bool isPlayer, CrashOptions options, int racesLeft, Random random, bool droveOn = false)
        {
            if (severity != CrashSeverity.Severe)
                return new CrashOutcome { Kind = CrashOutcomeKind.Unhurt };

            var (injury, fatal) = Odds(year);
            var roll = random.NextDouble();

            if (droveOn)
            {
                return roll < injury * DroveOnInjuryFactor
                    ? Injured(InjuryLength(random, DroveOnInjuryLengths), racesLeft)
                    : new CrashOutcome { Kind = CrashOutcomeKind.Unhurt };
            }

            if (roll < fatal)
            {
                var canDie = options?.FatalitiesEnabled == true && (!isPlayer || options.PlayerDeathEnabled);
                return canDie
                    ? new CrashOutcome { Kind = CrashOutcomeKind.Fatal }
                    : Injured(random.Next(LongInjuryMinRaces, LongInjuryMaxRaces + 1), racesLeft);
            }

            if (roll < fatal + injury)
                return Injured(InjuryLength(random, InjuryLengths), racesLeft);

            return new CrashOutcome { Kind = CrashOutcomeKind.Unhurt };
        }

        private static CrashOutcome Injured(int races, int racesLeft) =>
            new() { Kind = CrashOutcomeKind.Injured, RacesOut = Math.Clamp(races, 0, Math.Max(0, racesLeft)) };

        private static int InjuryLength(Random random, (int Races, double Weight)[] lengths)
        {
            var ticket = random.NextDouble() * lengths.Sum(l => l.Weight);
            foreach (var (races, weight) in lengths)
            {
                ticket -= weight;
                if (ticket < 0)
                    return races;
            }
            return lengths[^1].Races;
        }
    }
}
