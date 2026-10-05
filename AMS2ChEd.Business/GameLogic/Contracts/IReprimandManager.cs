using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public enum ReprimandConsequence
    {
        NONE,
        // the driver won't be retained at the end of the season
        FINAL_WARNING,
        // the driver is replaced for the rest of the season
        RELEASED,
        // the driver would have been released, but leads the championship
        SPARED_AS_LEADER
    }

    public class ReprimandNewsItem
    {
        public string DriverId { get; set; }
        public string TeamId { get; set; }
        public string TeammateId { get; set; }
        public ReprimandReason Reason { get; set; }
        public int ReprimandCount { get; set; }
        public ReprimandConsequence Consequence { get; set; }
        public string ReplacementDriverId { get; set; }
        public bool IsPlayer { get; set; }
    }

    public interface IReprimandManager
    {
        /// <summary>
        /// Hands out team-orders reprimands for a just-finished race, and applies their
        /// consequences (releasing a driver for the rest of the season by adding absences).
        /// Must be called after the standings have been updated with this race, but before
        /// NextGpIndex moves on to the next race.
        /// </summary>
        IReadOnlyList<ReprimandNewsItem> ProcessRace(
            ISaveGame saveGame,
            GrandPrixResult result,
            IReadOnlyList<HistoricalDriverStandingEntry> preRaceStandings,
            IReadOnlyCollection<string> playerContactDriverIds);

        int GetReprimandCount(ISaveGame saveGame, string driverId, string teamId);
    }
}
