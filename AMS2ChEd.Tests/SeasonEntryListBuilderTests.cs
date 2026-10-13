using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class SeasonEntryListBuilderTests
    {
        [TestMethod]
        public void ForRace_NoAbsences_EveryTeamWithItsContractedDriversAndNumbers()
        {
            var season = CreateSeason();

            var entries = SeasonEntryListBuilder.ForRace(season, raceId: 1);

            Assert.AreEqual(2, entries.Count);
            var williams = entries.Single(e => e.TeamId == "williams");
            Assert.AreEqual("hill", williams.Driver1Id);
            Assert.AreEqual(5, williams.Driver1Number);
            Assert.AreEqual("villeneuve", williams.Driver2Id);
            Assert.AreEqual(6, williams.Driver2Number);
        }

        [TestMethod]
        public void ForRace_AbsenceForThatRace_SubstituteTakesTheCarAndNumber()
        {
            var season = CreateSeason();
            season.Absences = new List<Absence>
            {
                new Absence { RaceId = 2, TeamId = "williams", DriverOut = "villeneuve", DriverIn = "test_driver" }
            };

            var entries = SeasonEntryListBuilder.ForRace(season, raceId: 2);

            var williams = entries.Single(e => e.TeamId == "williams");
            Assert.AreEqual("test_driver", williams.Driver2Id);
            Assert.AreEqual(6, williams.Driver2Number);
            Assert.AreEqual("hill", williams.Driver1Id);
        }

        [TestMethod]
        public void ForRace_AbsenceForAnotherRace_IsIgnored()
        {
            var season = CreateSeason();
            season.Absences = new List<Absence>
            {
                new Absence { RaceId = 2, TeamId = "williams", DriverOut = "villeneuve", DriverIn = "test_driver" }
            };

            var entries = SeasonEntryListBuilder.ForRace(season, raceId: 1);

            Assert.AreEqual("villeneuve", entries.Single(e => e.TeamId == "williams").Driver2Id);
        }

        [TestMethod]
        public void ForRace_ChainedAbsence_SubstitutesOwnTeamIsFilledToo()
        {
            var season = CreateSeason();
            season.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 3, TeamId = "williams", DriverOut = "villeneuve", DriverIn = "panis",
                    ChainedAbsence = new Absence { RaceId = 3, TeamId = "ligier", DriverOut = "panis", DriverIn = "reserve" }
                }
            };

            var entries = SeasonEntryListBuilder.ForRace(season, raceId: 3);

            Assert.AreEqual("panis", entries.Single(e => e.TeamId == "williams").Driver2Id);
            Assert.AreEqual("reserve", entries.Single(e => e.TeamId == "ligier").Driver1Id);
        }

        [TestMethod]
        public void ForRace_AbsenceWithoutSubstitute_LeavesTheCarEmpty()
        {
            var season = CreateSeason();
            season.Absences = new List<Absence>
            {
                new Absence { RaceId = 2, TeamId = "ligier", DriverOut = "diniz", DriverIn = "" }
            };

            var entries = SeasonEntryListBuilder.ForRace(season, raceId: 2);

            Assert.AreEqual("", entries.Single(e => e.TeamId == "ligier").Driver2Id);
        }

        [TestMethod]
        public void ForRace_DoesNotChangeTheSeasonsOwnContracts()
        {
            var season = CreateSeason();
            season.Absences = new List<Absence>
            {
                new Absence { RaceId = 2, TeamId = "williams", DriverOut = "villeneuve", DriverIn = "test_driver" }
            };

            SeasonEntryListBuilder.ForRace(season, raceId: 2);

            Assert.AreEqual("villeneuve", season.Teams.Single(t => t.TeamId == "williams").Driver2Contract.DriverId);
        }

        private static Season CreateSeason() => new Season
        {
            Year = 1996,
            Races = new List<Race> { new Race { RaceId = 1 }, new Race { RaceId = 2 }, new Race { RaceId = 3 } },
            Teams = new List<ITeamEntry>
            {
                new TeamEntry
                {
                    TeamId = "williams",
                    Driver1Contract = new DriverContract { DriverId = "hill", DriverNumber = 5 },
                    Driver2Contract = new DriverContract { DriverId = "villeneuve", DriverNumber = 6 }
                },
                new TeamEntry
                {
                    TeamId = "ligier",
                    Driver1Contract = new DriverContract { DriverId = "panis", DriverNumber = 9 },
                    Driver2Contract = new DriverContract { DriverId = "diniz", DriverNumber = 10 }
                }
            },
            Absences = new List<Absence>()
        };
    }
}
