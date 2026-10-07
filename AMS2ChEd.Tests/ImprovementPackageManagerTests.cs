using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class ImprovementPackageManagerTests
    {
        private const double TOLERANCE = 0.0001;

        #region Who may decide

        [TestMethod]
        public void CanDriverInstall_FirstDriver_IsAllowed()
        {
            var saveGame = CreateSaveGame();

            Assert.IsTrue(CreateManager().CanDriverInstall(saveGame, "D1"));
        }

        [TestMethod]
        public void CanDriverInstall_SecondDriver_IsNotAllowed()
        {
            var saveGame = CreateSaveGame();

            Assert.IsFalse(CreateManager().CanDriverInstall(saveGame, "D2"));
        }

        [TestMethod]
        public void CanDriverInstall_EqualDrivers_AreBothAllowed()
        {
            var saveGame = CreateSaveGame();
            var manager = CreateManager();

            Assert.IsTrue(manager.CanDriverInstall(saveGame, "D3"));
            Assert.IsTrue(manager.CanDriverInstall(saveGame, "D4"));
        }

        [TestMethod]
        public void CanDriverInstall_FirstDriverAbsentForNextRace_SecondDriverDecidesInstead()
        {
            var saveGame = CreateSaveGame();
            AddAbsence(saveGame, raceId: 1, teamId: "T1", driverOut: "D1", driverIn: "U1");
            var manager = CreateManager();

            Assert.IsFalse(manager.CanDriverInstall(saveGame, "D1"));
            Assert.IsFalse(manager.CanDriverInstall(saveGame, "U1"));
            Assert.IsTrue(manager.CanDriverInstall(saveGame, "D2"));
            Assert.IsTrue(manager.IsSecondDriverCoveringForAbsentLeader(saveGame, "D2"));
        }

        [TestMethod]
        public void CanDriverInstall_FirstDriverAbsentForAnotherRace_SecondDriverStillNotAllowed()
        {
            var saveGame = CreateSaveGame();
            AddAbsence(saveGame, raceId: 2, teamId: "T1", driverOut: "D1", driverIn: "U1");
            var manager = CreateManager();

            Assert.IsTrue(manager.CanDriverInstall(saveGame, "D1"));
            Assert.IsFalse(manager.CanDriverInstall(saveGame, "D2"));
            Assert.IsFalse(manager.IsSecondDriverCoveringForAbsentLeader(saveGame, "D2"));
        }

        [TestMethod]
        public void CanDriverInstall_EqualDriverAbsent_TeammateStillDecides()
        {
            var saveGame = CreateSaveGame();
            AddAbsence(saveGame, raceId: 1, teamId: "T2", driverOut: "D3", driverIn: "U1");
            var manager = CreateManager();

            Assert.IsFalse(manager.CanDriverInstall(saveGame, "D3"));
            Assert.IsTrue(manager.CanDriverInstall(saveGame, "D4"));
            Assert.IsFalse(manager.IsSecondDriverCoveringForAbsentLeader(saveGame, "D4"));
        }

        #endregion

        #region Installing

        [TestMethod]
        public void Install_StoresPackageOnTeamWithDriverRaceAndValues()
        {
            var saveGame = CreateSaveGame();

            var package = CreateManager().Install(saveGame, "T1", "D1");

            var team = Team(saveGame, "T1");
            Assert.AreSame(package, team.ImprovementPackages.Single());
            Assert.AreEqual("D1", package.DriverId);
            Assert.AreEqual(1, package.RaceId);
            Assert.AreEqual(0.01, package.Values["stat"], TOLERANCE);
        }

        [TestMethod]
        public void Install_SecondPackageForSameRace_IsRefused()
        {
            var saveGame = CreateSaveGame();
            var manager = CreateManager();

            manager.Install(saveGame, "T2", "D3");

            Assert.IsFalse(manager.CanDriverInstall(saveGame, "D4"));
            Assert.IsNull(manager.Install(saveGame, "T2", "D4"));
            Assert.AreEqual(1, Team(saveGame, "T2").ImprovementPackages.Count);
        }

        [TestMethod]
        public void Install_ThreePackagesInSeason_NoneLeftAfterwards()
        {
            var saveGame = CreateSaveGame();
            var manager = CreateManager();

            for (int raceIndex = 0; raceIndex < ImprovementPackageManager.PACKAGES_PER_SEASON; raceIndex++)
            {
                saveGame.NextGpIndex = raceIndex;
                Assert.IsNotNull(manager.Install(saveGame, "T1", "D1"));
            }

            saveGame.NextGpIndex = ImprovementPackageManager.PACKAGES_PER_SEASON;

            Assert.AreEqual(0, manager.GetRemainingPackages(Team(saveGame, "T1")));
            Assert.IsFalse(manager.CanDriverInstall(saveGame, "D1"));
            Assert.IsNull(manager.Install(saveGame, "T1", "D1"));
        }

        [TestMethod]
        public void GetPackagesForRace_ReturnsOnlyPackagesInstalledForThatRace()
        {
            var saveGame = CreateSaveGame();
            var manager = CreateManager();

            manager.Install(saveGame, "T1", "D1");
            saveGame.NextGpIndex = 1;
            manager.Install(saveGame, "T2", "D3");

            var packages = manager.GetPackagesForRace(saveGame, 2);

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual("T2", packages[0].TeamId);
            Assert.AreEqual("D3", packages[0].Package.DriverId);
        }

        #endregion

        #region AI decisions

        [TestMethod]
        public void ProcessAiDecisions_BeforeThirdRound_NobodyInstalls()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = ImprovementPackageManager.FIRST_RACE_INDEX_FOR_AI - 1;

            var installed = CreateManager(roll: 0.0).ProcessAiDecisions(saveGame);

            Assert.AreEqual(0, installed.Count);
        }

        [TestMethod]
        public void ProcessAiDecisions_WinningRoll_EveryTeamInstallsOnAnAiDriversInstruction()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 3;

            var installed = CreateManager(roll: 0.0).ProcessAiDecisions(saveGame);

            Assert.AreEqual(3, installed.Count);
            Assert.AreEqual("D1", installed.Single(i => i.TeamId == "T1").Package.DriverId);
            // the player never decides through the AI: their equal teammate does
            Assert.AreEqual("D5", installed.Single(i => i.TeamId == "T3").Package.DriverId);
            Assert.IsTrue(installed.All(i => i.Package.RaceId == 4));
        }

        [TestMethod]
        public void ProcessAiDecisions_LosingRoll_NobodyInstalls()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 3;

            var installed = CreateManager(roll: 0.99).ProcessAiDecisions(saveGame);

            Assert.AreEqual(0, installed.Count);
        }

        [TestMethod]
        public void ProcessAiDecisions_PlayerAlreadyInstalledForTheRace_TeammateDoesNotAddAnother()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 3;
            var manager = CreateManager(roll: 0.0);
            manager.Install(saveGame, "T3", "PLAYER");

            var installed = manager.ProcessAiDecisions(saveGame);

            Assert.IsFalse(installed.Any(i => i.TeamId == "T3"));
            Assert.AreEqual(1, Team(saveGame, "T3").ImprovementPackages.Count);
        }

        [TestMethod]
        public void ProcessAiDecisions_PlayerIsTheOnlyDecider_TeamDoesNotInstall()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 3;
            var playerTeam = Team(saveGame, "T3");
            playerTeam.Driver1Contract.Role = ContractRole.FIRST_DRIVER;
            playerTeam.Driver2Contract.Role = ContractRole.SECOND_DRIVER;

            var installed = CreateManager(roll: 0.0).ProcessAiDecisions(saveGame);

            Assert.IsFalse(installed.Any(i => i.TeamId == "T3"));
        }

        [TestMethod]
        public void GetInstallChance_TeamBelowExpectation_GrowsWithTheGap()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 3;

            // T1 is a top team (expected 1st) running 3rd: two positions below expectation
            var chance = CreateManager().GetInstallChance(saveGame, Team(saveGame, "T1"));

            Assert.AreEqual(
                ImprovementPackageManager.BASE_CHANCE + 2 * ImprovementPackageManager.CHANCE_PER_POSITION_BELOW_EXPECTATION,
                chance, TOLERANCE);
        }

        [TestMethod]
        public void GetInstallChance_TeamAboveExpectation_IsContent()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 3;

            // T3 is a minnow (expected 3rd) leading the championship comfortably
            var chance = CreateManager().GetInstallChance(saveGame, Team(saveGame, "T3"));

            Assert.AreEqual(ImprovementPackageManager.CONTENT_TEAM_CHANCE, chance, TOLERANCE);
        }

        [TestMethod]
        public void GetInstallChance_TeamBehindWithinARaceWin_AddsThreatBonus()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 3;
            // a win is worth 10 points in this season
            saveGame.CurrentConstructorStandings.Single(s => s.TeamId == "T2").Points = 90;

            var chance = CreateManager().GetInstallChance(saveGame, Team(saveGame, "T3"));

            Assert.AreEqual(
                ImprovementPackageManager.CONTENT_TEAM_CHANCE + ImprovementPackageManager.THREAT_CHANCE_BONUS,
                chance, TOLERANCE);
        }

        [TestMethod]
        public void GetInstallChance_TeamBehindMoreThanARaceWinAway_NoThreatBonus()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 3;
            saveGame.CurrentConstructorStandings.Single(s => s.TeamId == "T2").Points = 89;

            var chance = CreateManager().GetInstallChance(saveGame, Team(saveGame, "T3"));

            Assert.AreEqual(ImprovementPackageManager.CONTENT_TEAM_CHANCE, chance, TOLERANCE);
        }

        [TestMethod]
        public void GetInstallChance_LateInTheSeason_AddsUseItOrLoseItBonus()
        {
            var saveGame = CreateSaveGame();
            saveGame.NextGpIndex = 6;

            var chance = CreateManager().GetInstallChance(saveGame, Team(saveGame, "T3"));

            Assert.AreEqual(
                ImprovementPackageManager.CONTENT_TEAM_CHANCE + ImprovementPackageManager.LATE_SEASON_CHANCE_BONUS,
                chance, TOLERANCE);
        }

        [TestMethod]
        public void GetInstallChance_InstalledAtRecentRaces_IsCooledDown()
        {
            var manager = CreateManager();
            var expectationChance = ImprovementPackageManager.BASE_CHANCE + 2 * ImprovementPackageManager.CHANCE_PER_POSITION_BELOW_EXPECTATION;

            var installedLastRace = CreateSaveGame();
            installedLastRace.NextGpIndex = 2;
            manager.Install(installedLastRace, "T1", "D1");
            installedLastRace.NextGpIndex = 3;

            Assert.AreEqual(
                expectationChance * ImprovementPackageManager.INSTALLED_LAST_RACE_MULTIPLIER,
                manager.GetInstallChance(installedLastRace, Team(installedLastRace, "T1")), TOLERANCE);

            var installedTwoRacesAgo = CreateSaveGame();
            installedTwoRacesAgo.NextGpIndex = 1;
            manager.Install(installedTwoRacesAgo, "T1", "D1");
            installedTwoRacesAgo.NextGpIndex = 3;

            Assert.AreEqual(
                expectationChance * ImprovementPackageManager.INSTALLED_TWO_RACES_AGO_MULTIPLIER,
                manager.GetInstallChance(installedTwoRacesAgo, Team(installedTwoRacesAgo, "T1")), TOLERANCE);
        }

        #endregion

        #region Helpers

        private static ImprovementPackageManager CreateManager(double roll = 0.5)
        {
            return new ImprovementPackageManager(new FakeProvider(), new FixedRandom(roll));
        }

        private static ITeamEntry Team(ISaveGame saveGame, string teamId)
        {
            return saveGame.CurrentSeason.Teams.Single(t => t.TeamId == teamId);
        }

        private static void AddAbsence(ISaveGame saveGame, int raceId, string teamId, string driverOut, string driverIn)
        {
            saveGame.CurrentSeason.Absences = saveGame.CurrentSeason.Absences
                .Append(new Absence { RaceId = raceId, TeamId = teamId, DriverOut = driverOut, DriverIn = driverIn })
                .ToList();
        }

        // T1: D1 (FIRST) + D2 (SECOND), T2: D3 + D4 (EQUAL), T3: PLAYER + D5 (EQUAL).
        // Eight races. The standings are upside down compared to the teams' reputations, with
        // big points gaps: T3 (minnow) leads, T1 (top team) is last.
        private static ISaveGame CreateSaveGame()
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
                    PointsSystem = new Dictionary<string, int> { { "1", 10 }, { "2", 6 }, { "3", 4 } },
                    Teams = teams,
                    Races = Enumerable.Range(1, 8).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList(),
                    Absences = new List<Absence>()
                },
                NextGpIndex = 0,
                CurrentConstructorStandings = new List<ConstructorStandingEntry>
                {
                    new ConstructorStandingEntry { TeamId = "T3", Position = 1, Points = 100 },
                    new ConstructorStandingEntry { TeamId = "T2", Position = 2, Points = 50 },
                    new ConstructorStandingEntry { TeamId = "T1", Position = 3, Points = 10 },
                },
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = "T3" }
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

        private class FakeProvider : IImprovementPackageProvider
        {
            public IReadOnlyList<ImprovementStat> Stats => new[]
            {
                new ImprovementStat("stat", "Stat", true, "stat improved", "stat worsened")
            };

            public Dictionary<string, double> GenerateValues(Random random)
            {
                return new Dictionary<string, double> { { "stat", 0.01 } };
            }
        }

        // always rolls the same value, and always picks the first candidate
        private class FixedRandom : Random
        {
            private readonly double _roll;

            public FixedRandom(double roll)
            {
                _roll = roll;
            }

            public override double NextDouble() => _roll;

            public override int Next(int maxValue) => 0;
        }

        #endregion
    }
}
