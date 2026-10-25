using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

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
            var modifiedSeason = CloneAndModifySeason(season, playerDriverId, selectedTeamId, replacedDriverId, seasonDrivers);

            // Initialize standings
            var driverStandings = InitializeDriverStandings(modifiedSeason);
            var constructorStandings = InitializeConstructorStandings(modifiedSeason);

            // Add player to drivers list by only loading ratings for that year
            var allDrivers = seasonDrivers.DeepClone();

            var playerData = InitializePlayerData(playerName, playerNationality, playerAge, RaceSeatTeamId(season, selectedTeamId, replacedDriverId), playerDriverId);

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
            DriverHirer.AssignUndefinedRoles(modifiedSeason, seasonDrivers);

            // Initialize standings
            var driverStandings = InitializeDriverStandings(modifiedSeason);
            var constructorStandings = InitializeConstructorStandings(modifiedSeason);

            // Add player to drivers list by only loading ratings for that year
            var allDrivers = seasonDrivers.DeepClone();
            IDriverData selectedDriver = allDrivers.FirstOrDefault(d => d.DriverId == driverId);

            // playing as the team's reserve: no race seat
            _currentGame = InitializeNewSaveGameFromExistingDriver(selectedDriver, RaceSeatTeamId(season, selectedTeamId, driverId), modifiedSeason, driverStandings, constructorStandings, allDrivers);

            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs { NewState = GameState.SeasonOverview });
            return _currentGame;
        }

        /// <summary>
        /// Create a new game where the player is a driver taken from another season: the driver
        /// keeps their id (so the season they really belong to won't bring in a duplicate), name,
        /// year of birth and game-specific data, and only gets a new reputation.
        /// </summary>
        public ISaveGame CreateNewGameWithImportedDriver(
            IDriverData importedDriver,
            DriverReputation playerReputation,
            ISeason season,
            string selectedTeamId,
            string replacedDriverId,
            List<IDriverData> seasonDrivers)
        {
            var modifiedSeason = CloneAndModifySeason(season, importedDriver.DriverId, selectedTeamId, replacedDriverId, seasonDrivers);

            // Initialize standings
            var driverStandings = InitializeDriverStandings(modifiedSeason);
            var constructorStandings = InitializeConstructorStandings(modifiedSeason);

            var allDrivers = seasonDrivers.DeepClone();

            var playerDriver = importedDriver.DeepClone();
            playerDriver.Reputation = playerReputation;

            var playerData = InitializePlayerData(playerDriver.Name, playerDriver.Nationality, season.Year - playerDriver.YearOfBirth, RaceSeatTeamId(season, selectedTeamId, replacedDriverId), playerDriver.DriverId);

            allDrivers.RemoveAll(d => d.DriverId == playerDriver.DriverId);
            allDrivers.Add(playerDriver);

            _currentGame = InitializeNewSaveGame(playerData, modifiedSeason, driverStandings, constructorStandings, allDrivers);

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
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>()
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
        /// Game-specific fix-ups for saves made by older versions, run on every load.
        /// </summary>
        protected virtual void UpgradeConcreteSaveOnLoad(ISaveGame saveGame)
        {
        }

        /// <summary>
        /// Load an existing game
        /// </summary>
        public void LoadGame(ISaveGame saveGame)
        {
            // saves created before contract roles existed
            DriverHirer.AssignUndefinedRoles(saveGame.CurrentSeason, saveGame.Drivers);

            UpgradeConcreteSaveOnLoad(saveGame);
            RefreshAvatars(saveGame);

            _currentGame = saveGame;
            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs { NewState = GameState.SeasonOverview });
        }

        /// <summary>
        /// Saves keep their own copy of every driver, but a look is not career data: every driver
        /// the season's data describes takes its generic_avatar as it is now, so older saves get
        /// avatars and changed descriptions reach existing saves. Drivers it does not describe
        /// (generated ones) keep theirs, or get one picked from their id when shown; the player's
        /// driver is never touched.
        /// </summary>
        private void RefreshAvatars(ISaveGame saveGame)
        {
            if (saveGame?.Drivers == null || saveGame.CurrentSeason == null)
                return;
            Dictionary<string, IDriverData> seasonDrivers;
            try
            {
                seasonDrivers = LoadUpdatedDriversForRefresh(saveGame.CurrentSeason.Year);
            }
            catch (Exception)
            {
                return;
            }
            foreach (var driver in saveGame.Drivers.Where(d => d.DriverId != saveGame.PlayerData?.DriverId))
                if (seasonDrivers.TryGetValue(driver.DriverId, out var fromSeason) && fromSeason.GenericAvatar != null)
                    driver.GenericAvatar = fromSeason.GenericAvatar;
        }

        /// <summary>
        /// Refreshes an existing save's current season against the now-installed season pack,
        /// e.g. when a season was cloned from a previous year (no pack existed yet) and the real
        /// pack for that year has since been installed.
        /// </summary>
        public void UpdateSeasonInsideSave(ISaveGame saveGame)
        {
            var currentYear = saveGame.CurrentSeason.Year;
            var updatedSeason = LoadUpdatedSeasonForRefresh(currentYear);

            if (updatedSeason == null)
                return;

            var driversDict = LoadUpdatedDriversForRefresh(currentYear);
            var newDrivers = new List<IDriverData>();

            foreach (var driverInSave in saveGame.Drivers)
            {
                var driverInDatabase = driversDict.GetValueOrDefault(driverInSave.DriverId);

                if (driverInDatabase != null)
                {
                    // clone the driver but USE THE REPUTATION FROM CURRENT SAVE.
                    var clonedDriver = driverInDatabase.DeepClone();
                    clonedDriver.Reputation = driverInSave.Reputation;

                    if (clonedDriver.DriverId == saveGame.PlayerData.DriverId)
                    {
                        saveGame.PlayerData.Name = clonedDriver.Name;
                        saveGame.PlayerData.Nationality = clonedDriver.Nationality;
                    }

                    newDrivers.Add(clonedDriver);
                }
                else
                {
                    newDrivers.Add(driverInSave);
                }
            }

            ApplyConcreteSeasonUpdates(saveGame.CurrentSeason, updatedSeason);

            var teamEntryDict = updatedSeason.Teams.DeepClone().ToDictionary(t => t.TeamId);
            var newTeamEntries = new List<ITeamEntry>();

            foreach (var teamEntry in saveGame.CurrentSeason.Teams)
            {
                var teamFromUpdatedSeason = teamEntryDict.GetValueOrDefault(teamEntry.TeamId);

                if (teamFromUpdatedSeason != null)
                {
                    // just port the driver contracts. The reserve too, even when the save has none:
                    // the pack's reserve is history, the save's is what happened in this career
                    teamFromUpdatedSeason.Driver1Contract = teamEntry.Driver1Contract;
                    teamFromUpdatedSeason.Driver2Contract = teamEntry.Driver2Contract;
                    teamFromUpdatedSeason.ReserveContract = teamEntry.ReserveContract;

                    // update the flag "default prequalifying" (in case it was updated on merit)
                    teamFromUpdatedSeason.DefaultPrequalifying = teamEntry.DefaultPrequalifying;

                    // keep the improvement packages installed so far this season
                    teamFromUpdatedSeason.ImprovementPackages = teamEntry.ImprovementPackages;

                    ApplyConcreteTeamEntryUpdates(teamFromUpdatedSeason, updatedSeason.Year);

                    newTeamEntries.Add(teamFromUpdatedSeason);
                }
                else
                {
                    newTeamEntries.Add(teamEntry);
                }
            }

            saveGame.Drivers = newDrivers;
            saveGame.CurrentSeason.Teams = newTeamEntries;

            var newDriverNamesDict = newDrivers.ToDictionary(d => d.DriverId, d => d.Name);

            // update driver names in historical standings
            if (saveGame.HistoricalDriverStandings != null)
            {
                foreach (var year in saveGame.HistoricalDriverStandings)
                {
                    foreach (var driver in year.Standing)
                    {
                        if (newDriverNamesDict.ContainsKey(driver.DriverId))
                        {
                            driver.DriverName = newDriverNamesDict[driver.DriverId];
                        }
                    }
                }
            }

            var raceInfo = updatedSeason.Races.ToDictionary(r => r.RaceId);
            foreach (var race in saveGame.CurrentSeason.Races)
            {
                var raceFromNewSeason = raceInfo.GetValueOrDefault(race.RaceId);

                if (raceFromNewSeason != null)
                {
                    race.CoverPictureUrl = raceFromNewSeason.CoverPictureUrl ?? race.CoverPictureUrl;
                    race.RaceDate = raceFromNewSeason.RaceDate ?? race.RaceDate;
                    race.Circuit = raceFromNewSeason.Circuit;
                    race.RaceName = raceFromNewSeason.RaceName;
                    race.RaceShortName = raceFromNewSeason.RaceShortName ?? race.RaceShortName;
                }
            }
        }

        /// <summary>
        /// Loads the currently-installed season pack data for the given year, used to refresh a
        /// save whose season was previously cloned/placeholder data. Returns null when there's
        /// nothing to load (base game has no concept of on-disk season packs).
        /// </summary>
        protected virtual ISeason LoadUpdatedSeasonForRefresh(int year) => null;

        /// <summary>
        /// Loads the currently-installed driver ratings database for the given year, used to
        /// refresh a save's drivers against the now-installed season pack.
        /// </summary>
        protected virtual Dictionary<string, IDriverData> LoadUpdatedDriversForRefresh(int year) => new();

        /// <summary>
        /// Hook for game-specific season-level fields that need to be copied over from the
        /// freshly-loaded season pack onto the save's current season (e.g. AMS2's car class).
        /// </summary>
        protected virtual void ApplyConcreteSeasonUpdates(ISeason currentSeason, ISeason updatedSeason)
        {
        }

        /// <summary>
        /// Hook for game-specific per-team-entry fixups once a team entry has been refreshed from
        /// the newly-installed season pack (e.g. AMS2 texture path rewriting).
        /// </summary>
        protected virtual void ApplyConcreteTeamEntryUpdates(ITeamEntry updatedTeamEntry, int updatedSeasonYear)
        {
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
            _currentGame.NextGpEntryList = null;
            _currentGame.NextGpEntryListRaceId = null;

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

                if (!string.IsNullOrEmpty(team.Driver2Contract?.DriverId))
                {
                    standings.Add(new HistoricalDriverStandingEntry
                    {
                        Position = position++,
                        DriverId = team.Driver2Contract.DriverId,
                        TeamId = team.TeamId,
                        Points = 0,
                        PositionsTally = new PositionsTally()
                    });
                }
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

        private ISeason CloneAndModifySeason(ISeason originalSeason, string playerDriverId, string playerTeamId, string replacedDriverId, List<IDriverData> seasonDrivers)
        {
            var clonedSeason = originalSeason.DeepClone();

            // roles are worked out on the original lineup, so the player takes over
            // the role of the driver they replace
            DriverHirer.AssignUndefinedRoles(clonedSeason, seasonDrivers);

            var playerTeam = clonedSeason.Teams.FirstOrDefault(t => t.TeamId == playerTeamId);
            if (playerTeam != null && IsReserveOf(playerTeam, replacedDriverId))
            {
                // the player takes over the pack's reserve, and the call-ups the pack gave them at
                // this team; the pack's reserve becomes a free agent
                playerTeam.ReserveContract.DriverId = playerDriverId;
                playerTeam.ReserveContract.Races = originalSeason.Races.Count();

                foreach (var absence in clonedSeason.Absences ?? Enumerable.Empty<Absence>())
                {
                    if (absence.TeamId == playerTeam.TeamId && absence.DriverIn == replacedDriverId)
                    {
                        absence.DriverIn = playerDriverId;
                    }
                }
            }
            else if (playerTeam != null)
            {
                if (playerTeam.Driver1Contract.DriverId == replacedDriverId)
                {
                    playerTeam.Driver1Contract.DriverId = playerDriverId;
                    playerTeam.Driver1Contract.Races = originalSeason.Races.Count() + 1;
                }
                else if (!string.IsNullOrEmpty(replacedDriverId) && playerTeam.Driver2Contract?.DriverId == replacedDriverId)
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

        private static bool IsReserveOf(ITeamEntry team, string driverId) =>
            !string.IsNullOrEmpty(driverId) && team?.ReserveDriverId() == driverId;

        // the team the player races for: none when they take over the team's reserve
        private static string RaceSeatTeamId(ISeason season, string selectedTeamId, string replacedDriverId)
        {
            var team = season.Teams?.FirstOrDefault(t => t.TeamId == selectedTeamId);
            return IsReserveOf(team, replacedDriverId) ? null : selectedTeamId;
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
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>()
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