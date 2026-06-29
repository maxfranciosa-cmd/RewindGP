using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Core game engine managing the overall game state and progression
    /// </summary>
    public class GameEngine : IGameEngine
    {
        // Events for UI updates
        public event EventHandler<GameStateChangedEventArgs> GameStateChanged;
        public event EventHandler<SeasonProgressionEventArgs> SeasonProgressed;
        public event EventHandler<string> ErrorOccurred;

        private ISaveGame _currentGame;

        public ISaveGame CurrentGame => _currentGame;
        public bool IsGameActive => _currentGame != null;

        /// <summary>
        /// Create a new game with player data
        /// </summary>
        public ISaveGame CreateNewGame(
            string playerName,
            string playerNationality,
            int playerAge,
            DriverReputation playerReputation,
            IEnumerable<int> favouriteNumbers,
            ISeason season,
            string selectedTeamId,
            string replacedDriverId,
            List<IDriverData> seasonDrivers)
        {
            var playerDriverId = $"player_{playerName.ToLower().Replace(" ", "_")}";

            // Clone and modify the season
            var modifiedSeason = CloneAndModifySeason(season, playerDriverId, selectedTeamId, replacedDriverId);

            // Initialize standings
            var driverStandings = InitializeDriverStandings(modifiedSeason);
            var constructorStandings = InitializeConstructorStandings(modifiedSeason);

            // Add player to drivers list by only loading ratings for that year
            var allDrivers = seasonDrivers.DeepClone();

            var playerData = InitializePlayerData(playerName, playerNationality, playerAge, selectedTeamId, playerDriverId);

            allDrivers.Add(InitializePlayerDriverData(playerData, playerAge, playerReputation, season, playerDriverId, favouriteNumbers)); 

            _currentGame = InitializeNewSaveGame(playerData, modifiedSeason, driverStandings, constructorStandings, allDrivers);

            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs { NewState = GameState.SeasonOverview });
            return _currentGame;
        }

        /// <summary>
        /// Create a new game with player data
        /// </summary>
        public ISaveGame CreateNewGameWithExistingDriver(
            ISeason season,
            string selectedTeamId,
            string driverId,
            List<IDriverData> seasonDrivers)
        {
            // Clone the season
            var modifiedSeason = season.DeepClone();

            // Initialize standings
            var driverStandings = InitializeDriverStandings(modifiedSeason);
            var constructorStandings = InitializeConstructorStandings(modifiedSeason);

            // Add player to drivers list by only loading ratings for that year
            var allDrivers = seasonDrivers.DeepClone();
            IDriverData selectedDriver = allDrivers.FirstOrDefault(d => d.DriverId == driverId);

            _currentGame = InitializeNewSaveGameFromExistingDriver(selectedDriver, selectedTeamId, modifiedSeason, driverStandings, constructorStandings, allDrivers);

            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs { NewState = GameState.SeasonOverview });
            return _currentGame;
        }

        private ISaveGame InitializeNewSaveGameFromExistingDriver(IDriverData? selectedDriver, string selectedTeamId, ISeason modifiedSeason, List<HistoricalDriverStandingEntry> driverStandings, List<ConstructorStandingEntry> constructorStandings, List<IDriverData> allDrivers)
        {
            var provisionalSaveGame = new SaveGame
            {
                CurrentSeason = modifiedSeason,
                Drivers = allDrivers,
                NextGpIndex = 0,
                NextGpEntryList = null,
                PlayerData = InitializePlayerDataFromExistingDriver(selectedDriver, selectedTeamId),
                GrandPrixResults = new List<GrandPrixResult>(),
                CurrentDriverStandings = driverStandings,
                CurrentConstructorStandings = constructorStandings,
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                AccoladesAtStart = LoadAccoladesForNewGame(modifiedSeason.Year)
            };
            return InitializeConcreteNewSaveGame(provisionalSaveGame);
        }

        private IPlayerData InitializePlayerDataFromExistingDriver(IDriverData? selectedDriver, string selectedTeamId)
        {
            var provisionalPlayerData = new PlayerData
            {
                DriverId = selectedDriver.DriverId,
                Name = selectedDriver.Name,
                Nationality = selectedDriver.Nationality,
                TeamId = selectedTeamId
            };
            return InitializeConcretePlayerDataFromExistingDrver(selectedDriver, provisionalPlayerData);
        }

        protected virtual IPlayerData InitializeConcretePlayerDataFromExistingDrver(IDriverData selectedDriver, PlayerData provisionalPlayerData)
        {
            return provisionalPlayerData;
        }

        /// <summary>
        /// Load an existing game
        /// </summary>
        public void LoadGame(ISaveGame saveGame)
        {
            if (saveGame.AccoladesAtStart == null)
            {
                var year = saveGame.HistoricalDriverStandings?.Any() == true
                    ? saveGame.HistoricalDriverStandings.Min(s => s.Year)
                    : saveGame.CurrentSeason.Year;

                saveGame.AccoladesAtStart = LoadAccoladesForNewGame(year);
            }

            _currentGame = saveGame;
            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs { NewState = GameState.SeasonOverview });
        }

        /// <summary>
        /// Progress to the next Grand Prix
        /// </summary>
        public void ProgressToNextGrandPrix()
        {
            if (_currentGame == null)
            {
                ErrorOccurred?.Invoke(this, "No active game");
                return;
            }

            if (_currentGame.NextGpIndex >= _currentGame.CurrentSeason.Races.Count())
            {
                SeasonProgressed?.Invoke(this, new SeasonProgressionEventArgs
                {
                    IsSeasonComplete = true,
                    Message = "Season Complete!"
                });
                return;
            }

            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs { NewState = GameState.PreGrandPrix });
        }

        /// <summary>
        /// Complete current Grand Prix and update standings
        /// </summary>
        public void CompleteGrandPrix(GrandPrixResult result)
        {
            if (_currentGame == null) return;

            _currentGame.GrandPrixResults = _currentGame.GrandPrixResults.Append(result);
            _currentGame.NextGpIndex++;

            // Update standings would happen here
            // UpdateStandings(result);

            if (_currentGame.NextGpIndex >= _currentGame.CurrentSeason.Races.Count())
            {
                SeasonProgressed?.Invoke(this, new SeasonProgressionEventArgs
                {
                    IsSeasonComplete = true,
                    Message = $"Congratulations! Season {_currentGame.CurrentSeason.Year} is complete!"
                });
            }
            else
            {
                SeasonProgressed?.Invoke(this, new SeasonProgressionEventArgs
                {
                    IsSeasonComplete = false,
                    Message = $"GP {_currentGame.NextGpIndex} of {_currentGame.CurrentSeason.Races.Count()} complete"
                });
            }

            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs { NewState = GameState.SeasonOverview });
        }

        private List<HistoricalDriverStandingEntry> InitializeDriverStandings(ISeason season)
        {
            var standings = new List<HistoricalDriverStandingEntry>();
            int position = 1;

            foreach (var team in season.Teams)
            {
                standings.Add(new HistoricalDriverStandingEntry
                {
                    Position = position++,
                    DriverId = team.Driver1Contract.DriverId,
                    TeamId = team.TeamId,
                    Points = 0,
                    PositionsTally = new PositionsTally()
                });

                standings.Add(new HistoricalDriverStandingEntry
                {
                    Position = position++,
                    DriverId = team.Driver2Contract.DriverId,
                    TeamId = team.TeamId,
                    Points = 0,
                    PositionsTally = new PositionsTally()
                });
            }

            return standings;
        }

        private List<ConstructorStandingEntry> InitializeConstructorStandings(ISeason season)
        {
            var standings = new List<ConstructorStandingEntry>();
            int position = 1;

            foreach (var team in season.Teams)
            {
                standings.Add(new ConstructorStandingEntry
                {
                    Position = position++,
                    TeamId = team.TeamId,
                    Points = 0,
                    PositionsTally = new PositionsTally()
                });
            }

            return standings;
        }

        private ISeason CloneAndModifySeason(ISeason originalSeason, string playerDriverId, string playerTeamId, string replacedDriverId)
        {
            var clonedSeason = originalSeason.DeepClone();

            var playerTeam = clonedSeason.Teams.FirstOrDefault(t => t.TeamId == playerTeamId);
            if (playerTeam != null)
            {
                if (playerTeam.Driver1Contract.DriverId == replacedDriverId)
                {
                    playerTeam.Driver1Contract.DriverId = playerDriverId;
                    playerTeam.Driver1Contract.Races = originalSeason.Races.Count() + 1;
                }
                else if (playerTeam.Driver2Contract.DriverId == replacedDriverId)
                {
                    playerTeam.Driver2Contract.DriverId = playerDriverId;
                    playerTeam.Driver1Contract.Races = originalSeason.Races.Count() + 1;
                }

                // Remove absences for the replaced driver
                if (!string.IsNullOrEmpty(replacedDriverId))
                {
                    clonedSeason.Absences = clonedSeason.Absences
                        ?.Where(a => a.DriverOut != replacedDriverId)
                        .ToList();
                }
            }

            return clonedSeason;
        }

        protected virtual ISaveGame InitializeNewSaveGame(
            IPlayerData playerData,
            ISeason modifiedSeason,
            List<HistoricalDriverStandingEntry> driverStandings,
            List<ConstructorStandingEntry> constructorStandings,
            List<IDriverData> allDrivers)
        {
            var provisionalSaveGame = new SaveGame
            {
                CurrentSeason = modifiedSeason,
                Drivers = allDrivers,
                NextGpIndex = 0,
                NextGpEntryList = null,
                PlayerData = playerData,
                GrandPrixResults = new List<GrandPrixResult>(),
                CurrentDriverStandings = driverStandings,
                CurrentConstructorStandings = constructorStandings,
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                AccoladesAtStart = LoadAccoladesForNewGame(modifiedSeason.Year)
            };
            return InitializeConcreteNewSaveGame(provisionalSaveGame);
        }

        protected virtual IPlayerData InitializePlayerData(string playerName, string playerNationality,int playerAge, string selectedTeamId, string playerDriverId)
        {
            var provisionalPlayerData = new PlayerData
            {
                DriverId = playerDriverId,
                Name = playerName,
                Nationality = playerNationality,
                TeamId = selectedTeamId  
            };
            return InitializeConcretePlayerData(provisionalPlayerData);
        }

        protected virtual IPlayerData InitializeConcretePlayerData(IPlayerData provisionalPlayerData)
        {
            return provisionalPlayerData;
        }

        protected virtual ISaveGame InitializeConcreteNewSaveGame(ISaveGame provisionalSaveGame)
        {
            return provisionalSaveGame;
        }

        protected virtual HistoricalAccolades LoadAccoladesForNewGame(int seasonYear) => null;

        protected virtual IDriverData InitializePlayerDriverData(
            IPlayerData playerData,
            int age,
            DriverReputation playerReputation,
            ISeason season,
            string playerDriverId,
            IEnumerable<int> favouriteNumbers)
        {
            var provisionalDriverData = new DriverData
            {
                DriverId = playerDriverId,
                Name = playerData.Name,
                Nationality = playerData.Nationality,
                YearOfBirth = season.Year - age,
                PictureUrl = "drivers/portraits/default.png",
                FavouriteNumbers = favouriteNumbers,
                Reputation = playerReputation
            };

            return InitializeConcretePlayerDriverData(provisionalDriverData, playerData, season);
        }

        protected virtual IDriverData InitializeConcretePlayerDriverData(IDriverData provisionalDriverData, IPlayerData playerData, ISeason season)
        {
            return provisionalDriverData;
        }
        
    }
}