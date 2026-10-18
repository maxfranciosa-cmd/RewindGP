using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class HeadToHeadCalculatorTests
    {
        private const int Year = 1992;

        [TestMethod]
        public void GetTeamHeadToHead_DriverReplaced_ShowsAbsenceAndSubstituteAndLeavesTheRaceOutOfTheScore()
        {
            var saveGame = CreateSaveGame("Race A", "Race B", "Race C");
            AddRace(saveGame, "Race A", ("A", 1, 1), ("B", 2, 2));
            AddRace(saveGame, "Race B", ("A", 2, 2), ("SUB", 1, 1));
            AddRace(saveGame, "Race C", ("A", 2, 3), ("B", 1, 2));

            var headToHead = HeadToHeadCalculator.GetTeamHeadToHead(saveGame, "TEAM");

            var a = headToHead.Drivers.Single(d => d.DriverId == "A");
            var b = headToHead.Drivers.Single(d => d.DriverId == "B");
            var substitute = headToHead.Drivers.Single(d => d.DriverId == "SUB");

            Assert.IsTrue(substitute.IsSubstitute);
            Assert.IsTrue(b.Cells[1].Absent);
            Assert.IsFalse(substitute.Cells[0].Drove);
            Assert.AreEqual(1, substitute.Cells[1].Finish);
            Assert.IsTrue(headToHead.Races[1].HasSubstitute);
            Assert.AreEqual(2, headToHead.RacesTogether);
            Assert.AreEqual(1, headToHead.RacesWithSubstitute);
            Assert.IsFalse(a.Cells[1].Counted);

            // races A and C: one each
            Assert.AreEqual(1, a.QualifyingWins);
            Assert.AreEqual(1, b.QualifyingWins);
            Assert.AreEqual(1, a.RaceWins);
            Assert.AreEqual(1, b.RaceWins);
            Assert.AreEqual(2.0, a.AverageFinish.Value, 1e-9);
        }

        [TestMethod]
        public void GetTeamHeadToHead_BothRetired_RaceDoesNotCount()
        {
            var saveGame = CreateSaveGame("Race A");
            AddRace(saveGame, "Race A", ("A", 1, 10), ("B", 2, 12));
            foreach (var result in saveGame.GrandPrixResults.Single().RaceResults)
                result.DNF = true;

            var headToHead = HeadToHeadCalculator.GetTeamHeadToHead(saveGame, "TEAM");

            var a = headToHead.Drivers.Single(d => d.DriverId == "A");
            Assert.AreEqual(0, a.RaceWins);
            Assert.AreEqual(0, headToHead.Drivers.Single(d => d.DriverId == "B").RaceWins);
            Assert.IsFalse(a.Cells[0].Counted);
            Assert.AreEqual(1, a.Retirements);
            Assert.IsNull(a.AverageFinish);
            // qualifying still counts
            Assert.AreEqual(1, a.QualifyingWins);
        }

        [TestMethod]
        public void GetTeamHeadToHead_TeammateDidNotPrequalify_LosesBothQualifyingAndRace()
        {
            var saveGame = CreateSaveGame("Race A");
            AddRace(saveGame, "Race A", ("A", 20, 15), ("B", 0, 0));
            var gp = saveGame.GrandPrixResults.Single();
            gp.RaceResults.Single(r => r.DriverId == "B").DidNotPreQualify = true;
            gp.QualifyingResults.RemoveAll(q => q.DriverId == "B");

            var headToHead = HeadToHeadCalculator.GetTeamHeadToHead(saveGame, "TEAM");

            var a = headToHead.Drivers.Single(d => d.DriverId == "A");
            var b = headToHead.Drivers.Single(d => d.DriverId == "B");
            Assert.IsTrue(b.Cells[0].NotPrequalified);
            Assert.IsTrue(b.Cells[0].Drove);
            Assert.AreEqual(1, a.QualifyingWins);
            Assert.AreEqual(1, a.RaceWins);
            Assert.IsTrue(a.Cells[0].AheadOfTeammate);
            Assert.AreEqual(1, headToHead.RacesTogether);
        }

        [TestMethod]
        public void GetTeamHeadToHead_RaceNotRunYet_IsNeitherAbsentNorCounted()
        {
            var saveGame = CreateSaveGame("Race A", "Race B");
            AddRace(saveGame, "Race A", ("A", 1, 1), ("B", 2, 2));

            var headToHead = HeadToHeadCalculator.GetTeamHeadToHead(saveGame, "TEAM");

            Assert.AreEqual(1, headToHead.RacesRun);
            Assert.IsFalse(headToHead.Races[1].IsRun);
            Assert.IsTrue(headToHead.Drivers.All(d => !d.Cells[1].Absent && !d.Cells[1].Drove));
        }

        [TestMethod]
        public void GetTeamHeadToHead_ResultsMatchedToTheCalendarByName()
        {
            // the calendar was reordered after the races were run: names, not order, place them
            var saveGame = CreateSaveGame("Race B", "Race A");
            AddRace(saveGame, "Race A", ("A", 1, 1), ("B", 2, 2));

            var headToHead = HeadToHeadCalculator.GetTeamHeadToHead(saveGame, "TEAM");

            Assert.IsFalse(headToHead.Races[0].IsRun);
            Assert.IsTrue(headToHead.Races[1].IsRun);
            Assert.AreEqual(1, headToHead.Drivers.Single(d => d.DriverId == "A").Cells[1].Finish);
        }

        #region Helpers

        private static ISaveGame CreateSaveGame(params string[] raceNames)
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = Year,
                    Teams = new List<ITeamEntry>
                    {
                        new TeamEntry
                        {
                            TeamId = "TEAM",
                            TeamName = "Team",
                            Driver1Contract = new DriverContract { DriverId = "A" },
                            Driver2Contract = new DriverContract { DriverId = "B" }
                        }
                    },
                    Races = raceNames.Select((name, i) => new Race { RaceId = i + 1, RaceName = name }).ToList(),
                    Absences = new List<Absence>()
                },
                Drivers = new List<IDriverData>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                PlayerData = new PlayerData { DriverId = "A", TeamId = "TEAM" }
            };
        }

        // (driver, grid, finish) for each driver of the team at this race
        private static void AddRace(ISaveGame saveGame, string raceName, params (string DriverId, int Grid, int Finish)[] drivers)
        {
            saveGame.GrandPrixResults = saveGame.GrandPrixResults.Append(new GrandPrixResult
            {
                Year = Year,
                GrandPrixName = raceName,
                QualifyingResults = drivers.Select(d => new SessionResult { DriverId = d.DriverId, TeamId = "TEAM", Position = d.Grid }).ToList(),
                RaceResults = drivers.Select(d => new SessionResult { DriverId = d.DriverId, TeamId = "TEAM", Position = d.Finish }).ToList()
            }).ToList();
        }

        #endregion
    }
}
