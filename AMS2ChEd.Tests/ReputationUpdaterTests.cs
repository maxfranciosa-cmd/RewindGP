using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public sealed class ReputationUpdaterTests
    {
        public class DriverRecord
        {
            public int Year { get; set; }
            public string Name { get; set; }
            public int Podiums { get; set; }
            public int DNFs { get; set; }
            public int Position { get; set; }
            public int Age { get; set; }
            public DriverReputation CurrentReputation { get; set; }
            public DriverReputation ExpectedReputation { get; set; }
        }

 
        public static List<DriverRecord> Drivers = new()
        {
            // --------------------------
            //        F1 1994
            // --------------------------
            new() { Year = 1994, Name="Michael Schumacher", Position=1, Age=25, Podiums=10, DNFs=4, CurrentReputation=DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, ExpectedReputation= DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
            new() { Year = 1994, Name="Damon Hill", Position=2, Age=33, Podiums=10, DNFs=4, CurrentReputation=DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, ExpectedReputation= DriverReputation.AGEING_CHAMPIONSHIP_LEVEL },
            new() { Year = 1994, Name="Gerhard Berger", Position=3, Age=34, Podiums=3, DNFs=9 , CurrentReputation=DriverReputation.AGEING_STRONG_MIDFIELD, ExpectedReputation= DriverReputation.AGEING_CHAMPIONSHIP_LEVEL },
            new() { Year = 1994, Name="Mika Häkkinen", Position=4, Age=26, Podiums=3, DNFs=8 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN },
            new() { Year = 1994, Name="Jean Alesi", Position=5, Age=30, Podiums=1, DNFs=6 , CurrentReputation=DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, ExpectedReputation= DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN },
            new() { Year = 1994, Name="Rubens Barrichello", Position=6, Age=22, Podiums=1, DNFs=7 , CurrentReputation=DriverReputation.YOUNG_TALENT, ExpectedReputation= DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN },
            new() { Year = 1994, Name="Martin Brundle", Position=7, Age=35, Podiums=1, DNFs=10 , CurrentReputation=DriverReputation.AGEING_MIDFIELD, ExpectedReputation= DriverReputation.AGEING_STRONG_MIDFIELD },
            new() { Year = 1994, Name="David Coulthard", Position=8, Age=23, Podiums=1, DNFs=4 , CurrentReputation=DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, ExpectedReputation= DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN },
            new() { Year = 1994, Name="Nigel Mansell", Position=9, Age=41, Podiums=1, DNFs=3 , CurrentReputation=DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, ExpectedReputation= DriverReputation.JUST_ONE_LAST_DANCE },
            new() { Year = 1994, Name="Jos Verstappen", Position=10, Age=22, Podiums=2, DNFs=7 , CurrentReputation=DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, ExpectedReputation= DriverReputation.YOUNG_TALENT },
            new() { Year = 1994, Name="Olivier Panis", Position=11, Age=27, Podiums=1, DNFs=3 , CurrentReputation=DriverReputation.PRIME_STRONG_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_MIDFIELD },
            new() { Year = 1994, Name="Mark Blundell", Position=12, Age=28, Podiums=1, DNFs=10 , CurrentReputation=DriverReputation.PRIME_STRONG_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_MIDFIELD },
            new() { Year = 1994, Name="Heinz-Harald Frentzen", Position=13, Age=27, Podiums=0, DNFs=9 , CurrentReputation=DriverReputation.PRIME_STRONG_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_MIDFIELD },
            new() { Year = 1994, Name="Nicola Larini", Position=14, Age=30, Podiums=1, DNFs=1 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_MIDFIELD },
            new() { Year = 1994, Name="Christian Fittipaldi", Position=15, Age=23, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.YOUNG_TALENT, ExpectedReputation= DriverReputation.YOUNG_TALENT },
            new() { Year = 1994, Name="Eddie Irvine", Position=16, Age=28, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Ukyo Katayama", Position=17, Age=30, Podiums=0, DNFs=11 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Éric Bernard", Position=18, Age=30, Podiums=1, DNFs=6 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Karl Wendlinger", Position=19, Age=25, Podiums=0, DNFs=1 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Andrea de Cesaris", Position=20, Age=34, Podiums=0, DNFs=10 , CurrentReputation=DriverReputation.AGEING_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            // Beyond WILD_CARD_FLOOR_STANDINGS_THRESHOLD (P20): a pre-season reputation of
            // PRIME_MIDFIELD-or-below now falls to PAY_DRIVER_WILD_CARD instead of piling into
            // PAY_DRIVER_SEASON with everyone else who merely missed the points.
            new() { Year = 1994, Name="Pierluigi Martini", Position=21, Age=33, Podiums=0, DNFs=6 , CurrentReputation=DriverReputation.AGEING_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_WILD_CARD },
            new() { Year = 1994, Name="Gianni Morbidelli", Position=22, Age=26, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_WILD_CARD },
            new() { Year = 1994, Name="Érik Comas", Position=23, Age=30, Podiums=0, DNFs=7 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_WILD_CARD },
            new() { Year = 1994, Name="JJ Lehto", Position=24, Age=28, Podiums=0, DNFs=7 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_WILD_CARD },
            new() { Year = 1994, Name="Michele Alboreto", Position=25, Age=37, Podiums=0, DNFs=9 , CurrentReputation=DriverReputation.AGEING_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_WILD_CARD },


            // --------------------------
            //        F1 1995
            // --------------------------
            new() { Year = 1995, Name="Michael Schumacher", Position=1, Age=26, Podiums=11, DNFs=2 , CurrentReputation=DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, ExpectedReputation= DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
            new() { Year = 1995, Name="Damon Hill", Position=2, Age=34, Podiums=10, DNFs=5 , CurrentReputation=DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, ExpectedReputation= DriverReputation.AGEING_CHAMPIONSHIP_LEVEL },
            new() { Year = 1995, Name="David Coulthard", Position=3, Age=24, Podiums=8, DNFs=5, CurrentReputation=DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, ExpectedReputation= DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL },
            new() { Year = 1995, Name="Johnny Herbert", Position=4, Age=31, Podiums=3, DNFs=5 , CurrentReputation=DriverReputation.PRIME_STRONG_MIDFIELD, ExpectedReputation= DriverReputation.AGEING_STRONG_MIDFIELD },
            new() { Year = 1995, Name="Jean Alesi", Position=5, Age=31, Podiums=5, DNFs=8 , CurrentReputation=DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, ExpectedReputation= DriverReputation.AGEING_STRONG_MIDFIELD },
            new() { Year = 1995, Name="Gerhard Berger", Position=6, Age=36, Podiums=5, DNFs=7 , CurrentReputation=DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, ExpectedReputation= DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED },
            new() { Year = 1995, Name="Eddie Irvine", Position=7, Age=29, Podiums=3, DNFs=8 , CurrentReputation=DriverReputation.PAY_DRIVER_SEASON, ExpectedReputation= DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN },
            new() { Year = 1995, Name="Mika Häkkinen", Position=8, Age=27, Podiums=2, DNFs=5 , CurrentReputation=DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, ExpectedReputation= DriverReputation.PRIME_STRONG_MIDFIELD },
            new() { Year = 1995, Name="Rubens Barrichello", Position=9, Age=23, Podiums=1, DNFs=9 , CurrentReputation=DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, ExpectedReputation= DriverReputation.YOUNG_TALENT },
            new() { Year = 1995, Name="Olivier Panis", Position=10, Age=28, Podiums=0, DNFs=7 , CurrentReputation=DriverReputation.PRIME_STRONG_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_MIDFIELD },
            new() { Year = 1995, Name="Mark Blundell", Position=11, Age=29, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.PRIME_STRONG_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_MIDFIELD },
            new() { Year = 1995, Name="Mika Salo", Position=12, Age=28, Podiums=0, DNFs=7 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_MIDFIELD },
            new() { Year = 1995, Name="Martin Brundle", Position=13, Age=36, Podiums=0, DNFs=10 , CurrentReputation=DriverReputation.AGEING_STRONG_MIDFIELD, ExpectedReputation= DriverReputation.AGEING_MIDFIELD },
            new() { Year = 1995, Name="Heinz-Harald Frentzen", Position=14, Age=28, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_MIDFIELD },
            new() { Year = 1995, Name="Gianni Morbidelli", Position=15, Age=27, Podiums=1, DNFs=5 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PRIME_MIDFIELD },
            new() { Year = 1995, Name="Jean-Christophe Boullion", Position=16, Age=26, Podiums=0, DNFs=5 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1995, Name="Pedro Lamy", Position=17, Age=23, Podiums=0, DNFs=6 , CurrentReputation=DriverReputation.PAY_DRIVER_SEASON, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1995, Name="Pierluigi Martini", Position=18, Age=34, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.PAY_DRIVER_SEASON, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1995, Name="Jos Verstappen", Position=19, Age=23, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.YOUNG_TALENT, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1995, Name="Taki Inoue", Position=20, Age=32, Podiums=0, DNFs=10 , CurrentReputation=DriverReputation.PAY_DRIVER_SEASON, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
        };
        
        [TestMethod]
        [DynamicData(nameof(Drivers))]
        public void TestReputationUpdater(DriverRecord driver)
        {
            var reputationUpdater = new ReputationUpdater();

            var newReputation = reputationUpdater.GetNewReputation(
                driver.CurrentReputation,
                driver.Age,
                driver.Position,
                driver.Podiums,
                driver.DNFs,
                races: 3,
                totalSeasonRaces: 3,
                averageRacePosition: driver.Position
            );

            Assert.AreEqual(driver.ExpectedReputation, newReputation,
                $"{driver.Name} {driver.Year} Expected={driver.ExpectedReputation} Actual={newReputation}");
        }

        [TestMethod]
        [DataRow(DriverReputation.PRIME_STRONG_MIDFIELD, 3, DriverReputation.AGEING_CHAMPIONSHIP_LEVEL)]
        [DataRow(DriverReputation.AGEING_STRONG_MIDFIELD, 3, DriverReputation.AGEING_CHAMPIONSHIP_LEVEL)]
        [DataRow(DriverReputation.PRIME_STRONG_MIDFIELD, 8, DriverReputation.AGEING_STRONG_MIDFIELD)]
        [DataRow(DriverReputation.PRIME_STRONG_MIDFIELD, 16, DriverReputation.AGEING_MIDFIELD)]
        [DataRow(DriverReputation.PRIME_STRONG_MIDFIELD, 17, DriverReputation.PAY_DRIVER_SEASON)]
        public void GetNewReputation_StrongMidfielderTurningAgeing_IsJudgedAsAnAgeingOne(DriverReputation currentReputation, int standings, DriverReputation expected)
        {
            var newReputation = new ReputationUpdater().GetNewReputation(currentReputation, ReputationUpdater.OLD_DRIVER_AGE, standings, 0, 0, 3, 3, standings);

            Assert.AreEqual(expected, newReputation);
        }

        [TestMethod]
        [DataRow(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN)]
        [DataRow(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN)]
        public void GetNewReputation_UnprovenTurningAgeingOutsideTopEight_StaysAnAgeingReputation(DriverReputation currentReputation)
        {
            var newReputation = new ReputationUpdater().GetNewReputation(currentReputation, ReputationUpdater.OLD_DRIVER_AGE, 9, 0, 0, 3, 3, 9);

            Assert.AreEqual(DriverReputation.AGEING_MIDFIELD, newReputation);
            CollectionAssert.Contains(ReputationUpdater.AvailableReputationForAge(ReputationUpdater.OLD_DRIVER_AGE).ToList(), newReputation);
        }

        [TestMethod]
        public void GetNewReputation_PartTimerWhoNeverPreQualified_IsJudgedOnTheStandings()
        {
            // three pre-qualifying failures out of sixteen races: no race position to average
            var newReputation = new ReputationUpdater().GetNewReputation(DriverReputation.PAY_DRIVER_SEASON, 30, standings: 38, podiums: 0, dnfs: 0, races: 3, totalSeasonRaces: 16, averageRacePosition: 0);

            Assert.AreEqual(DriverReputation.PAY_DRIVER_WILD_CARD, newReputation);
        }

        // a clean season of five races, no DNFs: a wild card moves up its age band
        [TestMethod]
        [DataRow(22, 0, DriverReputation.YOUNG_TALENT)]
        [DataRow(22, 1, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN)]
        [DataRow(28, 0, DriverReputation.PRIME_MIDFIELD)]
        [DataRow(28, 1, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN)]
        [DataRow(35, 1, DriverReputation.AGEING_STRONG_MIDFIELD)]
        public void GetNewReputation_WildCardWithCleanSeason_MovesUp(int age, int podiums, DriverReputation expected)
        {
            var newReputation = new ReputationUpdater().GetNewReputation(DriverReputation.PAY_DRIVER_WILD_CARD, age, standings: 12, podiums, dnfs: 0, races: 5, totalSeasonRaces: 16, averageRacePosition: 8);

            Assert.AreEqual(expected, newReputation);
        }

        [TestMethod]
        public void GetNewReputation_WildCardWithADnf_StaysAWildCard()
        {
            var newReputation = new ReputationUpdater().GetNewReputation(DriverReputation.PAY_DRIVER_WILD_CARD, 22, standings: 12, podiums: 0, dnfs: 1, races: 5, totalSeasonRaces: 16, averageRacePosition: 8);

            Assert.AreEqual(DriverReputation.PAY_DRIVER_WILD_CARD, newReputation);
        }

        [TestMethod]
        public void GetNewReputation_PartTimer_IsJudgedOnTheirAverageRacePosition()
        {
            var newReputation = new ReputationUpdater().GetNewReputation(DriverReputation.PAY_DRIVER_SEASON, 30, standings: 25, podiums: 0, dnfs: 0, races: 3, totalSeasonRaces: 16, averageRacePosition: 9);

            Assert.AreEqual(DriverReputation.PRIME_STRONG_MIDFIELD, newReputation);
        }

        [TestMethod]
        [DataRow(DriverReputation.PAY_DRIVER_WILD_CARD, 28, DriverReputation.PAY_DRIVER_WILD_CARD)]
        [DataRow(DriverReputation.PAY_DRIVER_SEASON, 28, DriverReputation.PAY_DRIVER_WILD_CARD)]
        [DataRow(DriverReputation.YOUNG_TALENT, 20, DriverReputation.PAY_DRIVER_SEASON)]
        [DataRow(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, 20, DriverReputation.YOUNG_TALENT)]
        [DataRow(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 20, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN)]
        [DataRow(DriverReputation.PRIME_MIDFIELD, 28, DriverReputation.PAY_DRIVER_SEASON)]
        [DataRow(DriverReputation.PRIME_STRONG_MIDFIELD, 28, DriverReputation.PRIME_MIDFIELD)]
        [DataRow(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, 28, DriverReputation.PRIME_STRONG_MIDFIELD)]
        [DataRow(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, 28, DriverReputation.PRIME_STRONG_MIDFIELD)]
        [DataRow(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 28, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED)]
        [DataRow(DriverReputation.AGEING_MIDFIELD, 35, DriverReputation.PAY_DRIVER_SEASON)]
        [DataRow(DriverReputation.AGEING_STRONG_MIDFIELD, 35, DriverReputation.AGEING_MIDFIELD)]
        [DataRow(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED, 35, DriverReputation.AGEING_STRONG_MIDFIELD)]
        [DataRow(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, 35, DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED)]
        [DataRow(DriverReputation.JUST_ONE_LAST_DANCE, 42, DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED)]
        // crossing into an age band: remapped first, then one tier down the new band
        [DataRow(DriverReputation.PRIME_STRONG_MIDFIELD, ReputationUpdater.OLD_DRIVER_AGE, DriverReputation.AGEING_MIDFIELD)]
        public void GetNewReputationForInactiveDriver_Decaying_DropsOneTierInItsAgeBand(DriverReputation currentReputation, int age, DriverReputation expected)
        {
            var newReputation = new ReputationUpdater().GetNewReputationForInactiveDriver(currentReputation, age, decays: true);

            Assert.AreEqual(expected, newReputation);
            CollectionAssert.Contains(ReputationUpdater.AvailableReputationForAge(age).ToList(), newReputation);
        }

        [TestMethod]
        public void GetNewReputationForInactiveDriver_NotDecaying_OnlyMovesOntoTheAgeBand()
        {
            var updater = new ReputationUpdater();

            Assert.AreEqual(DriverReputation.PRIME_STRONG_MIDFIELD, updater.GetNewReputationForInactiveDriver(DriverReputation.PRIME_STRONG_MIDFIELD, 28, decays: false));
            Assert.AreEqual(DriverReputation.AGEING_STRONG_MIDFIELD, updater.GetNewReputationForInactiveDriver(DriverReputation.PRIME_STRONG_MIDFIELD, ReputationUpdater.OLD_DRIVER_AGE, decays: false));
        }
    }
}
