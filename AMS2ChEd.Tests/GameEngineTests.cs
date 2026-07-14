using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class GameEngineTests
    {
        private TestableGameEngine _engine;

        [TestInitialize]
        public void Setup()
        {
            _engine = new TestableGameEngine();
        }

        #region UpdateSeasonInsideSave Tests

        [TestMethod]
        public void UpdateSeasonInsideSave_NoUpdatedSeasonAvailable_DoesNothing()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var team = CreateTeam("team1", "driver1", 5, "driver2", 6);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };

            _engine.SeasonToReturn = null;

            // Act
            _engine.UpdateSeasonInsideSave(saveGame);

            // Assert
            Assert.AreSame(team, saveGame.CurrentSeason.Teams.Single());
            Assert.AreEqual(0, _engine.TeamEntryUpdateCalls.Count);
            Assert.AreEqual(0, _engine.SeasonUpdateCalls.Count);
        }

        [TestMethod]
        public void UpdateSeasonInsideSave_DriverInUpdatedDatabase_ClonesButKeepsSaveReputation()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            saveGame.Drivers = new List<IDriverData>
            {
                CreateDriver("D1", "Old Name", DriverReputation.PRIME_MIDFIELD)
            };

            var updatedSeason = CreateSeason(1995);
            _engine.SeasonToReturn = updatedSeason;
            _engine.DriversToReturn = new Dictionary<string, IDriverData>
            {
                ["D1"] = CreateDriver("D1", "New Name", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL)
            };

            // Act
            _engine.UpdateSeasonInsideSave(saveGame);

            // Assert - name comes from the updated database, reputation is kept from the save
            var resultDriver = saveGame.Drivers.Single();
            Assert.AreEqual("New Name", resultDriver.Name);
            Assert.AreEqual(DriverReputation.PRIME_MIDFIELD, resultDriver.Reputation);
        }

        [TestMethod]
        public void UpdateSeasonInsideSave_DriverNotInUpdatedDatabase_KeepsSaveDriverUnchanged()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var existingDriver = CreateDriver("D1", "Untouched Name", DriverReputation.PRIME_MIDFIELD);
            saveGame.Drivers = new List<IDriverData> { existingDriver };

            _engine.SeasonToReturn = CreateSeason(1995);
            _engine.DriversToReturn = new Dictionary<string, IDriverData>();

            // Act
            _engine.UpdateSeasonInsideSave(saveGame);

            // Assert
            Assert.AreSame(existingDriver, saveGame.Drivers.Single());
        }

        [TestMethod]
        public void UpdateSeasonInsideSave_TeamFoundInUpdatedSeason_PortsContractsAndCallsConcreteHook()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var oldTeam = CreateTeam("team1", "driver1", 5, "driver2", 6);
            oldTeam.DefaultPrequalifying = true;
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { oldTeam };

            var updatedSeason = CreateSeason(1995);
            var updatedTeam = CreateTeam("team1", "placeholder1", 0, "placeholder2", 0);
            updatedTeam.DefaultPrequalifying = false;
            updatedSeason.Teams = new List<ITeamEntry> { updatedTeam };

            _engine.SeasonToReturn = updatedSeason;

            // Act
            _engine.UpdateSeasonInsideSave(saveGame);

            // Assert - the resulting team entry ported the old contracts and prequalifying flag
            var resultTeam = saveGame.CurrentSeason.Teams.Single(t => t.TeamId == "team1");
            Assert.AreEqual("driver1", resultTeam.Driver1Contract.DriverId);
            Assert.AreEqual(5, resultTeam.Driver1Contract.DriverNumber);
            Assert.AreEqual("driver2", resultTeam.Driver2Contract.DriverId);
            Assert.AreEqual(6, resultTeam.Driver2Contract.DriverNumber);
            Assert.IsTrue(resultTeam.DefaultPrequalifying);

            // Assert - the concrete (game-specific) hook was invoked for this team with the updated year
            Assert.AreEqual(1, _engine.TeamEntryUpdateCalls.Count);
            Assert.AreEqual("team1", _engine.TeamEntryUpdateCalls[0].team.TeamId);
            Assert.AreEqual(1995, _engine.TeamEntryUpdateCalls[0].year);
        }

        [TestMethod]
        public void UpdateSeasonInsideSave_TeamNotFoundInUpdatedSeason_KeepsOldTeamAndSkipsHook()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var oldTeam = CreateTeam("team_gone", "driver1", 5, "driver2", 6);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { oldTeam };

            var updatedSeason = CreateSeason(1995);
            updatedSeason.Teams = new List<ITeamEntry>(); // team no longer present in the pack
            _engine.SeasonToReturn = updatedSeason;

            // Act
            _engine.UpdateSeasonInsideSave(saveGame);

            // Assert
            Assert.AreSame(oldTeam, saveGame.CurrentSeason.Teams.Single());
            Assert.AreEqual(0, _engine.TeamEntryUpdateCalls.Count);
        }

        [TestMethod]
        public void UpdateSeasonInsideSave_UpdatesDriverNamesInHistoricalStandings()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            saveGame.Drivers = new List<IDriverData> { CreateDriver("D1", "Old Name", DriverReputation.PRIME_MIDFIELD) };
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                new HistoricalDriverStanding
                {
                    Year = 1994,
                    Standing = new List<HisoricalDriverStandingEntry>
                    {
                        new HisoricalDriverStandingEntry { DriverId = "D1", DriverName = "Old Name" }
                    }
                }
            };

            _engine.SeasonToReturn = CreateSeason(1995);
            _engine.DriversToReturn = new Dictionary<string, IDriverData>
            {
                ["D1"] = CreateDriver("D1", "New Name", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL)
            };

            // Act
            _engine.UpdateSeasonInsideSave(saveGame);

            // Assert
            Assert.AreEqual("New Name", saveGame.HistoricalDriverStandings.Single().Standing.Single().DriverName);
        }

        [TestMethod]
        public void UpdateSeasonInsideSave_UpdatesRaceInfoFromUpdatedSeason()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            saveGame.CurrentSeason.Races = new List<Race>
            {
                new Race { RaceId = 1, RaceName = "Old GP", RaceShortName = "OGP", Circuit = "Old Circuit", RaceDate = "1994-01-01", CoverPictureUrl = "old.png" }
            };

            var updatedSeason = CreateSeason(1995);
            updatedSeason.Races = new List<Race>
            {
                new Race { RaceId = 1, RaceName = "New GP", RaceShortName = "NGP", Circuit = "New Circuit", RaceDate = "1995-03-03", CoverPictureUrl = "new.png" }
            };
            _engine.SeasonToReturn = updatedSeason;

            // Act
            _engine.UpdateSeasonInsideSave(saveGame);

            // Assert
            var race = saveGame.CurrentSeason.Races.Single();
            Assert.AreEqual("New GP", race.RaceName);
            Assert.AreEqual("NGP", race.RaceShortName);
            Assert.AreEqual("New Circuit", race.Circuit);
            Assert.AreEqual("1995-03-03", race.RaceDate);
            Assert.AreEqual("new.png", race.CoverPictureUrl);
        }

        [TestMethod]
        public void UpdateSeasonInsideSave_CallsConcreteSeasonUpdateHook()
        {
            // Arrange
            var saveGame = CreateSaveGame();
            var updatedSeason = CreateSeason(1995);
            _engine.SeasonToReturn = updatedSeason;

            // Act
            _engine.UpdateSeasonInsideSave(saveGame);

            // Assert
            Assert.AreEqual(1, _engine.SeasonUpdateCalls.Count);
            Assert.AreSame(saveGame.CurrentSeason, _engine.SeasonUpdateCalls[0].current);
            Assert.AreEqual(1995, _engine.SeasonUpdateCalls[0].updated.Year);
        }

        #endregion

        #region Helper Methods

        private ISaveGame CreateSaveGame()
        {
            return new SaveGame
            {
                CurrentSeason = CreateSeason(1994),
                Drivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                NextGpIndex = 0,
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player" }
            };
        }

        private ISeason CreateSeason(int year)
        {
            return new Season
            {
                Year = year,
                Teams = new List<ITeamEntry>(),
                Races = new List<Race>(),
                Absences = new List<Absence>()
            };
        }

        private IDriverData CreateDriver(string driverId, string name, DriverReputation reputation)
        {
            return new DriverData
            {
                DriverId = driverId,
                Name = name,
                Nationality = "GBR",
                Reputation = reputation
            };
        }

        private ITeamEntry CreateTeam(string teamId, string driver1Id, int driver1Number, string driver2Id, int driver2Number)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                TeamName = $"Test Team {teamId}",
                Driver1Contract = new DriverContract { DriverId = driver1Id, DriverNumber = driver1Number },
                Driver2Contract = new DriverContract { DriverId = driver2Id, DriverNumber = driver2Number }
            };
        }

        #endregion

        private class TestableGameEngine : GameEngine
        {
            public ISeason SeasonToReturn { get; set; }
            public Dictionary<string, IDriverData> DriversToReturn { get; set; } = new();
            public List<(ISeason current, ISeason updated)> SeasonUpdateCalls { get; } = new();
            public List<(ITeamEntry team, int year)> TeamEntryUpdateCalls { get; } = new();

            protected override ISeason LoadUpdatedSeasonForRefresh(int year) => SeasonToReturn;

            protected override Dictionary<string, IDriverData> LoadUpdatedDriversForRefresh(int year) => DriversToReturn;

            protected override void ApplyConcreteSeasonUpdates(ISeason currentSeason, ISeason updatedSeason)
            {
                SeasonUpdateCalls.Add((currentSeason, updatedSeason));
            }

            protected override void ApplyConcreteTeamEntryUpdates(ITeamEntry updatedTeamEntry, int updatedSeasonYear)
            {
                TeamEntryUpdateCalls.Add((updatedTeamEntry, updatedSeasonYear));
            }
        }
    }
}
