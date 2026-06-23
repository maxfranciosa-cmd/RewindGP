using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.RaceNumberSystem;

namespace AMS2ChEd.Tests.Business.Services.RaceNumberSystem
{
    [TestClass]
    public class KeepTeamsNumbersAndSwapWithChampionTests
    {
        private KeepTeamsNumbersAndSwapWithChampion _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new KeepTeamsNumbersAndSwapWithChampion();
        }

        #region AssignNumbersToCurrentSeason Tests

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionStillRacing_AssignsOneAndTwo()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var championDriver = CreateDriver("champion_id", "Champion Driver");
            var teammateDriver = CreateDriver("teammate_id", "Teammate Driver");

            // Previous year: champion won
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "champion_id", "team1", 100)
                })
            };

            // Current year: champion still racing with team1
            var team1 = CreateTeam("team1", championDriver.DriverId, 27, teammateDriver.DriverId, 28);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    })
                }
            };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionStillRacing_SwapsNumbersWithOldChampionTeam()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var newChampion = CreateDriver("new_champ_id", "New Champion");
            var oldChampionDriver = CreateDriver("old_champ_id", "Old Champion");

            // Previous year: new champion won
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "new_champ_id", "team2", 100)
                })
            };

            // Current year: new champion on team2 (had 27,28), old champion's team1 had 1,2
            var team1 = CreateTeam("team1", "driver1", 1, "driver2", 2);
            var team2 = CreateTeam("team2", newChampion.DriverId, 27, "teammate", 28);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    }).ToList()
                }
            };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - team2 gets 1,2
            Assert.AreEqual(1, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team2.Driver2Contract.DriverNumber);

            // Assert - team1 gets 27,28 (the swap)
            Assert.AreEqual(27, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(28, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionNotRacing_AssignsZeroAndTwo()
        {
            // Arrange
            var saveGame = CreateSaveGame();

            // Previous year: champion won with team1
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "retired_champ_id", "team1", 100)
                })
            };

            // Current year: champion not racing, team1 still exists
            var team1 = CreateTeam("team1", "new_driver1", 1, "new_driver2", 2);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    })
                }
            };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert
            Assert.AreEqual(0, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_OtherTeams_KeepTheirNumbers()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var champion = CreateDriver("champ_id", "Champion");

            // Previous year standings
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "champ_id", "team1", 100)
                })
            };

            // Current year: champion on team1, other teams keep their numbers
            var team1 = CreateTeam("team1", champion.DriverId, 27, "teammate", 28);
            var team2 = CreateTeam("team2", "driver3", 5, "driver4", 6);
            var team3 = CreateTeam("team3", "driver5", 11, "driver6", 12);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2, team3 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    })
                }
            };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - team1 gets 1,2
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);

            // Assert - other teams keep their numbers
            Assert.AreEqual(5, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(6, team2.Driver2Contract.DriverNumber);
            Assert.AreEqual(11, team3.Driver1Contract.DriverNumber);
            Assert.AreEqual(12, team3.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_NewTeam_GetsNextAvailableNumber()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var champion = CreateDriver("champ_id", "Champion");

            // Previous year standings
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "champ_id", "team1", 100)
                })
            };



            // Current year: new team with invalid numbers (0 or negative)
            var team1 = CreateTeam("team1", champion.DriverId, 5, "teammate", 6);
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = new [] {
                        new HistoricalConstructorStandingEntry
                        {
                            TeamId = team1.TeamId,
                        }
                    }
                }
            };
            var newTeam = CreateTeam("new_team", "new_driver1", 0, "new_driver2", 0);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, newTeam };


            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - new team gets next available numbers (skipping 1, 2, 5, 6)
            Assert.AreEqual(3, newTeam.Driver1Contract.DriverNumber);
            Assert.AreEqual(4, newTeam.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_NewTeam_Skips13()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var champion = CreateDriver("champ_id", "Champion");

            // Previous year standings
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "champ_id", "team1", 100)
                })
            };

            // Current year: teams occupying 1-12, new team should skip 13
            var teams = new List<ITeamEntry>();
            var team1 = CreateTeam("team1", champion.DriverId, 1, "teammate", 2);
            teams.Add(team1);

            // Fill numbers 5-12
            for (int i = 3; i <= 12; i++)
            {
                teams.Add(CreateTeam($"team{i}", $"driver{i}a", i, $"driver{i}b", 99));
            }
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    }).ToList()
                }
            };
            var newTeam = CreateTeam("new_team", "new_driver1", 0, "new_driver2", 0);
            teams.Add(newTeam);

            saveGame.CurrentSeason.Teams = teams;

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - new team gets 14 and 15 (skipping 13)
            Assert.AreEqual(14, newTeam.Driver1Contract.DriverNumber);
            Assert.AreEqual(15, newTeam.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_NoPreviousStandings_KeepsExistingNumbers()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>();

            var team1 = CreateTeam("team1", "driver1", 5, "driver2", 6);
            var team2 = CreateTeam("team2", "driver3", 11, "driver4", 12);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - all teams keep their numbers
            Assert.AreEqual(5, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(6, team1.Driver2Contract.DriverNumber);
            Assert.AreEqual(11, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(12, team2.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_NoChampionFoundInStandings_KeepsExistingNumbers()
        {
            // Arrange
            var saveGame = CreateSaveGame();

            // Previous year standings but no position 1 (champion)
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(2, "driver2", "team1", 95),
                    CreateDriverStanding(3, "driver3", "team2", 85)
                })
            };

            var team1 = CreateTeam("team1", "driver1", 5, "driver2", 6);
            var team2 = CreateTeam("team2", "driver3", 11, "driver4", 12);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - all teams keep their numbers
            Assert.AreEqual(5, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(6, team1.Driver2Contract.DriverNumber);
            Assert.AreEqual(11, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(12, team2.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionIsDriver2OnTeam_GetsNumberOne()
        {
            // Arrange
            var saveGame = CreateSaveGame();

            // Previous year: driver who is Driver2 won championship
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "champ_id", "team1", 100)
                })
            };

            // Current year: champion is Driver2
            var team1 = CreateTeam("team1", "teammate", 27, "champ_id", 28);
            var team2 = CreateTeam("team2", "driver3", 1, "driver4", 2);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t => 
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    })
                }
            };
            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - Driver2 (champion) gets 1, Driver1 (teammate) gets 2
            Assert.AreEqual(2, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(1, team1.Driver2Contract.DriverNumber);

            // Old champion team gets the swapped numbers
            Assert.AreEqual(27, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(28, team2.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_SameTeamWinsConsecutiveYears_KeepsOneAndTwo()
        {
            // Arrange
            var saveGame = CreateSaveGame();

            // Previous year: team1 driver won
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "champ_id", "team1", 100)
                })
            };

            // Current year: same team still has 1 and 2, champion still racing
            var team1 = CreateTeam("team1", "champ_id", 1, "teammate", 2);
            var team2 = CreateTeam("team2", "driver3", 3, "driver4", 4);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    })
                }
            };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - team1 keeps 1 and 2 (no swap since they're already champion team)
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);
            Assert.AreEqual(3, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(4, team2.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionNotRacing_OldTeamExistsButDifferentDrivers()
        {
            // Arrange
            var saveGame = CreateSaveGame();

            // Previous year: retired champion won
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "retired_champ", "team1", 100)
                })
            };

            // Current year: team1 exists but with completely different drivers
            var team1 = CreateTeam("team1", "new_driver1", 5, "new_driver2", 6);
            var team2 = CreateTeam("team2", "driver3", 7, "driver4", 8);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    })
                }
            };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - champion's old team gets 0 and 2
            Assert.AreEqual(0, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);

            // Other teams keep their numbers
            Assert.AreEqual(7, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(8, team2.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_OldChampionTeamInvalidNumbers_NoSwap()
        {
            // Arrange
            var saveGame = CreateSaveGame();

            // Previous year
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "new_champ", "team2", 100)
                })
            };

            // Current year: new champion had invalid numbers (1 and 2 themselves)
            var team1 = CreateTeam("team1", "old_driver1", 1, "old_driver2", 2);
            var team2 = CreateTeam("team2", "new_champ", 1, "teammate", 2);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    })
                }
            };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - team2 gets 1 and 2
            Assert.AreEqual(1, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team2.Driver2Contract.DriverNumber);

            // team1 keeps 1 and 2 since swap numbers were invalid (1 and 2 can't be swapped)
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_MultipleTeamsWithInvalidNumbers_AllGetAssigned()
        {
            // Arrange
            var saveGame = CreateSaveGame();

            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "champ", "team1", 100)
                })
            };

            // Current year: champion team and multiple new teams
            var team1 = CreateTeam("team1", "champ", 5, "teammate", 6);
            var newTeam1 = CreateTeam("new_team1", "driver1", 0, "driver2", 0);
            var newTeam2 = CreateTeam("new_team2", "driver3", -1, "driver4", -1);
            var newTeam3 = CreateTeam("new_team3", "driver5", 100, "driver6", 101); // > 99

            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, newTeam1, newTeam2, newTeam3 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    }).ToList()
                }
            };
            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - champion team gets 1 and 2
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);

            // New teams get sequential numbers (3, 4, 5, 6, 7, 8)
            Assert.AreEqual(3, newTeam1.Driver1Contract.DriverNumber);
            Assert.AreEqual(4, newTeam1.Driver2Contract.DriverNumber);
            Assert.AreEqual(5, newTeam2.Driver1Contract.DriverNumber);
            Assert.AreEqual(6, newTeam2.Driver2Contract.DriverNumber);
            Assert.AreEqual(7, newTeam3.Driver1Contract.DriverNumber);
            Assert.AreEqual(8, newTeam3.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionMovedToNewTeam_OldTeamSwapsNumbers()
        {
            // Arrange
            var saveGame = CreateSaveGame();

            // Previous year: champion was on team1
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>
                {
                    CreateDriverStanding(1, "champ", "team1", 100)
                })
            };

            // Current year: champion moved to team2 (which had 7,8), team1 still has 1,2
            var team1 = CreateTeam("team1", "new_driver1", 1, "new_driver2", 2);
            var team2 = CreateTeam("team2", "champ", 7, "teammate", 8);

            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
            {
                new HistoricalConstructorStanding
                {
                    Year = 2024,
                    Standing = saveGame.CurrentSeason.Teams.Select(t =>
                    new HistoricalConstructorStandingEntry
                    {
                        TeamId = t.TeamId,
                    }).ToList()
                }
            };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - team2 (champion's new team) gets 1 and 2
            Assert.AreEqual(1, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team2.Driver2Contract.DriverNumber);

            // team1 (old champion team) gets 7 and 8
            Assert.AreEqual(7, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(8, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_EmptyStandingsList_KeepsExistingNumbers()
        {
            // Arrange
            var saveGame = CreateSaveGame();

            // Previous year standings exist but standing list is empty
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                CreateHistoricalDriverStanding(2024, new List<HisoricalDriverStandingEntry>())
            };

            var team1 = CreateTeam("team1", "driver1", 5, "driver2", 6);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            // Act
            _service.AssignNumbersToCurrentSeason(saveGame);

            // Assert - keeps existing numbers
            Assert.AreEqual(5, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(6, team1.Driver2Contract.DriverNumber);
        }

        #endregion

        #region AssignNumberAtGameCreation Tests

        [TestMethod]
        public void AssignNumberAtGameCreation_PlayerHasNumberOne_GetsZero()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            saveGame.PlayerData = CreatePlayerData("player_id", "Player Name");

            var playerTeam = CreateTeam("team1", "player_id", 1, "teammate", 2);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { playerTeam };

            // Act
            _service.AssignNumberAtGameCreation(saveGame);

            // Assert
            Assert.AreEqual(0, playerTeam.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, playerTeam.Driver2Contract.DriverNumber); // Teammate unchanged
        }

        [TestMethod]
        public void AssignNumberAtGameCreation_PlayerDoesNotHaveOne_KeepsNumber()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            saveGame.PlayerData = CreatePlayerData("player_id", "Player Name");

            var playerTeam = CreateTeam("team1", "player_id", 5, "teammate", 6);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { playerTeam };

            // Act
            _service.AssignNumberAtGameCreation(saveGame);

            // Assert
            Assert.AreEqual(5, playerTeam.Driver1Contract.DriverNumber);
            Assert.AreEqual(6, playerTeam.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumberAtGameCreation_PlayerIsDriver2WithNumberOne_GetsZero()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            saveGame.PlayerData = CreatePlayerData("player_id", "Player Name");

            var playerTeam = CreateTeam("team1", "teammate", 2, "player_id", 1);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { playerTeam };

            // Act
            _service.AssignNumberAtGameCreation(saveGame);

            // Assert
            Assert.AreEqual(2, playerTeam.Driver1Contract.DriverNumber); // Teammate unchanged
            Assert.AreEqual(0, playerTeam.Driver2Contract.DriverNumber);
        }

        #endregion

        #region Helper Methods

        private ISaveGame CreateSaveGame()
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = 2025,
                    Teams = new List<ITeamEntry>()
                },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                Drivers = new List<IDriverData>()
            };
        }

        private IDriverData CreateDriver(string driverId, string name)
        {
            return new DriverData
            {
                DriverId = driverId,
                Name = name
            };
        }

        private ITeamEntry CreateTeam(string teamId, string driver1Id, int driver1Number, string driver2Id, int driver2Number)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                Driver1Contract = new DriverContract
                {
                    DriverId = driver1Id,
                    DriverNumber = driver1Number
                },
                Driver2Contract = new DriverContract
                {
                    DriverId = driver2Id,
                    DriverNumber = driver2Number
                }
            };
        }

        private HistoricalDriverStanding CreateHistoricalDriverStanding(int year, List<HisoricalDriverStandingEntry> standings)
        {
            return new HistoricalDriverStanding
            {
                Year = year,
                Standing = standings
            };
        }

        private HisoricalDriverStandingEntry CreateDriverStanding(int position, string driverId, string teamId, int points)
        {
            return new HisoricalDriverStandingEntry
            {
                Position = position,
                DriverId = driverId,
                TeamId = teamId,
                Points = points
            };
        }

        private IPlayerData CreatePlayerData(string driverId, string name)
        {
            return new PlayerData
            {
                DriverId = driverId,
                Name = name
            };
        }

        #endregion
    }
}