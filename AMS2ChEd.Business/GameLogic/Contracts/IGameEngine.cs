using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public interface IGameEngine
    {
        event EventHandler<GameStateChangedEventArgs> GameStateChanged;
        event EventHandler<SeasonProgressionEventArgs> SeasonProgressed;
        event EventHandler<string> ErrorOccurred;

        ISaveGame CurrentGame { get; }
        bool IsGameActive { get; }

        ISaveGame CreateNewGame(
            string playerName,
            string playerNationality,
            int playerAge,
            DriverReputation playerReputation,
            IEnumerable<int> favouriteNumbers,
            ISeason season,
            string selectedTeamId,
            string replacedDriverId,
            List<IDriverData> seasonDrivers);

        ISaveGame CreateNewGameWithExistingDriver(
            ISeason season,
            string selectedTeamId,
            string driverId,
            List<IDriverData> seasonDrivers);

        void LoadGame(ISaveGame saveGame);
        void ProgressToNextGrandPrix();
        void CompleteGrandPrix(GrandPrixResult result);
    }

    public enum GameState
    {
        MainMenu,
        PlayerCreation,
        TeamSelection,
        ContractNegotiation,
        SeasonOverview,
        PreGrandPrix,
        RaceWeekend,
        PostGrandPrix
    }

    public class GameStateChangedEventArgs : EventArgs
    {
        public GameState NewState { get; set; }
    }

    public class SeasonProgressionEventArgs : EventArgs
    {
        public bool IsSeasonComplete { get; set; }
        public string Message { get; set; }
    }
}
