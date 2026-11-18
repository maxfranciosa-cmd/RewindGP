namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// The age thresholds of one F1 era, from its first season until the next era starts: under
    /// YoungDriverAge a driver is YOUNG_*, from OldDriverAge on AGEING_*, PRIME_* in between; a
    /// driver may retire from MinRetiringAge and always has past MaxRetiringAge.
    /// </summary>
    public sealed record AgeEra(int FirstSeason, int YoungDriverAge, int OldDriverAge, int MinRetiringAge, int MaxRetiringAge);

    /// <summary>
    /// Drivers peaked later before the modern era: the age bands depend on the season. The year is
    /// the one the age is measured in (in the off-season, the season being hired for). The skill
    /// scripts SeasonPack.Common.ps1 and Test-SeasonProject.ps1 mirror this table.
    /// </summary>
    public static class AgeEras
    {
        // ordered by FirstSeason; the first row also covers any earlier season
        public static readonly IReadOnlyList<AgeEra> All = new[]
        {
            new AgeEra(FirstSeason: 1960, YoungDriverAge: 27, OldDriverAge: 33, MinRetiringAge: 34, MaxRetiringAge: 45),
            new AgeEra(FirstSeason: 2006, YoungDriverAge: 25, OldDriverAge: 31, MinRetiringAge: 34, MaxRetiringAge: 45),
        };

        public static AgeEra Modern => All[All.Count - 1];

        // the latest era started by seasonYear; no year means the modern era
        public static AgeEra For(int? seasonYear) =>
            seasonYear is int year ? All.LastOrDefault(e => e.FirstSeason <= year) ?? All[0] : Modern;
    }
}
