using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class ReserveDriversTests
    {
        private EndOfSeasonManager _endOfSeasonManager;
        private OffSeasonMovements _offSeasonMovements;
        private DriverHirer _driverHirer;

        [TestInitialize]
        public void Setup()
        {
            _driverHirer = new DriverHirer();
            _offSeasonMovements = new OffSeasonMovements(new DriverFirer(), _driverHirer);
            _endOfSeasonManager = new EndOfSeasonManager(new ReputationUpdater(), _offSeasonMovements, new Mock<IRandomDriverGenerator>().Object, _driverHirer);
        }

        #region Data model

        [TestMethod]
        public void Json_TeamAndAbsenceFromBeforeReserves_LoadWithNoReserveAndNoReason()
        {
            var team = JsonSerializer.Deserialize<TeamEntry>(
                "{ \"team_id\": \"T1\", \"driver1_contract\": { \"driver_id\": \"D1\", \"races\": 16 } }",
                DefaultJsonSerializerOptions.Instance);
            var absence = JsonSerializer.Deserialize<Absence>(
                "{ \"race_id\": 1, \"teamid\": \"T1\", \"driver_out\": \"D1\", \"driver_in\": \"U1\" }",
                DefaultJsonSerializerOptions.Instance);

            Assert.IsNull(team.ReserveContract);
            Assert.IsNull(team.ReserveDriverId());
            Assert.IsNull(absence.Reason);
        }

        [TestMethod]
        public void Json_ReserveContractAndPaidRide_RoundTrip()
        {
            var season = new Season
            {
                Year = 1996,
                Teams = new List<ITeamEntry> { Team("T1", TeamReputation.TOP_TEAM, "D1", "D2", reserveId: "R1") },
                Races = new List<Race>(),
                Absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "P", Reason = AbsenceReason.PAID_RIDE } }
            };

            var json = JsonSerializer.Serialize<ISeason>(season, DefaultJsonSerializerOptions.Instance);
            var loaded = JsonSerializer.Deserialize<ISeason>(json, DefaultJsonSerializerOptions.Instance);

            StringAssert.Contains(json, "\"reserve_contract\"");
            StringAssert.Contains(json, "\"PAID_RIDE\"");
            Assert.AreEqual("R1", loaded.Teams.Single().ReserveDriverId());
            Assert.AreEqual(AbsenceReason.PAID_RIDE, loaded.Absences.Single().Reason);
        }

        [TestMethod]
        public void SubstitutePool_ExcludesRaceDriversAndEveryReserve()
        {
            var season = new Season
            {
                Teams = new List<ITeamEntry>
                {
                    Team("T1", TeamReputation.TOP_TEAM, "D1", "D2", reserveId: "R1"),
                    Team("T2", TeamReputation.MINNOW, "D3", "")
                }
            };

            CollectionAssert.AreEquivalent(new[] { "D1", "D2", "D3", "R1" }, SubstitutePool.UnavailableDriverIds(season).ToList());
            Assert.AreEqual("T1", season.ReserveTeamOf("R1").TeamId);
            Assert.IsNull(season.ReserveTeamOf("D1"));
        }

        #endregion

        #region Reserve policy and wishes

        [TestMethod]
        [DataRow(DriverReputation.PRIME_MIDFIELD, TeamReputation.TOP_TEAM, DriverHirer.DriverPolicyFit.PerfectFit)]
        [DataRow(DriverReputation.YOUNG_TALENT, TeamReputation.TOP_TEAM, DriverHirer.DriverPolicyFit.GoodFit)]
        [DataRow(DriverReputation.PAY_DRIVER_SEASON, TeamReputation.TOP_TEAM, DriverHirer.DriverPolicyFit.UnderQualified)]
        [DataRow(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, TeamReputation.TOP_TEAM, DriverHirer.DriverPolicyFit.OverQualified)]
        [DataRow(DriverReputation.PAY_DRIVER_SEASON, TeamReputation.MIDFIELD, DriverHirer.DriverPolicyFit.PerfectFit)]
        public void DoesDriverFitReservePolicy_IsMoreLenientThanARaceSeat(DriverReputation driver, TeamReputation team, DriverHirer.DriverPolicyFit expected)
        {
            Assert.AreEqual(expected, _driverHirer.DoesDriverFitReservePolicy(driver, team));
        }

        [TestMethod]
        public void ReserveChance_TopTeamsMostLikely_MinnowsNever()
        {
            Assert.IsTrue(EndOfSeasonManager.ReserveChance(TeamReputation.TOP_TEAM) > EndOfSeasonManager.ReserveChance(TeamReputation.MIDFIELD_HIGH));
            Assert.IsTrue(EndOfSeasonManager.ReserveChance(TeamReputation.MIDFIELD_HIGH) > EndOfSeasonManager.ReserveChance(TeamReputation.MIDFIELD));
            Assert.AreEqual(0, EndOfSeasonManager.ReserveChance(TeamReputation.MINNOW));
            Assert.AreEqual(0, EndOfSeasonManager.ReserveChance(TeamReputation.SUPER_MINNOW));

            var wishes = EndOfSeasonManager.RollReserveWishes(new[]
            {
                Team("M", TeamReputation.MINNOW, "D1", "D2"),
                Team("S", TeamReputation.SUPER_MINNOW, "D3", "D4")
            }, new System.Random(1));
            Assert.AreEqual(0, wishes.Count);
        }

        [TestMethod]
        public void EveryReputation_HasAReserveAmbition()
        {
            foreach (var reputation in System.Enum.GetValues<DriverReputation>())
            {
                Assert.IsNotNull(OffSeasonMovements.AmbitionFor(reputation, isReserve: true), reputation.ToString());
            }
        }

        #endregion

        #region Season-start override of pack absences

        [TestMethod]
        public void GenerateNewSeason_PackAbsenceGoesToTheTeamsReserve_AndTheChainIsDropped()
        {
            var saveGame = OffSeasonSave();
            var nextSeason = NextSeason(saveGame);
            nextSeason.Absences = new List<Absence>
            {
                // the pack's substitute left their own team to stand in
                new Absence
                {
                    RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "D3",
                    ChainedAbsence = new Absence { RaceId = 1, TeamId = "T2", DriverOut = "D3", DriverIn = "U2" }
                }
            };

            var newSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, nextSeason, new[] { ReserveBallot("T1", "R1") });

            Assert.AreEqual("R1", newSeason.Teams.Single(t => t.TeamId == "T1").ReserveContract.DriverId);
            var absence = newSeason.Absences.Single();
            Assert.AreEqual("R1", absence.DriverIn);
            Assert.IsNull(absence.ChainedAbsence);
        }

        [TestMethod]
        public void GenerateNewSeason_SecondAbsenceAtTheSameRace_GoesThroughTheSubstitutePick()
        {
            var saveGame = OffSeasonSave();
            var nextSeason = NextSeason(saveGame);
            nextSeason.Absences = new List<Absence>
            {
                new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "U1" },
                new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D2", DriverIn = "U2" }
            };

            var newSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, nextSeason, new[] { ReserveBallot("T1", "R1") });

            var absences = newSeason.Absences.ToList();
            Assert.AreEqual("R1", absences.Single(a => a.DriverOut == "D1").DriverIn);
            Assert.AreEqual("U2", absences.Single(a => a.DriverOut == "D2").DriverIn);
        }

        [TestMethod]
        public void GenerateNewSeason_AnotherTeamsReserve_IsNotPickedAsSubstitute()
        {
            var saveGame = OffSeasonSave();
            var nextSeason = NextSeason(saveGame);
            // the pack has T2 call up a driver who is now T1's reserve
            nextSeason.Absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T2", DriverOut = "D3", DriverIn = "R1" } };

            var newSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, nextSeason, new[] { ReserveBallot("T1", "R1") });

            var absence = newSeason.Absences.Single();
            Assert.AreNotEqual("R1", absence.DriverIn);
            Assert.IsTrue(new[] { "U1", "U2" }.Contains(absence.DriverIn));
        }

        [TestMethod]
        public void GenerateNewSeason_NoReserve_PackAbsenceUnchanged()
        {
            var saveGame = OffSeasonSave();
            var nextSeason = NextSeason(saveGame);
            nextSeason.Absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "U1" } };

            var newSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, nextSeason, new List<TeamHiringBallot>());

            Assert.IsNull(newSeason.Teams.Single(t => t.TeamId == "T1").ReserveContract);
            Assert.AreEqual("U1", newSeason.Absences.Single().DriverIn);
        }

        [TestMethod]
        public void GenerateNewSeason_PacksReserveWithoutAHiring_IsNotKept()
        {
            var saveGame = OffSeasonSave();
            var nextSeason = NextSeason(saveGame);
            nextSeason.Teams.Single(t => t.TeamId == "T1").ReserveContract = new DriverContract { DriverId = "R1", Races = 3 };

            var newSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, nextSeason, new List<TeamHiringBallot>());

            Assert.IsNull(newSeason.Teams.Single(t => t.TeamId == "T1").ReserveContract);
        }

        [TestMethod]
        public void GenerateNewSeason_PlayerAsReserve_HasNoRaceSeatTeam()
        {
            var saveGame = OffSeasonSave();
            var newSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, NextSeason(saveGame), new[] { ReserveBallot("T1", "PLAYER") });

            Assert.AreEqual("PLAYER", newSeason.Teams.Single(t => t.TeamId == "T1").ReserveContract.DriverId);
            Assert.IsNull(saveGame.PlayerData.TeamId);
        }

        #endregion

        #region Off-season: hiring reserves

        [TestMethod]
        public void FinalBallotResults_RaceSeatsAreSettledBeforeReserveSeats()
        {
            // the top team wants Y as its reserve, a lesser team wants Y to race: the race seat wins
            var reserveBallot = new TeamHiringBallot
            {
                OriginalTeamHiring = new TeamHiring
                {
                    TeamId = "TOP", TeamReputation = TeamReputation.TOP_TEAM, Slot = TeamHiring.ReserveSlot,
                    DriverId = "Y", DriverReputation = DriverReputation.YOUNG_TALENT,
                    OtherPotentialCandidates = new List<DriverResume> { new DriverResume { Id = "Z", Reputation = DriverReputation.YOUNG_TALENT } }
                },
                Candidates = new List<TeamHiringBallotCandidate>()
            };
            var raceBallot = new TeamHiringBallot
            {
                OriginalTeamHiring = new TeamHiring
                {
                    TeamId = "MID", TeamReputation = TeamReputation.MIDFIELD, Slot = 2, Role = DriverRole.SECOND_DRIVER,
                    DriverId = "Y", DriverReputation = DriverReputation.YOUNG_TALENT,
                    OtherPotentialCandidates = new List<DriverResume>()
                },
                Candidates = new List<TeamHiringBallotCandidate>()
            };

            var results = _offSeasonMovements.FinalBallotResults(new[] { reserveBallot, raceBallot }).ToList();

            Assert.AreEqual("Y", results.Single(h => h.TeamId == "MID").DriverId);
            Assert.AreEqual("Z", results.Single(h => h.TeamId == "TOP" && h.IsReserve).DriverId);
        }

        [TestMethod]
        public void FinalBallotResults_GuaranteedHiring_SignsWhateverTheCandidates()
        {
            var ballot = new TeamHiringBallot
            {
                OriginalTeamHiring = new TeamHiring
                {
                    TeamId = "TOP", TeamReputation = TeamReputation.TOP_TEAM, Slot = 2, Role = DriverRole.SECOND_DRIVER,
                    DriverId = "R1", DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, Guaranteed = true
                },
                Candidates = new List<TeamHiringBallotCandidate>
                {
                    new TeamHiringBallotCandidate { DriverId = "C1", DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN }
                }
            };

            var results = _offSeasonMovements.FinalBallotResults(new[] { ballot }).ToList();

            Assert.AreEqual("R1", results.Single().DriverId);
        }

        [TestMethod]
        public void GetReservePromotion_PerfectFitForTheOpenSeat_IsOffered()
        {
            var saveGame = OffSeasonSave(reserveId: "R1");
            var drops = Drops(("T1", false, true), ("T2", false, false));

            var promotion = _endOfSeasonManager.GetReservePromotion(saveGame, NextSeason(saveGame).Teams, drops, "T1", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN);

            Assert.AreEqual(2, promotion.Slot);
            Assert.AreEqual(DriverRole.SECOND_DRIVER, promotion.Role);
            Assert.IsTrue(promotion.IsOffered);
        }

        [TestMethod]
        public void GetReservePromotion_StrongMidfielderAtAMidfieldHighTeam_IsOfferedTheSecondSeat()
        {
            // the reserve table of a MIDFIELD_HIGH team is all midfielders: a strong one is a
            // PerfectFit for its second seat, so they have a way into the car
            var saveGame = OffSeasonSave(reserveId: "R1");
            saveGame.CurrentSeason.Teams.Single(t => t.TeamId == "T1").Reputation = TeamReputation.MIDFIELD_HIGH;
            var drops = Drops(("T1", false, true), ("T2", false, false));

            foreach (var reputation in new[] { DriverReputation.PRIME_STRONG_MIDFIELD, DriverReputation.AGEING_STRONG_MIDFIELD })
            {
                var nextSeason = NextSeason(saveGame);
                var promotion = _endOfSeasonManager.GetReservePromotion(saveGame, nextSeason.Teams, drops, "T1", reputation);

                Assert.AreEqual(2, promotion.Slot, reputation.ToString());
                Assert.AreEqual(DriverRole.SECOND_DRIVER, promotion.Role, reputation.ToString());
                Assert.IsTrue(promotion.IsOffered, reputation.ToString());
            }
        }

        [TestMethod]
        public void ReserveAmbitions_PromotableAtAMidfieldHighTeam_ApplyThere()
        {
            // a reserve at a MIDFIELD_HIGH team can be promoted only as a PerfectFit for a race seat
            // there, so these reputations take reserve roles there too, not at top teams only
            var promotable = new[]
            {
                DriverReputation.PRIME_STRONG_MIDFIELD,
                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED,
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED,
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN,
                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN
            };

            foreach (var reputation in promotable)
            {
                var ambition = OffSeasonMovements.AmbitionFor(reputation, isReserve: true);
                Assert.AreEqual(TeamReputation.MIDFIELD_HIGH, ambition.MinReputation, reputation.ToString());
                Assert.AreEqual(TeamReputation.TOP_TEAM, ambition.MaxReputation, reputation.ToString());
            }
        }

        [TestMethod]
        public void GetReservePromotion_NotAPerfectFit_IsNotOffered_AndNoSeatMeansNone()
        {
            var saveGame = OffSeasonSave(reserveId: "R1");

            var underQualified = _endOfSeasonManager.GetReservePromotion(saveGame, NextSeason(saveGame).Teams, Drops(("T1", false, true), ("T2", false, false)), "T1", DriverReputation.YOUNG_TALENT);
            var noSeat = _endOfSeasonManager.GetReservePromotion(saveGame, NextSeason(saveGame).Teams, Drops(("T1", false, false), ("T2", false, false)), "T1", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN);

            Assert.AreEqual(DriverHirer.DriverPolicyFit.UnderQualified, underQualified.Fit);
            Assert.IsFalse(underQualified.IsOffered);
            Assert.IsNull(noSeat);
        }

        [TestMethod]
        public void TeamPicks_AiReservePerfectFit_IsPromotedBeforeTheMarket()
        {
            var saveGame = OffSeasonSave(reserveId: "R1", reserveReputation: DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN);

            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(1997, saveGame, NextSeason(saveGame).Teams, Drops(("T1", false, true), ("T2", false, false))).ToList();

            var ballot = ballots.Single(b => b.OriginalTeamHiring.TeamId == "T1" && b.OriginalTeamHiring.Slot == 2);
            Assert.AreEqual("R1", ballot.OriginalTeamHiring.DriverId);
            Assert.IsTrue(ballot.OriginalTeamHiring.Guaranteed);
            // a promoted reserve isn't on the market
            Assert.IsFalse(ballots.Any(b => b.Candidates.Any(c => c.DriverId == "R1")));
        }

        [TestMethod]
        public void TeamPicks_AiReserveNotAPerfectFit_TheSeatGoesToTheMarket()
        {
            var saveGame = OffSeasonSave(reserveId: "R1", reserveReputation: DriverReputation.YOUNG_TALENT);

            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(1997, saveGame, NextSeason(saveGame).Teams, Drops(("T1", false, true), ("T2", false, false))).ToList();

            Assert.IsFalse(ballots.Single(b => b.OriginalTeamHiring.TeamId == "T1").OriginalTeamHiring.Guaranteed);
        }

        [TestMethod]
        public void TeamPicks_TeamWantingAReserve_PrefersThePacksReserve()
        {
            var saveGame = OffSeasonSave();
            // a perfect fit for a top team's reserve seat, but not the pack's choice
            AddDriver(saveGame, "KID", DriverReputation.PRIME_STRONG_MIDFIELD);
            var nextSeason = NextSeason(saveGame);
            nextSeason.Teams.Single(t => t.TeamId == "T1").ReserveContract = new DriverContract { DriverId = "U1", Races = 3 };
            var plan = new ReservePlan { TeamsWantingReserve = new HashSet<string> { "T1" } };

            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(1997, saveGame, nextSeason.Teams, Drops(("T1", false, false), ("T2", false, false)), plan).ToList();

            var reserveBallot = ballots.Single();
            Assert.IsTrue(reserveBallot.OriginalTeamHiring.IsReserve);
            Assert.AreEqual("U1", reserveBallot.OriginalTeamHiring.DriverId);
        }

        [TestMethod]
        public void TeamPicks_TeamNotWantingAReserve_HasNoReserveSeat()
        {
            var saveGame = OffSeasonSave(reserveId: "R1");

            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(1997, saveGame, NextSeason(saveGame).Teams, Drops(("T1", false, false), ("T2", false, false)), new ReservePlan()).ToList();

            Assert.AreEqual(0, ballots.Count);
        }

        [TestMethod]
        public void TeamPicks_PlayerStaysOnAsReserve_IsGuaranteedTheReserveSeat()
        {
            var saveGame = OffSeasonSave(reserveId: "PLAYER");
            var plan = new ReservePlan { TeamsWantingReserve = new HashSet<string> { "T1" }, PlayerStaysReserveAtTeamId = "T1" };

            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(1997, saveGame, NextSeason(saveGame).Teams, Drops(("T1", false, false), ("T2", false, false)), plan).ToList();

            var hiring = ballots.Single().OriginalTeamHiring;
            Assert.AreEqual("PLAYER", hiring.DriverId);
            Assert.IsTrue(hiring.IsReserve);
            Assert.IsTrue(hiring.Guaranteed);
            // if the player wins a race seat, the team falls back on the others
            Assert.IsTrue(hiring.OtherPotentialCandidates.Any());
        }

        [TestMethod]
        public void TeamPicks_PlayerAcceptedThePromotion_TakesTheSeat()
        {
            var saveGame = OffSeasonSave(reserveId: "PLAYER");
            var plan = new ReservePlan
            {
                PlayerPromotion = new ReservePromotion { TeamId = "T1", Slot = 2, Role = DriverRole.SECOND_DRIVER, Fit = DriverHirer.DriverPolicyFit.PerfectFit }
            };

            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(1997, saveGame, NextSeason(saveGame).Teams, Drops(("T1", false, true), ("T2", false, false)), plan).ToList();

            var hiring = ballots.Single().OriginalTeamHiring;
            Assert.AreEqual("PLAYER", hiring.DriverId);
            Assert.AreEqual(2, hiring.Slot);
            Assert.IsTrue(hiring.Guaranteed);
        }

        #endregion

        #region Absences with a reserve

        [TestMethod]
        public void ProcessAbsences_PlayerReserveAsDriverIn_StepsInWithoutApplying()
        {
            var saveGame = RaceSave();
            saveGame.PlayerData.TeamId = null;
            Team(saveGame, "T1").ReserveContract = new DriverContract { DriverId = "PLAYER", Races = 3 };
            var entryList = EntryList(saveGame);
            var absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "PLAYER" } };
            var provider = new Mock<IAbsenceDecisionProvider>();

            new AbsenceManager().ProcessAbsences(entryList, absences, saveGame, provider.Object);

            Assert.AreEqual("PLAYER", entryList.Single(e => e.TeamId == "T1").Driver1Id);
            provider.Verify(p => p.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), true), Times.Once);
        }

        [TestMethod]
        public void ProcessAbsences_PlayerLeavesTheirSeat_TheirTeamsReserveCoversIt()
        {
            var saveGame = RaceSave();
            Team(saveGame, "T3").ReserveContract = new DriverContract { DriverId = "R3", Races = 3 };
            var entryList = EntryList(saveGame);
            // the player (YOUNG_TALENT) beats the declared AGEING_MIDFIELD stand-in
            var absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "U3" } };
            var provider = new Mock<IAbsenceDecisionProvider>();
            provider.Setup(p => p.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), It.IsAny<bool>())).Returns(true);
            provider.Setup(p => p.DoesPlayerTeamAllowLeave(It.IsAny<string>(), It.IsAny<Absence>())).Returns(true);

            new AbsenceManager().ProcessAbsences(entryList, absences, saveGame, provider.Object);

            Assert.AreEqual("PLAYER", entryList.Single(e => e.TeamId == "T1").Driver1Id);
            Assert.AreEqual("R3", absences.Single().ChainedAbsence.DriverIn);
        }

        [TestMethod]
        public void ProcessAbsences_PlayerWinsTheApplication_IsRecordedAsDriverIn()
        {
            var saveGame = RaceSave();
            var entryList = EntryList(saveGame);
            var absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "U3" } };
            var provider = new Mock<IAbsenceDecisionProvider>();
            provider.Setup(p => p.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), It.IsAny<bool>())).Returns(true);
            provider.Setup(p => p.DoesPlayerTeamAllowLeave(It.IsAny<string>(), It.IsAny<Absence>())).Returns(true);

            new AbsenceManager().ProcessAbsences(entryList, absences, saveGame, provider.Object);

            Assert.AreEqual("PLAYER", absences.Single().DriverIn);
        }

        [TestMethod]
        public void ProcessAbsences_PlayerWithoutASeatBeatsAChainedStandIn_TheStandInKeepsTheirSeat()
        {
            var saveGame = RaceSave();
            // the player is a free agent: someone else races in their T3 seat
            saveGame.PlayerData.TeamId = null;
            Team(saveGame, "T3").Driver1Contract.DriverId = "R3";
            saveGame.Drivers.Single(d => d.DriverId == "D4").Reputation = DriverReputation.AGEING_MIDFIELD;
            var entryList = EntryList(saveGame);
            // D4 was to leave T2 to stand in at T1, U3 taking D4's T2 seat
            var absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "D4",
                    ChainedAbsence = new Absence { RaceId = 1, TeamId = "T2", DriverOut = "D4", DriverIn = "U3" }
                }
            };
            var provider = new Mock<IAbsenceDecisionProvider>();
            provider.Setup(p => p.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), It.IsAny<bool>())).Returns(true);

            new AbsenceManager().ProcessAbsences(entryList, absences, saveGame, provider.Object);

            Assert.AreEqual("PLAYER", entryList.Single(e => e.TeamId == "T1").Driver1Id);
            Assert.AreEqual("D4", entryList.Single(e => e.TeamId == "T2").Driver2Id);
            Assert.AreEqual("PLAYER", absences.Single().DriverIn);
            Assert.IsNull(absences.Single().ChainedAbsence);
        }

        [TestMethod]
        public void ProcessAbsences_PlayerIsAReserve_TheirTeamIsAskedForLeave()
        {
            var saveGame = RaceSave();
            // the player is T2's reserve: someone else races in their T3 seat
            saveGame.PlayerData.TeamId = null;
            Team(saveGame, "T3").Driver1Contract.DriverId = "R3";
            Team(saveGame, "T2").ReserveContract = new DriverContract { DriverId = "PLAYER", Races = 3 };
            var entryList = EntryList(saveGame);
            var absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "U3" } };
            var provider = new Mock<IAbsenceDecisionProvider>();
            provider.Setup(p => p.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), It.IsAny<bool>())).Returns(true);
            provider.Setup(p => p.DoesPlayerTeamAllowLeave(It.IsAny<string>(), It.IsAny<Absence>())).Returns(false);

            new AbsenceManager().ProcessAbsences(entryList, absences, saveGame, provider.Object);

            provider.Verify(p => p.DoesPlayerTeamAllowLeave("T2", It.IsAny<Absence>()), Times.Once);
            Assert.AreEqual("U3", entryList.Single(e => e.TeamId == "T1").Driver1Id);
            Assert.IsNull(absences.Single().ChainedAbsence);
        }

        #endregion

        #region Paid rides

        [TestMethod]
        public void PaidRides_Budget_ByReputationAndOnlyWithoutAnyContract()
        {
            var wildCard = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            var season = PaidRideSave(DriverReputation.PAY_DRIVER_SEASON);
            var noMoney = PaidRideSave(DriverReputation.YOUNG_TALENT);
            var reserve = PaidRideSave(DriverReputation.PAY_DRIVER_SEASON);
            Team(reserve, "M1").ReserveContract = new DriverContract { DriverId = "PLAYER", Races = 3 };
            var racing = PaidRideSave(DriverReputation.PAY_DRIVER_SEASON);
            racing.PlayerData.TeamId = "M1";

            Assert.AreEqual(3, PaidRides.Budget(wildCard));
            Assert.AreEqual(6, PaidRides.Budget(season));
            Assert.AreEqual(0, PaidRides.Budget(noMoney));
            Assert.AreEqual(0, PaidRides.Budget(reserve));
            Assert.AreEqual(0, PaidRides.Budget(racing));
        }

        [TestMethod]
        public void PaidRides_Offers_FollowTheTeamsLineUps()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);

            var offers = PaidRides.Offers(saveGame, 0);

            // the super minnow: its driver who brings no money; the minnow with no pay driver: both
            // seats; the minnow with two: none; the top team: never
            Assert.AreEqual("S2", offers.Single(o => o.TeamId == "SM").DriverOutId);
            CollectionAssert.AreEquivalent(new[] { "M1A", "M1B" }, offers.Where(o => o.TeamId == "M1").Select(o => o.DriverOutId).ToList());
            Assert.IsFalse(offers.Any(o => o.TeamId == "M2"));
            Assert.IsFalse(offers.Any(o => o.TeamId == "TOP"));
        }

        [TestMethod]
        public void PaidRides_MinnowWithOnePayDriver_SellsTheOtherSeatOrNothing_TheSameEveryTime()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);

            var first = PaidRides.Offers(saveGame, 0).Where(o => o.TeamId == "M3").Select(o => o.DriverOutId).ToList();
            var second = PaidRides.Offers(saveGame, 0).Where(o => o.TeamId == "M3").Select(o => o.DriverOutId).ToList();

            CollectionAssert.AreEqual(first, second);
            Assert.IsTrue(first.All(id => id == "M3B"));

            // over many races the coin toss lands both ways
            var outcomes = Enumerable.Range(1, 40).Select(race => PaidRides.IsSeatOnSale(1996, race, "M3")).Distinct().Count();
            Assert.AreEqual(2, outcomes);
        }

        [TestMethod]
        public void PaidRides_Buy_AddsAPaidRideAbsence_AndSpendsTheBudget()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            var offer = PaidRides.Offers(saveGame, 0).First(o => o.TeamId == "SM");

            var absence = PaidRides.Buy(saveGame, 0, offer);

            Assert.AreEqual(AbsenceReason.PAID_RIDE, absence.Reason);
            Assert.AreEqual("PLAYER", absence.DriverIn);
            Assert.AreEqual("S2", absence.DriverOut);
            Assert.AreEqual(2, PaidRides.Remaining(saveGame));
            // already racing at that race: nothing more to buy there
            Assert.AreEqual(0, PaidRides.Offers(saveGame, 0).Count);
        }

        [TestMethod]
        public void PaidRides_BudgetSpent_NoOffers()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            for (var race = 0; race < 3; race++)
            {
                PaidRides.Buy(saveGame, race, PaidRides.Offers(saveGame, race).First());
            }

            Assert.AreEqual(0, PaidRides.Remaining(saveGame));
            Assert.AreEqual(0, PaidRides.Offers(saveGame, 3).Count);
        }

        [TestMethod]
        public void PaidRides_DriverAlreadyOutForTheRace_HasNoSeatToSell()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            saveGame.CurrentSeason.Absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "M1", DriverOut = "M1A", DriverIn = "U1" } };

            var offers = PaidRides.Offers(saveGame, 0);

            Assert.AreEqual("M1B", offers.Single(o => o.TeamId == "M1").DriverOutId);
        }

        [TestMethod]
        public void PaidRides_SeatCoveredByAChainedAbsence_HasNoSeatToSell()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            // M1A stands in at the top team; their own seat is covered by the chain
            saveGame.CurrentSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1, TeamId = "TOP", DriverOut = "T1A", DriverIn = "M1A",
                    ChainedAbsence = new Absence { RaceId = 1, TeamId = "M1", DriverOut = "M1A", DriverIn = "U1" }
                }
            };

            var offers = PaidRides.Offers(saveGame, 0);

            Assert.AreEqual("M1B", offers.Single(o => o.TeamId == "M1").DriverOutId);
        }

        [TestMethod]
        public void PaddockOpportunities_ListsAbsencesCallUpsAndRides()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            saveGame.CurrentSeason.Absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "TOP", DriverOut = "T1A", DriverIn = "U1" } };

            var opportunities = PaddockOpportunities.For(saveGame, 0);

            Assert.AreEqual("T1A", opportunities.OpenAbsences.Single().DriverOut);
            Assert.AreEqual(0, opportunities.CallUps.Count);
            Assert.IsTrue(opportunities.PaidRideOffers.Any());
            Assert.IsTrue(opportunities.HasChoices);

            PaidRides.Buy(saveGame, 0, opportunities.PaidRideOffers.First());
            var afterBuying = PaddockOpportunities.For(saveGame, 0);
            Assert.AreEqual(1, afterBuying.CallUps.Count);
            Assert.IsFalse(afterBuying.HasChoices);
        }

        [TestMethod]
        public void PaidRides_AiRides_OnlyFreePayDriversBuy()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_SEASON);
            AddDriver(saveGame, "AI1", DriverReputation.PAY_DRIVER_SEASON);
            AddDriver(saveGame, "AI2", DriverReputation.PAY_DRIVER_WILD_CARD);
            AddDriver(saveGame, "NOMONEY", DriverReputation.YOUNG_TALENT);
            AddDriver(saveGame, "RES", DriverReputation.PAY_DRIVER_SEASON);
            Team(saveGame, "TOP").ReserveContract = new DriverContract { DriverId = "RES", Races = 3 };
            AddDriver(saveGame, "OLD", DriverReputation.PAY_DRIVER_SEASON);
            saveGame.RetiredDrivers = new List<IDriverData> { saveGame.Drivers.Single(d => d.DriverId == "OLD") };
            AddDriver(saveGame, "BUSY", DriverReputation.PAY_DRIVER_SEASON);
            saveGame.CurrentSeason.Absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "TOP", DriverOut = "T1A", DriverIn = "BUSY" } };

            // every roll buys
            var bought = PaidRides.BuyAiRides(saveGame, 0, new FixedRandom(nextDouble: 0, next: 0));

            CollectionAssert.AreEquivalent(new[] { "AI1", "AI2" }, bought.Select(a => a.DriverIn).ToList());
            Assert.IsTrue(bought.All(a => a.Reason == AbsenceReason.PAID_RIDE && a.RaceId == 1));
            Assert.AreEqual(2, bought.Select(a => (a.TeamId, a.DriverOut)).Distinct().Count());
        }

        [TestMethod]
        public void PaidRides_AiRides_NoRollUnderTheChance_NobodyBuys()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_SEASON);
            AddDriver(saveGame, "AI1", DriverReputation.PAY_DRIVER_SEASON);

            var bought = PaidRides.BuyAiRides(saveGame, 0, new FixedRandom(nextDouble: 0.99, next: 0));

            Assert.AreEqual(0, bought.Count);
        }

        [TestMethod]
        public void PaidRides_AiRides_StopWhenTheBudgetIsSpent()
        {
            var saveGame = PaidRideSave(DriverReputation.YOUNG_TALENT);
            saveGame.CurrentSeason.Races = Enumerable.Range(1, 8).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList();
            AddDriver(saveGame, "AI1", DriverReputation.PAY_DRIVER_WILD_CARD);

            var rides = Enumerable.Range(0, 8).Sum(race => PaidRides.BuyAiRides(saveGame, race, new FixedRandom(nextDouble: 0, next: 0)).Count);

            Assert.AreEqual(3, rides);
            Assert.AreEqual(0, PaidRides.RemainingFor(saveGame, "AI1"));
        }

        [TestMethod]
        public void PaidRides_StrongDriver_IsNeverStoodDown_TheTeamSellsTheOtherSeat()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            // S2 would be the super minnow's seat on sale; M1A is one of the minnow's two
            saveGame.Drivers.Single(d => d.DriverId == "S2").Reputation = DriverReputation.PRIME_STRONG_MIDFIELD;
            saveGame.Drivers.Single(d => d.DriverId == "M1A").Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL;

            var offers = PaidRides.Offers(saveGame, 0);

            Assert.AreEqual("S1", offers.Single(o => o.TeamId == "SM").DriverOutId);
            Assert.AreEqual("M1B", offers.Single(o => o.TeamId == "M1").DriverOutId);
        }

        [TestMethod]
        public void PaidRides_DriverOutOfFourRaces_IsNotStoodDownAgain()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            saveGame.CurrentSeason.Races = Enumerable.Range(1, 8).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList();
            // M1A is out of four races: a pack absence, a release to come, a paid ride and a chained one
            saveGame.CurrentSeason.Absences = new List<Absence>
            {
                new Absence { RaceId = 2, TeamId = "M1", DriverOut = "M1A", DriverIn = "X1" },
                new Absence { RaceId = 8, TeamId = "M1", DriverOut = "M1A", DriverIn = "X2" },
                new Absence { RaceId = 3, TeamId = "M1", DriverOut = "M1A", DriverIn = "X3", Reason = AbsenceReason.PAID_RIDE },
                new Absence
                {
                    RaceId = 4, TeamId = "TOP", DriverOut = "T1A", DriverIn = "M1A",
                    ChainedAbsence = new Absence { RaceId = 4, TeamId = "M1", DriverOut = "M1A", DriverIn = "X4" }
                }
            };

            Assert.AreEqual(4, PaidRides.AbsencesOf(saveGame.CurrentSeason, "M1A"));
            Assert.AreEqual("M1B", PaidRides.Offers(saveGame, 0).Single(o => o.TeamId == "M1").DriverOutId);

            // the AI drivers can't stand them down either
            AddDriver(saveGame, "AI1", DriverReputation.PAY_DRIVER_SEASON);
            Assert.IsFalse(Enumerable.Range(0, 8)
                .SelectMany(race => PaidRides.BuyAiRides(saveGame, race, new FixedRandom(nextDouble: 0, next: 0)))
                .Any(a => a.DriverOut == "M1A"));
        }

        [TestMethod]
        public void PaidRides_AbsencesCap_IsAQuarterOfTheSeasonWhenLessThanFour()
        {
            var season = new Season();

            season.Races = Enumerable.Range(1, 20).Select(i => new Race { RaceId = i }).ToList();
            Assert.AreEqual(4, PaidRides.MaxAbsencesPerDriver(season));

            season.Races = Enumerable.Range(1, 16).Select(i => new Race { RaceId = i }).ToList();
            Assert.AreEqual(4, PaidRides.MaxAbsencesPerDriver(season));

            season.Races = Enumerable.Range(1, 15).Select(i => new Race { RaceId = i }).ToList();
            Assert.AreEqual(3, PaidRides.MaxAbsencesPerDriver(season));
        }

        [TestMethod]
        public void PaidRides_ShortSeason_DriverOutOfThreeRacesIsNotStoodDownAgain()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            saveGame.CurrentSeason.Races = Enumerable.Range(1, 12).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList();
            saveGame.CurrentSeason.Absences = Enumerable.Range(2, 2)
                .Select(race => new Absence { RaceId = race, TeamId = "SM", DriverOut = "S2", DriverIn = $"X{race}" })
                .ToList();

            // out of two races of twelve: the cap is three, so the seat is still on sale
            Assert.AreEqual("S2", PaidRides.Offers(saveGame, 0).Single(o => o.TeamId == "SM").DriverOutId);

            // a third absence reaches it
            saveGame.CurrentSeason.Absences = saveGame.CurrentSeason.Absences
                .Append(new Absence { RaceId = 4, TeamId = "SM", DriverOut = "S2", DriverIn = "X4" })
                .ToList();
            Assert.IsFalse(PaidRides.Offers(saveGame, 0).Any(o => o.DriverOutId == "S2"));
        }

        [TestMethod]
        public void PaidRides_ContestedSeat_TheAiRideDoesntCountTowardsTheCap()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            AddDriver(saveGame, "AI1", DriverReputation.PAY_DRIVER_SEASON);
            // a season long enough for the full cap of four
            saveGame.CurrentSeason.Races = Enumerable.Range(1, 16).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList();
            // S2 is out of three races, the fourth being the AI driver's ride at this one
            saveGame.CurrentSeason.Absences = new List<Absence>
            {
                new Absence { RaceId = 2, TeamId = "SM", DriverOut = "S2", DriverIn = "X1" },
                new Absence { RaceId = 3, TeamId = "SM", DriverOut = "S2", DriverIn = "X2" },
                new Absence { RaceId = 4, TeamId = "SM", DriverOut = "S2", DriverIn = "X3" },
                new Absence { RaceId = 1, TeamId = "SM", DriverOut = "S2", DriverIn = "AI1", Reason = AbsenceReason.PAID_RIDE }
            };

            var offer = PaidRides.Offers(saveGame, 0).Single(o => o.TeamId == "SM");

            Assert.AreEqual("S2", offer.DriverOutId);
            Assert.AreEqual("AI1", offer.RivalBidderId);
        }

        [TestMethod]
        public void PaidRides_SeatAnAiDriverBought_IsOfferedToThePlayerAsContested()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            AddDriver(saveGame, "AI1", DriverReputation.PAY_DRIVER_SEASON);
            var aiRide = new Absence { RaceId = 1, TeamId = "SM", DriverOut = "S2", DriverIn = "AI1", Reason = AbsenceReason.PAID_RIDE };
            saveGame.CurrentSeason.Absences = new List<Absence> { aiRide };

            var offer = PaidRides.Offers(saveGame, 0).Single(o => o.TeamId == "SM");

            Assert.AreEqual("S2", offer.DriverOutId);
            Assert.AreEqual("AI1", offer.RivalBidderId);
            Assert.AreSame(aiRide, offer.RivalRide);
            // it's bid for, not applied for like an absence
            Assert.IsFalse(PaddockOpportunities.For(saveGame, 0).OpenAbsences.Any());
        }

        [TestMethod]
        public void PaidRides_ContestWon_ThePlayerTakesOverTheAiRide()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            AddDriver(saveGame, "AI1", DriverReputation.PAY_DRIVER_SEASON);
            var aiRide = new Absence { RaceId = 1, TeamId = "SM", DriverOut = "S2", DriverIn = "AI1", Reason = AbsenceReason.PAID_RIDE };
            saveGame.CurrentSeason.Absences = new List<Absence> { aiRide };
            var offer = PaidRides.Offers(saveGame, 0).Single(o => o.TeamId == "SM");

            var absence = PaidRides.Buy(saveGame, 0, offer, new FixedRandom(nextDouble: 0, next: 0));

            Assert.AreSame(aiRide, absence);
            Assert.AreEqual("PLAYER", aiRide.DriverIn);
            Assert.AreEqual(1, saveGame.CurrentSeason.Absences.Count());
            Assert.AreEqual(2, PaidRides.Remaining(saveGame));
            Assert.AreEqual(6, PaidRides.RemainingFor(saveGame, "AI1"));
        }

        [TestMethod]
        public void PaidRides_ContestLost_NothingChanges()
        {
            var saveGame = PaidRideSave(DriverReputation.PAY_DRIVER_WILD_CARD);
            AddDriver(saveGame, "AI1", DriverReputation.PAY_DRIVER_SEASON);
            var aiRide = new Absence { RaceId = 1, TeamId = "SM", DriverOut = "S2", DriverIn = "AI1", Reason = AbsenceReason.PAID_RIDE };
            saveGame.CurrentSeason.Absences = new List<Absence> { aiRide };
            var offer = PaidRides.Offers(saveGame, 0).Single(o => o.TeamId == "SM");

            var absence = PaidRides.Buy(saveGame, 0, offer, new FixedRandom(nextDouble: 0.99, next: 0));

            Assert.IsNull(absence);
            Assert.AreEqual("AI1", aiRide.DriverIn);
            Assert.AreEqual(3, PaidRides.Remaining(saveGame));
            // the player can still buy another seat at this race
            Assert.IsTrue(PaidRides.Offers(saveGame, 0).Any(o => o.RivalBidderId == null));
        }

        [TestMethod]
        public void ProcessAbsences_AiPaidRide_CantBeAppliedFor()
        {
            var saveGame = RaceSave();
            saveGame.PlayerData.TeamId = null;
            Team(saveGame, "T3").Driver1Contract.DriverId = "U3";
            var entryList = EntryList(saveGame);
            var absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T3", DriverOut = "D5", DriverIn = "R3", Reason = AbsenceReason.PAID_RIDE } };
            var provider = new Mock<IAbsenceDecisionProvider>();
            provider.Setup(p => p.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), It.IsAny<bool>())).Returns(true);
            provider.Setup(p => p.DoesPlayerTeamAllowLeave(It.IsAny<string>(), It.IsAny<Absence>())).Returns(true);

            new AbsenceManager().ProcessAbsences(entryList, absences, saveGame, provider.Object);

            // the news is shown, but whoever paid races
            provider.Verify(p => p.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), false), Times.Once);
            Assert.AreEqual("R3", entryList.Single(e => e.TeamId == "T3").Driver2Id);
        }

        #endregion

        #region Simulated teammate collisions

        [TestMethod]
        public void TeammateCollision_VictimRetires_AtFaultIsReported_PositionsRenumbered()
        {
            var results = new List<ParticipantData>
            {
                new ParticipantData { DriverId = "A1", TeamId = "A", Position = 1 },
                new ParticipantData { DriverId = "A2", TeamId = "A", Position = 2 },
                new ParticipantData { DriverId = "B1", TeamId = "B", Position = 3 },
                new ParticipantData { DriverId = "SOLO", TeamId = "C", Position = 4 }
            };

            // every roll collides; the second car is at fault and drops back rather than retiring
            var contacts = SimulatedIncidentGenerator.ApplyTeammateCollisions(results, new FixedRandom(nextDouble: 0, next: 1));

            var contact = contacts.Single();
            Assert.AreEqual("A2", contact.AtFaultDriverId);
            Assert.AreEqual("A1", contact.OtherDriverId);
            Assert.IsTrue(results.Single(r => r.DriverId == "A1").DNF);
            Assert.IsFalse(results.Single(r => r.DriverId == "A2").DNF);
            // the classified cars, the one dropping back behind them, then the retirement
            CollectionAssert.AreEqual(new[] { "B1", "SOLO", "A2", "A1" }, results.Select(r => r.DriverId).ToList());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, results.Select(r => r.Position).ToList());
        }

        [TestMethod]
        public void TeammateCollision_NoRollUnderTheChance_ChangesNothing()
        {
            var results = new List<ParticipantData>
            {
                new ParticipantData { DriverId = "A1", TeamId = "A", Position = 1 },
                new ParticipantData { DriverId = "A2", TeamId = "A", Position = 2 }
            };

            var contacts = SimulatedIncidentGenerator.ApplyTeammateCollisions(results, new FixedRandom(nextDouble: 0.99, next: 0));

            Assert.AreEqual(0, contacts.Count);
            Assert.IsFalse(results.Any(r => r.DNF));
        }

        private class FixedRandom : System.Random
        {
            private readonly double _nextDouble;
            private readonly int _next;

            public FixedRandom(double nextDouble, int next)
            {
                _nextDouble = nextDouble;
                _next = next;
            }

            public override double NextDouble() => _nextDouble;
            public override int Next(int maxValue) => _next;
        }

        #endregion

        #region Market outlook

        [TestMethod]
        public void MarketOutlook_TopTeam_ShowsAReserveSeat_MinnowsNever()
        {
            var saveGame = OffSeasonSave();
            AddTeam(saveGame, Team("MIN", TeamReputation.MINNOW, "N1", "N2"));
            AddDriver(saveGame, "N1", DriverReputation.PAY_DRIVER_SEASON);
            AddDriver(saveGame, "N2", DriverReputation.PAY_DRIVER_SEASON);
            // the player is a PRIME_MIDFIELD, a perfect fit for a top team's reserve seat

            var outlook = OutlookEstimator().Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            var reserveSeat = outlook.Seats.Single(s => s.TeamId == "T1" && s.IsReserve);
            Assert.AreEqual(EndOfSeasonManager.ReserveChance(TeamReputation.TOP_TEAM), reserveSeat.OpenChance, 0.08);
            Assert.AreEqual(DriverHirer.DriverPolicyFit.PerfectFit, reserveSeat.PlayerFit);
            Assert.IsFalse(outlook.Seats.Any(s => s.TeamId == "MIN" && s.IsReserve));
        }

        [TestMethod]
        public void MarketOutlook_AiReservePerfectFit_TakesTheOpeningSeat()
        {
            var saveGame = OffSeasonSave(reserveId: "R1", reserveReputation: DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN);
            // D2's contract ends: the seat opens every time, and the reserve is promoted to it
            Team(saveGame, "T1").Driver2Contract.Races = 3;
            saveGame.Drivers.Single(d => d.DriverId == "PLAYER").Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;

            var outlook = OutlookEstimator().Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            Assert.IsFalse(outlook.Seats.Any(s => s.TeamId == "T1" && s.Slot == 2));
        }

        [TestMethod]
        public void MarketOutlook_PlayerReservePerfectFit_ThePromotionIsCertain()
        {
            var saveGame = OffSeasonSave(reserveId: "PLAYER");
            Team(saveGame, "T1").Driver2Contract.Races = 3;
            saveGame.Drivers.Single(d => d.DriverId == "PLAYER").Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;

            var outlook = OutlookEstimator().Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            var seat = outlook.Seats.Single(s => s.TeamId == "T1" && s.Slot == 2);
            Assert.AreEqual(1, seat.Chance, 1e-9);
        }

        private static MarketOutlookEstimator OutlookEstimator()
        {
            var endOfSeasonManager = new Mock<IEndOfSeasonManager>();
            endOfSeasonManager
                .Setup(m => m.ProjectNewReputations(It.IsAny<ISaveGame>(), It.IsAny<int>()))
                .Returns((ISaveGame saveGame, int _) => saveGame.Drivers.ToDictionary(d => d.DriverId, d => d.Reputation));
            return new MarketOutlookEstimator(endOfSeasonManager.Object, new DriverFirer(), new DriverHirer());
        }

        #endregion

        #region Helpers

        // 1996, three races. T1 (TOP): D1 FIRST + D2 SECOND; T2 (MIDFIELD): D3 + D4. Free: U1, U2,
        // the player (and the reserve, when given). Contracts run past the season.
        private static ISaveGame OffSeasonSave(string reserveId = null, DriverReputation reserveReputation = DriverReputation.YOUNG_TALENT)
        {
            var t1 = Team("T1", TeamReputation.TOP_TEAM, "D1", "D2", reserveId);
            t1.Driver1Contract.Role = ContractRole.FIRST_DRIVER;
            t1.Driver2Contract.Role = ContractRole.SECOND_DRIVER;
            var t2 = Team("T2", TeamReputation.MIDFIELD, "D3", "D4");
            t2.Driver1Contract.Role = ContractRole.EQUAL;
            t2.Driver2Contract.Role = ContractRole.EQUAL;

            var saveGame = new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = 1996,
                    Teams = new List<ITeamEntry> { t1, t2 },
                    Races = Enumerable.Range(1, 3).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList(),
                    Absences = new List<Absence>()
                },
                Drivers = new List<IDriverData>
                {
                    Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                    Driver("D2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN),
                    Driver("D3", DriverReputation.PRIME_MIDFIELD),
                    Driver("D4", DriverReputation.PRIME_MIDFIELD),
                    Driver("U1", DriverReputation.AGEING_MIDFIELD),
                    Driver("U2", DriverReputation.PRIME_MIDFIELD),
                    Driver("PLAYER", DriverReputation.PRIME_MIDFIELD)
                },
                RetiredDrivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player" },
                Reprimands = new List<Reprimand>()
            };

            if (reserveId != null && reserveId != "PLAYER")
                AddDriver(saveGame, reserveId, reserveReputation);

            return saveGame;
        }

        // the same teams next season, as the season pack has them (no roles, no reserves)
        private static ISeason NextSeason(ISaveGame saveGame)
        {
            return new Season
            {
                Year = saveGame.CurrentSeason.Year + 1,
                Teams = saveGame.CurrentSeason.Teams.Select(t => (ITeamEntry)Team(t.TeamId, t.Reputation, t.Driver1Contract.DriverId, t.Driver2Contract.DriverId)).ToList(),
                Races = saveGame.CurrentSeason.Races.ToList(),
                Absences = new List<Absence>()
            };
        }

        private static TeamHiringBallot ReserveBallot(string teamId, string driverId)
        {
            return new TeamHiringBallot
            {
                OriginalTeamHiring = new TeamHiring
                {
                    TeamId = teamId,
                    TeamReputation = TeamReputation.TOP_TEAM,
                    DriverId = driverId,
                    DriverReputation = DriverReputation.YOUNG_TALENT,
                    Slot = TeamHiring.ReserveSlot,
                    ExcludeDriverIds = new HashSet<string>(),
                    OtherPotentialCandidates = new List<DriverResume>()
                },
                Candidates = new List<TeamHiringBallotCandidate>()
            };
        }

        private static List<DropTeamResult> Drops(params (string TeamId, bool Driver1, bool Driver2)[] drops)
        {
            return drops.Select(d => new DropTeamResult
            {
                TeamId = d.TeamId,
                DropDriver1 = d.Driver1 ? DriverFirerOutcome.DROPPED_UNDERPERFORMING : DriverFirerOutcome.NOT_DROPPED,
                DropDriver2 = d.Driver2 ? DriverFirerOutcome.DROPPED_UNDERPERFORMING : DriverFirerOutcome.NOT_DROPPED
            }).ToList();
        }

        // 1996, four races. T1 (TOP): D1 + D2; T2 (MIDFIELD): D3 + D4; T3 (MINNOW): PLAYER + D5.
        // Free: U3 (AGEING_MIDFIELD), R3.
        private static ISaveGame RaceSave()
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = 1996,
                    Teams = new List<ITeamEntry>
                    {
                        Team("T1", TeamReputation.TOP_TEAM, "D1", "D2"),
                        Team("T2", TeamReputation.MIDFIELD, "D3", "D4"),
                        Team("T3", TeamReputation.MINNOW, "PLAYER", "D5")
                    },
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
                    Driver("U3", DriverReputation.AGEING_MIDFIELD),
                    Driver("R3", DriverReputation.PAY_DRIVER_SEASON)
                },
                RetiredDrivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = "T3" },
                Reprimands = new List<Reprimand>()
            };
        }

        private static List<EntryListEntry> EntryList(ISaveGame saveGame)
        {
            return saveGame.CurrentSeason.Teams.Select(t => new EntryListEntry
            {
                TeamId = t.TeamId,
                Driver1Id = t.Driver1Contract.DriverId,
                Driver2Id = t.Driver2Contract.DriverId
            }).ToList();
        }

        // 1996, four races, the player a free agent. SM (SUPER_MINNOW): S1 pays + S2 doesn't;
        // M1 (MINNOW): nobody pays; M2: both pay; M3: M3A pays, M3B doesn't; TOP never sells.
        private static ISaveGame PaidRideSave(DriverReputation playerReputation)
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = 1996,
                    Teams = new List<ITeamEntry>
                    {
                        Team("TOP", TeamReputation.TOP_TEAM, "T1A", "T1B"),
                        Team("SM", TeamReputation.SUPER_MINNOW, "S1", "S2"),
                        Team("M1", TeamReputation.MINNOW, "M1A", "M1B"),
                        Team("M2", TeamReputation.MINNOW, "M2A", "M2B"),
                        Team("M3", TeamReputation.MINNOW, "M3A", "M3B")
                    },
                    Races = Enumerable.Range(1, 4).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList(),
                    Absences = new List<Absence>()
                },
                Drivers = new List<IDriverData>
                {
                    Driver("T1A", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                    Driver("T1B", DriverReputation.PRIME_STRONG_MIDFIELD),
                    Driver("S1", DriverReputation.PAY_DRIVER_SEASON),
                    Driver("S2", DriverReputation.YOUNG_TALENT),
                    Driver("M1A", DriverReputation.PRIME_MIDFIELD),
                    Driver("M1B", DriverReputation.YOUNG_TALENT),
                    Driver("M2A", DriverReputation.PAY_DRIVER_SEASON),
                    Driver("M2B", DriverReputation.PAY_DRIVER_WILD_CARD),
                    Driver("M3A", DriverReputation.PAY_DRIVER_SEASON),
                    Driver("M3B", DriverReputation.AGEING_MIDFIELD),
                    Driver("U1", DriverReputation.AGEING_MIDFIELD),
                    Driver("PLAYER", playerReputation)
                },
                RetiredDrivers = new List<IDriverData>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player" }
            };
        }

        private static TeamEntry Team(string teamId, TeamReputation reputation, string driver1Id, string driver2Id, string reserveId = null)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                TeamName = $"Team {teamId}",
                Reputation = reputation,
                Driver1Contract = new DriverContract { DriverId = driver1Id, Races = 10 },
                Driver2Contract = new DriverContract { DriverId = driver2Id, Races = 10 },
                ReserveContract = reserveId == null ? null : new DriverContract { DriverId = reserveId, Races = 3 }
            };
        }

        private static ITeamEntry Team(ISaveGame saveGame, string teamId) => saveGame.CurrentSeason.Teams.Single(t => t.TeamId == teamId);

        private static void AddTeam(ISaveGame saveGame, ITeamEntry team) =>
            saveGame.CurrentSeason.Teams = saveGame.CurrentSeason.Teams.Append(team).ToList();

        private static IDriverData Driver(string id, DriverReputation reputation, int yearOfBirth = 1970)
        {
            return new DriverData { DriverId = id, Name = $"Driver {id}", YearOfBirth = yearOfBirth, Reputation = reputation };
        }

        private static void AddDriver(ISaveGame saveGame, string id, DriverReputation reputation) =>
            saveGame.Drivers = saveGame.Drivers.Append(Driver(id, reputation)).ToList();

        #endregion
    }
}
