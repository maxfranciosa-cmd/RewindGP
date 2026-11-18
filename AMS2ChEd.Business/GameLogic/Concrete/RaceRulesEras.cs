namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// The race rules of one F1 era, from its first season until the next era starts.
    /// PitSpeedLimitKph is null when there was no pit lane speed limit.
    /// </summary>
    public sealed record RaceRulesEra(int FirstSeason, bool StandingStart, bool RefuellingAllowed, int? PitSpeedLimitKph, bool SafetyCar);

    /// <summary>
    /// The rules a race is run under depend on the season: refuelling was banned from 1984 to 1993
    /// and again from 2010, the safety car came in 1993, the pit lane speed limit in 1994.
    /// </summary>
    public static class RaceRulesEras
    {
        // ordered by FirstSeason; the first row also covers any earlier season
        public static readonly IReadOnlyList<RaceRulesEra> All = new[]
        {
            new RaceRulesEra(FirstSeason: 1960, StandingStart: true, RefuellingAllowed: true,  PitSpeedLimitKph: null, SafetyCar: false),
            new RaceRulesEra(FirstSeason: 1984, StandingStart: true, RefuellingAllowed: false, PitSpeedLimitKph: null, SafetyCar: false),
            new RaceRulesEra(FirstSeason: 1993, StandingStart: true, RefuellingAllowed: false, PitSpeedLimitKph: null, SafetyCar: true),
            new RaceRulesEra(FirstSeason: 1994, StandingStart: true, RefuellingAllowed: true,  PitSpeedLimitKph: 80,   SafetyCar: true),
            new RaceRulesEra(FirstSeason: 2010, StandingStart: true, RefuellingAllowed: false, PitSpeedLimitKph: 80,   SafetyCar: true),
        };

        public static RaceRulesEra Modern => All[All.Count - 1];

        // the latest era started by seasonYear; no year means the modern era
        public static RaceRulesEra For(int? seasonYear) =>
            seasonYear is int year ? All.LastOrDefault(e => e.FirstSeason <= year) ?? All[0] : Modern;
    }
}
