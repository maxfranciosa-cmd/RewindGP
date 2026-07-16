using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Moq;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    /// <summary>
    /// A team with no second car this season is represented as a real Driver2Contract with an
    /// empty DriverId (and its historically-reserved DriverNumber still populated), not a null
    /// contract - see EndOfSeasonManager/OffSeasonMovements for the corresponding production fix.
    /// </summary>
    [TestClass]
    public class OneCarTeamTests
    {
        private EndOfSeasonManager _endOfSeasonManager;
        private Mock<IReputationUpdater> _mockReputationUpdater;
        private OffSeasonMovements _offSeasonMovements;

        [TestInitialize]
        public void Setup()
        {
            _mockReputationUpdater = new Mock<IReputationUpdater>();
            _mockReputationUpdater
                .Setup(r => r.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<DriverReputation, int, int, int, int, int>((rep, age, pos, pods, dnfs, races) => rep);

            _offSeasonMovements = new OffSeasonMovements(new DriverFirer(), new DriverHirer());

            _endOfSeasonManager = new EndOfSeasonManager(
                _mockReputationUpdater.Object,
                _offSeasonMovements,
                new Mock<IRandomDriverGenerator>().Object);
        }

        [TestMethod]
        public void ExecuteTeamDrops_OneCarTeam_Driver2NeverDropped()
        {
            // Arrange - a MINNOW team with only driver1; Driver2Contract exists (reserved
            // number 4) but has no DriverId.
            var oneCarTeam = CreateOneCarTestTeamEntry("T1", TeamReputation.MINNOW, "D1", driver2Number: 4);
            var saveGame = CreateTestSaveGame(2024);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { oneCarTeam };
            saveGame.Drivers = new List<IDriverData>
            {
                CreateTestDriver("D1", "Driver One", 1995, DriverReputation.PRIME_MIDFIELD)
            };

            var nextSeasonTeam = CreateOneCarTestTeamEntry("T1", TeamReputation.MINNOW, "TBD1", driver2Number: 4);
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { nextSeasonTeam });

            // Act
            var results = _endOfSeasonManager.ExecuteTeamDrops(saveGame, newSeason).ToList();

            // Assert - no exception, and there was never a real second driver to drop
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, results[0].DropDriver1);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, results[0].DropDriver2);
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_OneCarTeam_NeverGeneratesSecondDriverBallot()
        {
            // Arrange
            var oneCarTeam = CreateOneCarTestTeamEntry("T1", TeamReputation.MINNOW, "D1", driver2Number: 4);
            var saveGame = CreateTestSaveGame(2024);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { oneCarTeam };
            saveGame.Drivers = new List<IDriverData>
            {
                CreateTestDriver("D1", "Driver One", 1995, DriverReputation.PRIME_MIDFIELD)
            };

            var nextSeasonTeam = CreateOneCarTestTeamEntry("T1", TeamReputation.MINNOW, "TBD1", driver2Number: 4);
            var newSeasonTeamEntries = new List<ITeamEntry> { nextSeasonTeam };

            var dropResults = _endOfSeasonManager
                .ExecuteTeamDrops(saveGame, CreateTestSeason(2025, newSeasonTeamEntries))
                .ToList();

            // Act
            var ballots = _endOfSeasonManager
                .TeamPicksPotentialReplacementsDrivers(2025, saveGame, newSeasonTeamEntries, dropResults)
                .ToList();

            // Assert - driver1's contract is safe (MINNOW, races left, not retiring) and there's
            // no real driver2 to replace, so no ballot should ever be generated for this team.
            Assert.IsFalse(ballots.Any(b => b.OriginalTeamHiring.TeamId == "T1"),
                "A one-car team should never get a hiring ballot for either seat in this scenario");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_TwoOneCarTeams_DoesNotThrowOnDuplicateEmptyDriverId()
        {
            // Arrange - two different one-car teams, both continuing into next season with no
            // hiring ballots (both drivers retained). Before the fix, employedDriversIds.Add would
            // throw ArgumentException the second time it tried to add the "" key for Driver2.
            var team1 = CreateOneCarTestTeamEntry("T1", TeamReputation.MINNOW, "D1", driver2Number: 4);
            var team2 = CreateOneCarTestTeamEntry("T2", TeamReputation.MINNOW, "D2", driver2Number: 6);

            var saveGame = CreateTestSaveGame(2024);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team1, team2 };
            saveGame.Drivers = new List<IDriverData>
            {
                CreateTestDriver("D1", "Driver One", 1995, DriverReputation.PRIME_MIDFIELD),
                CreateTestDriver("D2", "Driver Two", 1996, DriverReputation.PRIME_MIDFIELD)
            };

            var nextTeam1 = CreateOneCarTestTeamEntry("T1", TeamReputation.MINNOW, "TBD1", driver2Number: 4);
            var nextTeam2 = CreateOneCarTestTeamEntry("T2", TeamReputation.MINNOW, "TBD2", driver2Number: 6);
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { nextTeam1, nextTeam2 });

            var ballots = new List<TeamHiringBallot>();

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            var resultTeam1 = resultSeason.Teams.First(t => t.TeamId == "T1");
            var resultTeam2 = resultSeason.Teams.First(t => t.TeamId == "T2");

            Assert.AreEqual("D1", resultTeam1.Driver1Contract.DriverId, "driver1 should be retained for T1");
            Assert.AreEqual("D2", resultTeam2.Driver1Contract.DriverId, "driver1 should be retained for T2");
            Assert.IsTrue(string.IsNullOrEmpty(resultTeam1.Driver2Contract?.DriverId), "T1 should stay one-car");
            Assert.IsTrue(string.IsNullOrEmpty(resultTeam2.Driver2Contract?.DriverId), "T2 should stay one-car");
        }

        private ISaveGame CreateTestSaveGame(int year)
        {
            return new SaveGame
            {
                CurrentSeason = new Season { Year = year, Teams = new List<ITeamEntry>(), Races = new List<Race>() },
                Drivers = new List<IDriverData>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", TeamId = null }
            };
        }

        private IDriverData CreateTestDriver(string driverId, string name, int yearOfBirth, DriverReputation reputation)
        {
            return new DriverData
            {
                DriverId = driverId,
                Name = name,
                YearOfBirth = yearOfBirth,
                Reputation = reputation
            };
        }

        private ITeamEntry CreateOneCarTestTeamEntry(string teamId, TeamReputation reputation, string driver1Id, int driver2Number)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                Reputation = reputation,
                Driver1Contract = new DriverContract { DriverId = driver1Id, Races = 20 },
                Driver2Contract = new DriverContract { DriverId = null, Races = 0, DriverNumber = driver2Number }
            };
        }

        private ISeason CreateTestSeason(int year, List<ITeamEntry> teams)
        {
            return new Season
            {
                Year = year,
                Teams = teams,
                Races = new List<Race>
                {
                    new Race { RaceId = 1, RaceName = "Race 1" }
                },
                Absences = new List<Absence>()
            };
        }
    }
}
