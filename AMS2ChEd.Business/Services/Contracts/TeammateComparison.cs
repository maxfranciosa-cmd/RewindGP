namespace AMS2ChEd.Business.Services.Contracts
{
    /// <summary>
    /// How a driver's season measured up against the only drivers in the same car: their
    /// teammates' finishes, and how dominant that car was. Read by
    /// <see cref="ReputationUpdater.StandingsJudgedBy"/>.
    /// </summary>
    public class TeammateComparison
    {
        // races where the driver and a teammate were both classified, by who finished ahead
        public int RacesAhead { get; set; }
        public int RacesBehind { get; set; }

        public int Wins { get; set; }
        public int TeammateWins { get; set; }

        // the team won at least half the races, or scored well clear of the next constructor
        public bool DominantCar { get; set; }

        public int HeadToHeadRaces => RacesAhead + RacesBehind;
        public double HeadToHeadShare => HeadToHeadRaces == 0 ? 0 : (double)RacesAhead / HeadToHeadRaces;
    }
}
