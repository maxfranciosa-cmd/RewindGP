using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class TeamOrdersEndOfSeasonTests
    {
        private EndOfSeasonManager _endOfSeasonManager;

        [TestInitialize]
        public void Setup()
        {
            var driverHirer = new DriverHirer();
            var offSeasonMovements = new OffSeasonMovements(new DriverFirer(), driverHirer);
            _endOfSeasonManager = new EndOfSeasonManager(new ReputationUpdater(), offSeasonMovements, new Mock<IRandomDriverGenerator>().Object, driverHirer);
        }

        #region Disciplinary drops

        [TestMethod]
        public void ExecuteTeamDrops_DriverWithTwoReprimands_IsDroppedForDisciplinaryReasons()
        {
            var saveGame = CreateSaveGame(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL));
            AddReprimands(saveGame, "D2", "T1", 2);

            var drops = _endOfSeasonManager.ExecuteTeamDrops(saveGame, NextSeason(saveGame)).ToList();

            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, drops.Single().DropDriver1);
            Assert.AreEqual(DriverFirerOutcome.DROPPED_DISCIPLINARY, drops.Single().DropDriver2);
        }

        [TestMethod]
        public void ExecuteTeamDrops_DriverWithOneReprimand_IsNotDropped()
        {
            var saveGame = CreateSaveGame(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL));
            AddReprimands(saveGame, "D2", "T1", 1);

            var drops = _endOfSeasonManager.ExecuteTeamDrops(saveGame, NextSeason(saveGame)).ToList();

            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, drops.Single().DropDriver2);
        }

        [TestMethod]
        public void ExecuteTeamDrops_ChampionWithTwoReprimands_IsNotDropped()
        {
            var saveGame = CreateSaveGame(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL));
            AddReprimands(saveGame, "D2", "T1", 2);
            saveGame.CurrentDriverStandings = new List<HistoricalDriverStandingEntry>
            {
                new HistoricalDriverStandingEntry { DriverId = "D2", Position = 1 },
                new HistoricalDriverStandingEntry { DriverId = "D1", Position = 2 }
            };

            var drops = _endOfSeasonManager.ExecuteTeamDrops(saveGame, NextSeason(saveGame)).ToList();

            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, drops.Single().DropDriver2);
        }

        [TestMethod]
        public void ExecuteTeamDrops_RetiringDriverWithReprimands_StaysRetiring()
        {
            var saveGame = CreateSaveGame(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, yearOfBirth: 1960));
            AddReprimands(saveGame, "D2", "T1", 2);

            var drops = _endOfSeasonManager.ExecuteTeamDrops(saveGame, NextSeason(saveGame)).ToList();

            Assert.AreEqual(DriverFirerOutcome.DROPPED_RETIRING, drops.Single().DropDriver2);
        }

        [TestMethod]
        public void StartNewSeason_ClearsReprimands()
        {
            var saveGame = CreateSaveGame(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL));
            AddReprimands(saveGame, "D2", "T1", 2);

            _endOfSeasonManager.StartNewSeason(saveGame, NextSeason(saveGame));

            Assert.AreEqual(0, saveGame.Reprimands.Count);
        }

        #endregion

        #region Job ad roles

        [TestMethod]
        public void TeamPicks_StayingFirstDriver_TeamLooksForSecondDriver()
        {
            var ad = GetSingleAd(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                stayingDriverReputation: DriverReputation.PRIME_MIDFIELD,
                dropDriver1: false);

            Assert.AreEqual(2, ad.Slot);
            Assert.AreEqual(DriverRole.SECOND_DRIVER, ad.Role);
        }

        [TestMethod]
        public void TeamPicks_StayingSecondDriverInSlotOne_TeamLooksForFirstDriverInSlotTwo()
        {
            var ad = GetSingleAd(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.SECOND_DRIVER, "D2", ContractRole.FIRST_DRIVER),
                stayingDriverReputation: DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL,
                dropDriver1: false);

            Assert.AreEqual(2, ad.Slot);
            Assert.AreEqual(DriverRole.FIRST_DRIVER, ad.Role);
        }

        [TestMethod]
        [DataRow(TeamReputation.TOP_TEAM, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverRole.SECOND_DRIVER)]
        [DataRow(TeamReputation.TOP_TEAM, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, DriverRole.FIRST_DRIVER)]
        [DataRow(TeamReputation.MIDFIELD, DriverReputation.JUST_ONE_LAST_DANCE, DriverRole.SECOND_DRIVER)]
        [DataRow(TeamReputation.MIDFIELD, DriverReputation.AGEING_STRONG_MIDFIELD, DriverRole.FIRST_DRIVER)]
        [DataRow(TeamReputation.SUPER_MINNOW, DriverReputation.YOUNG_TALENT, DriverRole.SECOND_DRIVER)]
        [DataRow(TeamReputation.SUPER_MINNOW, DriverReputation.PAY_DRIVER_SEASON, DriverRole.FIRST_DRIVER)]
        public void TeamPicks_StayingEqualDriver_RoleDependsOnReputationThreshold(TeamReputation teamReputation, DriverReputation stayingDriverReputation, DriverRole expectedAdRole)
        {
            var ad = GetSingleAd(
                Team("T1", teamReputation, "D1", ContractRole.EQUAL, "D2", ContractRole.EQUAL),
                stayingDriverReputation: stayingDriverReputation,
                dropDriver1: true);

            Assert.AreEqual(1, ad.Slot);
            Assert.AreEqual(expectedAdRole, ad.Role);
        }

        [TestMethod]
        public void TeamPicks_BothSeatsOpen_SlotOneFirstSlotTwoSecond()
        {
            var team = Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.EQUAL, "D2", ContractRole.EQUAL);
            var saveGame = CreateSaveGame(team,
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("U1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("U2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL));
            var drops = new List<DropTeamResult>
            {
                new DropTeamResult { TeamId = "T1", DropDriver1 = DriverFirerOutcome.DROPPED_UNDERPERFORMING, DropDriver2 = DriverFirerOutcome.DROPPED_UNDERPERFORMING }
            };

            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(2025, saveGame, NextSeason(saveGame).Teams, drops).ToList();

            Assert.AreEqual(DriverRole.FIRST_DRIVER, ballots.Single(b => b.OriginalTeamHiring.Slot == 1).OriginalTeamHiring.Role);
            Assert.AreEqual(DriverRole.SECOND_DRIVER, ballots.Single(b => b.OriginalTeamHiring.Slot == 2).OriginalTeamHiring.Role);
        }

        #endregion

        #region Roles after hiring

        [TestMethod]
        public void GenerateNewSeason_HiredSecondDriver_KeepsHierarchy()
        {
            var saveGame = CreateSaveGame(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_MIDFIELD),
                Driver("U1", DriverReputation.PRIME_STRONG_MIDFIELD));

            var team = GenerateNewSeason(saveGame, Hiring("U1", DriverReputation.PRIME_STRONG_MIDFIELD, slot: 2, DriverRole.SECOND_DRIVER));

            Assert.AreEqual("D1", team.Driver1Contract.DriverId);
            Assert.AreEqual(ContractRole.FIRST_DRIVER, team.Driver1Contract.Role);
            Assert.AreEqual("U1", team.Driver2Contract.DriverId);
            Assert.AreEqual(ContractRole.SECOND_DRIVER, team.Driver2Contract.Role);
        }

        [TestMethod]
        public void GenerateNewSeason_HiringLandsInItsSlot_AndBetterReputationLeads()
        {
            // the slot-1 seat was advertised as FIRST_DRIVER, but the driver staying in slot 2 has
            // the better reputation, so they end up leading the team
            var saveGame = CreateSaveGame(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL),
                Driver("U1", DriverReputation.PRIME_STRONG_MIDFIELD));

            var team = GenerateNewSeason(saveGame, Hiring("U1", DriverReputation.PRIME_STRONG_MIDFIELD, slot: 1, DriverRole.FIRST_DRIVER));

            Assert.AreEqual("U1", team.Driver1Contract.DriverId);
            Assert.AreEqual(ContractRole.SECOND_DRIVER, team.Driver1Contract.Role);
            Assert.AreEqual("D2", team.Driver2Contract.DriverId);
            Assert.AreEqual(ContractRole.FIRST_DRIVER, team.Driver2Contract.Role);
        }

        [TestMethod]
        public void GenerateNewSeason_EqualReputations_HiredDriverTakesTheAdvertisedRole()
        {
            var saveGame = CreateSaveGame(
                Team("T1", TeamReputation.MIDFIELD, "D1", ContractRole.EQUAL, "D2", ContractRole.EQUAL),
                Driver("D1", DriverReputation.PRIME_MIDFIELD),
                Driver("D2", DriverReputation.PRIME_MIDFIELD),
                Driver("U1", DriverReputation.PRIME_MIDFIELD));

            var team = GenerateNewSeason(saveGame, Hiring("U1", DriverReputation.PRIME_MIDFIELD, slot: 2, DriverRole.FIRST_DRIVER));

            Assert.AreEqual(ContractRole.SECOND_DRIVER, team.Driver1Contract.Role);
            Assert.AreEqual(ContractRole.FIRST_DRIVER, team.Driver2Contract.Role);
        }

        [TestMethod]
        public void GenerateNewSeason_TeamWithNoHirings_KeepsCurrentRolesNotThePackOnes()
        {
            var saveGame = CreateSaveGame(
                Team("T1", TeamReputation.TOP_TEAM, "D1", ContractRole.FIRST_DRIVER, "D2", ContractRole.SECOND_DRIVER),
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_MIDFIELD));

            var team = GenerateNewSeason(saveGame);

            Assert.AreEqual(ContractRole.FIRST_DRIVER, team.Driver1Contract.Role);
            Assert.AreEqual(ContractRole.SECOND_DRIVER, team.Driver2Contract.Role);
        }

        #endregion

        #region Helpers

        private TeamJobAdResult GetSingleAd(ITeamEntry team, DriverReputation stayingDriverReputation, bool dropDriver1)
        {
            var droppedId = dropDriver1 ? team.Driver1Contract.DriverId : team.Driver2Contract.DriverId;
            var stayingId = dropDriver1 ? team.Driver2Contract.DriverId : team.Driver1Contract.DriverId;

            var saveGame = CreateSaveGame(team,
                Driver(droppedId, DriverReputation.PRIME_MIDFIELD),
                Driver(stayingId, stayingDriverReputation),
                // candidates willing to join teams from every tier
                Driver("U1", DriverReputation.PRIME_STRONG_MIDFIELD),
                Driver("U2", DriverReputation.PAY_DRIVER_SEASON));

            var drops = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = team.TeamId,
                    DropDriver1 = dropDriver1 ? DriverFirerOutcome.DROPPED_UNDERPERFORMING : DriverFirerOutcome.NOT_DROPPED,
                    DropDriver2 = dropDriver1 ? DriverFirerOutcome.NOT_DROPPED : DriverFirerOutcome.DROPPED_UNDERPERFORMING
                }
            };

            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(2025, saveGame, NextSeason(saveGame).Teams, drops).ToList();

            var hiring = ballots.Single().OriginalTeamHiring;
            return new TeamJobAdResult { Slot = hiring.Slot, Role = hiring.Role };
        }

        private class TeamJobAdResult
        {
            public int Slot { get; set; }
            public DriverRole Role { get; set; }
        }

        private ITeamEntry GenerateNewSeason(ISaveGame saveGame, params TeamHiring[] hirings)
        {
            var nextSeason = NextSeason(saveGame);
            var ballots = hirings.Select(h => new TeamHiringBallot
            {
                OriginalTeamHiring = h,
                Candidates = new List<TeamHiringBallotCandidate>()
            });

            var newSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, nextSeason, ballots);
            return newSeason.Teams.Single();
        }

        private static TeamHiring Hiring(string driverId, DriverReputation reputation, int slot, DriverRole role)
        {
            return new TeamHiring
            {
                TeamId = "T1",
                DriverId = driverId,
                DriverReputation = reputation,
                TeamReputation = TeamReputation.TOP_TEAM,
                Role = role,
                Slot = slot,
                ExcludeDriverIds = new HashSet<string>(),
                OtherPotentialCandidates = new List<DriverResume>()
            };
        }

        // the next season comes from the season pack, where every contract is EQUAL
        private static ISeason NextSeason(ISaveGame saveGame)
        {
            var teams = saveGame.CurrentSeason.Teams
                .Select(t => Team(t.TeamId, t.Reputation, t.Driver1Contract.DriverId, ContractRole.EQUAL, t.Driver2Contract.DriverId, ContractRole.EQUAL))
                .ToList();

            return new Season
            {
                Year = saveGame.CurrentSeason.Year + 1,
                Teams = teams,
                Races = saveGame.CurrentSeason.Races.ToList(),
                Absences = new List<Absence>()
            };
        }

        private static ISaveGame CreateSaveGame(ITeamEntry team, params IDriverData[] drivers)
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = 2024,
                    Teams = new List<ITeamEntry> { team },
                    Races = Enumerable.Range(1, 3).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList(),
                    Absences = new List<Absence>()
                },
                Drivers = drivers.ToList(),
                RetiredDrivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player" },
                Reprimands = new List<Reprimand>()
            };
        }

        private static ITeamEntry Team(string teamId, TeamReputation reputation, string driver1Id, ContractRole driver1Role, string driver2Id, ContractRole driver2Role)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                TeamName = $"Team {teamId}",
                Reputation = reputation,
                Driver1Contract = new DriverContract { DriverId = driver1Id, Races = 100, Role = driver1Role },
                Driver2Contract = new DriverContract { DriverId = driver2Id, Races = 100, Role = driver2Role }
            };
        }

        private static IDriverData Driver(string id, DriverReputation reputation, int yearOfBirth = 2000)
        {
            return new DriverData { DriverId = id, Name = $"Driver {id}", YearOfBirth = yearOfBirth, Reputation = reputation };
        }

        private static void AddReprimands(ISaveGame saveGame, string driverId, string teamId, int count)
        {
            for (var i = 0; i < count; i++)
            {
                saveGame.Reprimands.Add(new Reprimand { DriverId = driverId, TeamId = teamId, RaceId = i + 1, Reason = ReprimandReason.FINISHED_AHEAD_OF_FIRST_DRIVER });
            }
        }

        #endregion
    }
}
