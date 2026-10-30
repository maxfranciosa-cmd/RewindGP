using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class ReprimandManagerTests
    {
        private ReprimandManager _reprimandManager;

        [TestInitialize]
        public void Setup()
        {
            _reprimandManager = new ReprimandManager();
        }

        #region Finishing rule

        [TestMethod]
        public void ProcessRace_SecondDriverFinishesRightAheadOfFirstDriver_IsReprimanded()
        {
            var saveGame = CreateSaveGame();
            var result = CreateResult(("D2", 3), ("D1", 4));

            var news = Process(saveGame, result);

            Assert.AreEqual(1, saveGame.Reprimands.Count);
            var reprimand = saveGame.Reprimands.Single();
            Assert.AreEqual("D2", reprimand.DriverId);
            Assert.AreEqual("T1", reprimand.TeamId);
            Assert.AreEqual(1, reprimand.RaceId);
            Assert.AreEqual(ReprimandReason.FINISHED_AHEAD_OF_FIRST_DRIVER, reprimand.Reason);

            Assert.AreEqual(1, news.Count);
            Assert.AreEqual("D1", news[0].TeammateId);
            Assert.AreEqual(1, news[0].ReprimandCount);
            Assert.AreEqual(ReprimandConsequence.NONE, news[0].Consequence);
        }

        [TestMethod]
        public void ProcessRace_SecondDriverTwoPlacesAheadOfFirstDriver_IsNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            var result = CreateResult(("D2", 3), ("D3", 4), ("D1", 5));

            var news = Process(saveGame, result);

            Assert.AreEqual(0, saveGame.Reprimands.Count);
            Assert.AreEqual(0, news.Count);
        }

        [TestMethod]
        public void ProcessRace_SecondDriverBehindFirstDriver_IsNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            var result = CreateResult(("D1", 3), ("D2", 4));

            Process(saveGame, result);

            Assert.AreEqual(0, saveGame.Reprimands.Count);
        }

        [TestMethod]
        public void ProcessRace_EqualStatusTeammates_AreNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            var result = CreateResult(("D4", 3), ("D3", 4));

            Process(saveGame, result);

            Assert.AreEqual(0, saveGame.Reprimands.Count);
        }

        [TestMethod]
        public void ProcessRace_RolesInReversedSlots_ReprimandsTheSecondDriver()
        {
            var saveGame = CreateSaveGame();
            var team = saveGame.CurrentSeason.Teams.First(t => t.TeamId == "T1");
            team.Driver1Contract.Role = ContractRole.SECOND_DRIVER;
            team.Driver2Contract.Role = ContractRole.FIRST_DRIVER;
            var result = CreateResult(("D1", 3), ("D2", 4));

            Process(saveGame, result);

            Assert.AreEqual("D1", saveGame.Reprimands.Single().DriverId);
        }

        [TestMethod]
        public void ProcessRace_SecondDriverDnf_IsNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            var result = CreateResult(("D2", 3), ("D1", 4));
            result.RaceResults.First(r => r.DriverId == "D2").DNF = true;

            Process(saveGame, result);

            Assert.AreEqual(0, saveGame.Reprimands.Count);
        }

        [TestMethod]
        public void ProcessRace_FirstDriverDidNotPreQualify_SecondDriverIsNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            var result = CreateResult(("D2", 3));
            result.RaceResults.Add(new SessionResult { DriverId = "D1", TeamId = "T1", Position = 0, DidNotPreQualify = true });

            Process(saveGame, result);

            Assert.AreEqual(0, saveGame.Reprimands.Count);
        }

        [TestMethod]
        public void ProcessRace_SecondHalfAndSecondDriverAheadInStandings_IsExempt()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 2; // third race of four
            var preRaceStandings = Standings(("D2", 1), ("D1", 2));
            var result = CreateResult(("D2", 3), ("D1", 4));

            Process(saveGame, result, preRaceStandings);

            Assert.AreEqual(0, saveGame.Reprimands.Count);
        }

        [TestMethod]
        public void ProcessRace_SecondHalfAndSecondDriverBehindInStandings_IsReprimanded()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 2;
            var preRaceStandings = Standings(("D1", 1), ("D2", 2));
            var result = CreateResult(("D2", 3), ("D1", 4));

            Process(saveGame, result, preRaceStandings);

            Assert.AreEqual(1, saveGame.Reprimands.Count);
            Assert.AreEqual(3, saveGame.Reprimands.Single().RaceId);
        }

        [TestMethod]
        public void ProcessRace_FirstHalfEvenIfSecondDriverAheadInStandings_IsReprimanded()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 1;
            var preRaceStandings = Standings(("D2", 1), ("D1", 2));
            var result = CreateResult(("D2", 3), ("D1", 4));

            Process(saveGame, result, preRaceStandings);

            Assert.AreEqual(1, saveGame.Reprimands.Count);
        }

        #endregion

        #region Contact rule

        [TestMethod]
        public void ProcessRace_PlayerContactWithTeammate_ReprimandsThePlayer()
        {
            var saveGame = CreateSaveGame();
            var result = CreateResult(("PLAYER", 5), ("D5", 6));

            var news = Process(saveGame, result, contactDriverIds: new[] { "D5" });

            var reprimand = saveGame.Reprimands.Single();
            Assert.AreEqual("PLAYER", reprimand.DriverId);
            Assert.AreEqual("T3", reprimand.TeamId);
            Assert.AreEqual(ReprimandReason.CONTACT_WITH_TEAMMATE, reprimand.Reason);
            Assert.IsTrue(news.Single().IsPlayer);
        }

        [TestMethod]
        public void ProcessRace_PlayerContactWithNonTeammate_IsNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            var result = CreateResult(("PLAYER", 5), ("D5", 6));

            Process(saveGame, result, contactDriverIds: new[] { "D1" });

            Assert.AreEqual(0, saveGame.Reprimands.Count);
        }

        #endregion

        #region Consequences

        [TestMethod]
        public void ProcessRace_SecondReprimand_IsAFinalWarning()
        {
            var saveGame = CreateSaveGame();
            AddPreviousReprimands(saveGame, "D2", "T1", 1);
            var result = CreateResult(("D2", 3), ("D1", 4));

            var news = Process(saveGame, result);

            Assert.AreEqual(2, news.Single().ReprimandCount);
            Assert.AreEqual(ReprimandConsequence.FINAL_WARNING, news.Single().Consequence);
            Assert.AreEqual(0, saveGame.CurrentSeason.Absences.Count());
        }

        [TestMethod]
        public void ProcessRace_ThirdReprimand_ReleasesDriverForTheRemainingRaces()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 1; // races 3 and 4 remain after this one
            AddPreviousReprimands(saveGame, "D2", "T1", 2);
            var result = CreateResult(("D2", 3), ("D1", 4));

            var news = Process(saveGame, result);

            var item = news.Single();
            Assert.AreEqual(ReprimandConsequence.RELEASED, item.Consequence);
            // U1 is the best unemployed driver within what a top team takes as a stand-in
            Assert.AreEqual("U1", item.ReplacementDriverId);

            var absences = saveGame.CurrentSeason.Absences.ToList();
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, absences.Select(a => a.RaceId).ToList());
            Assert.IsTrue(absences.All(a => a.DriverOut == "D2" && a.DriverIn == "U1" && a.TeamId == "T1"));
        }

        [TestMethod]
        public void ProcessRace_ThirdReprimand_NobodyFree_GeneratedDriverReplacesForTheRemainingRaces()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 1;
            AddPreviousReprimands(saveGame, "D2", "T1", 2);
            // every unemployed driver already stands in somewhere in the rest of the season
            saveGame.CurrentSeason.Absences = new List<Absence>
            {
                new Absence { RaceId = 3, TeamId = "T2", DriverOut = "D3", DriverIn = "U1" },
                new Absence { RaceId = 3, TeamId = "T2", DriverOut = "D4", DriverIn = "U2" },
                new Absence { RaceId = 4, TeamId = "T3", DriverOut = "D5", DriverIn = "U3" }
            };
            var generator = new Moq.Mock<AMS2ChEd.Business.Services.Contracts.IRandomDriverGenerator>();
            generator.Setup(g => g.GenerateDriver(Moq.It.IsAny<IEnumerable<IDriverData>>(), 1996))
                .Returns(Driver("GEN1", DriverReputation.PAY_DRIVER_SEASON));
            _reprimandManager = new ReprimandManager(generator.Object);

            var news = Process(saveGame, CreateResult(("D2", 3), ("D1", 4)));

            Assert.AreEqual("GEN1", news.Single().ReplacementDriverId);
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "GEN1"));
            Assert.IsTrue(saveGame.CurrentSeason.Absences.Where(a => a.DriverOut == "D2").All(a => a.DriverIn == "GEN1"));
        }

        [TestMethod]
        public void ProcessRace_ThirdReprimand_DoesNotDuplicateExistingAbsencesNorReuseBusyDrivers()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 1;
            AddPreviousReprimands(saveGame, "D2", "T1", 2);
            saveGame.CurrentSeason.Absences = new List<Absence>
            {
                // D2 was already due to miss race 3
                new Absence { RaceId = 3, TeamId = "T1", DriverOut = "D2", DriverIn = "U2" },
                // U1 already stands in for someone at race 4
                new Absence { RaceId = 4, TeamId = "T2", DriverOut = "D3", DriverIn = "U1" }
            };
            var result = CreateResult(("D2", 3), ("D1", 4));

            var news = Process(saveGame, result);

            Assert.AreEqual("U3", news.Single().ReplacementDriverId);
            var absences = saveGame.CurrentSeason.Absences.ToList();
            Assert.AreEqual(3, absences.Count);
            Assert.AreEqual(1, absences.Count(a => a.RaceId == 3 && a.DriverOut == "D2"));
            Assert.IsTrue(absences.Any(a => a.RaceId == 4 && a.DriverOut == "D2" && a.DriverIn == "U3"));
        }

        [TestMethod]
        public void ProcessRace_ThirdReprimandWhileLeadingTheChampionship_IsSpared()
        {
            var saveGame = CreateSaveGame();
            AddPreviousReprimands(saveGame, "D2", "T1", 2);
            saveGame.CurrentDriverStandings = Standings(("D2", 1), ("D1", 2));
            var result = CreateResult(("D2", 3), ("D1", 4));

            var news = Process(saveGame, result);

            Assert.AreEqual(ReprimandConsequence.SPARED_AS_LEADER, news.Single().Consequence);
            Assert.AreEqual(0, saveGame.CurrentSeason.Absences.Count());
        }

        [TestMethod]
        public void ProcessRace_FourthReprimandAfterLosingTheLead_ReleasesDriver()
        {
            var saveGame = CreateSaveGame();
            AddPreviousReprimands(saveGame, "D2", "T1", 3);
            saveGame.CurrentDriverStandings = Standings(("D1", 1), ("D2", 2));
            var result = CreateResult(("D2", 3), ("D1", 4));

            var news = Process(saveGame, result);

            Assert.AreEqual(4, news.Single().ReprimandCount);
            Assert.AreEqual(ReprimandConsequence.RELEASED, news.Single().Consequence);
            Assert.IsTrue(saveGame.CurrentSeason.Absences.Any(a => a.DriverOut == "D2"));
        }

        [TestMethod]
        public void ProcessRace_ReprimandsWithAnotherTeam_DoNotCount()
        {
            var saveGame = CreateSaveGame();
            AddPreviousReprimands(saveGame, "D2", "T9", 2);
            var result = CreateResult(("D2", 3), ("D1", 4));

            var news = Process(saveGame, result);

            Assert.AreEqual(1, news.Single().ReprimandCount);
            Assert.AreEqual(ReprimandConsequence.NONE, news.Single().Consequence);
        }

        #endregion

        #region Substitutes

        [TestMethod]
        public void ProcessRace_SubstituteInSecondDriverSeatFinishesRightAheadOfFirstDriver_IsNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpEntryList.First(e => e.TeamId == "T1").Driver2Id = "U1";
            var result = CreateResult(("U1", 3), ("D1", 4));

            var news = Process(saveGame, result);

            Assert.AreEqual(0, saveGame.Reprimands.Count);
            Assert.AreEqual(0, news.Count);
        }

        [TestMethod]
        public void ProcessRace_SecondDriverFinishesRightAheadOfSubstituteInFirstDriverSeat_IsNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpEntryList.First(e => e.TeamId == "T1").Driver1Id = "U1";
            var result = CreateResult(("D2", 3), ("U1", 4));

            var news = Process(saveGame, result);

            Assert.AreEqual(0, saveGame.Reprimands.Count);
            Assert.AreEqual(0, news.Count);
        }

        [TestMethod]
        public void ProcessRace_PlayerSubstituteFinishesRightAheadOfFirstDriverAfterContact_IsNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpEntryList.First(e => e.TeamId == "T3").Driver1Id = "U2";
            saveGame.NextGpEntryList.First(e => e.TeamId == "T1").Driver2Id = "PLAYER";
            var result = CreateResult(("PLAYER", 3), ("D1", 4));

            var news = Process(saveGame, result, contactDriverIds: new List<string> { "D1" });

            Assert.AreEqual(0, saveGame.Reprimands.Count);
            Assert.AreEqual(0, news.Count);
        }

        #endregion

        [TestMethod]
        public void DriverContract_WithoutRoleInJson_DeserializesAsUndefined()
        {
            var contract = JsonSerializer.Deserialize<DriverContract>("{\"driver_id\":\"D1\",\"races\":16,\"drivernumber\":5}");

            Assert.AreEqual(ContractRole.UNDEFINED, contract.Role);
        }

        #region Reserves and simulated contacts

        [TestMethod]
        public void ProcessRace_ThirdReprimand_TheTeamsReserveIsTheReplacement()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 1;
            // a weaker driver than U1, but the team's own reserve
            Team(saveGame, "T1").ReserveContract = new DriverContract { DriverId = "U3", Races = 4 };
            AddPreviousReprimands(saveGame, "D2", "T1", 2);

            var news = Process(saveGame, CreateResult(("D2", 3), ("D1", 4)));

            Assert.AreEqual("U3", news.Single().ReplacementDriverId);
            Assert.IsTrue(saveGame.CurrentSeason.Absences.All(a => a.DriverIn == "U3"));
        }

        [TestMethod]
        public void ProcessRace_ThirdReprimand_AnotherTeamsReserveIsNeverTheReplacement()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 1;
            Team(saveGame, "T2").ReserveContract = new DriverContract { DriverId = "U1", Races = 4 };
            AddPreviousReprimands(saveGame, "D2", "T1", 2);

            var news = Process(saveGame, CreateResult(("D2", 3), ("D1", 4)));

            Assert.AreEqual("U2", news.Single().ReplacementDriverId);
        }

        [TestMethod]
        public void ProcessRace_ThirdReprimand_PlayerAsTheReserve_StandsInForTheRestOfTheSeason()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 1;
            // the player is T1's reserve rather than racing for T3
            Team(saveGame, "T3").Driver1Contract.DriverId = "D6";
            saveGame.Drivers = saveGame.Drivers.Append(Driver("D6", DriverReputation.PRIME_MIDFIELD)).ToList();
            saveGame.NextGpEntryList.Single(e => e.TeamId == "T3").Driver1Id = "D6";
            saveGame.PlayerData.TeamId = null;
            Team(saveGame, "T1").ReserveContract = new DriverContract { DriverId = "PLAYER", Races = 4 };
            AddPreviousReprimands(saveGame, "D2", "T1", 2);

            var news = Process(saveGame, CreateResult(("D2", 3), ("D1", 4)));

            Assert.AreEqual("PLAYER", news.Single().ReplacementDriverId);
        }

        [TestMethod]
        public void ProcessRace_SimulatedTeammateContact_ReprimandsTheDriverAtFault()
        {
            var saveGame = CreateSaveGame();
            var contacts = new List<TeammateContact> { new TeammateContact { AtFaultDriverId = "D3", OtherDriverId = "D4" } };

            var news = _reprimandManager.ProcessRace(saveGame, CreateResult(("D1", 1), ("D2", 2)), saveGame.CurrentDriverStandings.ToList(), new List<string>(), contacts);

            var item = news.Single();
            Assert.AreEqual("D3", item.DriverId);
            Assert.AreEqual("D4", item.TeammateId);
            Assert.AreEqual(ReprimandReason.CONTACT_WITH_TEAMMATE, item.Reason);
            Assert.AreEqual(1, saveGame.Reprimands.Count(r => r.DriverId == "D3" && r.TeamId == "T2"));
        }

        [TestMethod]
        public void ProcessRace_SimulatedTeammateContactBySubstitute_IsNotReprimanded()
        {
            var saveGame = CreateSaveGame();
            // U2 stands in for D3 this race
            saveGame.NextGpEntryList.Single(e => e.TeamId == "T2").Driver1Id = "U2";
            var contacts = new List<TeammateContact> { new TeammateContact { AtFaultDriverId = "U2", OtherDriverId = "D4" } };

            var news = _reprimandManager.ProcessRace(saveGame, CreateResult(("D1", 1), ("D2", 2)), saveGame.CurrentDriverStandings.ToList(), new List<string>(), contacts);

            Assert.AreEqual(0, news.Count);
        }

        private static ITeamEntry Team(ISaveGame saveGame, string teamId) => saveGame.CurrentSeason.Teams.Single(t => t.TeamId == teamId);

        #endregion

        #region Helpers

        private IReadOnlyList<ReprimandNewsItem> Process(
            ISaveGame saveGame,
            GrandPrixResult result,
            List<HistoricalDriverStandingEntry> preRaceStandings = null,
            IReadOnlyCollection<string> contactDriverIds = null)
        {
            return _reprimandManager.ProcessRace(
                saveGame,
                result,
                preRaceStandings ?? saveGame.CurrentDriverStandings.ToList(),
                contactDriverIds ?? new List<string>());
        }

        // T1: D1 (FIRST) + D2 (SECOND), T2: D3 + D4 (EQUAL), T3: PLAYER + D5 (EQUAL).
        // U1-U3 are unemployed. Four races, so the second half starts at index 2.
        private ISaveGame CreateSaveGame()
        {
            var teams = new List<ITeamEntry>
            {
                CreateTeam("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                CreateTeam("T2", TeamReputation.MIDFIELD, "D3", ContractRole.EQUAL, "D4", ContractRole.EQUAL),
                CreateTeam("T3", TeamReputation.MINNOW, "PLAYER", ContractRole.EQUAL, "D5", ContractRole.EQUAL),
            };

            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = 1996,
                    Teams = teams,
                    Races = Enumerable.Range(1, 4).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList(),
                    Absences = new List<Absence>()
                },
                Drivers = new List<IDriverData>
                {
                    Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                    Driver("D2", DriverReputation.PRIME_STRONG_MIDFIELD),
                    Driver("D3", DriverReputation.PRIME_MIDFIELD),
                    Driver("D4", DriverReputation.PRIME_MIDFIELD),
                    Driver("D5", DriverReputation.YOUNG_TALENT),
                    Driver("PLAYER", DriverReputation.YOUNG_TALENT),
                    // top teams take stand-ins up to YOUNG_CHAMPIONSHIP_LEVEL
                    Driver("U1", DriverReputation.PRIME_STRONG_MIDFIELD),
                    Driver("U2", DriverReputation.PRIME_MIDFIELD),
                    Driver("U3", DriverReputation.AGEING_MIDFIELD),
                },
                RetiredDrivers = new List<IDriverData>(),
                NextGpIndex = 0,
                NextGpEntryList = teams.Select(t => new EntryListEntry
                {
                    TeamId = t.TeamId,
                    Driver1Id = t.Driver1Contract.DriverId,
                    Driver2Id = t.Driver2Contract.DriverId
                }).ToList(),
                CurrentDriverStandings = Standings(("D1", 1), ("D2", 2), ("D3", 3), ("D4", 4), ("PLAYER", 5), ("D5", 6)),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = "T3" },
                Reprimands = new List<Reprimand>()
            };
        }

        private static ITeamEntry CreateTeam(string teamId, TeamReputation reputation, string driver1Id, ContractRole driver1Role, string driver2Id, ContractRole driver2Role)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                TeamName = $"Team {teamId}",
                Reputation = reputation,
                Driver1Contract = new DriverContract { DriverId = driver1Id, Races = 20, Role = driver1Role },
                Driver2Contract = new DriverContract { DriverId = driver2Id, Races = 20, Role = driver2Role }
            };
        }

        private static IDriverData Driver(string id, DriverReputation reputation)
        {
            return new DriverData { DriverId = id, Name = $"Driver {id}", YearOfBirth = 1970, Reputation = reputation };
        }

        private static GrandPrixResult CreateResult(params (string DriverId, int Position)[] results)
        {
            return new GrandPrixResult
            {
                Year = 1996,
                GrandPrixName = "Test GP",
                RaceResults = results.Select(r => new SessionResult { DriverId = r.DriverId, Position = r.Position }).ToList()
            };
        }

        private static List<HistoricalDriverStandingEntry> Standings(params (string DriverId, int Position)[] standings)
        {
            return standings.Select(s => new HistoricalDriverStandingEntry { DriverId = s.DriverId, Position = s.Position }).ToList();
        }

        private static void AddPreviousReprimands(ISaveGame saveGame, string driverId, string teamId, int count)
        {
            for (var i = 0; i < count; i++)
            {
                saveGame.Reprimands.Add(new Reprimand { DriverId = driverId, TeamId = teamId, RaceId = 0, Reason = ReprimandReason.FINISHED_AHEAD_OF_FIRST_DRIVER });
            }
        }

        #endregion
    }
}
