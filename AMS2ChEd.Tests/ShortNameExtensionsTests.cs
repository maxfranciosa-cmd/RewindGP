using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Tests.Business.Helpers
{
    [TestClass]
    public class ShortNameExtensionsTests
    {
        // ---- drivers: last name ----

        [TestMethod]
        [DataRow("Pedro De La Rosa", "De La Rosa")]
        [DataRow("Giedo Van Der Garde", "Van Der Garde")]
        [DataRow("JJ Lehto", "Lehto")]                      // "JJ" is short, but it's the first word
        [DataRow("Michael Schumacher", "Schumacher")]
        [DataRow("Jos Verstappen", "Verstappen")]
        [DataRow("Juan Pablo Montoya", "Montoya")]          // "Pablo" is longer than 3: not part of it
        [DataRow("Jos De La Rosa", "De La Rosa")]            // a short first word still isn't taken
        [DataRow("Zanardi", "Zanardi")]
        [DataRow("  Alain   Prost ", "Prost")]
        public void LastName_LastWordAndTheShortWordsBeforeIt(string fullName, string expected)
        {
            Assert.AreEqual(expected, ShortNameExtensions.LastName(fullName));
        }

        [TestMethod]
        [DataRow("Pedro De La Rosa", "Pedro", "De La Rosa")]
        [DataRow("Giedo Van Der Garde", "Giedo", "Van Der Garde")]
        [DataRow("JJ Lehto", "JJ", "Lehto")]
        [DataRow("Juan Pablo Montoya", "Juan Pablo", "Montoya")]
        [DataRow("Jos De La Rosa", "Jos", "De La Rosa")]
        [DataRow("Zanardi", "", "Zanardi")]
        [DataRow("  Alain   Prost ", "Alain", "Prost")]
        public void FirstNameAndSurname_SplitTheFullNameBetweenThem(string fullName, string firstName, string surname)
        {
            IDriverData driver = new DriverData { Name = fullName };

            Assert.AreEqual(firstName, driver.FirstName());
            Assert.AreEqual(surname, driver.Surname());
            Assert.AreEqual(firstName, ShortNameExtensions.FirstName(fullName));
        }

        [TestMethod]
        public void FirstNameAndSurname_NoName_ReturnTheNameAsIs()
        {
            IDriverData driver = new DriverData { Name = null };

            Assert.IsNull(driver.FirstName());
            Assert.IsNull(driver.Surname());
            Assert.AreEqual("", new DriverData { Name = "" }.Surname());
        }

        // ---- drivers: telling namesakes apart ----

        [TestMethod]
        public void ShortDriverName_Alone_IsTheLastName()
        {
            var names = new[] { "Michael Schumacher", "Damon Hill" };

            Assert.AreEqual("Schumacher", ShortNameExtensions.ShortDriverName("Michael Schumacher", names));
        }

        [TestMethod]
        public void ShortDriverName_TwoNamesakes_FirstInitialEach()
        {
            var names = new[] { "Michael Schumacher", "Ralf Schumacher" };

            Assert.AreEqual("M Schumacher", ShortNameExtensions.ShortDriverName("Michael Schumacher", names));
            Assert.AreEqual("R Schumacher", ShortNameExtensions.ShortDriverName("Ralf Schumacher", names));
        }

        [TestMethod]
        public void ShortDriverName_FirstNamesSharingTheirStart_AsManyLettersAsNeeded()
        {
            var names = new[] { "Michael Schumacher", "Ralf Schumacher", "Mick Schumacher" };

            Assert.AreEqual("Mich Schumacher", ShortNameExtensions.ShortDriverName("Michael Schumacher", names));
            Assert.AreEqual("Mick Schumacher", ShortNameExtensions.ShortDriverName("Mick Schumacher", names));
            Assert.AreEqual("R Schumacher", ShortNameExtensions.ShortDriverName("Ralf Schumacher", names));
        }

        [TestMethod]
        public void ShortDriverName_NamesakesWithParticles_CompareTheWholeLastName()
        {
            var names = new[] { "Pedro De La Rosa", "Maria De La Rosa", "Rosa Bianchi" };

            Assert.AreEqual("P De La Rosa", ShortNameExtensions.ShortDriverName("Pedro De La Rosa", names));
            Assert.AreEqual("Bianchi", ShortNameExtensions.ShortDriverName("Rosa Bianchi", names));
        }

        [TestMethod]
        public void ShortDriverName_SameFullNameTwice_GivesTheWholeFirstNameAndDoesNotFail()
        {
            var names = new[] { "Michael Schumacher", "Michael Schumacher", "Ralf Schumacher" };

            Assert.AreEqual("M Schumacher", ShortNameExtensions.ShortDriverName("Michael Schumacher", names));
        }

        [TestMethod]
        public void ShortDriverName_CaseAndSpacingDoNotMatter()
        {
            var names = new[] { "michael  schumacher", "Ralf Schumacher" };

            Assert.AreEqual("M Schumacher", ShortNameExtensions.ShortDriverName("Michael Schumacher", names));
        }

        [TestMethod]
        public void ShortDriverNames_FromTheSave_CoverActiveRetiredAndThePlayer()
        {
            var saveGame = new SaveGame
            {
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "michael_schumacher", Name = "Michael Schumacher" },
                    new DriverData { DriverId = "damon_hill", Name = "Damon Hill" }
                },
                RetiredDrivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "graham_hill", Name = "Graham Hill" }
                },
                PlayerData = new PlayerData { DriverId = "player", Name = "Mazzimo Schumacher" }
            };

            var shortNames = saveGame.ShortDriverNames();

            Assert.AreEqual("Mi Schumacher", shortNames["michael_schumacher"]);
            Assert.AreEqual("Ma Schumacher", shortNames["player"]);
            Assert.AreEqual("D Hill", shortNames["damon_hill"]);
            Assert.AreEqual("G Hill", shortNames["graham_hill"]);
            Assert.AreEqual("D Hill", saveGame.ShortDriverName("damon_hill"));
            Assert.AreEqual("unknown_id", saveGame.ShortDriverName("unknown_id"));
        }

        // ---- teams ----

        [TestMethod]
        [DataRow("Williams Renault", "williams", "Williams")]
        [DataRow("Mild Seven Benetton Renault", "benetton", "Benetton")]
        [DataRow("Scuderia Ferrari Marlboro", "ferrari", "Ferrari")]
        [DataRow("Red Bull Sauber Petronas", "sauber", "Sauber")]
        [DataRow("Red Bull Racing Renault", "red_bull", "Red Bull")]
        [DataRow("Ligier Mugen-Honda", "ligier_prost", "Ligier")]
        [DataRow("B&H Jordan Peugeot", "jordan", "Jordan")]
        [DataRow("Willis Rellaux", "williams", "Willis")]          // no word matches the id: drop the engine
        [DataRow("Lumiere Buzen-Hotta", "ligier_prost", "Lumiere")]
        [DataRow("Ferroni", "ferrari", "Ferroni")]               // one word stays whole
        [DataRow("McLaren Mercedes", "mclaren", "McLaren")]
        public void ShortTeamName_FromNameAndId(string teamName, string teamId, string expected)
        {
            Assert.AreEqual(expected, ShortNameExtensions.ShortTeamName(teamName, teamId));
        }

        [TestMethod]
        public void ShortTeamName_PackShortNameWins()
        {
            ITeamEntry team = new TeamEntry { TeamId = "arrows", TeamName = "Footwork Hart", ShortName = "Footwork Arrows" };

            Assert.AreEqual("Footwork Arrows", team.ShortTeamName());
        }

        [TestMethod]
        public void ShortTeamName_NoPackShortName_FallsBackToTheRule()
        {
            ITeamEntry team = new TeamEntry { TeamId = "benetton", TeamName = "Mild Seven Benetton Renault" };

            Assert.AreEqual("Benetton", team.ShortTeamName());
        }
    }
}
