using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Business.Services.CrashDetection;
using AMS2ChEd.Business.Settings;
using Moq;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class CrashIncidentsTests
    {
        // returns the given values in turn (NextDouble), and the lowest value for Next
        private class ScriptedRandom : Random
        {
            private readonly Queue<double> _values;
            public ScriptedRandom(params double[] values) => _values = new Queue<double>(values);
            public override double NextDouble() => _values.Count > 0 ? _values.Dequeue() : 0.99;
            public override int Next(int minValue, int maxValue) => minValue;
            public override int Next(int maxValue) => 0;
        }

        // 1996: a Severe crash is fatal under 0.015, an injury under 0.315; the next draw picks the
        // injury's length (0 -> 1 race, 0.5 -> 2 races, 0.99 -> 8)
        private static ScriptedRandom Fatal() => new(0.001);
        private static ScriptedRandom Injury(double length = 0) => new(0.1, length);
        private static ScriptedRandom Unhurt() => new(0.9);

        private static readonly CrashOptions FatalitiesOn = new() { FatalitiesEnabled = true };
        private static readonly CrashOptions EverythingOn = new() { FatalitiesEnabled = true, PlayerDeathEnabled = true };

        #region CrashOutcomeRoller

        [TestMethod]
        [DataRow(CrashSeverity.Minor)]
        [DataRow(CrashSeverity.Moderate)]
        public void Roll_LessThanSevere_IsAlwaysUnhurt(CrashSeverity severity)
        {
            var outcome = CrashOutcomeRoller.Roll(severity, 1955, false, EverythingOn, 10, Fatal());

            Assert.AreEqual(CrashOutcomeKind.Unhurt, outcome.Kind);
        }

        [TestMethod]
        public void Roll_FatalWithFatalitiesOn_Kills()
        {
            Assert.AreEqual(CrashOutcomeKind.Fatal, CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, false, FatalitiesOn, 10, Fatal()).Kind);
        }

        [TestMethod]
        public void Roll_FatalWithFatalitiesOff_IsALongInjury()
        {
            var outcome = CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, false, new CrashOptions(), 20, Fatal());

            Assert.AreEqual(CrashOutcomeKind.Injured, outcome.Kind);
            Assert.AreEqual(CrashOutcomeRoller.LongInjuryMinRaces, outcome.RacesOut);
        }

        [TestMethod]
        public void Roll_FatalForThePlayer_NeedsItsOwnOption()
        {
            Assert.AreEqual(CrashOutcomeKind.Injured, CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, true, FatalitiesOn, 20, Fatal()).Kind);
            Assert.AreEqual(CrashOutcomeKind.Fatal, CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, true, EverythingOn, 20, Fatal()).Kind);
            // the player's own option alone isn't enough
            Assert.AreEqual(CrashOutcomeKind.Injured, CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, true, new CrashOptions { PlayerDeathEnabled = true }, 20, Fatal()).Kind);
        }

        [TestMethod]
        public void Roll_Injury_IsCappedAtTheRacesLeft()
        {
            var outcome = CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, false, FatalitiesOn, 3, Injury(length: 0.99));

            Assert.AreEqual(CrashOutcomeKind.Injured, outcome.Kind);
            Assert.AreEqual(3, outcome.RacesOut);
        }

        [TestMethod]
        public void Roll_InTheLastRace_IsAnInjuryWithNoRacesOut()
        {
            var outcome = CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, false, FatalitiesOn, 0, Injury());

            Assert.AreEqual(CrashOutcomeKind.Injured, outcome.Kind);
            Assert.AreEqual(0, outcome.RacesOut);
        }

        [TestMethod]
        public void Odds_FallWithEachSafetyEra()
        {
            var years = new[] { 1955, 1965, 1975, 1985, 1998, 2010, 2020 };
            var odds = years.Select(CrashOutcomeRoller.Odds).ToList();

            for (int i = 1; i < odds.Count; i++)
            {
                Assert.IsTrue(odds[i].Fatal < odds[i - 1].Fatal, $"fatal odds in {years[i]}");
                Assert.IsTrue(odds[i].Injury < odds[i - 1].Injury, $"injury odds in {years[i]}");
            }
        }

        [TestMethod]
        public void Roll_DrovenOn_NeverKills_AndIsShort()
        {
            // what would be fatal is a short injury: 1 race on a 0 length draw, 3 at most
            var outcome = CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1955, false, EverythingOn, 10, new ScriptedRandom(0.001, 0.0), droveOn: true);
            Assert.AreEqual(CrashOutcomeKind.Injured, outcome.Kind);
            Assert.AreEqual(1, outcome.RacesOut);

            var longest = CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1955, false, EverythingOn, 10, new ScriptedRandom(0.001, 0.99), droveOn: true);
            Assert.AreEqual(3, longest.RacesOut);
        }

        [TestMethod]
        public void Roll_DrovenOn_IsAThirdAsLikelyToInjure()
        {
            // 1996: an injury under 0.204 normally, under 0.068 after driving on
            Assert.AreEqual(CrashOutcomeKind.Injured, CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, false, FatalitiesOn, 10, new ScriptedRandom(0.1)).Kind);
            Assert.AreEqual(CrashOutcomeKind.Unhurt, CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, false, FatalitiesOn, 10, new ScriptedRandom(0.1), droveOn: true).Kind);
            Assert.AreEqual(CrashOutcomeKind.Injured, CrashOutcomeRoller.Roll(CrashSeverity.Severe, 1996, false, FatalitiesOn, 10, new ScriptedRandom(0.05), droveOn: true).Kind);
        }

        #endregion

        #region CrashIncidentManager

        [TestMethod]
        public void ApplyRace_Injury_GivesTheSeatToAStandInForTheRacesMissed()
        {
            var saveGame = CreateSaveGame();

            var news = CrashIncidentManager.ApplyRace(saveGame, 0, Crash("D3", "T2"), FatalitiesOn, Injury(length: 0.5));

            var item = news.Single();
            Assert.AreEqual(CrashOutcomeKind.Injured, item.Outcome);
            Assert.AreEqual(2, item.RacesOut);
            // a midfield team takes stand-ins up to PRIME_CHAMPIONSHIP_LEVEL_WASHED: the best free one
            Assert.AreEqual("U1", item.StandInDriverId);

            var absences = saveGame.CurrentSeason.Absences.ToList();
            Assert.AreEqual(2, absences.Count);
            CollectionAssert.AreEqual(new[] { 2, 3 }, absences.Select(a => a.RaceId).ToList());
            Assert.IsTrue(absences.All(a => a.DriverOut == "D3" && a.DriverIn == "U1" && a.TeamId == "T2" && a.Reason == AbsenceReason.INJURY && a.IncidentRaceId == 1));

            Assert.IsTrue(saveGame.IsOutOfAction("D3", 2));
            Assert.IsFalse(saveGame.IsOutOfAction("D3", 4));
            Assert.IsFalse(saveGame.IsDeceased("D3"));
        }

        [TestMethod]
        public void ApplyRace_Injury_TheTeamsReserveStandsInFirst()
        {
            var saveGame = CreateSaveGame();
            Team(saveGame, "T2").ReserveContract = new DriverContract { DriverId = "U3", Races = 4 };

            var news = CrashIncidentManager.ApplyRace(saveGame, 0, Crash("D3", "T2"), FatalitiesOn, Injury());

            Assert.AreEqual("U3", news.Single().StandInDriverId);
        }

        [TestMethod]
        public void ApplyRace_Death_MissesEveryRaceLeft()
        {
            var saveGame = CreateSaveGame();

            var news = CrashIncidentManager.ApplyRace(saveGame, 1, Crash("D3", "T2"), FatalitiesOn, Fatal());

            Assert.AreEqual(CrashOutcomeKind.Fatal, news.Single().Outcome);
            Assert.IsTrue(saveGame.IsDeceased("D3"));
            var absences = saveGame.CurrentSeason.Absences.ToList();
            CollectionAssert.AreEqual(new[] { 3, 4 }, absences.Select(a => a.RaceId).ToList());
            Assert.IsTrue(absences.All(a => a.Reason == AbsenceReason.DECEASED && a.IncidentRaceId == 2));
            Assert.AreEqual(CasualtyKind.DECEASED, saveGame.Casualties.Single().Kind);
        }

        [TestMethod]
        public void ApplyRace_Unhurt_IsNewsOnlyForASevereCrash()
        {
            var saveGame = CreateSaveGame();
            var crashes = new[]
            {
                new RaceCrash { DriverId = "D3", TeamId = "T2", Severity = CrashSeverity.Severe },
                new RaceCrash { DriverId = "D4", TeamId = "T2", Severity = CrashSeverity.Moderate }
            };

            var news = CrashIncidentManager.ApplyRace(saveGame, 0, crashes, FatalitiesOn, Unhurt());

            Assert.AreEqual("D3", news.Single().DriverId);
            Assert.AreEqual(CrashOutcomeKind.Unhurt, news.Single().Outcome);
            Assert.IsNull(saveGame.Casualties);
            Assert.AreEqual(0, saveGame.CurrentSeason.Absences.Count());
        }

        [TestMethod]
        public void ApplyRace_DrivenOn_IsNewsOnlyWhenItInjured()
        {
            var saveGame = CreateSaveGame();
            var crash = new[] { new RaceCrash { DriverId = "D3", TeamId = "T2", Severity = CrashSeverity.Severe, DroveOn = true } };

            Assert.AreEqual(0, CrashIncidentManager.ApplyRace(saveGame, 0, crash, FatalitiesOn, Unhurt()).Count);

            var item = CrashIncidentManager.ApplyRace(saveGame, 0, crash, FatalitiesOn, new ScriptedRandom(0.01, 0.0)).Single();
            Assert.IsTrue(item.DroveOn);
            Assert.AreEqual(CrashOutcomeKind.Injured, item.Outcome);
            Assert.AreEqual(1, item.RacesOut);
            Assert.IsTrue(saveGame.IsOutOfAction("D3", 2));
        }

        [TestMethod]
        public void ApplyRace_AlreadyOutOfARace_KeepsTheStandIn_WithTheCrashAsTheReason()
        {
            var saveGame = CreateSaveGame();
            // a season pack's absence at race 2
            saveGame.CurrentSeason.Absences = new List<Absence> { new Absence { RaceId = 2, TeamId = "T2", DriverOut = "D3", DriverIn = "U2" } };

            CrashIncidentManager.ApplyRace(saveGame, 0, Crash("D3", "T2"), FatalitiesOn, Injury(length: 0.5));

            var absences = saveGame.CurrentSeason.Absences.ToList();
            var packAbsence = absences.Single(a => a.RaceId == 2);
            Assert.AreEqual("U2", packAbsence.DriverIn);
            Assert.AreEqual(AbsenceReason.INJURY, packAbsence.Reason);
            Assert.AreEqual(1, packAbsence.IncidentRaceId);
            Assert.AreEqual(AbsenceReason.INJURY, absences.Single(a => a.RaceId == 3).Reason);
        }

        [TestMethod]
        public void ApplyRace_Death_APaidRideOnTheSeatBecomesADeceasedAbsence_AndLeavesTheBudget()
        {
            var saveGame = CreateSaveGame();
            saveGame.Drivers = saveGame.Drivers.Append(Driver("PAY", DriverReputation.PAY_DRIVER_SEASON)).ToList();
            saveGame.CurrentSeason.Absences = new List<Absence>
            {
                new Absence { RaceId = 3, TeamId = "T2", DriverOut = "D3", DriverIn = "PAY", Reason = AbsenceReason.PAID_RIDE }
            };

            CrashIncidentManager.ApplyRace(saveGame, 1, Crash("D3", "T2"), FatalitiesOn, Fatal());

            var ride = saveGame.CurrentSeason.Absences.Single(a => a.RaceId == 3);
            Assert.AreEqual("PAY", ride.DriverIn);
            Assert.AreEqual(AbsenceReason.DECEASED, ride.Reason);
            Assert.AreEqual(2, ride.IncidentRaceId);
            Assert.AreEqual(0, PaidRides.Used(saveGame, "PAY"));
        }

        [TestMethod]
        public void ApplyRace_AnAbsenceAfterTheRecovery_KeepsItsReason()
        {
            var saveGame = CreateSaveGame();
            saveGame.CurrentSeason.Absences = new List<Absence>
            {
                new Absence { RaceId = 4, TeamId = "T2", DriverOut = "D3", DriverIn = "U2", Reason = AbsenceReason.PAID_RIDE }
            };

            // out of race 2 only
            CrashIncidentManager.ApplyRace(saveGame, 0, Crash("D3", "T2"), FatalitiesOn, Injury());

            var later = saveGame.CurrentSeason.Absences.Single(a => a.RaceId == 4);
            Assert.AreEqual(AbsenceReason.PAID_RIDE, later.Reason);
            Assert.IsNull(later.IncidentRaceId);
        }

        [TestMethod]
        public void ApplyRace_AChainedAbsenceAtTheDriversOwnTeam_GetsTheCrashAsTheReasonToo()
        {
            var saveGame = CreateSaveGame();
            // D3 was to leave T2 to stand in for D1 at T1 in race 2, U3 covering D3's own seat
            saveGame.CurrentSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 2, TeamId = "T1", DriverOut = "D1", DriverIn = "D3",
                    ChainedAbsence = new Absence { RaceId = 2, TeamId = "T2", DriverOut = "D3", DriverIn = "U3" }
                }
            };

            CrashIncidentManager.ApplyRace(saveGame, 0, Crash("D3", "T2"), FatalitiesOn, Injury());

            var absence = saveGame.CurrentSeason.Absences.Single();
            Assert.AreNotEqual("D3", absence.DriverIn);
            Assert.AreEqual("U3", absence.ChainedAbsence.DriverIn);
            Assert.AreEqual(AbsenceReason.INJURY, absence.ChainedAbsence.Reason);
            Assert.IsNull(absence.Reason);
        }

        [TestMethod]
        public void ApplyRace_InjuredStandIn_IsReplacedWhereTheyWereToStandIn()
        {
            var saveGame = CreateSaveGame();
            // U1 was to stand in for D1 at race 2; they crash in race 1 (in D4's car)
            saveGame.CurrentSeason.Absences = new List<Absence> { new Absence { RaceId = 2, TeamId = "T1", DriverOut = "D1", DriverIn = "U1" } };

            CrashIncidentManager.ApplyRace(saveGame, 0, Crash("U1", "T2"), FatalitiesOn, Injury());

            var absence = saveGame.CurrentSeason.Absences.Single();
            Assert.AreEqual("U2", absence.DriverIn);
            Assert.IsTrue(saveGame.IsOutOfAction("U1", 2));
        }

        [TestMethod]
        public void ApplyRace_TheSameDriverIsRolledOnce_AndNeverAfterDying()
        {
            var saveGame = CreateSaveGame();

            CrashIncidentManager.ApplyRace(saveGame, 0, Crash("D3", "T2").Concat(Crash("D3", "T2")), FatalitiesOn, Fatal());
            var later = CrashIncidentManager.ApplyRace(saveGame, 1, Crash("D3", "T2"), FatalitiesOn, Fatal());

            Assert.AreEqual(1, saveGame.Casualties.Count);
            Assert.AreEqual(0, later.Count);
        }

        [TestMethod]
        public void PickStandIn_LeavesOutInjuredAndDeadDrivers()
        {
            var saveGame = CreateSaveGame();
            saveGame.Casualties = new List<Casualty>
            {
                new Casualty { DriverId = "U1", Year = 1996, Kind = CasualtyKind.INJURED, RaceIdsOut = new List<int> { 3 } },
                new Casualty { DriverId = "U2", Year = 1995, Kind = CasualtyKind.DECEASED }
            };

            var pick = SubstitutePool.PickStandIn(saveGame, Team(saveGame, "T2"), new[] { 3, 4 }, saveGame.CurrentSeason.Absences, "D3");

            Assert.AreEqual("U3", pick);
            // U1 is back by race 4
            Assert.AreEqual("U1", SubstitutePool.PickStandIn(saveGame, Team(saveGame, "T2"), new[] { 4 }, saveGame.CurrentSeason.Absences, "D3"));
        }

        [TestMethod]
        public void SimulatedCrashes_OnlyRetirements_NeverThePlayer_AndEveryTeammateCollision()
        {
            var results = new List<ParticipantData>
            {
                new ParticipantData { DriverId = "D1", TeamId = "T1", Position = 1 },
                new ParticipantData { DriverId = "D2", TeamId = "T1", Position = 2, DNF = true },
                new ParticipantData { DriverId = "PLAYER", TeamId = "T3", Position = 3, DNF = true },
                new ParticipantData { DriverId = "D3", TeamId = "T2", Position = 4, DNF = true },
            };
            var contacts = new[] { new TeammateContact { AtFaultDriverId = "D3", OtherDriverId = "D4" } };

            // D2's retirement is a failure (0.9 >= 0.4); D3 collided, so it's a crash whatever the roll
            var crashes = SimulatedIncidentGenerator.RollCrashes(results, contacts, "PLAYER", new ScriptedRandom(0.9, 0.0));

            Assert.AreEqual("D3", crashes.Single().DriverId);
            Assert.AreEqual(CrashSeverity.Severe, crashes.Single().Severity);
        }

        [TestMethod]
        public void SimulatedCrashes_AFinisherCanHaveCarriedOnFromOne()
        {
            var results = new List<ParticipantData>
            {
                new ParticipantData { DriverId = "D1", TeamId = "T1", Position = 1 },
                new ParticipantData { DriverId = "PLAYER", TeamId = "T3", Position = 2 },
                new ParticipantData { DriverId = "D2", TeamId = "T1", Position = 3 },
                new ParticipantData { DriverId = "D3", TeamId = "T2", Position = 4 }
            };
            // D3 hit his teammate and carried on: a crash whatever the roll, its severity rolled
            var contacts = new[] { new TeammateContact { AtFaultDriverId = "D3", OtherDriverId = "D4" } };

            // D1 a heavy shunt (under the chance), D2 nothing, D3's severity Severe; never the player
            var crashes = SimulatedIncidentGenerator.RollCrashes(results, contacts, "PLAYER", new ScriptedRandom(0.0, 0.5, 0.0));

            CollectionAssert.AreEqual(new[] { "D1", "D3" }, crashes.Select(c => c.DriverId).ToList());
            Assert.IsTrue(crashes.All(c => c.DroveOn && c.Severity == CrashSeverity.Severe));
        }

        #endregion

        #region Out of action: absences, paid rides, the paddock

        [TestMethod]
        public void ProcessAbsences_InjuredPlayer_IsNotAskedToApply()
        {
            var saveGame = CreateSaveGame();
            saveGame.Casualties = new List<Casualty> { new Casualty { DriverId = "PLAYER", Year = 1996, Kind = CasualtyKind.INJURED, RaceIdsOut = new List<int> { 1 } } };
            var absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "U1" } };
            var decisions = new Mock<IAbsenceDecisionProvider>();

            new AbsenceManager().ProcessAbsences(EntryList(saveGame), absences, saveGame, decisions.Object);

            decisions.Verify(d => d.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public void ProcessAbsences_DeadPlayer_IsNeverAskedToApply()
        {
            var saveGame = CreateSaveGame();
            saveGame.Casualties = new List<Casualty> { new Casualty { DriverId = "PLAYER", Year = 1994, Kind = CasualtyKind.DECEASED } };
            var absences = new List<Absence> { new Absence { RaceId = 3, TeamId = "T1", DriverOut = "D1", DriverIn = "U1" } };
            var decisions = new Mock<IAbsenceDecisionProvider>();

            new AbsenceManager().ProcessAbsences(EntryList(saveGame), absences, saveGame, decisions.Object);

            decisions.Verify(d => d.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public void ProcessAbsences_NobodyForTheSeat_GeneratedDriverCoversTheWholeSpell()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 1;
            var spell = Enumerable.Range(1, 3).Select(raceId => new Absence { RaceId = raceId, TeamId = "T2", DriverOut = "D3", DriverIn = "", Reason = AbsenceReason.INJURY }).ToList();
            saveGame.CurrentSeason.Absences = spell;
            var entryList = EntryList(saveGame);

            new AbsenceManager(Generator().Object).ProcessAbsences(entryList, spell.Where(a => a.RaceId == 2).ToList(), saveGame, new Mock<IAbsenceDecisionProvider>().Object);

            Assert.AreEqual("GEN1", entryList.Single(e => e.TeamId == "T2").Driver1Id);
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "GEN1"));
            Assert.AreEqual("", spell[0].DriverIn, "a race already run keeps its empty car");
            Assert.AreEqual("GEN1", spell[2].DriverIn);
        }

        [TestMethod]
        public void ProcessAbsences_PlayerLeavesAndNobodyCanCoverTheirSeat_GeneratedDriverTakesIt()
        {
            var saveGame = CreateSaveGame();
            // nobody free is at or below the player's reputation, the player's team has no reserve
            saveGame.Drivers = saveGame.Drivers.Where(d => !d.DriverId.StartsWith("U")).ToList();
            var absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T2", DriverOut = "D3", DriverIn = "" } };
            saveGame.CurrentSeason.Absences = absences;
            var decisions = new Mock<IAbsenceDecisionProvider>();
            decisions.Setup(d => d.DoesPlayerWantToApply(It.IsAny<AbsenceOpportunity>(), It.IsAny<bool>())).Returns(true);
            decisions.Setup(d => d.DoesPlayerTeamAllowLeave(It.IsAny<string>(), It.IsAny<Absence>())).Returns(true);
            var entryList = EntryList(saveGame);

            new AbsenceManager(Generator().Object).ProcessAbsences(entryList, absences, saveGame, decisions.Object);

            // GEN1 is the team's pick for D3's car, but the player outranks them; GEN2 then takes the
            // player's own seat
            Assert.AreEqual("PLAYER", entryList.Single(e => e.TeamId == "T2").Driver1Id);
            Assert.AreEqual("GEN2", entryList.Single(e => e.TeamId == "T3").Driver1Id);
        }

        [TestMethod]
        public void PaidRides_InjuredPayDriver_BuysNothing()
        {
            var saveGame = CreateSaveGame();
            saveGame.Drivers = saveGame.Drivers.Append(Driver("PAY", DriverReputation.PAY_DRIVER_SEASON)).ToList();
            saveGame.Casualties = new List<Casualty> { new Casualty { DriverId = "PAY", Year = 1996, Kind = CasualtyKind.INJURED, RaceIdsOut = new List<int> { 1 } } };

            var bought = PaidRides.BuyAiRides(saveGame, 0, new ScriptedRandom(0.0, 0.0, 0.0, 0.0));

            Assert.IsFalse(bought.Any(a => a.DriverIn == "PAY"));
        }

        [TestMethod]
        public void PaddockOpportunities_ForAnInjuredPlayer_AreEmpty()
        {
            var saveGame = CreateSaveGame();
            saveGame.PlayerData.TeamId = null;
            saveGame.Drivers.Single(d => d.DriverId == "PLAYER").Reputation = DriverReputation.PAY_DRIVER_SEASON;
            saveGame.CurrentSeason.Absences = new List<Absence> { new Absence { RaceId = 1, TeamId = "T1", DriverOut = "D1", DriverIn = "U1" } };
            saveGame.Casualties = new List<Casualty> { new Casualty { DriverId = "PLAYER", Year = 1996, Kind = CasualtyKind.INJURED, RaceIdsOut = new List<int> { 1 } } };

            var opportunities = PaddockOpportunities.For(saveGame, 0);

            Assert.AreEqual(0, opportunities.OpenAbsences.Count);
            Assert.AreEqual(0, opportunities.PaidRideOffers.Count);
        }

        #endregion

        #region End of season

        [TestMethod]
        public void ExecuteTeamDrops_DeadDriver_LeavesTheSeatAsARetiringOne()
        {
            var saveGame = CreateSaveGame();
            saveGame.Casualties = new List<Casualty> { new Casualty { DriverId = "D3", Year = 1996, Kind = CasualtyKind.DECEASED } };

            var drops = EndOfSeasonManager().ExecuteTeamDrops(saveGame, NextSeason(saveGame)).ToList();

            Assert.AreEqual(DriverFirerOutcome.DROPPED_RETIRING, drops.Single(d => d.TeamId == "T2").DropDriver1);
        }

        [TestMethod]
        public void ExecuteTeamDrops_DeadPlayer_LeavesTheSeatToo()
        {
            var saveGame = CreateSaveGame();
            saveGame.Casualties = new List<Casualty> { new Casualty { DriverId = "PLAYER", Year = 1996, Kind = CasualtyKind.DECEASED } };

            var drops = EndOfSeasonManager().ExecuteTeamDrops(saveGame, NextSeason(saveGame)).ToList();

            Assert.AreEqual(DriverFirerOutcome.DROPPED_RETIRING, drops.Single(d => d.TeamId == "T3").DropDriver1);
        }

        [TestMethod]
        public void TeamPicks_DeadDriverIsRetired_TheDeadPlayerIsNot_AndNeitherIsOnTheMarket()
        {
            var saveGame = CreateSaveGame();
            saveGame.Casualties = new List<Casualty>
            {
                new Casualty { DriverId = "D3", Year = 1996, Kind = CasualtyKind.DECEASED },
                new Casualty { DriverId = "PLAYER", Year = 1996, Kind = CasualtyKind.DECEASED }
            };
            var manager = EndOfSeasonManager();
            var nextSeason = NextSeason(saveGame);
            var drops = manager.ExecuteTeamDrops(saveGame, nextSeason).ToList();

            var ballots = manager.TeamPicksPotentialReplacementsDrivers(1997, saveGame, nextSeason.Teams, drops).ToList();

            Assert.IsTrue(saveGame.RetiredDrivers.Any(d => d.DriverId == "D3"));
            Assert.IsFalse(saveGame.RetiredDrivers.Any(d => d.DriverId == "PLAYER"));
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "PLAYER"));
            Assert.IsFalse(ballots.Any(b => b.OriginalTeamHiring.DriverId is "PLAYER" or "D3"));
            Assert.IsFalse(ballots.SelectMany(b => b.Candidates ?? Enumerable.Empty<TeamHiringBallotCandidate>()).Any(c => c.DriverId is "PLAYER" or "D3"));
        }

        [TestMethod]
        public void UpdateDriversPool_DeadDriverWithoutASeat_RetiresQuietly()
        {
            var saveGame = CreateSaveGame();
            saveGame.Casualties = new List<Casualty> { new Casualty { DriverId = "U1", Year = 1996, Kind = CasualtyKind.DECEASED } };

            EndOfSeasonManager().UpdateDriversPoolForNextSeason(1997, saveGame, new Dictionary<string, IDriverData>());

            Assert.IsTrue(saveGame.RetiredDrivers.Any(d => d.DriverId == "U1"));
            Assert.IsFalse(saveGame.Drivers.Any(d => d.DriverId == "U1"));
        }

        [TestMethod]
        public void Casualties_SurviveASaveRoundTrip()
        {
            var saveGame = CreateSaveGame();
            CrashIncidentManager.ApplyRace(saveGame, 0, Crash("D3", "T2"), FatalitiesOn, Injury());

            var json = System.Text.Json.JsonSerializer.Serialize<ISaveGame>(saveGame, DefaultJsonSerializerOptions.Instance);
            var loaded = System.Text.Json.JsonSerializer.Deserialize<ISaveGame>(json, DefaultJsonSerializerOptions.Instance);

            StringAssert.Contains(json, "\"casualties\"");
            StringAssert.Contains(json, "\"INJURY\"");
            Assert.AreEqual(CasualtyKind.INJURED, loaded.Casualties.Single().Kind);
            Assert.AreEqual(1, loaded.CurrentSeason.Absences.Single().IncidentRaceId);
            Assert.IsTrue(loaded.IsOutOfAction("D3", 2));
        }

        #endregion

        #region Helpers

        private static IEnumerable<RaceCrash> Crash(string driverId, string teamId) =>
            new[] { new RaceCrash { DriverId = driverId, TeamId = teamId, Severity = CrashSeverity.Severe } };

        private static Mock<IRandomDriverGenerator> Generator()
        {
            var generator = new Mock<IRandomDriverGenerator>();
            var generated = 0;
            generator.Setup(g => g.GenerateDriver(It.IsAny<IEnumerable<IDriverData>>(), It.IsAny<int>()))
                .Returns(() => Driver($"GEN{++generated}", DriverReputation.PAY_DRIVER_SEASON));
            return generator;
        }

        private static EndOfSeasonManager EndOfSeasonManager()
        {
            var driverHirer = new DriverHirer();
            var generator = new Mock<IRandomDriverGenerator>();
            var generated = 0;
            generator.Setup(g => g.GenerateDriver(It.IsAny<IEnumerable<IDriverData>>(), It.IsAny<int>()))
                .Returns(() => Driver($"GEN{++generated}", DriverReputation.PAY_DRIVER_SEASON));
            return new EndOfSeasonManager(new ReputationUpdater(), new OffSeasonMovements(new DriverFirer(), driverHirer), generator.Object, driverHirer);
        }

        private static ISeason NextSeason(ISaveGame saveGame) => new Season
        {
            Year = 1997,
            Teams = saveGame.CurrentSeason.Teams.Select(t => (ITeamEntry)new TeamEntry
            {
                TeamId = t.TeamId,
                TeamName = t.TeamName,
                Reputation = t.Reputation,
                Driver1Contract = new DriverContract { DriverId = t.Driver1Contract.DriverId, Races = 20 },
                Driver2Contract = new DriverContract { DriverId = t.Driver2Contract.DriverId, Races = 20 }
            }).ToList(),
            Races = saveGame.CurrentSeason.Races,
            Absences = new List<Absence>()
        };

        private static ITeamEntry Team(ISaveGame saveGame, string teamId) => saveGame.CurrentSeason.Teams.Single(t => t.TeamId == teamId);

        private static List<EntryListEntry> EntryList(ISaveGame saveGame) => saveGame.CurrentSeason.Teams.Select(t => new EntryListEntry
        {
            TeamId = t.TeamId,
            Driver1Id = t.Driver1Contract.DriverId,
            Driver2Id = t.Driver2Contract.DriverId
        }).ToList();

        // T1: D1 + D2 (top team), T2: D3 + D4 (midfield), T3: PLAYER + D5 (minnow). U1-U3 have no
        // seat. Four races, ids 1 to 4; contracts end with the season
        private static ISaveGame CreateSaveGame()
        {
            var teams = new List<ITeamEntry>
            {
                CreateTeam("T1", TeamReputation.TOP_TEAM, "D1", "D2"),
                CreateTeam("T2", TeamReputation.MIDFIELD, "D3", "D4"),
                CreateTeam("T3", TeamReputation.MINNOW, "PLAYER", "D5"),
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
                    Driver("U1", DriverReputation.PRIME_STRONG_MIDFIELD),
                    Driver("U2", DriverReputation.PRIME_MIDFIELD),
                    Driver("U3", DriverReputation.AGEING_MIDFIELD, yearOfBirth: 1962),
                },
                RetiredDrivers = new List<IDriverData>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                NextGpIndex = 0,
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = "T3" },
                Reprimands = new List<Reprimand>()
            };
        }

        private static ITeamEntry CreateTeam(string teamId, TeamReputation reputation, string driver1Id, string driver2Id) => new TeamEntry
        {
            TeamId = teamId,
            TeamName = $"Team {teamId}",
            Reputation = reputation,
            Driver1Contract = new DriverContract { DriverId = driver1Id, Races = 4, Role = ContractRole.EQUAL },
            Driver2Contract = new DriverContract { DriverId = driver2Id, Races = 4, Role = ContractRole.EQUAL }
        };

        private static IDriverData Driver(string id, DriverReputation reputation, int yearOfBirth = 1970) =>
            new DriverData { DriverId = id, Name = $"Driver {id}", YearOfBirth = yearOfBirth, Reputation = reputation };

        #endregion
    }
}
