using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class DriverDatabaseTests
    {
        [TestMethod]
        public void Build_DriverWithASeat_IsRacingWithHisTeamAndNumber()
        {
            var saveGame = CreateTestSaveGame(1996);
            saveGame.Drivers = new List<IDriverData> { Driver("michael_schumacher", "Michael Schumacher", 1969) };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { Team("ferrari", "Scuderia Ferrari Marlboro", "#DC143C", ("michael_schumacher", 1), ("eddie_irvine", 2)) };

            var entry = DriverDatabase.Build(saveGame).Single();

            Assert.AreEqual(DriverDatabaseStatus.RACING, entry.Status);
            Assert.AreEqual("ferrari", entry.TeamId);
            Assert.AreEqual("Scuderia Ferrari Marlboro", entry.TeamName);
            Assert.AreEqual("Ferrari", entry.TeamShortName);
            Assert.AreEqual("#DC143C", entry.TeamColor);
            Assert.AreEqual(1, entry.CarNumber);
            Assert.AreEqual(27, entry.Age);
        }

        [TestMethod]
        public void Build_DriverInThePoolWithoutASeat_IsAFreeAgentWithoutTeam()
        {
            var saveGame = CreateTestSaveGame(1996);
            saveGame.Drivers = new List<IDriverData> { Driver("karl_wendlinger", "Karl Wendlinger", 1968) };

            var entry = DriverDatabase.Build(saveGame).Single();

            Assert.AreEqual(DriverDatabaseStatus.FREE_AGENT, entry.Status);
            Assert.IsNull(entry.TeamId);
            Assert.IsNull(entry.TeamName);
            Assert.IsNull(entry.CarNumber);
        }

        [TestMethod]
        public void Build_RetiredDriver_IsRetiredWithoutTeam()
        {
            var saveGame = CreateTestSaveGame(1996);
            saveGame.RetiredDrivers = new List<IDriverData> { Driver("alain_prost", "Alain Prost", 1955) };

            var entry = DriverDatabase.Build(saveGame).Single();

            Assert.AreEqual(DriverDatabaseStatus.RETIRED, entry.Status);
            Assert.IsNull(entry.TeamId);
            Assert.AreEqual(41, entry.Age);
        }

        [TestMethod]
        public void Build_DriverInThePoolAndAmongTheRetired_IsListedOnceFromThePool()
        {
            var saveGame = CreateTestSaveGame(1996);
            saveGame.Drivers = new List<IDriverData> { Driver("comeback", "Come Back", 1960) };
            saveGame.RetiredDrivers = new List<IDriverData> { Driver("comeback", "Come Back", 1960) };

            var entry = DriverDatabase.Build(saveGame).Single();

            Assert.AreEqual(DriverDatabaseStatus.FREE_AGENT, entry.Status);
        }

        [TestMethod]
        [DataRow("Pedro De La Rosa", "Pedro", "De La Rosa")]
        [DataRow("Juan Pablo Montoya", "Juan Pablo", "Montoya")]
        [DataRow("Zanardi", "", "Zanardi")]
        public void Build_SplitsFirstNameAndSurname(string fullName, string firstName, string surname)
        {
            var saveGame = CreateTestSaveGame(1996);
            saveGame.Drivers = new List<IDriverData> { Driver("someone", fullName, 1970) };

            var entry = DriverDatabase.Build(saveGame).Single();

            Assert.AreEqual(fullName, entry.FullName);
            Assert.AreEqual(firstName, entry.FirstName);
            Assert.AreEqual(surname, entry.Surname);
        }

        [TestMethod]
        public void Build_SortsBySurnameThenFirstName()
        {
            var saveGame = CreateTestSaveGame(1996);
            saveGame.Drivers = new List<IDriverData>
            {
                Driver("ralf", "Ralf Schumacher", 1975),
                Driver("alesi", "Jean Alesi", 1964),
                Driver("michael", "Michael Schumacher", 1969)
            };
            saveGame.RetiredDrivers = new List<IDriverData> { Driver("berger", "Gerhard Berger", 1959) };

            var ids = DriverDatabase.Build(saveGame).Select(e => e.DriverId).ToList();

            CollectionAssert.AreEqual(new List<string> { "alesi", "berger", "michael", "ralf" }, ids);
        }

        [TestMethod]
        public void Build_ThePlayer_IsMarked()
        {
            var saveGame = CreateTestSaveGame(1996);
            saveGame.Drivers = new List<IDriverData> { Driver("PLAYER", "Test Player", 1974), Driver("damon_hill", "Damon Hill", 1960) };

            var entries = DriverDatabase.Build(saveGame);

            Assert.IsTrue(entries.Single(e => e.DriverId == "PLAYER").IsPlayer);
            Assert.IsFalse(entries.Single(e => e.DriverId == "damon_hill").IsPlayer);
        }

        [TestMethod]
        public void Build_UnknownYearOfBirthOrNoNumber_LeavesThemEmpty()
        {
            var saveGame = CreateTestSaveGame(1996);
            saveGame.Drivers = new List<IDriverData> { Driver("nobody", "No Body", 0) };
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { Team("minardi", "Lunardi Fond", "#228B22", ("nobody", 0)) };

            var entry = DriverDatabase.Build(saveGame).Single();

            Assert.IsNull(entry.Age);
            Assert.IsNull(entry.CarNumber);
            Assert.AreEqual(DriverDatabaseStatus.RACING, entry.Status);
        }

        private static IDriverData Driver(string id, string name, int yearOfBirth)
        {
            return new DriverData { DriverId = id, Name = name, Nationality = "GBR", YearOfBirth = yearOfBirth };
        }

        private static ITeamEntry Team(string id, string name, string color, params (string DriverId, int Number)[] seats)
        {
            return new TeamEntry
            {
                TeamId = id,
                TeamName = name,
                Color = color,
                Driver1Contract = seats.Length > 0 ? new DriverContract { DriverId = seats[0].DriverId, DriverNumber = seats[0].Number } : null,
                Driver2Contract = seats.Length > 1 ? new DriverContract { DriverId = seats[1].DriverId, DriverNumber = seats[1].Number } : null
            };
        }

        private static SaveGame CreateTestSaveGame(int year)
        {
            return new SaveGame
            {
                CurrentSeason = new Season { Year = year, Races = new List<Race>(), Teams = new List<ITeamEntry>(), Absences = new List<Absence>() },
                Drivers = new List<IDriverData>(),
                RetiredDrivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                NextGpIndex = 0,
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = "T1" }
            };
        }
    }
}
