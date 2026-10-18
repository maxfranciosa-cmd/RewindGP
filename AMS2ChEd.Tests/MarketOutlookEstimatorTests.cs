using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Moq;

namespace AMS2ChEd.Tests.Business.Services
{
    [TestClass]
    public class MarketOutlookEstimatorTests
    {
        private const int Year = 1994;

        // the estimate replays the off-season a few hundred times: chances land within a few points
        private const double Tolerance = 0.08;

        private MarketOutlookEstimator CreateEstimator()
        {
            // reputations stay as they are, so each test sets the next season's reputations directly
            var endOfSeasonManager = new Mock<IEndOfSeasonManager>();
            endOfSeasonManager
                .Setup(m => m.ProjectNewReputations(It.IsAny<ISaveGame>(), It.IsAny<int>()))
                .Returns((ISaveGame saveGame, int _) => saveGame.Drivers.ToDictionary(d => d.DriverId, d => d.Reputation));

            return new MarketOutlookEstimator(endOfSeasonManager.Object, new DriverFirer(), new DriverHirer());
        }

        [TestMethod]
        public void Estimate_PlayerUnderContract_TeamKeepsThemAndTheirSeatIsNotOffered()
        {
            var saveGame = CreateSaveGame(
                CreateTeam("PLAYER_TEAM", TeamReputation.SUPER_MINNOW, ("PLAYER", 10), ("MATE", 10)));
            AddDriver(saveGame, "PLAYER", DriverReputation.PRIME_MIDFIELD);
            AddDriver(saveGame, "MATE", DriverReputation.PAY_DRIVER_SEASON);

            var outlook = CreateEstimator().Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            Assert.AreEqual("PLAYER_TEAM", outlook.PlayerTeamId);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, outlook.PlayerContractOutcome);
            Assert.IsFalse(outlook.Seats.Any(s => s.TeamId == "PLAYER_TEAM" && s.Slot == 1));
        }

        [TestMethod]
        public void Estimate_SeatNobodyElseFits_IsVeryLikely()
        {
            var saveGame = CreateSaveGame(
                CreateTeam("PLAYER_TEAM", TeamReputation.SUPER_MINNOW, ("PLAYER", 10), ("MATE", 10)),
                // the minnow's leader is 50: certain to retire, and nobody but the player fits
                CreateTeam("MINNOW", TeamReputation.MINNOW, ("OLD", 10), ("PAYER", 10)));
            AddDriver(saveGame, "PLAYER", DriverReputation.PRIME_MIDFIELD);
            AddDriver(saveGame, "MATE", DriverReputation.PAY_DRIVER_SEASON);
            AddDriver(saveGame, "OLD", DriverReputation.AGEING_MIDFIELD, yearOfBirth: Year + 1 - 50);
            AddDriver(saveGame, "PAYER", DriverReputation.PAY_DRIVER_SEASON);

            var outlook = CreateEstimator().Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            var seat = outlook.Seats.Single(s => s.TeamId == "MINNOW" && s.Slot == 1);
            Assert.AreEqual(1, seat.OpenChance, 1e-9);
            Assert.AreEqual(1, seat.Chance, 1e-9);
            Assert.AreEqual(MarketChance.VeryLikely, seat.Band);
            Assert.AreEqual(DriverRole.FIRST_DRIVER, seat.Role);
            Assert.AreEqual(DriverHirer.DriverPolicyFit.PerfectFit, seat.PlayerFit);
            Assert.AreEqual(MarketChance.VeryLikely, outlook.HiredBand);
        }

        [TestMethod]
        public void Estimate_FreeDriverFittingBetter_TakesTheSeat()
        {
            var saveGame = CreateSaveGame(
                CreateTeam("PLAYER_TEAM", TeamReputation.SUPER_MINNOW, ("PLAYER", 10), ("MATE", 10)),
                CreateTeam("TOP", TeamReputation.TOP_TEAM, ("OLD", 10), ("STAR", 10)));
            AddDriver(saveGame, "PLAYER", DriverReputation.PRIME_MIDFIELD);
            AddDriver(saveGame, "MATE", DriverReputation.PAY_DRIVER_SEASON);
            AddDriver(saveGame, "OLD", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, yearOfBirth: Year + 1 - 50);
            AddDriver(saveGame, "STAR", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL);
            // without a team
            AddDriver(saveGame, "CHAMPION", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL);

            var outlook = CreateEstimator().Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            var seat = outlook.Seats.Single(s => s.TeamId == "TOP" && s.Slot == 1);
            Assert.AreEqual(0, seat.Chance, 1e-9);
            Assert.AreEqual(MarketChance.LongShot, seat.Band);
            CollectionAssert.Contains(seat.BetterFitRivals, "CHAMPION");
        }

        [TestMethod]
        public void Estimate_RivalAsGoodAsThePlayer_SeatIsATossUp()
        {
            // The team picks the player or the rival provisionally, evenly; whoever it picked keeps the
            // seat, as the team's own pick wins a tie
            var saveGame = CreateSaveGame(
                CreateTeam("PLAYER_TEAM", TeamReputation.SUPER_MINNOW, ("PLAYER", 10), ("MATE", 10)),
                CreateTeam("MID", TeamReputation.MIDFIELD, ("LEADER", 10), ("OLD", 10)));
            AddDriver(saveGame, "PLAYER", DriverReputation.PRIME_MIDFIELD);
            AddDriver(saveGame, "MATE", DriverReputation.PAY_DRIVER_SEASON);
            AddDriver(saveGame, "LEADER", DriverReputation.PRIME_STRONG_MIDFIELD);
            AddDriver(saveGame, "OLD", DriverReputation.PRIME_MIDFIELD, yearOfBirth: Year + 1 - 50);
            AddDriver(saveGame, "RIVAL", DriverReputation.PRIME_MIDFIELD);
            saveGame.CurrentSeason.Teams.Single(t => t.TeamId == "MID").Driver1Contract.Role = ContractRole.FIRST_DRIVER;

            var outlook = CreateEstimator().Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            var seat = outlook.Seats.Single(s => s.TeamId == "MID" && s.Slot == 2);
            Assert.AreEqual(DriverRole.SECOND_DRIVER, seat.Role);
            Assert.AreEqual(0.5, seat.Chance, Tolerance);
            CollectionAssert.Contains(seat.SameFitRivals, "RIVAL");
        }

        [TestMethod]
        public void Estimate_SeatOfADriverUnderContract_OpensOnlyIfTheyRetire()
        {
            var saveGame = CreateSaveGame(
                CreateTeam("PLAYER_TEAM", TeamReputation.SUPER_MINNOW, ("PLAYER", 10), ("MATE", 10)),
                CreateTeam("MINNOW", TeamReputation.MINNOW, ("VETERAN", 10), ("PAYER", 10)));
            AddDriver(saveGame, "PLAYER", DriverReputation.PRIME_MIDFIELD);
            AddDriver(saveGame, "MATE", DriverReputation.PAY_DRIVER_SEASON);
            AddDriver(saveGame, "VETERAN", DriverReputation.AGEING_MIDFIELD, yearOfBirth: Year + 1 - 38);
            AddDriver(saveGame, "PAYER", DriverReputation.PAY_DRIVER_SEASON);

            var outlook = CreateEstimator().Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            var seat = outlook.Seats.Single(s => s.TeamId == "MINNOW" && s.Slot == 1);
            Assert.AreEqual(EndOfSeasonManager.RetirementChance(38), seat.OpenChance, Tolerance);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, seat.ExitingDriverOutcome);
            Assert.AreEqual("VETERAN", seat.ExitingDriverId);
        }

        [TestMethod]
        public void Estimate_SameSave_GivesTheSameOutlook()
        {
            var saveGame = CreateSaveGame(
                CreateTeam("PLAYER_TEAM", TeamReputation.SUPER_MINNOW, ("PLAYER", 10), ("MATE", 10)),
                CreateTeam("MID", TeamReputation.MIDFIELD, ("LEADER", 3), ("SECOND", 3)));
            AddDriver(saveGame, "PLAYER", DriverReputation.PRIME_MIDFIELD);
            AddDriver(saveGame, "MATE", DriverReputation.PAY_DRIVER_SEASON);
            AddDriver(saveGame, "LEADER", DriverReputation.PRIME_STRONG_MIDFIELD);
            AddDriver(saveGame, "SECOND", DriverReputation.PRIME_MIDFIELD);
            AddDriver(saveGame, "RIVAL", DriverReputation.PRIME_MIDFIELD);

            var estimator = CreateEstimator();
            var first = estimator.Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());
            var second = estimator.Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            CollectionAssert.AreEqual(first.Seats.Select(s => s.Chance).ToList(), second.Seats.Select(s => s.Chance).ToList());
        }

        [TestMethod]
        public void Estimate_ManyOpenSeats_SuggestsAtMostFourApplications()
        {
            var teams = new List<ITeamEntry> { CreateTeam("PLAYER_TEAM", TeamReputation.SUPER_MINNOW, ("PLAYER", 10), ("MATE", 10)) };
            for (int i = 0; i < 4; i++)
            {
                teams.Add(CreateTeam($"MINNOW{i}", TeamReputation.MINNOW, ($"A{i}", 3), ($"B{i}", 3)));
            }
            var saveGame = CreateSaveGame(teams.ToArray());
            AddDriver(saveGame, "PLAYER", DriverReputation.PRIME_MIDFIELD);
            AddDriver(saveGame, "MATE", DriverReputation.PAY_DRIVER_SEASON);
            for (int i = 0; i < 4; i++)
            {
                AddDriver(saveGame, $"A{i}", DriverReputation.PAY_DRIVER_SEASON);
                AddDriver(saveGame, $"B{i}", DriverReputation.PAY_DRIVER_SEASON);
            }

            var outlook = CreateEstimator().Estimate(saveGame, NextSeason(saveGame), new Dictionary<string, IDriverData>());

            Assert.AreEqual(8, outlook.Seats.Count);
            Assert.AreEqual(MarketOutlookEstimator.MaxApplications, outlook.BestApplications.Count);
            var missedAll = outlook.BestApplications.Aggregate(1.0, (missed, s) => missed * (1 - s.Chance));
            Assert.AreEqual(1 - missedAll, outlook.HiredChance, 1e-9);
        }

        [TestMethod]
        public void RetirementChance_GrowsFromThirtyFourToFortyTwo()
        {
            Assert.AreEqual(0, EndOfSeasonManager.RetirementChance(33));
            Assert.AreEqual(1 / 9.0, EndOfSeasonManager.RetirementChance(34), 1e-9);
            Assert.AreEqual(5 / 9.0, EndOfSeasonManager.RetirementChance(38), 1e-9);
            Assert.AreEqual(1, EndOfSeasonManager.RetirementChance(42), 1e-9);
            Assert.AreEqual(1, EndOfSeasonManager.RetirementChance(43));
        }

        [TestMethod]
        public void ProjectNewReputations_MidSeason_CountsTheRacesRunAsTheSeasonAndLeavesTheSaveAlone()
        {
            var reputationUpdater = new Mock<IReputationUpdater>();
            reputationUpdater
                .Setup(r => r.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()))
                .Returns(DriverReputation.PRIME_STRONG_MIDFIELD);
            var endOfSeasonManager = new EndOfSeasonManager(reputationUpdater.Object, new Mock<IOffSeasonMovements>().Object, new Mock<IRandomDriverGenerator>().Object, new DriverHirer());

            var saveGame = CreateSaveGame(CreateTeam("PLAYER_TEAM", TeamReputation.MIDFIELD, ("PLAYER", 10), ("MATE", 10)));
            AddDriver(saveGame, "PLAYER", DriverReputation.PRIME_MIDFIELD);
            AddDriver(saveGame, "MATE", DriverReputation.PRIME_MIDFIELD);
            saveGame.CurrentDriverStandings = new List<HistoricalDriverStandingEntry>
            {
                new HistoricalDriverStandingEntry { DriverId = "PLAYER", Position = 1, TeamId = "PLAYER_TEAM" },
                new HistoricalDriverStandingEntry { DriverId = "MATE", Position = 2, TeamId = "PLAYER_TEAM" }
            };
            // two of the season's three races run
            saveGame.GrandPrixResults = Enumerable.Range(1, 2).Select(raceId => new GrandPrixResult
            {
                Year = Year,
                RaceResults = new List<SessionResult>
                {
                    new SessionResult { DriverId = "PLAYER", Position = 1 },
                    new SessionResult { DriverId = "MATE", Position = 2 }
                }
            }).ToList();

            var reputations = endOfSeasonManager.ProjectNewReputations(saveGame, Year + 1);

            Assert.AreEqual(DriverReputation.PRIME_STRONG_MIDFIELD, reputations["PLAYER"]);
            reputationUpdater.Verify(r => r.GetNewReputation(DriverReputation.PRIME_MIDFIELD, It.IsAny<int>(), 1, 2, 0, 2, 2, 1.0), Times.Once);
            Assert.AreEqual(DriverReputation.PRIME_MIDFIELD, saveGame.Drivers.Single(d => d.DriverId == "PLAYER").Reputation);
        }

        #region Helpers

        private static ISaveGame CreateSaveGame(params ITeamEntry[] teams)
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = Year,
                    Teams = teams.ToList(),
                    Races = new List<Race>
                    {
                        new Race { RaceId = 1, RaceName = "Test GP 1" },
                        new Race { RaceId = 2, RaceName = "Test GP 2" },
                        new Race { RaceId = 3, RaceName = "Test GP 3" }
                    },
                    Absences = new List<Absence>()
                },
                Drivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = teams.First().TeamId }
            };
        }

        // the same teams next season, with the same reputations
        private static ISeason NextSeason(ISaveGame saveGame)
        {
            return new Season
            {
                Year = Year + 1,
                Teams = saveGame.CurrentSeason.Teams.Select(t => (ITeamEntry)CreateTeam(t.TeamId, t.Reputation, (t.Driver1Contract.DriverId, 10), (t.Driver2Contract.DriverId, 10))).ToList(),
                Races = saveGame.CurrentSeason.Races,
                Absences = new List<Absence>()
            };
        }

        // races: the contract length; the season has 3 races, so 3 or fewer means it ends this season
        private static TeamEntry CreateTeam(string teamId, TeamReputation reputation, (string DriverId, int Races) driver1, (string DriverId, int Races) driver2)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                TeamName = $"Team {teamId}",
                Reputation = reputation,
                Driver1Contract = new DriverContract { DriverId = driver1.DriverId, Races = driver1.Races },
                Driver2Contract = new DriverContract { DriverId = driver2.DriverId, Races = driver2.Races }
            };
        }

        private static void AddDriver(ISaveGame saveGame, string driverId, DriverReputation reputation, int yearOfBirth = 1970)
        {
            saveGame.Drivers = saveGame.Drivers.Append(new DriverData
            {
                DriverId = driverId,
                Name = driverId,
                YearOfBirth = yearOfBirth,
                Nationality = "GBR",
                Reputation = reputation
            }).ToList();
        }

        #endregion
    }
}
