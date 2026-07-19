using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class StandingsManagerTests
    {
        private StandingsManager _standingsManager;

        [TestInitialize]
        public void Setup()
        {
            _standingsManager = new StandingsManager();
        }

        #region UpdateStandings Tests

        [TestMethod]
        public void UpdateStandings_NullRacesToCount_SumsAllPointsLikeBefore()
        {
            var saveGame = CreateTestSaveGame(2024, racesToCount: null, raceCount: 4);

            PlayRace(saveGame, 0, ("D1", "T1", 1), ("D2", "T1", 2)); // D1:10 D2:8
            PlayRace(saveGame, 1, ("D1", "T1", 2), ("D2", "T1", 1)); // D1:8  D2:10
            PlayRace(saveGame, 2, ("D1", "T1", 1), ("D2", "T1", 3)); // D1:10 D2:6
            PlayRace(saveGame, 3, ("D1", "T1", 5), ("D2", "T1", 1)); // D1:2  D2:10

            var d1 = saveGame.CurrentDriverStandings.First(s => s.DriverId == "D1");
            var d2 = saveGame.CurrentDriverStandings.First(s => s.DriverId == "D2");

            Assert.AreEqual(30, d1.Points);
            Assert.AreEqual(34, d2.Points);
        }

        [TestMethod]
        public void UpdateStandings_MoreRacesPlayedThanRacesToCount_DropsWorstResults()
        {
            var saveGame = CreateTestSaveGame(2024, racesToCount: 3, raceCount: 5);

            PlayRace(saveGame, 0, ("D1", "T1", 1)); // 10
            PlayRace(saveGame, 1, ("D1", "T1", 2)); // 8
            PlayRace(saveGame, 2, ("D1", "T1", 1)); // 10
            PlayRace(saveGame, 3, ("D1", "T1", 5)); // 2
            PlayRace(saveGame, 4, ("D1", "T1", 1)); // 10

            var d1 = saveGame.CurrentDriverStandings.First(s => s.DriverId == "D1");

            Assert.AreEqual(30, d1.Points); // best 3 of [10,8,10,2,10] -> 10+10+10
        }

        [TestMethod]
        public void UpdateStandings_FewerRacesPlayedThanRacesToCount_NothingDroppedYet()
        {
            var saveGame = CreateTestSaveGame(2024, racesToCount: 5, raceCount: 5);

            PlayRace(saveGame, 0, ("D1", "T1", 1)); // 10
            PlayRace(saveGame, 1, ("D1", "T1", 5)); // 2
            PlayRace(saveGame, 2, ("D1", "T1", 3)); // 6

            var d1 = saveGame.CurrentDriverStandings.First(s => s.DriverId == "D1");

            Assert.AreEqual(18, d1.Points); // only 3 played, racesToCount=5 -> nothing dropped yet
        }

        [TestMethod]
        public void UpdateStandings_RaceDriverDidNotEnter_DoesNotAffectTheirTotal()
        {
            var saveGame = CreateTestSaveGame(2024, racesToCount: 2, raceCount: 3);

            PlayRace(saveGame, 0, ("D1", "T1", 1), ("D2", "T2", 2)); // D1 races: 10
            PlayRace(saveGame, 1, ("D2", "T2", 1));                 // D1 sits this one out
            PlayRace(saveGame, 2, ("D1", "T1", 5));                 // D1 races: 2

            var d1 = saveGame.CurrentDriverStandings.First(s => s.DriverId == "D1");

            // D1 only ever appears in 2 races (10 and 2) - racesToCount=2 means
            // nothing gets dropped, and the race they skipped is irrelevant.
            Assert.AreEqual(12, d1.Points);
        }

        [TestMethod]
        public void UpdateStandings_ResultAlreadyInHistory_IsNotDoubleCounted()
        {
            // Mirrors ScenarioSaveBuilder: GrandPrixResults is pre-populated with the
            // exact same object reference before UpdateStandings is called with it,
            // unlike RaceWeekendWindow which calls UpdateStandings before appending.
            var saveGame = CreateTestSaveGame(2024, racesToCount: null, raceCount: 1);
            var result = BuildResult(saveGame, 0, ("D1", "T1", 1)); // 10 points

            saveGame.GrandPrixResults = new List<GrandPrixResult> { result };
            saveGame.NextGpIndex = 0;

            _standingsManager.UpdateStandings(saveGame, result);

            var d1 = saveGame.CurrentDriverStandings.First(s => s.DriverId == "D1");

            Assert.AreEqual(10, d1.Points);
        }

        [TestMethod]
        public void UpdateStandings_ConstructorsAlwaysCountEveryRace_RegardlessOfRacesToCount()
        {
            var saveGame = CreateTestSaveGame(2024, racesToCount: 2, raceCount: 4);
            saveGame.CurrentConstructorStandings = new List<ConstructorStandingEntry>
            {
                new ConstructorStandingEntry { TeamId = "T1", Points = 0 }
            };

            PlayRace(saveGame, 0, ("D1", "T1", 1)); // 10
            PlayRace(saveGame, 1, ("D1", "T1", 5)); // 2
            PlayRace(saveGame, 2, ("D1", "T1", 1)); // 10
            PlayRace(saveGame, 3, ("D1", "T1", 5)); // 2

            var team = saveGame.CurrentConstructorStandings.First(s => s.TeamId == "T1");
            var driver = saveGame.CurrentDriverStandings.First(s => s.DriverId == "D1");

            Assert.AreEqual(24, team.Points);  // 10+2+10+2, every race counts for constructors
            Assert.AreEqual(20, driver.Points); // best 2 of [10,2,10,2] -> 10+10, drivers drop the worst 2
        }

        #endregion

        #region ChampionshipScoringCalculator Tests

        [TestMethod]
        public void GetDiscardedResults_ReturnsLowestScoringResultsForDriver()
        {
            var season = new Season { RacesToCountTowardsChampionship = 3 };
            var results = new List<GrandPrixResult>
            {
                MakeDriverResult("D1", 10),
                MakeDriverResult("D1", 8),
                MakeDriverResult("D1", 10),
                MakeDriverResult("D1", 2),
                MakeDriverResult("D1", 10),
            };

            var discarded = ChampionshipScoringCalculator.GetDiscardedResults("D1", season, results);

            Assert.AreEqual(2, discarded.Count);
            Assert.IsTrue(discarded.Contains(results[1])); // the 8
            Assert.IsTrue(discarded.Contains(results[3])); // the 2
        }

        [TestMethod]
        public void GetDiscardedResults_NullRacesToCount_ReturnsEmptySet()
        {
            var season = new Season { RacesToCountTowardsChampionship = null };
            var results = new List<GrandPrixResult> { MakeDriverResult("D1", 10), MakeDriverResult("D1", 2) };

            var discarded = ChampionshipScoringCalculator.GetDiscardedResults("D1", season, results);

            Assert.AreEqual(0, discarded.Count);
        }

        #endregion

        #region Helper Methods

        private ISaveGame CreateTestSaveGame(int year, int? racesToCount, int raceCount)
        {
            var races = Enumerable.Range(1, raceCount)
                .Select(i => new Race { RaceId = i, RaceName = $"Test GP {i}" })
                .ToList();

            var season = new Season
            {
                Year = year,
                Races = races,
                Teams = new List<ITeamEntry>(),
                Absences = new List<Absence>(),
                PointsSystem = new Dictionary<string, int> { { "1", 10 }, { "2", 8 }, { "3", 6 }, { "4", 4 }, { "5", 2 } },
                RacesToCountTowardsChampionship = racesToCount
            };

            return new SaveGame
            {
                CurrentSeason = season,
                Drivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                NextGpIndex = 0,
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = "T1" }
            };
        }

        private GrandPrixResult BuildResult(ISaveGame saveGame, int raceIndex, params (string DriverId, string TeamId, int Position)[] entries)
        {
            return new GrandPrixResult
            {
                Year = saveGame.CurrentSeason.Year,
                GrandPrixName = saveGame.CurrentSeason.Races.ElementAt(raceIndex).RaceName,
                QualifyingResults = new List<SessionResult>(),
                RaceResults = entries.Select(e => new SessionResult
                {
                    DriverId = e.DriverId,
                    TeamId = e.TeamId,
                    Position = e.Position
                }).ToList()
            };
        }

        // Mirrors RaceWeekendWindow's real call order: UpdateStandings runs before
        // the result is appended to GrandPrixResults.
        private void PlayRace(ISaveGame saveGame, int raceIndex, params (string DriverId, string TeamId, int Position)[] entries)
        {
            saveGame.NextGpIndex = raceIndex;
            var result = BuildResult(saveGame, raceIndex, entries);
            _standingsManager.UpdateStandings(saveGame, result);
            saveGame.GrandPrixResults = saveGame.GrandPrixResults.Append(result);
        }

        private GrandPrixResult MakeDriverResult(string driverId, double points)
        {
            return new GrandPrixResult
            {
                RaceResults = new List<SessionResult> { new SessionResult { DriverId = driverId, Points = points } }
            };
        }

        #endregion
    }
}
