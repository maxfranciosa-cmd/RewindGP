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
            new() { Year = 1994, Name="Christian Fittipaldi", Position=15, Age=23, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.YOUNG_TALENT, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Eddie Irvine", Position=16, Age=28, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Ukyo Katayama", Position=17, Age=30, Podiums=0, DNFs=11 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Éric Bernard", Position=18, Age=30, Podiums=1, DNFs=6 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Karl Wendlinger", Position=19, Age=25, Podiums=0, DNFs=1 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Andrea de Cesaris", Position=20, Age=34, Podiums=0, DNFs=10 , CurrentReputation=DriverReputation.AGEING_MIDFIELD, ExpectedReputation= DriverReputation.AGEING_MIDFIELD },
            new() { Year = 1994, Name="Pierluigi Martini", Position=21, Age=33, Podiums=0, DNFs=6 , CurrentReputation=DriverReputation.AGEING_MIDFIELD, ExpectedReputation= DriverReputation.AGEING_MIDFIELD },
            new() { Year = 1994, Name="Gianni Morbidelli", Position=22, Age=26, Podiums=0, DNFs=8 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Érik Comas", Position=23, Age=30, Podiums=0, DNFs=7 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="JJ Lehto", Position=24, Age=28, Podiums=0, DNFs=7 , CurrentReputation=DriverReputation.PRIME_MIDFIELD, ExpectedReputation= DriverReputation.PAY_DRIVER_SEASON },
            new() { Year = 1994, Name="Michele Alboreto", Position=25, Age=37, Podiums=0, DNFs=9 , CurrentReputation=DriverReputation.AGEING_MIDFIELD, ExpectedReputation= DriverReputation.AGEING_MIDFIELD },


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
                3
            );

            Assert.AreEqual(driver.ExpectedReputation, newReputation,
                $"{driver.Name} {driver.Year} Expected={driver.ExpectedReputation} Actual={newReputation}");
        }
    }
}
