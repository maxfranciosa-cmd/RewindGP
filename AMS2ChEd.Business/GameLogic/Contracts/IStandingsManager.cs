using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public interface IStandingsManager
    {
        event EventHandler<StandingsUpdatedEventArgs> StandingsUpdated;

        void UpdateStandings(ISaveGame saveGame, GrandPrixResult result);

        IEnumerable<DriverStandingDisplayData> GetDriverStandingsDisplay(ISaveGame saveGame);

        IEnumerable<ConstructorStandingDisplayData> GetConstructorStandingsDisplay(ISaveGame saveGame, Dictionary<string, string> teamNames);

    }
    public class DriverStandingDisplayData
    {
        public int Position { get; set; }
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string TeamId { get; set; }
        public double Points { get; set; }
        public bool IsPlayer { get; set; }
    }

    public class ConstructorStandingDisplayData
    {
        public int Position { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public double Points { get; set; }
        public bool IsPlayerTeam { get; set; }
    }

    public class StandingsUpdatedEventArgs : EventArgs
    {
        public IEnumerable<HistoricalDriverStandingEntry> DriverStandings { get; set; }
        public IEnumerable<ConstructorStandingEntry> ConstructorStandings { get; set; }
    }
}
