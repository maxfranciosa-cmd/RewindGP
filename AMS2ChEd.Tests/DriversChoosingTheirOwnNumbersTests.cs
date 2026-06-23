using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.RaceNumberSystem;

namespace AMS2ChEd.Tests.Business.Services.RaceNumberSystem
{
    [TestClass]
    public class DriversChoosingTheirOwnNumbersTests
    {
        private DriversChoosingTheirOwnNumbers _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new DriversChoosingTheirOwnNumbers();
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionStillRacing_GetsNumberOne()
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
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ_id", TeamId = "team1", Points = 100 },
                            new HisoricalDriverStandingEntry { Position = 2, DriverId = "teammate_id", TeamId = "team1", Points = 90 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "champ_id", Name = "Champion", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "teammate_id", Name = "Teammate", FavouriteNumbers = new[] { 44 } }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "champ_id", DriverNumber = 33 },
                Driver2Contract = new DriverContract { DriverId = "teammate_id", DriverNumber = 44 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion gets 1, teammate picks their favorite (44)
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(44, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionNotRacing_SecondPlacePicksFirst()
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
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "retired_champ", TeamId = "team1", Points = 100 },
                            new HisoricalDriverStandingEntry { Position = 2, DriverId = "driver1", TeamId = "team2", Points = 90 },
                            new HisoricalDriverStandingEntry { Position = 3, DriverId = "driver2", TeamId = "team2", Points = 80 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "driver1", Name = "Driver 1", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "driver2", Name = "Driver 2", FavouriteNumbers = new[] { 44 } }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team2",
                Driver1Contract = new DriverContract { DriverId = "driver1", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver2", DriverNumber = 0 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Driver1 (2nd place) picks first and gets 33, Driver2 (3rd place) picks second and gets 44
            Assert.AreEqual(33, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(44, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionshipOrder_HigherPlacePicksFirst()
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
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ", TeamId = "team1", Points = 100 },
                            new HisoricalDriverStandingEntry { Position = 2, DriverId = "second", TeamId = "team2", Points = 90 },
                            new HisoricalDriverStandingEntry { Position = 3, DriverId = "third", TeamId = "team3", Points = 80 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "champ", Name = "Champion", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "second", Name = "Second", FavouriteNumbers = new[] { 44 } },
                    new DriverData { DriverId = "third", Name = "Third", FavouriteNumbers = new[] { 44, 55 } } // Also wants 44
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "second", DriverNumber = 0 }
            };
            var team2 = new TeamEntry
            {
                TeamId = "team3",
                Driver1Contract = new DriverContract { DriverId = "third", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 } // Dummy
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion gets 1, second place picks 44, third place gets 55 (44 already taken)
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(44, team1.Driver2Contract.DriverNumber);
            Assert.AreEqual(55, team2.Driver1Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_NewDriverNotInPreviousStandings_PicksLast()
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
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ", TeamId = "team1", Points = 100 },
                            new HisoricalDriverStandingEntry { Position = 2, DriverId = "second", TeamId = "team2", Points = 90 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "champ", Name = "Champion", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "second", Name = "Second", FavouriteNumbers = new[] { 44 } },
                    new DriverData { DriverId = "rookie", Name = "Rookie", FavouriteNumbers = new[] { 44, 55 } } // Not in standings
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "second", DriverNumber = 0 }
            };
            var team2 = new TeamEntry
            {
                TeamId = "team2",
                Driver1Contract = new DriverContract { DriverId = "rookie", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 } // Dummy
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion gets 1, second place picks 44, rookie picks last and gets 55 (44 taken)
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(44, team1.Driver2Contract.DriverNumber);
            Assert.AreEqual(55, team2.Driver1Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_ChampionIsDriver2_GetsNumberOne()
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
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ_id", TeamId = "team1", Points = 100 },
                            new HisoricalDriverStandingEntry { Position = 2, DriverId = "teammate_id", TeamId = "team1", Points = 90 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "teammate_id", Name = "Teammate", FavouriteNumbers = new[] { 44 } },
                    new DriverData { DriverId = "champ_id", Name = "Champion", FavouriteNumbers = new[] { 33 } }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "teammate_id", DriverNumber = 44 },
                Driver2Contract = new DriverContract { DriverId = "champ_id", DriverNumber = 33 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion (Driver2) gets 1, teammate picks 44
            Assert.AreEqual(44, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(1, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_NoPreviousStandings_KeepsExistingNumbers()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "driver1", Name = "Driver 1", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "driver2", Name = "Driver 2", FavouriteNumbers = new[] { 44 } }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "driver1", DriverNumber = 33 },
                Driver2Contract = new DriverContract { DriverId = "driver2", DriverNumber = 44 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // No previous season - keep existing numbers
            Assert.AreEqual(33, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(44, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_EmptyStandingsList_KeepsExistingNumbers()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>
                {
                    new HistoricalDriverStanding
                    {
                        Year = 2024,
                        Standing = new List<HisoricalDriverStandingEntry>() // Empty
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "driver1", Name = "Driver 1", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "driver2", Name = "Driver 2", FavouriteNumbers = new[] { 44 } }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "driver1", DriverNumber = 33 },
                Driver2Contract = new DriverContract { DriverId = "driver2", DriverNumber = 44 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Empty standings list - keep existing numbers
            Assert.AreEqual(33, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(44, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_AllFavoritesTaken_GetsNextAvailable()
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
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ", TeamId = "team1", Points = 100 },
                            new HisoricalDriverStandingEntry { Position = 2, DriverId = "driver1", TeamId = "team2", Points = 90 },
                            new HisoricalDriverStandingEntry { Position = 3, DriverId = "driver2", TeamId = "team3", Points = 80 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "champ", Name = "Champion", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "driver1", Name = "Driver 1", FavouriteNumbers = new[] { 44 } },
                    new DriverData { DriverId = "driver2", Name = "Driver 2", FavouriteNumbers = new[] { 44 } } // Same as driver1
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver1", DriverNumber = 0 }
            };
            var team2 = new TeamEntry
            {
                TeamId = "team3",
                Driver1Contract = new DriverContract { DriverId = "driver2", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 } // Dummy
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion gets 1, driver1 picks 44, driver2 gets next available (2)
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(44, team1.Driver2Contract.DriverNumber);
            Assert.AreEqual(2, team2.Driver1Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_NoFavoritesSpecified_GetsNextAvailable()
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
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ", TeamId = "team1", Points = 100 },
                            new HisoricalDriverStandingEntry { Position = 2, DriverId = "driver1", TeamId = "team2", Points = 90 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "champ", Name = "Champion", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "driver1", Name = "Driver 1" } // No favorites
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver1", DriverNumber = 0 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion gets 1, driver1 with no favorites gets next available (2)
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumbersToCurrentSeason_InvalidFavoriteNumber_GetsNextAvailable()
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
                            new HisoricalDriverStandingEntry { Position = 1, DriverId = "champ", TeamId = "team1", Points = 100 },
                            new HisoricalDriverStandingEntry { Position = 2, DriverId = "driver1", TeamId = "team2", Points = 90 }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "champ", Name = "Champion", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "driver1", Name = "Driver 1", FavouriteNumbers = new[] { 1, 0, -5 } } // All invalid
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "champ", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver1", DriverNumber = 0 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumbersToCurrentSeason(saveGame);

            // Champion gets 1, driver1 with invalid favorites gets next available (2)
            Assert.AreEqual(1, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(2, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumberAtGameCreation_AssignsFirstFavoriteToEachDriver()
        {
            var saveGame = new SaveGame
            {
                PlayerData = new PlayerData { DriverId = "driver1" },
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "driver1", Name = "Driver 1", FavouriteNumbers = new[] { 33, 3 } },
                    new DriverData { DriverId = "driver2", Name = "Driver 2", FavouriteNumbers = new[] { 44, 4 } }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "driver1", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver2", DriverNumber = 44 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumberAtGameCreation(saveGame);

            Assert.AreEqual(33, team1.Driver1Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumberAtGameCreation_FirstFavoriteTaken_AssignsSecondFavorite()
        {
            var saveGame = new SaveGame
            {
                PlayerData = new PlayerData { DriverId = "driver2" },
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "driver1", Name = "Driver 1", FavouriteNumbers = new[] { 33 } },
                    new DriverData { DriverId = "driver2", Name = "Driver 2", FavouriteNumbers = new[] { 33, 44 } }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "driver1", DriverNumber = 33 },
                Driver2Contract = new DriverContract { DriverId = "driver2", DriverNumber = 0 }
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumberAtGameCreation(saveGame);

            Assert.AreEqual(33, team1.Driver1Contract.DriverNumber);
            Assert.AreEqual(44, team1.Driver2Contract.DriverNumber);
        }

        [TestMethod]
        public void AssignNumberAtGameCreation_ReservesNumberOneForChampion()
        {
            var saveGame = new SaveGame
            {
                PlayerData = new PlayerData { DriverId = "driver1" },
                CurrentSeason = new Season { Year = 2025, Teams = new List<ITeamEntry>() },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "driver1", Name = "Driver 1", FavouriteNumbers = new[] { 1, 2 } }
                }
            };

            var team1 = new TeamEntry
            {
                TeamId = "team1",
                Driver1Contract = new DriverContract { DriverId = "driver1", DriverNumber = 0 },
                Driver2Contract = new DriverContract { DriverId = "driver2", DriverNumber = 3 } // Dummy
            };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1 };

            _service.AssignNumberAtGameCreation(saveGame);

            // Should skip favorite #1 (reserved) and get #2
            Assert.AreEqual(2, team1.Driver1Contract.DriverNumber);
        }
    }
}