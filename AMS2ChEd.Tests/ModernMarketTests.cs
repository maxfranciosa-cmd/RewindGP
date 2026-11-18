using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Moq;
using System.Text.Json;

namespace AMS2ChEd.Tests
{
    // Phase 1 of the modern driver market: from ModernMarket.Year nobody is a wild card and race
    // seats aren't sold race by race; older seasons are unchanged.
    [TestClass]
    public class ModernMarketTests
    {
        private const int OldYear = 1995;
        private const int ModernYear = 2020;

        #region Clamp

        [TestMethod]
        public void Clamp_ModernSeason_WildCardBecomesSeason()
        {
            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, ModernMarket.Clamp(DriverReputation.PAY_DRIVER_WILD_CARD, ModernMarket.Year));
        }

        [TestMethod]
        public void Clamp_SeasonBeforeThreshold_WildCardStays()
        {
            Assert.AreEqual(DriverReputation.PAY_DRIVER_WILD_CARD, ModernMarket.Clamp(DriverReputation.PAY_DRIVER_WILD_CARD, ModernMarket.Year - 1));
        }

        [TestMethod]
        public void Clamp_OtherReputations_AreUnchanged()
        {
            foreach (var reputation in Enum.GetValues<DriverReputation>().Where(r => r != DriverReputation.PAY_DRIVER_WILD_CARD))
            {
                Assert.AreEqual(reputation, ModernMarket.Clamp(reputation, ModernYear));
                Assert.AreEqual(reputation, ModernMarket.Clamp(reputation, OldYear));
            }
        }

        #endregion

        #region Player creation

        [TestMethod]
        public void AvailableReputationForAge_ModernSeason_NeverWildCard()
        {
            for (int age = ReputationUpdater.MINIMUM_AGE; age <= 45; age++)
            {
                var reputations = ReputationUpdater.AvailableReputationForAge(age, ModernYear).ToList();
                CollectionAssert.DoesNotContain(reputations, DriverReputation.PAY_DRIVER_WILD_CARD, $"age {age}");
                CollectionAssert.Contains(reputations, DriverReputation.PAY_DRIVER_SEASON, $"age {age}");
            }
        }

        [TestMethod]
        public void AvailableReputationForAge_OldSeasonOrNoSeason_Unchanged()
        {
            // the last season before the modern market: the modern age bands (AgeEras), as with no
            // year, but wild cards still exist
            var lastPreModernYear = ModernMarket.Year - 1;
            for (int age = ReputationUpdater.MINIMUM_AGE; age <= 45; age++)
            {
                var withoutYear = ReputationUpdater.AvailableReputationForAge(age).ToList();
                CollectionAssert.Contains(withoutYear, DriverReputation.PAY_DRIVER_WILD_CARD, $"age {age}");
                CollectionAssert.Contains(ReputationUpdater.AvailableReputationForAge(age, OldYear).ToList(), DriverReputation.PAY_DRIVER_WILD_CARD, $"age {age}");
                CollectionAssert.AreEqual(withoutYear, ReputationUpdater.AvailableReputationForAge(age, lastPreModernYear).ToList(), $"age {age}");
            }
        }

        #endregion

        #region End-of-season reputations and retirement

        [TestMethod]
        public void InactivePaySeasonDriver_ModernSeason_StaysPaySeason()
        {
            var saveGame = CreateSaveGameWithIdleDriver(ModernYear - 1, DriverReputation.PAY_DRIVER_SEASON, age: 26);

            CreateEndOfSeasonManager().UpdateDriversPoolForNextSeason(ModernYear, saveGame, new Dictionary<string, IDriverData>());

            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, saveGame.Drivers.Single(d => d.DriverId == "idle").Reputation);
            Assert.IsFalse(saveGame.RetiredDrivers.Any());
        }

        [TestMethod]
        public void InactivePaySeasonDriver_OldSeason_BecomesWildCard()
        {
            var saveGame = CreateSaveGameWithIdleDriver(OldYear - 1, DriverReputation.PAY_DRIVER_SEASON, age: 26);

            CreateEndOfSeasonManager().UpdateDriversPoolForNextSeason(OldYear, saveGame, new Dictionary<string, IDriverData>());

            Assert.AreEqual(DriverReputation.PAY_DRIVER_WILD_CARD, saveGame.Drivers.Single(d => d.DriverId == "idle").Reputation);
        }

        [TestMethod]
        [DataRow(OldYear)]
        [DataRow(ModernYear)]
        public void InactivePaySeasonDriverAged32_Retires(int nextSeasonYear)
        {
            var saveGame = CreateSaveGameWithIdleDriver(nextSeasonYear - 1, DriverReputation.PAY_DRIVER_SEASON, age: 32);

            CreateEndOfSeasonManager().UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, new Dictionary<string, IDriverData>());

            Assert.IsFalse(saveGame.Drivers.Any(d => d.DriverId == "idle"));
            Assert.IsTrue(saveGame.RetiredDrivers.Any(d => d.DriverId == "idle"));
        }

        [TestMethod]
        [DataRow(OldYear)]
        [DataRow(ModernYear)]
        public void InactiveAgeingMidfielderAged32_DropsToPaySeason_RetiresTheWinterAfter(int nextSeasonYear)
        {
            var saveGame = CreateSaveGameWithIdleDriver(nextSeasonYear - 1, DriverReputation.AGEING_MIDFIELD, age: 32);
            var manager = CreateEndOfSeasonManager();

            manager.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, new Dictionary<string, IDriverData>());

            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, saveGame.Drivers.Single(d => d.DriverId == "idle").Reputation);
            Assert.IsFalse(saveGame.RetiredDrivers.Any());

            // one more idle winter
            saveGame.CurrentSeason.Year = nextSeasonYear;
            manager.UpdateDriversPoolForNextSeason(nextSeasonYear + 1, saveGame, new Dictionary<string, IDriverData>());

            Assert.IsTrue(saveGame.RetiredDrivers.Any(d => d.DriverId == "idle"));
        }

        [TestMethod]
        public void RookieListedAsWildCard_ModernSeason_ComesInAsPaySeason()
        {
            var saveGame = CreateSaveGameWithIdleDriver(ModernYear - 1, DriverReputation.PRIME_MIDFIELD, age: 26);
            var rookies = new Dictionary<string, IDriverData>
            {
                { "rookie", new DriverData { DriverId = "rookie", Name = "Rookie", YearOfBirth = ModernYear - 20, Reputation = DriverReputation.PAY_DRIVER_WILD_CARD } }
            };

            CreateEndOfSeasonManager().UpdateDriversPoolForNextSeason(ModernYear, saveGame, rookies);

            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, saveGame.Drivers.Single(d => d.DriverId == "rookie").Reputation);
        }

        [TestMethod]
        public void RookieListedAsWildCard_OldSeason_StaysWildCard()
        {
            var saveGame = CreateSaveGameWithIdleDriver(OldYear - 1, DriverReputation.PRIME_MIDFIELD, age: 26);
            var rookies = new Dictionary<string, IDriverData>
            {
                { "rookie", new DriverData { DriverId = "rookie", Name = "Rookie", YearOfBirth = OldYear - 20, Reputation = DriverReputation.PAY_DRIVER_WILD_CARD } }
            };

            CreateEndOfSeasonManager().UpdateDriversPoolForNextSeason(OldYear, saveGame, rookies);

            Assert.AreEqual(DriverReputation.PAY_DRIVER_WILD_CARD, saveGame.Drivers.Single(d => d.DriverId == "rookie").Reputation);
        }

        [TestMethod]
        public void ProjectNewReputations_ModernSeason_NoWildCard()
        {
            var saveGame = CreateSaveGameWithIdleDriver(ModernYear - 1, DriverReputation.PAY_DRIVER_SEASON, age: 26);

            var projected = CreateEndOfSeasonManager().ProjectNewReputations(saveGame, ModernYear);

            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, projected["idle"]);
        }

        #endregion

        #region Drops

        [TestMethod]
        public void MidfieldTeam_DropsPaySeasonDriver_OnlyInModernSeasons()
        {
            var firer = new DriverFirer();

            Assert.AreEqual(DriverFirerOutcome.DROPPED_UNDERPERFORMING, firer.WillDropDriver(TeamReputation.MIDFIELD, DriverReputation.PAY_DRIVER_SEASON, 10, false, false, ModernYear));
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, firer.WillDropDriver(TeamReputation.MIDFIELD, DriverReputation.PAY_DRIVER_SEASON, 10, false, false, OldYear));
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, firer.WillDropDriver(TeamReputation.MIDFIELD, DriverReputation.PAY_DRIVER_SEASON, 10, false, false));
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, firer.WillDropDriver(TeamReputation.MIDFIELD, DriverReputation.YOUNG_TALENT, 10, false, false, ModernYear));
        }

        [TestMethod]
        public void DropDrivers_PassesTheSeasonYear()
        {
            var movements = new OffSeasonMovements(new DriverFirer(), new DriverHirer());
            var team = new TeamSituation
            {
                TeamId = "mid",
                Reputation = TeamReputation.MIDFIELD,
                Driver1 = new DriverSituation { DriverId = "a", Reputation = DriverReputation.PAY_DRIVER_SEASON, RacesLeftInContract = 10 },
                Driver2 = new DriverSituation { DriverId = "b", Reputation = DriverReputation.PRIME_MIDFIELD, RacesLeftInContract = 10 }
            };

            var modern = movements.DropDrivers(new[] { team }, ModernYear).Single();
            var old = movements.DropDrivers(new[] { team }, OldYear).Single();

            Assert.AreEqual(DriverFirerOutcome.DROPPED_UNDERPERFORMING, modern.DropDriver1);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, modern.DropDriver2);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, old.DropDriver1);
        }

        #endregion

        #region Paid rides

        [TestMethod]
        public void PaidRides_ModernSeason_NoOffersBudgetOrAiPurchases()
        {
            var saveGame = CreatePaidRidesSaveGame(ModernYear);

            Assert.IsFalse(PaidRides.Enabled(ModernYear));
            Assert.AreEqual(0, PaidRides.Budget(saveGame));
            Assert.AreEqual(0, PaidRides.Offers(saveGame, 1).Count);
            Assert.AreEqual(0, PaidRides.BuyAiRides(saveGame, 1, new AlwaysZeroRandom()).Count);

            var paddock = PaddockOpportunities.For(saveGame, 1);
            Assert.AreEqual(0, paddock.PaidRideOffers.Count);
            Assert.AreEqual(0, paddock.PaidRidesBudget);
            Assert.IsFalse(paddock.HasChoices);
        }

        [TestMethod]
        public void PaidRides_OldSeason_Unchanged()
        {
            var saveGame = CreatePaidRidesSaveGame(OldYear);

            Assert.IsTrue(PaidRides.Enabled(OldYear));
            Assert.AreEqual(PaidRides.BudgetFor(DriverReputation.PAY_DRIVER_SEASON), PaidRides.Budget(saveGame));
            Assert.IsTrue(PaidRides.Offers(saveGame, 1).Count > 0);
            Assert.IsTrue(PaddockOpportunities.For(saveGame, 1).HasChoices);
            Assert.AreEqual(1, PaidRides.BuyAiRides(saveGame, 1, new AlwaysZeroRandom()).Count);
        }

        #endregion

        #region Baked data

        [TestMethod]
        public void BakedDrivers_FromTheModernMarketOn_HaveNoWildCard()
        {
            var baked = Path.Combine(RepoRoot(), "AMS2ChEd", "BakedSeasons");
            var checkedYears = 0;
            foreach (var folder in Directory.GetDirectories(baked))
            {
                if (!int.TryParse(Path.GetFileName(folder), out var year) || !ModernMarket.IsModern(year))
                    continue;

                var file = Path.Combine(folder, "drivers.json");
                if (!File.Exists(file))
                    continue;

                using var json = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var driver in json.RootElement.GetProperty("drivers").EnumerateArray())
                {
                    var reputation = driver.TryGetProperty("reputation", out var r) ? r.GetString() : null;
                    Assert.AreNotEqual(nameof(DriverReputation.PAY_DRIVER_WILD_CARD), reputation, $"{year}: {driver.GetProperty("driver_id").GetString()}");
                }
                checkedYears++;
            }

            Assert.IsTrue(checkedYears > 0, "no baked season from the modern market on");
        }

        #endregion

        #region Helpers

        private static EndOfSeasonManager CreateEndOfSeasonManager()
        {
            var driverHirer = new DriverHirer();
            return new EndOfSeasonManager(new ReputationUpdater(), new OffSeasonMovements(new DriverFirer(), driverHirer), new Mock<IRandomDriverGenerator>().Object, driverHirer);
        }

        // a season with one team of two racing drivers, and "idle", who has no seat and ran no race,
        // aged `age` in the next season
        private static SaveGame CreateSaveGameWithIdleDriver(int year, DriverReputation idleReputation, int age)
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = year,
                    Teams = new List<ITeamEntry>
                    {
                        new TeamEntry
                        {
                            TeamId = "team",
                            Reputation = TeamReputation.MIDFIELD,
                            Driver1Contract = new DriverContract { DriverId = "d1", Races = 10 },
                            Driver2Contract = new DriverContract { DriverId = "d2", Races = 10 }
                        }
                    },
                    Races = Enumerable.Range(1, 3).Select(i => new Race { RaceId = i, RaceName = $"Race {i}" }).ToList()
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "d1", Name = "D1", YearOfBirth = year - 27, Reputation = DriverReputation.PRIME_MIDFIELD },
                    new DriverData { DriverId = "d2", Name = "D2", YearOfBirth = year - 27, Reputation = DriverReputation.PRIME_MIDFIELD },
                    new DriverData { DriverId = "idle", Name = "Idle", YearOfBirth = year + 1 - age, Reputation = idleReputation }
                },
                PlayerData = new PlayerData { DriverId = "d1", Name = "D1", TeamId = "team" },
                RetiredDrivers = new List<IDriverData>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>()
            };
        }

        // a minnow without pay drivers (both seats on sale), the player a season pay driver without
        // a seat, and an AI season pay driver without a seat
        private static SaveGame CreatePaidRidesSaveGame(int year)
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = year,
                    Teams = new List<ITeamEntry>
                    {
                        new TeamEntry
                        {
                            TeamId = "minnow",
                            Reputation = TeamReputation.MINNOW,
                            Driver1Contract = new DriverContract { DriverId = "m1", Races = 8 },
                            Driver2Contract = new DriverContract { DriverId = "m2", Races = 8 }
                        }
                    },
                    Races = Enumerable.Range(1, 8).Select(i => new Race { RaceId = i, RaceName = $"Race {i}" }).ToList(),
                    Absences = new List<Absence>()
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "m1", Name = "M1", YearOfBirth = year - 27, Reputation = DriverReputation.PRIME_MIDFIELD },
                    new DriverData { DriverId = "m2", Name = "M2", YearOfBirth = year - 27, Reputation = DriverReputation.PRIME_MIDFIELD },
                    new DriverData { DriverId = "player", Name = "Player", YearOfBirth = year - 22, Reputation = DriverReputation.PAY_DRIVER_SEASON },
                    new DriverData { DriverId = "ai_pay", Name = "AI Pay", YearOfBirth = year - 22, Reputation = DriverReputation.PAY_DRIVER_SEASON }
                },
                PlayerData = new PlayerData { DriverId = "player", Name = "Player" },
                RetiredDrivers = new List<IDriverData>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>()
            };
        }

        // every roll succeeds
        private class AlwaysZeroRandom : Random
        {
            public override double NextDouble() => 0;
            public override int Next() => 0;
            public override int Next(int maxValue) => 0;
            public override int Next(int minValue, int maxValue) => minValue;
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "AMS2ChEd", "BakedSeasons")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("repository root");
        }

        #endregion
    }
}
