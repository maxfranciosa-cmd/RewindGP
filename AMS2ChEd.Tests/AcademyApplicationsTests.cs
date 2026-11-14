using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Moq;
using System.Text.Json;

namespace AMS2ChEd.Tests
{
    // Phase 3 of the modern driver market: pre-filled applications, sister-team contracts and
    // renewals, the player's academy_id, and the market outlook following the same rules.
    [TestClass]
    public class AcademyApplicationsTests
    {
        // the season being hired for
        private const int HiringYear = 2025;

        private static readonly Academy RedBull = new()
        {
            Id = "red_bull",
            ParentTeams = new List<string> { "PARENT" },
            SisterTeams = new List<string> { "SISTER" },
            Drivers = new List<string> { "JUNIOR" }
        };

        // every open seat the table could name
        private static readonly AcademySeat[] AllSeats =
        {
            new("PARENT", DriverRole.FIRST_DRIVER, false),
            new("PARENT", DriverRole.SECOND_DRIVER, false),
            new("PARENT", DriverRole.SECOND_DRIVER, true),
            new("SISTER", DriverRole.FIRST_DRIVER, false),
            new("SISTER", DriverRole.SECOND_DRIVER, false),
            new("SISTER", DriverRole.SECOND_DRIVER, true),
            new("ELSEWHERE", DriverRole.SECOND_DRIVER, false)
        };

        #region The pre-fill table

        [TestMethod]
        public void SecondDriverAtTheSisterTeam_AppliesToItsFirstSeat_AndToBothParentSeats()
        {
            var seats = Prefilled(SeasonWith(("SISTER", "LEAD", ContractRole.FIRST_DRIVER), ("SISTER", "JUNIOR", ContractRole.SECOND_DRIVER)));
            CollectionAssert.AreEquivalent(new[] { AllSeats[0], AllSeats[1], AllSeats[3] }, seats);
        }

        [TestMethod]
        public void FirstDriverAtTheSisterTeam_AppliesToBothParentSeats()
        {
            var seats = Prefilled(SeasonWith(("SISTER", "JUNIOR", ContractRole.FIRST_DRIVER), ("SISTER", "MATE", ContractRole.SECOND_DRIVER)));
            CollectionAssert.AreEquivalent(new[] { AllSeats[0], AllSeats[1] }, seats);
        }

        [TestMethod]
        public void EqualContract_IsFirstDriver_WhenFirstDriverMaterialForTheTeam()
        {
            // a MINNOW takes a PRIME_MIDFIELD as first driver material, not a YOUNG_TALENT
            var season = SeasonWith(("SISTER", "JUNIOR", ContractRole.EQUAL), ("SISTER", "MATE", ContractRole.EQUAL));
            CollectionAssert.AreEquivalent(new[] { AllSeats[0], AllSeats[1] }, Prefilled(season, DriverReputation.PRIME_MIDFIELD));
            CollectionAssert.AreEquivalent(new[] { AllSeats[0], AllSeats[1], AllSeats[3] }, Prefilled(season, DriverReputation.YOUNG_TALENT));
        }

        [TestMethod]
        public void NoRaceSeat_AppliesToTheSisterSecondSeat_AndTheParentReserve()
        {
            var season = SeasonWith(("SISTER", "LEAD", ContractRole.FIRST_DRIVER));
            // a reserve has no race seat either
            season.Teams.Single().ReserveContract = new DriverContract { DriverId = "JUNIOR", Races = 3 };

            CollectionAssert.AreEquivalent(new[] { AllSeats[2], AllSeats[4] }, Prefilled(season));
            CollectionAssert.AreEquivalent(new[] { AllSeats[2], AllSeats[4] }, Prefilled(SeasonWith()));
        }

        [TestMethod]
        [DataRow(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN)]
        [DataRow(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL)]
        public void NoRaceSeat_ChampionshipProspect_AlsoAppliesToBothParentRaceSeats(DriverReputation reputation)
        {
            CollectionAssert.AreEquivalent(new[] { AllSeats[0], AllSeats[1], AllSeats[2], AllSeats[4] }, Prefilled(SeasonWith(), reputation));
        }

        [TestMethod]
        public void SisterTeamDriver_ChampionshipProspect_KeepsHisOwnRow()
        {
            // the clause is for members without a race seat only
            var seats = Prefilled(SeasonWith(("SISTER", "LEAD", ContractRole.FIRST_DRIVER), ("SISTER", "JUNIOR", ContractRole.SECOND_DRIVER)), DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN);
            CollectionAssert.AreEquivalent(new[] { AllSeats[0], AllSeats[1], AllSeats[3] }, seats);
        }

        [TestMethod]
        public void MemberAtTheParentTeam_OrOutsideHisAcademy_AndNonMembers_GetNothing()
        {
            Assert.AreEqual(0, Prefilled(SeasonWith(("PARENT", "JUNIOR", ContractRole.SECOND_DRIVER))).Count);
            Assert.AreEqual(0, Prefilled(SeasonWith(("ELSEWHERE", "JUNIOR", ContractRole.SECOND_DRIVER))).Count);
            // not listed, and too old
            Assert.AreEqual(0, AcademyApplications.PrefilledSeats(SeasonWith(), Context(("STRANGER", HiringYear - 20)), "STRANGER", DriverReputation.YOUNG_TALENT, AllSeats, s => s).Count);
            Assert.AreEqual(0, AcademyApplications.PrefilledSeats(SeasonWith(), Context(("JUNIOR", HiringYear - 25)), "JUNIOR", DriverReputation.YOUNG_TALENT, AllSeats, s => s).Count);
            Assert.AreEqual(0, AcademyApplications.PrefilledSeats(SeasonWith(), null, "JUNIOR", DriverReputation.YOUNG_TALENT, AllSeats, s => s).Count);
        }

        [TestMethod]
        public void OnlyOpenSeats_ArePrefilled()
        {
            var open = new[] { AllSeats[1], AllSeats[6] };
            var seats = AcademyApplications.PrefilledSeats(SeasonWith(), Context(("JUNIOR", HiringYear - 20)), "JUNIOR", DriverReputation.YOUNG_TALENT, open, s => s);
            Assert.AreEqual(0, seats.Count);

            var guaranteed = Ballot("SISTER", 2, DriverRole.SECOND_DRIVER);
            guaranteed.OriginalTeamHiring.Guaranteed = true;
            var ballots = new[] { guaranteed, Ballot("PARENT", TeamHiring.ReserveSlot, DriverRole.SECOND_DRIVER) };
            var prefilled = AcademyApplications.PrefilledBallots(SeasonWith(), Context(("JUNIOR", HiringYear - 20)), "JUNIOR", DriverReputation.YOUNG_TALENT, ballots);
            CollectionAssert.AreEqual(new[] { ballots[1] }, prefilled);
        }

        #endregion

        #region The off-season

        [TestMethod]
        public void TeamPicks_AnAiMember_IsOnAParentBallotAboveHisAmbition()
        {
            var (saveGame, nextSeason, drops) = MarketSave(juniorYearOfBirth: HiringYear - 20);
            var ballots = EndOfSeasonManager().TeamPicksPotentialReplacementsDrivers(HiringYear, saveGame, nextSeason.Teams, drops, null,
                AcademyContext.For(nextSeason, saveGame.Drivers, "PLAYER", null)).ToList();

            // a YOUNG_TALENT aims no higher than MIDFIELD
            var parentLeader = ballots.Single(b => b.OriginalTeamHiring.TeamId == "PARENT" && b.OriginalTeamHiring.Slot == 1);
            Assert.IsTrue(parentLeader.Candidates.Any(c => c.DriverId == "JUNIOR") || parentLeader.OriginalTeamHiring.DriverId == "JUNIOR");

            var (saveGame2, nextSeason2, drops2) = MarketSave(juniorYearOfBirth: HiringYear - 20);
            var without = EndOfSeasonManager().TeamPicksPotentialReplacementsDrivers(HiringYear, saveGame2, nextSeason2.Teams, drops2).ToList();
            Assert.IsFalse(without.Single(b => b.OriginalTeamHiring.TeamId == "PARENT" && b.OriginalTeamHiring.Slot == 1).Candidates.Any(c => c.DriverId == "JUNIOR"));
        }

        [TestMethod]
        public void ExpiringSisterTeamMember_IsNeverExcludedFromHisOwnSeat_OneWhoAgedOutCanBe()
        {
            bool ExcludedFromOwnSeat(int juniorYearOfBirth)
            {
                var (saveGame, nextSeason, drops) = MarketSave(juniorYearOfBirth);
                var ballots = EndOfSeasonManager().TeamPicksPotentialReplacementsDrivers(HiringYear, saveGame, nextSeason.Teams, drops, null,
                    AcademyContext.For(nextSeason, saveGame.Drivers, "PLAYER", null)).ToList();
                var ownSeat = ballots.Single(b => b.OriginalTeamHiring.TeamId == "SISTER" && b.OriginalTeamHiring.Slot == 2);
                return ownSeat.OriginalTeamHiring.ExcludeDriverIds?.Contains("JUNIOR") == true;
            }

            Assert.IsFalse(Enumerable.Range(0, 40).Any(_ => ExcludedFromOwnSeat(HiringYear - 20)));
            // 25 in the season being hired for: the coin toss as for anyone
            Assert.IsTrue(Enumerable.Range(0, 40).Any(_ => ExcludedFromOwnSeat(HiringYear - 25)));
        }

        [TestMethod]
        public void SisterTeamMember_SignsForOneSeason_ANonMemberPerfectFitGetsTheExtraRace()
        {
            var (saveGame, nextSeason, _) = MarketSave(juniorYearOfBirth: HiringYear - 20);
            var academies = AcademyContext.For(nextSeason, saveGame.Drivers, "PLAYER", null);
            var races = nextSeason.Races.Count();

            // both hired as a PerfectFit at a MINNOW: the junior through the academy, the pay driver on his own
            var junior = EndOfSeasonManager().GenerateNewSeasonWithNewHirings(saveGame, nextSeason, new[] { Hired("SISTER", 2, "JUNIOR", DriverReputation.YOUNG_TALENT) }, academies);
            Assert.AreEqual(races, junior.Teams.Single(t => t.TeamId == "SISTER").Driver2Contract.Races);

            var payDriver = EndOfSeasonManager().GenerateNewSeasonWithNewHirings(saveGame, nextSeason, new[] { Hired("SISTER", 2, "PAYER", DriverReputation.PAY_DRIVER_SEASON) }, academies);
            Assert.AreEqual(races + 1, payDriver.Teams.Single(t => t.TeamId == "SISTER").Driver2Contract.Races);
        }

        #endregion

        #region The player

        [TestMethod]
        public void Json_OldSaveWithoutAcademyId_LoadsWithNoAcademy_AndWritesNoKey()
        {
            var player = JsonSerializer.Deserialize<PlayerData>("{\"driverid\":\"PLAYER\",\"name\":\"Player\"}", DefaultJsonSerializerOptions.Instance);
            Assert.IsNull(player.AcademyId);
            Assert.IsFalse(JsonSerializer.Serialize(player, DefaultJsonSerializerOptions.Instance).Contains("academy_id"));

            var member = JsonSerializer.Deserialize<PlayerData>("{\"driverid\":\"PLAYER\",\"academy_id\":\"red_bull\"}", DefaultJsonSerializerOptions.Instance);
            Assert.AreEqual("red_bull", member.AcademyId);
            StringAssert.Contains(JsonSerializer.Serialize(member, DefaultJsonSerializerOptions.Instance), "\"academy_id\"");
        }

        [TestMethod]
        public void CanChooseAcademy_OnlyYoungNextSeason_AndOnlyWhenTheSeasonHasAcademies()
        {
            var (saveGame, nextSeason, _) = MarketSave(juniorYearOfBirth: HiringYear - 20);
            Player(saveGame).YearOfBirth = HiringYear - 24;
            Assert.IsTrue(OffSeasonOrchestrator.CanChooseAcademy(saveGame, nextSeason));

            Player(saveGame).YearOfBirth = HiringYear - 25;
            Assert.IsFalse(OffSeasonOrchestrator.CanChooseAcademy(saveGame, nextSeason));

            Player(saveGame).YearOfBirth = HiringYear - 20;
            nextSeason.Academies = null;
            Assert.IsFalse(OffSeasonOrchestrator.CanChooseAcademy(saveGame, nextSeason));
        }

        [TestMethod]
        public void UpdatePlayerAcademy_LeavesOnAnOutsideContract_KeepsItAtItsTeams()
        {
            foreach (var (teamId, reserve, stays) in new[] { ("SISTER", false, true), ("PARENT", true, true), ("ELSEWHERE", false, false), ("ELSEWHERE", true, false) })
            {
                var (saveGame, newSeason) = NewSeasonWithPlayerAt(teamId, reserve);
                Assert.IsNull(OffSeasonOrchestrator.UpdatePlayerAcademy(saveGame, newSeason));
                Assert.AreEqual(stays ? "red_bull" : null, saveGame.PlayerData.AcademyId, $"{teamId} reserve={reserve}");
            }

            // no contract at all: still a member
            var (unemployed, season) = NewSeasonWithPlayerAt(null, false);
            OffSeasonOrchestrator.UpdatePlayerAcademy(unemployed, season);
            Assert.AreEqual("red_bull", unemployed.PlayerData.AcademyId);
        }

        [TestMethod]
        public void UpdatePlayerAcademy_AgesOut_WithALetter_AndKeepsADormantId()
        {
            var (saveGame, newSeason) = NewSeasonWithPlayerAt("SISTER", false);
            Player(saveGame).YearOfBirth = HiringYear - 25;
            Assert.AreEqual("red_bull", OffSeasonOrchestrator.UpdatePlayerAcademy(saveGame, newSeason));
            Assert.IsNull(saveGame.PlayerData.AcademyId);
            // the contract is untouched
            Assert.AreEqual("PLAYER", newSeason.Teams.Single(t => t.TeamId == "SISTER").Driver2Contract.DriverId);

            // a season without his academy: dormant, the id is kept even at a team outside it
            var (dormant, noAcademy) = NewSeasonWithPlayerAt("ELSEWHERE", false);
            noAcademy.Academies = null;
            Assert.IsNull(OffSeasonOrchestrator.UpdatePlayerAcademy(dormant, noAcademy));
            Assert.AreEqual("red_bull", dormant.PlayerData.AcademyId);
            Assert.IsNull(AcademyMembership.AcademyOf(noAcademy, "PLAYER", HiringYear - 20, "PLAYER", "red_bull"));
        }

        [TestMethod]
        public void PlayerWithAnAcademyId_IsAMember_OfThatAcademyOnly()
        {
            var context = new AcademyContext(new[] { RedBull }, HiringYear, new Dictionary<string, int> { { "PLAYER", HiringYear - 20 } }, "PLAYER", "red_bull");
            Assert.AreEqual("red_bull", context.AcademyOf("PLAYER")?.Id);
            var noAcademy = new AcademyContext(new[] { RedBull }, HiringYear, new Dictionary<string, int> { { "PLAYER", HiringYear - 20 } }, "PLAYER", null);
            Assert.IsNull(noAcademy.AcademyOf("PLAYER"));
        }

        #endregion

        #region Market outlook

        [TestMethod]
        public void MarketOutlook_PrefilledSeats_AreAlwaysApplied_AndTheFreePicksFillTheRest()
        {
            // a player without a race seat, in an academy with two sister teams opening their second seat
            var outlook = Estimate(PlayerOutlookSave(sisterTeams: 2, otherTeams: 4));

            var prefilled = outlook.BestApplications.Where(s => s.IsPrefilled).ToList();
            Assert.AreEqual(2, prefilled.Count);
            CollectionAssert.AreEquivalent(new[] { "S1", "S2" }, prefilled.Select(s => s.TeamId).ToList());
            Assert.AreEqual(MarketOutlookEstimator.MaxApplications, outlook.BestApplications.Count);
            Assert.AreEqual(2, outlook.FreeApplications);
            Assert.IsTrue(outlook.Seats.Where(s => !s.IsPrefilled).All(s => s.TeamId.StartsWith("O")));
        }

        [TestMethod]
        public void MarketOutlook_MoreThanFourPrefilled_LeaveNoFreePick_AndAllCountTowardsTheChance()
        {
            var outlook = Estimate(PlayerOutlookSave(sisterTeams: 5, otherTeams: 2));

            Assert.AreEqual(5, outlook.BestApplications.Count);
            Assert.IsTrue(outlook.BestApplications.All(s => s.IsPrefilled));
            Assert.AreEqual(0, outlook.FreeApplications);
            var missedAll = outlook.BestApplications.Aggregate(1.0, (missed, s) => missed * (1 - s.Chance));
            Assert.AreEqual(1 - missedAll, outlook.HiredChance, 1e-9);
        }

        [TestMethod]
        public void MarketOutlook_WithoutAnAcademy_HasNoPrefilledSeats()
        {
            var (saveGame, nextSeason) = PlayerOutlookSave(sisterTeams: 2, otherTeams: 4);
            saveGame.PlayerData.AcademyId = null;
            var outlook = Estimate((saveGame, nextSeason));

            Assert.IsFalse(outlook.Seats.Any(s => s.IsPrefilled));
            Assert.AreEqual(MarketOutlookEstimator.MaxApplications, outlook.BestApplications.Count);
        }

        [TestMethod]
        public void MarketOutlook_AnAiMember_IsARivalOnAParentSeatAboveHisAmbition()
        {
            var (saveGame, nextSeason) = RivalOutlookSave(academy: true, sisterQuits: true);
            var seat = Estimate((saveGame, nextSeason)).Seats.Single(s => s.TeamId == "PARENT" && s.Slot == 2);
            CollectionAssert.Contains(seat.BetterFitRivals, "JUNIOR");

            var (saveGame2, nextSeason2) = RivalOutlookSave(academy: false, sisterQuits: true);
            var control = Estimate((saveGame2, nextSeason2)).Seats.Single(s => s.TeamId == "PARENT" && s.Slot == 2);
            // without the academy he's at most among those the team could pick from, no better a fit than the player
            CollectionAssert.DoesNotContain(control.BetterFitRivals, "JUNIOR");
        }

        [TestMethod]
        public void MarketOutlook_ASisterTeamMembersExpiringSeat_IsNotShownAsOpening()
        {
            var (saveGame, nextSeason) = RivalOutlookSave(academy: true);
            Assert.IsNull(Estimate((saveGame, nextSeason)).Seats.SingleOrDefault(s => s.TeamId == "SISTER" && s.Slot == 1));

            var (saveGame2, nextSeason2) = RivalOutlookSave(academy: false);
            Assert.IsNotNull(Estimate((saveGame2, nextSeason2)).Seats.SingleOrDefault(s => s.TeamId == "SISTER" && s.Slot == 1));
        }

        #endregion

        #region Helpers

        private static AcademyContext Context(params (string DriverId, int YearOfBirth)[] drivers) =>
            new(new[] { RedBull }, HiringYear, drivers.ToDictionary(d => d.DriverId, d => d.YearOfBirth), "PLAYER", null);

        private static List<AcademySeat> Prefilled(ISeason currentSeason, DriverReputation reputation = DriverReputation.YOUNG_TALENT) =>
            AcademyApplications.PrefilledSeats(currentSeason, Context(("JUNIOR", HiringYear - 20)), "JUNIOR", reputation, AllSeats, s => s);

        // this season's teams, built from (team, driver, role) race seats; every team is a MINNOW
        private static Season SeasonWith(params (string TeamId, string DriverId, ContractRole Role)[] seats)
        {
            var teams = seats.GroupBy(s => s.TeamId).Select(g =>
            {
                var drivers = g.ToList();
                return (ITeamEntry)new TeamEntry
                {
                    TeamId = g.Key,
                    TeamName = g.Key,
                    Reputation = TeamReputation.MINNOW,
                    Driver1Contract = new DriverContract { DriverId = drivers[0].DriverId, Races = 3, Role = drivers[0].Role },
                    Driver2Contract = drivers.Count > 1 ? new DriverContract { DriverId = drivers[1].DriverId, Races = 3, Role = drivers[1].Role } : new DriverContract { DriverId = "" }
                };
            }).ToList();
            return new Season { Year = HiringYear - 1, Teams = teams, Races = new List<Race>(), Absences = new List<Absence>() };
        }

        private static TeamHiringBallot Ballot(string teamId, int slot, DriverRole role) => new()
        {
            OriginalTeamHiring = new TeamHiring { TeamId = teamId, Slot = slot, Role = role, ExcludeDriverIds = new HashSet<string>(), OtherPotentialCandidates = new List<DriverResume>() },
            Candidates = new List<TeamHiringBallotCandidate>()
        };

        private static TeamHiringBallot Hired(string teamId, int slot, string driverId, DriverReputation reputation)
        {
            var ballot = Ballot(teamId, slot, DriverRole.SECOND_DRIVER);
            ballot.OriginalTeamHiring.TeamReputation = TeamReputation.MINNOW;
            ballot.OriginalTeamHiring.DriverId = driverId;
            ballot.OriginalTeamHiring.DriverReputation = reputation;
            ballot.OriginalTeamHiring.Guaranteed = true;
            return ballot;
        }

        private static EndOfSeasonManager EndOfSeasonManager()
        {
            var driverHirer = new DriverHirer();
            var generator = new Mock<IRandomDriverGenerator>();
            var generated = 0;
            generator
                .Setup(g => g.GenerateDriver(It.IsAny<IEnumerable<IDriverData>>(), It.IsAny<int>()))
                .Returns(() => new DriverData { DriverId = $"GEN{++generated}", Name = "Generated", YearOfBirth = 1995, Reputation = DriverReputation.PRIME_MIDFIELD });
            return new EndOfSeasonManager(new ReputationUpdater(), new OffSeasonMovements(new DriverFirer(), driverHirer), generator.Object, driverHirer);
        }

        private static IDriverData Player(ISaveGame saveGame) => saveGame.Drivers.Single(d => d.DriverId == "PLAYER");

        private static SaveGame EmptySave(Season currentSeason, List<IDriverData> drivers, string playerTeamId = null, string playerAcademyId = null) => new()
        {
            CurrentSeason = currentSeason,
            Drivers = drivers,
            RetiredDrivers = new List<IDriverData>(),
            CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
            CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
            HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
            HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
            GrandPrixResults = new List<GrandPrixResult>(),
            PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Player", TeamId = playerTeamId, AcademyId = playerAcademyId },
            Reprimands = new List<Reprimand>()
        };

        private static List<Race> Races() => Enumerable.Range(1, 3).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList();

        private static TeamEntry Team(string id, TeamReputation reputation, string d1, string d2, int races = 10, ContractRole role1 = ContractRole.FIRST_DRIVER, ContractRole role2 = ContractRole.SECOND_DRIVER) => new()
        {
            TeamId = id,
            TeamName = id,
            Reputation = reputation,
            Driver1Contract = new DriverContract { DriverId = d1, Races = races, Role = role1 },
            Driver2Contract = new DriverContract { DriverId = d2, Races = races, Role = role2 }
        };

        // 2024 ending. PARENT (TOP) drops its leader; SISTER (MINNOW): LEAD under contract, JUNIOR
        // (YOUNG_TALENT) second with his contract running out; a pay driver and the player are free
        private static (ISaveGame, Season, List<DropTeamResult>) MarketSave(int juniorYearOfBirth)
        {
            var current = new Season
            {
                Year = HiringYear - 1,
                Teams = new List<ITeamEntry>
                {
                    Team("PARENT", TeamReputation.TOP_TEAM, "LEADER", "WINGMAN"),
                    new TeamEntry
                    {
                        TeamId = "SISTER", TeamName = "SISTER", Reputation = TeamReputation.MINNOW,
                        Driver1Contract = new DriverContract { DriverId = "LEAD", Races = 10, Role = ContractRole.FIRST_DRIVER },
                        Driver2Contract = new DriverContract { DriverId = "JUNIOR", Races = 3, Role = ContractRole.SECOND_DRIVER }
                    }
                },
                Races = Races(),
                Absences = new List<Absence>()
            };
            var saveGame = EmptySave(current, new List<IDriverData>
            {
                new DriverData { DriverId = "LEADER", Name = "Leader", YearOfBirth = 1990, Reputation = DriverReputation.PRIME_MIDFIELD },
                new DriverData { DriverId = "WINGMAN", Name = "Wingman", YearOfBirth = 1990, Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN },
                new DriverData { DriverId = "LEAD", Name = "Lead", YearOfBirth = 1995, Reputation = DriverReputation.PRIME_MIDFIELD },
                new DriverData { DriverId = "JUNIOR", Name = "Junior", YearOfBirth = juniorYearOfBirth, Reputation = DriverReputation.YOUNG_TALENT },
                new DriverData { DriverId = "PAYER", Name = "Payer", YearOfBirth = 1995, Reputation = DriverReputation.PAY_DRIVER_SEASON },
                // the parent's pick for its leader seat, so it isn't first-picked for the junior
                new DriverData { DriverId = "STAR", Name = "Star", YearOfBirth = 1990, Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new DriverData { DriverId = "PLAYER", Name = "Player", YearOfBirth = 1990, Reputation = DriverReputation.PRIME_MIDFIELD }
            });
            var nextSeason = new Season
            {
                Year = HiringYear,
                Teams = new List<ITeamEntry>
                {
                    Team("PARENT", TeamReputation.TOP_TEAM, "LEADER", "WINGMAN"),
                    Team("SISTER", TeamReputation.MINNOW, "LEAD", "JUNIOR")
                },
                Races = Races(),
                Absences = new List<Absence>(),
                Academies = new List<Academy> { RedBull }
            };
            var drops = new List<DropTeamResult>
            {
                new() { TeamId = "PARENT", DropDriver1 = DriverFirerOutcome.DROPPED_UNDERPERFORMING, DropDriver2 = DriverFirerOutcome.NOT_DROPPED },
                new() { TeamId = "SISTER", DropDriver1 = DriverFirerOutcome.NOT_DROPPED, DropDriver2 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED }
            };
            return (saveGame, nextSeason, drops);
        }

        // the new season with the player at a team (race seat or reserve), or nowhere, and in the
        // red_bull academy; 20 years old
        private static (ISaveGame, Season) NewSeasonWithPlayerAt(string teamId, bool reserve)
        {
            var newSeason = new Season
            {
                Year = HiringYear,
                Teams = new List<ITeamEntry>
                {
                    Team("PARENT", TeamReputation.TOP_TEAM, "LEADER", "WINGMAN"),
                    Team("SISTER", TeamReputation.MINNOW, "LEAD", "MATE"),
                    Team("ELSEWHERE", TeamReputation.MIDFIELD, "A", "B")
                },
                Races = Races(),
                Absences = new List<Absence>(),
                Academies = new List<Academy> { RedBull }
            };
            var team = newSeason.Teams.SingleOrDefault(t => t.TeamId == teamId);
            if (team != null && reserve)
                team.ReserveContract = new DriverContract { DriverId = "PLAYER", Races = 3 };
            else if (team != null)
                team.Driver2Contract.DriverId = "PLAYER";

            var saveGame = EmptySave(newSeason, new List<IDriverData>
            {
                new DriverData { DriverId = "PLAYER", Name = "Player", YearOfBirth = HiringYear - 20, Reputation = DriverReputation.YOUNG_TALENT }
            }, playerTeamId: reserve ? null : teamId, playerAcademyId: "red_bull");
            return (saveGame, newSeason);
        }

        // 2024 ending. The player (YOUNG_TALENT, in the academy) has no team. Each sister team S1..
        // and other team O1.. (all MINNOW) has a PRIME_MIDFIELD leader under contract and a
        // 50-year-old second driver certain to retire
        private static (ISaveGame, Season) PlayerOutlookSave(int sisterTeams, int otherTeams)
        {
            var ids = Enumerable.Range(1, sisterTeams).Select(i => $"S{i}").Concat(Enumerable.Range(1, otherTeams).Select(i => $"O{i}")).ToList();
            var drivers = new List<IDriverData>
            {
                new DriverData { DriverId = "PLAYER", Name = "Player", YearOfBirth = HiringYear - 20, Reputation = DriverReputation.YOUNG_TALENT }
            };
            foreach (var id in ids)
            {
                drivers.Add(new DriverData { DriverId = $"{id}_LEAD", Name = id, YearOfBirth = 1995, Reputation = DriverReputation.PRIME_MIDFIELD });
                drivers.Add(new DriverData { DriverId = $"{id}_OLD", Name = id, YearOfBirth = HiringYear - 50, Reputation = DriverReputation.AGEING_MIDFIELD });
            }

            ITeamEntry MinnowTeam(string id) => Team(id, TeamReputation.MINNOW, $"{id}_LEAD", $"{id}_OLD", role1: ContractRole.UNDEFINED, role2: ContractRole.UNDEFINED);
            var current = new Season { Year = HiringYear - 1, Teams = ids.Select(MinnowTeam).ToList(), Races = Races(), Absences = new List<Absence>() };
            var saveGame = EmptySave(current, drivers, playerAcademyId: "red_bull");
            var academy = new Academy { Id = "red_bull", ParentTeams = new List<string>(), SisterTeams = ids.Where(i => i.StartsWith("S")).ToList(), Drivers = new List<string>() };
            var nextSeason = new Season { Year = HiringYear, Teams = ids.Select(MinnowTeam).ToList(), Races = Races(), Absences = new List<Absence>(), Academies = new List<Academy> { academy } };
            return (saveGame, nextSeason);
        }

        // 2024 ending. PARENT (TOP): a leader under contract and a 50-year-old second driver certain
        // to retire. SISTER (MINNOW): JUNIOR (YOUNG_TALENT, beyond a TOP team's reach on his own)
        // leads it, his contract running out, with a pay driver under contract. A free star fits the
        // parent seat best, so it isn't first-picked for the junior. The player is a free pay driver.
        // sisterQuits: SISTER is gone next season, so the junior has no seat of his own to be kept for
        private static (ISaveGame, Season) RivalOutlookSave(bool academy, bool sisterQuits = false)
        {
            var current = new Season
            {
                Year = HiringYear - 1,
                Teams = new List<ITeamEntry>
                {
                    Team("PARENT", TeamReputation.TOP_TEAM, "LEADER", "OLD"),
                    new TeamEntry
                    {
                        TeamId = "SISTER", TeamName = "SISTER", Reputation = TeamReputation.MINNOW,
                        Driver1Contract = new DriverContract { DriverId = "JUNIOR", Races = 3, Role = ContractRole.FIRST_DRIVER },
                        Driver2Contract = new DriverContract { DriverId = "MATE", Races = 10, Role = ContractRole.SECOND_DRIVER }
                    }
                },
                Races = Races(),
                Absences = new List<Absence>()
            };
            var saveGame = EmptySave(current, new List<IDriverData>
            {
                new DriverData { DriverId = "LEADER", Name = "Leader", YearOfBirth = 1995, Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new DriverData { DriverId = "OLD", Name = "Old", YearOfBirth = HiringYear - 50, Reputation = DriverReputation.AGEING_MIDFIELD },
                new DriverData { DriverId = "JUNIOR", Name = "Junior", YearOfBirth = HiringYear - 20, Reputation = DriverReputation.YOUNG_TALENT },
                new DriverData { DriverId = "MATE", Name = "Mate", YearOfBirth = 1995, Reputation = DriverReputation.PAY_DRIVER_SEASON },
                new DriverData { DriverId = "STAR", Name = "Star", YearOfBirth = 1992, Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN },
                new DriverData { DriverId = "PLAYER", Name = "Player", YearOfBirth = 1995, Reputation = DriverReputation.PAY_DRIVER_SEASON }
            });
            var nextSeason = new Season
            {
                Year = HiringYear,
                Teams = new List<ITeamEntry>
                {
                    Team("PARENT", TeamReputation.TOP_TEAM, "LEADER", "OLD"),
                    Team("SISTER", TeamReputation.MINNOW, "JUNIOR", "MATE")
                }.Where(t => !sisterQuits || t.TeamId != "SISTER").ToList(),
                Races = Races(),
                Absences = new List<Absence>(),
                Academies = academy ? new List<Academy> { RedBull } : null
            };
            return (saveGame, nextSeason);
        }

        private static MarketOutlook Estimate((ISaveGame saveGame, Season nextSeason) save)
        {
            // reputations stay as they are
            var endOfSeasonManager = new Mock<IEndOfSeasonManager>();
            endOfSeasonManager
                .Setup(m => m.ProjectNewReputations(It.IsAny<ISaveGame>(), It.IsAny<int>()))
                .Returns((ISaveGame s, int _) => s.Drivers.ToDictionary(d => d.DriverId, d => d.Reputation));

            return new MarketOutlookEstimator(endOfSeasonManager.Object, new DriverFirer(), new DriverHirer())
                .Estimate(save.saveGame, save.nextSeason, new Dictionary<string, IDriverData>());
        }

        #endregion
    }
}
