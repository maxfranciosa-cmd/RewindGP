using AMS2ChEd.Business.GameLogic.Concrete;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class LegendsCalculatorTests
    {
        // ---- FindWinMilestone ----

        [TestMethod]
        public void FindWinMilestone_DrawsLevelWithLegend_ReturnsLevel()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("winner", 25),
                Entry("niki_lauda", 25, 1975, 1977, 1984),
                Entry("someone_else", 40)
            };

            var milestone = LegendsCalculator.FindWinMilestone(hallOfFame, "winner");

            Assert.AreEqual(WinMilestoneKind.LEVEL, milestone.Kind);
            Assert.AreEqual("niki_lauda", milestone.Legend.DriverId);
            Assert.AreEqual(25, milestone.Wins);
        }

        [TestMethod]
        public void FindWinMilestone_GoesPastLegend_ReturnsPassed()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("winner", 32),
                Entry("nigel_mansell", 31, 1992),
                Entry("alain_prost", 51, 1985, 1986, 1989, 1993)
            };

            var milestone = LegendsCalculator.FindWinMilestone(hallOfFame, "winner");

            Assert.AreEqual(WinMilestoneKind.PASSED, milestone.Kind);
            Assert.AreEqual("nigel_mansell", milestone.Legend.DriverId);
        }

        [TestMethod]
        public void FindWinMilestone_LevelAndPassedBothAvailable_NamesTheGreaterFigure()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("winner", 14),
                Entry("one_time_champion", 14, 1980),
                Entry("triple_champion", 13, 1960, 1961, 1962),
                Entry("record_holder", 50)
            };

            var milestone = LegendsCalculator.FindWinMilestone(hallOfFame, "winner");

            Assert.AreEqual(WinMilestoneKind.PASSED, milestone.Kind);
            Assert.AreEqual("triple_champion", milestone.Legend.DriverId);
        }

        [TestMethod]
        public void FindWinMilestone_EqualFame_PrefersDrawingLevel()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("winner", 12),
                Entry("level_legend", 12, 1970),
                Entry("passed_legend", 11, 1971),
                Entry("record_holder", 50)
            };

            var milestone = LegendsCalculator.FindWinMilestone(hallOfFame, "winner");

            Assert.AreEqual(WinMilestoneKind.LEVEL, milestone.Kind);
            Assert.AreEqual("level_legend", milestone.Legend.DriverId);
        }

        [TestMethod]
        public void FindWinMilestone_JustBrokeAllTimeRecord_ReturnsRecord()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("winner", 52),
                Entry("alain_prost", 51, 1985, 1986, 1989, 1993),
                Entry("ayrton_senna", 41, 1988, 1990, 1991)
            };

            var milestone = LegendsCalculator.FindWinMilestone(hallOfFame, "winner");

            Assert.AreEqual(WinMilestoneKind.RECORD, milestone.Kind);
            Assert.AreEqual("alain_prost", milestone.Legend.DriverId);
        }

        [TestMethod]
        public void FindWinMilestone_AlreadyHoldsRecord_NoMilestoneUnlessSomeoneMatches()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("winner", 60),
                Entry("alain_prost", 51, 1985, 1986, 1989, 1993)
            };

            Assert.IsNull(LegendsCalculator.FindWinMilestone(hallOfFame, "winner"));
        }

        [TestMethod]
        public void FindWinMilestone_MatchedDriverIsNoLegend_ReturnsNull()
        {
            // no title and fewer than MinWinsForLegend wins
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("winner", 6),
                Entry("journeyman", 6),
                Entry("record_holder", 50)
            };

            Assert.IsNull(LegendsCalculator.FindWinMilestone(hallOfFame, "winner"));
        }

        [TestMethod]
        public void FindWinMilestone_TooFewWins_ReturnsNull()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("winner", LegendsCalculator.MinWinsForMilestone - 1),
                Entry("phil_hill", LegendsCalculator.MinWinsForMilestone - 1, 1961),
                Entry("record_holder", 50)
            };

            Assert.IsNull(LegendsCalculator.FindWinMilestone(hallOfFame, "winner"));
        }

        [TestMethod]
        public void FindWinMilestone_DriverNotInHallOfFame_ReturnsNull()
        {
            var hallOfFame = new List<HallOfFameEntry> { Entry("niki_lauda", 25, 1975) };

            Assert.IsNull(LegendsCalculator.FindWinMilestone(hallOfFame, "winner"));
            Assert.IsNull(LegendsCalculator.FindWinMilestone(null, "winner"));
        }

        // ---- FindTitleComparison ----

        [TestMethod]
        public void FindTitleComparison_FirstTitle_ReturnsNull()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("champion", 8),
                Entry("james_hunt", 10, 1976),
                Entry("jody_scheckter", 10, 1979)
            };

            Assert.IsNull(LegendsCalculator.FindTitleComparison(hallOfFame, "champion", 1995));
        }

        [TestMethod]
        public void FindTitleComparison_FirstTitle_ChampionMissingFromHallOfFame_ReturnsNull()
        {
            var hallOfFame = new List<HallOfFameEntry> { Entry("james_hunt", 10, 1976) };

            Assert.IsNull(LegendsCalculator.FindTitleComparison(hallOfFame, "champion", 1995));
        }

        [TestMethod]
        public void FindTitleComparison_SecondTitle_JoinsOldestAndNewestEra()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("champion", 20, 1993),
                Entry("jim_clark", 25, 1963, 1965),
                Entry("graham_hill", 14, 1962, 1968),
                Entry("emerson_fittipaldi", 14, 1972, 1974),
                Entry("mika_hakkinen", 20, 1998, 1999)
            };

            var comparison = LegendsCalculator.FindTitleComparison(hallOfFame, "champion", 1995);

            Assert.AreEqual(TitleComparisonKind.JOINS, comparison.Kind);
            Assert.AreEqual(2, comparison.Titles);
            CollectionAssert.AreEqual(new[] { "jim_clark", "mika_hakkinen" }, comparison.Legends.Select(l => l.DriverId).ToArray());
        }

        [TestMethod]
        public void FindTitleComparison_JustClinchedYearAlreadyInHistory_IsNotCountedTwice()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("champion", 20, 1993, 1995),
                Entry("jim_clark", 25, 1963, 1965)
            };

            var comparison = LegendsCalculator.FindTitleComparison(hallOfFame, "champion", 1995);

            Assert.AreEqual(2, comparison.Titles);
            Assert.AreEqual(TitleComparisonKind.JOINS, comparison.Kind);
        }

        [TestMethod]
        public void FindTitleComparison_SingleEqual_JoinsOneLegend()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("champion", 30, 1990, 1993),
                Entry("jack_brabham", 14, 1959, 1960, 1966),
                Entry("juan_manuel_fangio", 24, 1951, 1954, 1955, 1956, 1957)
            };

            var comparison = LegendsCalculator.FindTitleComparison(hallOfFame, "champion", 1995);

            Assert.AreEqual(TitleComparisonKind.JOINS, comparison.Kind);
            Assert.AreEqual("jack_brabham", comparison.Legends.Single().DriverId);
        }

        [TestMethod]
        public void FindTitleComparison_NobodyEqualButSomeoneOneBehind_Passes()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("champion", 30, 1990, 1992, 1993),
                Entry("three_time_champion", 25, 1963, 1965, 1966),
                Entry("juan_manuel_fangio", 24, 1951, 1954, 1955, 1956, 1957)
            };

            var comparison = LegendsCalculator.FindTitleComparison(hallOfFame, "champion", 1995);

            Assert.AreEqual(TitleComparisonKind.PASSES, comparison.Kind);
            Assert.AreEqual(4, comparison.Titles);
            Assert.AreEqual("three_time_champion", comparison.Legends.Single().DriverId);
        }

        [TestMethod]
        public void FindTitleComparison_GoesPastEveryone_Record()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("champion", 60, 1990, 1991, 1992, 1993, 1994),
                Entry("juan_manuel_fangio", 24, 1951, 1954, 1955, 1956, 1957)
            };

            var comparison = LegendsCalculator.FindTitleComparison(hallOfFame, "champion", 1995);

            Assert.AreEqual(TitleComparisonKind.RECORD, comparison.Kind);
            Assert.AreEqual(6, comparison.Titles);
            Assert.AreEqual("juan_manuel_fangio", comparison.Legends.Single().DriverId);
        }

        [TestMethod]
        public void FindTitleComparison_AlreadyHeldRecord_ExtendsIt()
        {
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("champion", 70, 1990, 1991, 1992, 1993, 1994, 1995),
                Entry("juan_manuel_fangio", 24, 1951, 1954, 1955, 1956, 1957)
            };

            var comparison = LegendsCalculator.FindTitleComparison(hallOfFame, "champion", 1996);

            Assert.AreEqual(TitleComparisonKind.EXTENDS_RECORD, comparison.Kind);
            Assert.AreEqual(7, comparison.Titles);
            Assert.AreEqual(0, comparison.Legends.Count);
        }

        [TestMethod]
        public void FindTitleComparison_GapToNearestLegend_ReturnsNull()
        {
            // 3 titles, nobody on 3 or 2, someone further ahead
            var hallOfFame = new List<HallOfFameEntry>
            {
                Entry("champion", 30, 1990, 1992),
                Entry("juan_manuel_fangio", 24, 1951, 1954, 1955, 1956, 1957)
            };

            Assert.IsNull(LegendsCalculator.FindTitleComparison(hallOfFame, "champion", 1995));
        }

        private static HallOfFameEntry Entry(string driverId, int wins, params int[] championshipYears) =>
            new HallOfFameEntry
            {
                DriverId = driverId,
                Name = driverId,
                Wins = wins,
                ChampionshipYears = championshipYears.ToList(),
                Status = HallOfFameStatus.HISTORIC
            };
    }
}
