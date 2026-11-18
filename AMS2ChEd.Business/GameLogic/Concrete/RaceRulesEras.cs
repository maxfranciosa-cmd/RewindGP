namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// The race rules of one F1 era, from its first season until the next era starts.
    /// PitSpeedLimit and SafetyCar are recorded but not applied yet: the AMS2 module doesn't know
    /// where AMS2 keeps its pit speed limit and Full Course Yellow switches.
    /// </summary>
    public sealed record RaceRulesEra(int FirstSeason, bool StandingStart, bool RefuellingAllowed, bool PitSpeedLimit, bool SafetyCar);

    /// <summary>
    /// The rules a race is run under depend on the season: refuelling was banned from 1984 to 1993
    /// and again from 2010, the safety car came in 1993, the pit lane speed limit in 1994.
    /// </summary>
    public static class RaceRulesEras
    {
        // ordered by FirstSeason; the first row also covers any earlier season
        public static readonly IReadOnlyList<RaceRulesEra> All = new[]
        {
            new RaceRulesEra(FirstSeason: 1960, StandingStart: true, RefuellingAllowed: true,  PitSpeedLimit: false, SafetyCar: false),
            new RaceRulesEra(FirstSeason: 1984, StandingStart: true, RefuellingAllowed: false, PitSpeedLimit: false, SafetyCar: false),
            new RaceRulesEra(FirstSeason: 1993, StandingStart: true, RefuellingAllowed: false, PitSpeedLimit: false, SafetyCar: true),
            new RaceRulesEra(FirstSeason: 1994, StandingStart: true, RefuellingAllowed: true,  PitSpeedLimit: true,  SafetyCar: true),
            new RaceRulesEra(FirstSeason: 2010, StandingStart: true, RefuellingAllowed: false, PitSpeedLimit: true,  SafetyCar: true),
        };

        public static RaceRulesEra Modern => All[All.Count - 1];

        // the latest era started by seasonYear; no year means the modern era
        public static RaceRulesEra For(int? seasonYear) =>
            seasonYear is int year ? All.LastOrDefault(e => e.FirstSeason <= year) ?? All[0] : Modern;
    }
}
