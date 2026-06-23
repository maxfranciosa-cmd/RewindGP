using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.RaceNumberSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace AMS2ChEd.Tests.Business.Services.RaceNumberSystem
{
    [TestClass]
    public class AssignNumbersToTeamsChampionshipTallyTests
    {
        private AssignNumbersToTeamsChampionshipTally _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new AssignNumbersToTeamsChampionshipTally();
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionStillRacing_GetsOneTeammateGetsTwo()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>
                {
                    new HistoricalDriverStanding
                    {
                        Year = 2024,
                        Standing = new List<HisoricalDriverStandingEntry>
                        {
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ_id", TeamId = "team1", Points = 100 }
                        }
                    }
                },
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
                {
                    new HistoricalConstructorStanding
                    {
                        Year = 2024,
                        Standing = new List<HistoricalConstructorStandingEntry>
                        {
                            new HistoricalConstructorStandingEntry { Position = 1, TeamId = "team1", Points = 200 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "champ_id", Name = "Champion" },
                    new DriverData { DriverId = "teammate_id", Name = "Teammate" }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "champ_id", DriverNumber = 5 },
                Driver2Contract = new DriverContract { DriverId = "teammate_id", DriverNumber = 6 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionNotRacing_OldTeamGetsZeroAndTwo()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>
                {
                    new HistoricalDriverStanding
                    {
                        Year = 2024,
                        Standing = new List<HisoricalDriverStandingEntry>
                        {
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "retired_champ", TeamId = "team1", Points = 100 }
                        }
                    }
                },
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
                {
                    new HistoricalConstructorStanding
                    {
                        Year = 2024,
                        Standing = new List<HistoricalConstructorStandingEntry>
                        {
                            new HistoricalConstructorStandingEntry { Position = 1, TeamId = "team1", Points = 200 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "driver1", Name = "Driver 1" },
                    new DriverData { DriverId = "driver2", Name = "Driver 2" }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "driver1", DriverNumber = 5 },
                Driver2Contract = new DriverContract { DriverId = "driver2", DriverNumber = 6 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            Assert.AreEqual(0, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_RemainingTeams_NumberedByConstructorPosition()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>
                {
                    new HistoricalDriverStanding
                    {
                        Year = 2024,
                        Standing = new List<HisoricalDriverStandingEntry>
                        {
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ", TeamId = "team1", Points = 100 }
                        }
                    }
                },
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
                {
                    new HistoricalConstructorStanding
                    {
                        Year = 2024,
                        Standing = new List<HistoricalConstructorStandingEntry>
                        {
                            new HistoricalConstructorStandingEntry { Position = 1, TeamId = "team1", Points = 200 },
                            new HistoricalConstructorStandingEntry { Position = 2, TeamId = "team2", Points = 150 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "champ", Name = "Champion" },
                    new DriverData { DriverId = "teammate", Name = "Teammate" },
                    new DriverData { DriverId = "driver3", Name = "Driver 3" },
                    new DriverData { DriverId = "driver4", Name = "Driver 4" }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                TeamName = "Team 1",
                Driver1Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "teammate", DriverNumber = 0 }
            };
            var team2 = new TeamEntry
            {
                TeamId = "team2",
                TeamName = "Team 2",
                Driver1Contract = new DriverContract { DriverId = "driver3", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver4", DriverNumber = 0 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion's team gets 1 and 2
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);

            // Second place constructor team gets 3 and 4
            Assert.AreEqual(3, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(4, team2.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_Skips13()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>
                {
                    new HistoricalDriverStanding
                    {
                        Year = 2024,
                        Standing = new List<HisoricalDriverStandingEntry>
                        {
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "driver1a", TeamId = "team1", Points = 100 }
                        }
                    }
                },
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
                {
                    new HistoricalConstructorStanding
                    {
                        Year = 2024,
                        Standing = new List<HistoricalConstructorStandingEntry>
                        {
                            new HistoricalConstructorStandingEntry { Position = 1, TeamId = "team1", Points = 200 },
                            new HistoricalConstructorStandingEntry { Position = 2, TeamId = "team2", Points = 150 },
                            new HistoricalConstructorStandingEntry { Position = 3, TeamId = "team3", Points = 100 },
                            new HistoricalConstructorStandingEntry { Position = 4, TeamId = "team4", Points = 80 },
                            new HistoricalConstructorStandingEntry { Position = 5, TeamId = "team5", Points = 60 },
                            new HistoricalConstructorStandingEntry { Position = 6, TeamId = "team6", Points = 40 }
                        }
                    }
                },
                Drivers = new List<IDriverData>()
            };

            var teams = new List<ITeamEntry>();

            // Create 7 teams (team1 gets 1-2, team2 gets 3-4, etc., team6 gets 11-12, team7 gets 14-15, skipping 13)
            for (int i = 1; i <= 7; i++)
            {
                var team = new TeamEntry
                {
                    TeamId = $"team{i}",
                    TeamName = $"Team {i}",
                    Driver1Contract = new DriverContract { DriverId = $"driver{i}a", DriverNumber = 0 },
                    Driver2Contract = new DriverContract { DriverId = $"driver{i}b", DriverNumber = 0 }
                };
                teams.Add(team);
            }

            saveGame.CurrentSeason.Teams = teams;

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Team 1 (champion's team): 1-2
            Assert.AreEqual(1, teams[0].Driver1Contract.DriverNumber);
            Assert.AreEqual(2, teams[0].Driver2Contract.DriverNumber);

            // Team 6 gets 11-12
            Assert.AreEqual(11, teams[5].Driver1Contract.DriverNumber);
            Assert.AreEqual(12, teams[5].Driver2Contract.DriverNumber);

            // Team 7 (new team) should get 14-15, skipping 13
            Assert.AreEqual(14, teams[6].Driver1Contract.DriverNumber);
            Assert.AreEqual(15, teams[6].Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_NewTeams_GetHighNumbers()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>
                {
                    new HistoricalDriverStanding
                    {
                        Year = 2024,
                        Standing = new List<HisoricalDriverStandingEntry>
                        {
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ", TeamId = "team1", Points = 100 }
                        }
                    }
                },
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
                {
                    new HistoricalConstructorStanding
                    {
                        Year = 2024,
                        Standing = new List<HistoricalConstructorStandingEntry>
                        {
                            new HistoricalConstructorStandingEntry { Position = 1, TeamId = "team1", Points = 200 },
                            new HistoricalConstructorStandingEntry { Position = 2, TeamId = "team2", Points = 150 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "champ", Name = "Champion" },
                    new DriverData { DriverId = "teammate", Name = "Teammate" },
                    new DriverData { DriverId = "driver3", Name = "Driver 3" },
                    new DriverData { DriverId = "driver4", Name = "Driver 4" },
                    new DriverData { DriverId = "rookie1", Name = "Rookie 1" },
                    new DriverData { DriverId = "rookie2", Name = "Rookie 2" }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                TeamName = "Team 1",
                Driver1Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "teammate", DriverNumber = 0 }
            };
            var team2 = new TeamEntry
            {
                TeamId = "team2",
                TeamName = "Team 2",
                Driver1Contract = new DriverContract { DriverId = "driver3", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver4", DriverNumber = 0 }
            };
            var newTeam = new TeamEntry
            {
                TeamId = "new_team",
                TeamName = "New Team",
                Driver1Contract = new DriverContract { DriverId = "rookie1", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "rookie2", DriverNumber = 0 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2, newTeam };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion's team gets 1-2
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);

            // Second place team gets 3-4
            Assert.AreEqual(3, team2.Driver1Contract.DriverNumber);
            Assert.AreEqual(4, team2.Driver2Contract.DriverNumber);

            // New team (not in previous standings) gets 5-6 (after all teams with positions)
            Assert.AreEqual(5, newTeam.Driver1Contract.DriverNumber);
            Assert.AreEqual(6, newTeam.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_EmptyStandingsList_AssignsSequentially()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>
                {
                    new HistoricalDriverStanding
                    {
                        Year = 2024,
                        Standing = new List<HisoricalDriverStandingEntry>() // Empty list
                    }
                },
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
                {
                    new HistoricalConstructorStanding
                    {
                        Year = 2024,
                        Standing = new List<HistoricalConstructorStandingEntry>() // Empty list
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "driver1", Name = "Driver 1" },
                    new DriverData { DriverId = "driver2", Name = "Driver 2" }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "driver1", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver2", DriverNumber = 0 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionNotRacing_OldTeamDoesNotExist_RemainingTeamsStartAt2()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>
                {
                    new HistoricalDriverStanding
                    {
                        Year = 2024,
                        Standing = new List<HisoricalDriverStandingEntry>
                        {
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "retired_champ", TeamId = "old_team", Points = 100 }
                        }
                    }
                },
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>
                {
                    new HistoricalConstructorStanding
                    {
                        Year = 2024,
                        Standing = new List<HistoricalConstructorStandingEntry>
                        {
                            new HistoricalConstructorStandingEntry { Position = 1, TeamId = "old_team", Points = 200 },
                            new HistoricalConstructorStandingEntry { Position = 2, TeamId = "team1", Points = 150 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "driver1", Name = "Driver 1" },
                    new DriverData { DriverId = "driver2", Name = "Driver 2" }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                TeamName = "Team 1",
                Driver1Contract = new DriverContract { DriverId = "driver1", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver2", DriverNumber = 0 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion not racing, old team doesn't exist, so remaining teams start from 3
            Assert.AreEqual(3, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(4, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumberAtGameCreation_PlayerHasNumberOne_GetsZero()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                PlayerData = new PlayerData { DriverId = "player_id", Name = "Player" },
                Drivers = new List<IDriverData>()
            };

            var playerTeam = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "player_id", DriverNumber = 1 },
                Driver2Contract = new DriverContract { DriverId = "teammate", DriverNumber = 2 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { playerTeam };

            _service.AssignNumberAtGameCreation(saveGame);

            Assert.AreEqual(0, playerTeam.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, playerTeam.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumberAtGameCreation_PlayerDoesNotHaveOne_KeepsNumber()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                PlayerData = new PlayerData { DriverId = "player_id", Name = "Player" },
                Drivers = new List<IDriverData>()
            };

            var playerTeam = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "player_id", DriverNumber = 5 },
                Driver2Contract = new DriverContract { DriverId = "teammate", DriverNumber = 6 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { playerTeam };

            _service.AssignNumberAtGameCreation(saveGame);

            Assert.AreEqual(5, playerTeam.Driver1Contract.DriverNumber);
            Assert.AreEqual(6, playerTeam.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumberAtGameCreation_PlayerIsDriver2WithNumberOne_GetsZero()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                PlayerData = new PlayerData { DriverId = "player_id", Name = "Player" },
                Drivers = new List<IDriverData>()
            };

            var playerTeam = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "teammate", DriverNumber = 2 },
                Driver2Contract = new DriverContract { DriverId = "player_id", DriverNumber = 1 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { playerTeam };

            _service.AssignNumberAtGameCreation(saveGame);

            Assert.AreEqual(2, playerTeam.Driver1Contract.DriverNumber);
            Assert.AreEqual(0, playerTeam.Driver2Contract.DriverNumber);
        }
    }
}