using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Moq;
using System.Text.Json;
using static AMS2ChEd.Business.Services.DriverHirer;

namespace AMS2ChEd.Tests
{
    // Phase 2 of the modern driver market: academies in the season data, who belongs to them, and
    // the better fit a member gets at his academy's teams while hiring.
    [TestClass]
    public class AcademyTests
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

        private static readonly Academy Ferrari = new()
        {
            Id = "ferrari",
            ParentTeams = new List<string> { "OTHER_PARENT" },
            SisterTeams = new List<string> { "OTHER_SISTER" },
            Drivers = new List<string> { "OTHER_JUNIOR" }
        };

        private readonly DriverHirer _driverHirer = new();

        #region The academy fit

        [TestMethod]
        [DataRow(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, "SISTER", DriverPolicyFit.PerfectFit)]
        [DataRow(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, "PARENT", DriverPolicyFit.PerfectFit)]
        [DataRow(DriverReputation.YOUNG_TALENT, "SISTER", DriverPolicyFit.PerfectFit)]
        [DataRow(DriverReputation.YOUNG_TALENT, "PARENT", DriverPolicyFit.GoodFit)]
        [DataRow(DriverReputation.PAY_DRIVER_SEASON, "SISTER", DriverPolicyFit.GoodFit)]
        [DataRow(DriverReputation.PRIME_MIDFIELD, "PARENT", DriverPolicyFit.GoodFit)]
        public void AcademyFit_FollowsTheTable(DriverReputation reputation, string teamId, DriverPolicyFit expected)
        {
            Assert.AreEqual(expected, AcademyMembership.AcademyFit(RedBull, teamId, reputation));
        }

        [TestMethod]
        public void AcademyFit_AtATeamOutsideTheAcademy_IsNone()
        {
            Assert.IsNull(AcademyMembership.AcademyFit(RedBull, "OTHER_SISTER", DriverReputation.YOUNG_TALENT));
        }

        [TestMethod]
        public void MemberAtHisAcademysTeam_GetsTheAcademyFit_OnEverySeat()
        {
            var academies = Context(("JUNIOR", HiringYear - 20));

            // a MINNOW sister team rates a YOUNG_TALENT a GoodFit as second driver, and only a GoodFit as reserve
            Assert.AreEqual(DriverPolicyFit.GoodFit, _driverHirer.DoesDriverFitSeat(DriverReputation.YOUNG_TALENT, DriverRole.SECOND_DRIVER, TeamReputation.MINNOW, false));
            Assert.AreEqual(DriverPolicyFit.PerfectFit, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_TALENT, DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, academies));
            Assert.AreEqual(DriverPolicyFit.PerfectFit, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_TALENT, DriverRole.FIRST_DRIVER, "SISTER", TeamReputation.MINNOW, false, academies));
            Assert.AreEqual(DriverPolicyFit.PerfectFit, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_TALENT, DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, true, academies));

            // a top parent team rates a YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN UnderQualified to lead it
            Assert.AreEqual(DriverPolicyFit.UnderQualified, _driverHirer.DoesDriverFitSeat(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverRole.FIRST_DRIVER, TeamReputation.TOP_TEAM, false));
            Assert.AreEqual(DriverPolicyFit.PerfectFit, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverRole.FIRST_DRIVER, "PARENT", TeamReputation.TOP_TEAM, false, academies));
        }

        [TestMethod]
        public void BetterOfNormalAndAcademyFit_Wins()
        {
            var academies = Context(("JUNIOR", HiringYear - 20));

            // already a PerfectFit to lead a top team: the academy's GoodFit doesn't lower it
            Assert.AreEqual(DriverPolicyFit.PerfectFit, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, DriverRole.FIRST_DRIVER, "PARENT", TeamReputation.TOP_TEAM, false, academies));
            // OverQualified weighs as much as PerfectFit: it stays OverQualified
            Assert.AreEqual(DriverPolicyFit.OverQualified, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, academies));
        }

        [TestMethod]
        public void NonMember_OrMemberAtAnotherAcademysTeam_KeepsHisNormalFit()
        {
            var academies = Context(("JUNIOR", HiringYear - 20), ("NOBODY", HiringYear - 20));

            Assert.AreEqual(DriverPolicyFit.GoodFit, _driverHirer.DoesDriverFitSeat("NOBODY", DriverReputation.YOUNG_TALENT, DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, academies));
            Assert.AreEqual(DriverPolicyFit.GoodFit, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_TALENT, DriverRole.SECOND_DRIVER, "OTHER_SISTER", TeamReputation.MINNOW, false, academies));
        }

        [TestMethod]
        public void MemberAged25InTheHiringSeason_GetsNoAcademyFit()
        {
            var academies = Context(("JUNIOR", HiringYear - 25));

            Assert.IsNull(academies.AcademyOf("JUNIOR"));
            Assert.AreEqual(DriverPolicyFit.GoodFit, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_TALENT, DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, academies));
        }

        [TestMethod]
        public void WithoutAcademies_TheFitIsTheReputationOnlyOne()
        {
            foreach (var driverReputation in Enum.GetValues<DriverReputation>())
                foreach (var teamReputation in Enum.GetValues<TeamReputation>())
                    foreach (var role in Enum.GetValues<DriverRole>())
                        foreach (var isReserve in new[] { false, true })
                            Assert.AreEqual(
                                _driverHirer.DoesDriverFitSeat(driverReputation, role, teamReputation, isReserve),
                                _driverHirer.DoesDriverFitSeat("JUNIOR", driverReputation, role, "SISTER", teamReputation, isReserve, null));
        }

        [TestMethod]
        public void AcademyContext_SeasonWithoutAcademies_IsNull()
        {
            Assert.IsNull(AcademyContext.For(new Season { Year = HiringYear }, new List<IDriverData>(), "PLAYER", null));
            Assert.IsNull(AcademyContext.For(new Season { Year = HiringYear, Academies = new List<Academy>() }, new List<IDriverData>(), "PLAYER", null));
        }

        #endregion

        #region Membership

        [TestMethod]
        public void AcademyOf_ListedAndYoung_IsAMember()
        {
            var season = new Season { Year = HiringYear, Academies = new List<Academy> { RedBull, Ferrari } };

            Assert.AreEqual("red_bull", AcademyMembership.AcademyOf(season, "JUNIOR", HiringYear - 24, "PLAYER", null)?.Id);
            Assert.IsNull(AcademyMembership.AcademyOf(season, "JUNIOR", HiringYear - 25, "PLAYER", null));
            Assert.IsNull(AcademyMembership.AcademyOf(season, "NOBODY", HiringYear - 20, "PLAYER", null));
        }

        [TestMethod]
        public void AcademyOf_ThePlayer_GoesByHisAcademyId_NeverByTheList()
        {
            var season = new Season { Year = HiringYear, Academies = new List<Academy> { RedBull, Ferrari } };

            Assert.AreEqual("ferrari", AcademyMembership.AcademyOf(season, "PLAYER", HiringYear - 20, "PLAYER", "ferrari")?.Id);
            Assert.IsNull(AcademyMembership.AcademyOf(season, "PLAYER", HiringYear - 20, "PLAYER", null));
            // dormant: the season lacks his academy
            Assert.IsNull(AcademyMembership.AcademyOf(season, "PLAYER", HiringYear - 20, "PLAYER", "mercedes"));
            // the player listed in an academy's drivers is still not a member of it
            Assert.IsNull(AcademyMembership.AcademyOf(season, "JUNIOR", HiringYear - 20, "JUNIOR", null));
        }

        [TestMethod]
        public void AcademyOfTeam_FindsParentAndSister()
        {
            var season = new Season { Year = HiringYear, Academies = new List<Academy> { RedBull, Ferrari } };

            Assert.AreEqual("red_bull", AcademyMembership.AcademyOfTeam(season, "PARENT")?.Id);
            Assert.AreEqual("ferrari", AcademyMembership.AcademyOfTeam(season, "OTHER_SISTER")?.Id);
            Assert.IsNull(AcademyMembership.AcademyOfTeam(season, "NOWHERE"));
        }

        #endregion

        #region Leaving the academy

        [TestMethod]
        [DataRow(null, true)]
        [DataRow("PARENT", true)]
        [DataRow("SISTER", true)]
        [DataRow("OTHER_SISTER", false)]
        [DataRow("NOWHERE", false)]
        public void AcademyOf_ListedJunior_IsOutWhileHisLastSigningIsOutside(string lastTeamId, bool isMember)
        {
            var season = new Season { Year = HiringYear, Academies = new List<Academy> { RedBull, Ferrari } };

            var academy = AcademyMembership.AcademyOf(season, "JUNIOR", HiringYear - 20, "PLAYER", null, lastTeamId);

            Assert.AreEqual(isMember ? "red_bull" : null, academy?.Id);
        }

        [TestMethod]
        public void AcademyOf_ThePlayer_IgnoresHisLastTeam()
        {
            var season = new Season { Year = HiringYear, Academies = new List<Academy> { RedBull } };

            Assert.AreEqual("red_bull", AcademyMembership.AcademyOf(season, "PLAYER", HiringYear - 20, "PLAYER", "red_bull", "NOWHERE")?.Id);
        }

        [TestMethod]
        public void LastTeams_CurrentContractFirst_ThenTheLatestEarlierSeason()
        {
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = HiringYear - 1,
                    Teams = new List<ITeamEntry>
                    {
                        new TeamEntry
                        {
                            TeamId = "OUTSIDE",
                            Driver1Contract = new DriverContract { DriverId = "SIGNED_OUT" },
                            Driver2Contract = new DriverContract { DriverId = "" },
                            ReserveContract = new DriverContract { DriverId = "RESERVE_OUT" }
                        },
                        new TeamEntry { TeamId = "SISTER", Driver1Contract = new DriverContract { DriverId = "CAME_BACK" } }
                    }
                },
                HistoricalDriverStandings = new List<HistoricalDriverStanding>
                {
                    Standing(HiringYear - 3, ("UNSEATED", "SISTER"), ("CAME_BACK", "OUTSIDE")),
                    Standing(HiringYear - 2, ("UNSEATED", "OUTSIDE"), ("CAME_BACK", "OUTSIDE")),
                    // the current season, archived: a stand-in drive doesn't count
                    Standing(HiringYear - 1, ("STAND_IN", "OUTSIDE"))
                }
            };

            var lastTeams = AcademyMembership.LastTeams(saveGame);

            Assert.AreEqual("OUTSIDE", lastTeams["SIGNED_OUT"]);
            Assert.AreEqual("OUTSIDE", lastTeams["RESERVE_OUT"]);
            Assert.AreEqual("SISTER", lastTeams["CAME_BACK"]);
            Assert.AreEqual("OUTSIDE", lastTeams["UNSEATED"]);
            Assert.IsFalse(lastTeams.ContainsKey("STAND_IN"));
            Assert.IsFalse(lastTeams.ContainsKey(""));
        }

        [TestMethod]
        public void JuniorWhoSignedOutside_GetsNoAcademyFit_UntilHeSignsBack()
        {
            var yearsOfBirth = new Dictionary<string, int> { { "JUNIOR", HiringYear - 20 } };
            var outside = new AcademyContext(new[] { RedBull }, HiringYear, yearsOfBirth, "PLAYER", null, new Dictionary<string, string> { { "JUNIOR", "NOWHERE" } });
            var back = new AcademyContext(new[] { RedBull }, HiringYear, yearsOfBirth, "PLAYER", null, new Dictionary<string, string> { { "JUNIOR", "SISTER" } });

            Assert.IsNull(outside.AcademyOf("JUNIOR"));
            Assert.AreEqual(DriverPolicyFit.GoodFit, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_TALENT, DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, outside));
            Assert.AreEqual(DriverPolicyFit.PerfectFit, _driverHirer.DoesDriverFitSeat("JUNIOR", DriverReputation.YOUNG_TALENT, DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, back));
        }

        private static HistoricalDriverStanding Standing(int year, params (string DriverId, string TeamId)[] entries) => new()
        {
            Year = year,
            Standing = entries.Select(e => new HisoricalDriverStandingEntry { DriverId = e.DriverId, TeamId = e.TeamId }).ToList()
        };

        #endregion

        #region Academy first pick

        [TestMethod]
        public void PickAcademyMember_TakesAMember_OnlyWhenNobodyFitsBetter()
        {
            var academies = Context(("JUNIOR", HiringYear - 20), ("ROOKIE", HiringYear - 20), ("CHAMP", HiringYear - 30), ("PLAYER", HiringYear - 20), ("OTHER_JUNIOR", HiringYear - 20));
            DriverResume Resume(string id, DriverReputation reputation) => new() { Id = id, Reputation = reputation };

            // a PerfectFit at his sister team: nobody fits better
            Assert.AreEqual("JUNIOR", _driverHirer.PickAcademyMember(new[] { Resume("ROOKIE", DriverReputation.YOUNG_TALENT), Resume("JUNIOR", DriverReputation.YOUNG_TALENT) },
                DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, academies)?.Id);
            // a GoodFit to lead his parent team while a champion, a PerfectFit, is free
            Assert.IsNull(_driverHirer.PickAcademyMember(new[] { Resume("CHAMP", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), Resume("JUNIOR", DriverReputation.YOUNG_TALENT) },
                DriverRole.FIRST_DRIVER, "PARENT", TeamReputation.TOP_TEAM, false, academies));
            // another academy's junior, and the player, get no first pick
            Assert.IsNull(_driverHirer.PickAcademyMember(new[] { Resume("OTHER_JUNIOR", DriverReputation.YOUNG_TALENT) },
                DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, academies));
            Assert.IsNull(_driverHirer.PickAcademyMember(new[] { Resume("PLAYER", DriverReputation.YOUNG_TALENT) },
                DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, PlayerContext(HiringYear - 20)));
            Assert.IsNull(_driverHirer.PickAcademyMember(new[] { Resume("JUNIOR", DriverReputation.YOUNG_TALENT) },
                DriverRole.SECOND_DRIVER, "SISTER", TeamReputation.MINNOW, false, null));
        }

        [TestMethod]
        public void PickPotentialNewDrivers_SisterTeamKeepsItsJunior_BeforeATopTeamPicks()
        {
            // a top team outside the academy picks first and rates both young talents alike
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);
            var academies = Context(("JUNIOR", HiringYear - 20), ("ROOKIE", HiringYear - 20));

            for (int i = 0; i < 30; i++)
            {
                var ads = new[]
                {
                    new TeamJobAd { TeamId = "OUTSIDE", TeamReputation = TeamReputation.TOP_TEAM, Role = DriverRole.SECOND_DRIVER, Slot = 2 },
                    new TeamJobAd { TeamId = "SISTER", TeamReputation = TeamReputation.MINNOW, Role = DriverRole.SECOND_DRIVER, Slot = 2 }
                };
                var pool = new[]
                {
                    new UnemployedDriver { DriverId = "JUNIOR", Reputation = DriverReputation.YOUNG_TALENT },
                    new UnemployedDriver { DriverId = "ROOKIE", Reputation = DriverReputation.YOUNG_TALENT }
                };

                var hirings = movements.PickPotentialNewDrivers(ads, pool, out _, academies).ToList();

                var sister = hirings.Single(h => h.TeamId == "SISTER");
                Assert.AreEqual("JUNIOR", sister.DriverId);
                Assert.IsTrue(sister.Guaranteed && sister.AcademyFirstPick);
                Assert.AreEqual("ROOKIE", hirings.Single(h => h.TeamId == "OUTSIDE").DriverId);
            }
        }

        [TestMethod]
        public void PickPotentialNewDrivers_ParentTakesTheBetterDriver_AndTheSisterTheJunior()
        {
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);
            var academies = Context(("JUNIOR", HiringYear - 20), ("CHAMP", HiringYear - 30));

            for (int i = 0; i < 30; i++)
            {
                var ads = new[]
                {
                    new TeamJobAd { TeamId = "PARENT", TeamReputation = TeamReputation.TOP_TEAM, Role = DriverRole.FIRST_DRIVER, Slot = 1 },
                    new TeamJobAd { TeamId = "SISTER", TeamReputation = TeamReputation.MINNOW, Role = DriverRole.SECOND_DRIVER, Slot = 2 }
                };
                var pool = new[]
                {
                    new UnemployedDriver { DriverId = "CHAMP", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                    new UnemployedDriver { DriverId = "JUNIOR", Reputation = DriverReputation.YOUNG_TALENT }
                };

                var hirings = movements.PickPotentialNewDrivers(ads, pool, out _, academies).ToList();

                var parent = hirings.Single(h => h.TeamId == "PARENT");
                Assert.AreEqual("CHAMP", parent.DriverId);
                Assert.IsFalse(parent.AcademyFirstPick);
                Assert.AreEqual("JUNIOR", hirings.Single(h => h.TeamId == "SISTER").DriverId);
            }
        }

        [TestMethod]
        public void PickPotentialNewDrivers_ParentKeepsItsJuniorAsReserve()
        {
            // two top teams' reserve seats; to anyone but his parent team the junior, now a pay
            // driver, is no better than the other one
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);
            var academies = Context(("OTHER_JUNIOR", HiringYear - 19), ("PAYER", HiringYear - 24));

            for (int i = 0; i < 30; i++)
            {
                var ads = new[]
                {
                    new TeamJobAd { TeamId = "OUTSIDE", TeamReputation = TeamReputation.TOP_TEAM, Role = DriverRole.SECOND_DRIVER, Slot = TeamHiring.ReserveSlot },
                    new TeamJobAd { TeamId = "OTHER_PARENT", TeamReputation = TeamReputation.TOP_TEAM, Role = DriverRole.SECOND_DRIVER, Slot = TeamHiring.ReserveSlot }
                };
                var pool = new[]
                {
                    new UnemployedDriver { DriverId = "OTHER_JUNIOR", Reputation = DriverReputation.PAY_DRIVER_SEASON },
                    new UnemployedDriver { DriverId = "PAYER", Reputation = DriverReputation.PAY_DRIVER_SEASON }
                };

                var hirings = movements.PickPotentialNewDrivers(ads, pool, out _, academies).ToList();

                Assert.AreEqual("OTHER_JUNIOR", hirings.Single(h => h.TeamId == "OTHER_PARENT").DriverId);
                Assert.AreEqual("PAYER", hirings.Single(h => h.TeamId == "OUTSIDE").DriverId);
            }
        }

        [TestMethod]
        public void FinalBallots_AJuniorKeptForARaceSeat_SignsNowhereElse()
        {
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);
            var academies = Context(("JUNIOR", HiringYear - 20));

            for (int i = 0; i < 30; i++)
            {
                // as a YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN he'd beat the top team's pay-driver pick
                var outside = SlotBallot("OUTSIDE", TeamReputation.TOP_TEAM, 2, ("PAYER", DriverReputation.PAY_DRIVER_SEASON), ("JUNIOR", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN));
                var sister = SlotBallot("SISTER", TeamReputation.MINNOW, 2, ("JUNIOR", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN), ("JUNIOR", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN));
                sister.OriginalTeamHiring.Guaranteed = true;
                sister.OriginalTeamHiring.AcademyFirstPick = true;

                var results = movements.FinalBallotResults(new[] { outside, sister }, academies).ToList();

                Assert.AreEqual("PAYER", results.Single(r => r.TeamId == "OUTSIDE").DriverId);
                Assert.AreEqual("JUNIOR", results.Single(r => r.TeamId == "SISTER").DriverId);
            }
        }

        [TestMethod]
        public void FinalBallots_AJuniorKeptForAReserveSeat_CanStillWinARaceSeat_ButNoOtherReserveSeat()
        {
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);
            var academies = Context(("JUNIOR", HiringYear - 20));

            for (int i = 0; i < 30; i++)
            {
                TeamHiringBallot KeptReserve()
                {
                    var ballot = SlotBallot("SISTER", TeamReputation.MIDFIELD, TeamHiring.ReserveSlot, ("JUNIOR", DriverReputation.YOUNG_TALENT), ("BACKUP", DriverReputation.YOUNG_TALENT));
                    ballot.OriginalTeamHiring.Guaranteed = true;
                    ballot.OriginalTeamHiring.AcademyFirstPick = true;
                    return ballot;
                }

                // another top team's reserve seat would take him over its pay-driver pick
                var otherReserve = SlotBallot("OUTSIDE", TeamReputation.TOP_TEAM, TeamHiring.ReserveSlot, ("PAYER", DriverReputation.PAY_DRIVER_SEASON), ("JUNIOR", DriverReputation.YOUNG_TALENT));
                var results = movements.FinalBallotResults(new[] { otherReserve, KeptReserve() }, academies).ToList();
                Assert.AreEqual("PAYER", results.Single(r => r.TeamId == "OUTSIDE").DriverId);
                Assert.AreEqual("JUNIOR", results.Single(r => r.TeamId == "SISTER").DriverId);

                // a race seat beats the reserve seat
                var raceSeat = SlotBallot("RACING", TeamReputation.MINNOW, 2, (null, default), ("JUNIOR", DriverReputation.YOUNG_TALENT));
                results = movements.FinalBallotResults(new[] { raceSeat, KeptReserve() }, academies).ToList();
                Assert.AreEqual("JUNIOR", results.Single(r => r.TeamId == "RACING").DriverId);
                Assert.AreEqual("BACKUP", results.Single(r => r.TeamId == "SISTER").DriverId);
            }
        }

        [TestMethod]
        public void TieOnFit_AtHisAcademysTeam_GoesToTheJunior()
        {
            // a minnow's leader seat: a YOUNG_TALENT and a pay driver are both a GoodFit; the pay
            // driver is the sister team's junior
            var academies = Context(("OTHER_JUNIOR", HiringYear - 19), ("ROOKIE", HiringYear - 20));
            var junior = new DriverResume { Id = "OTHER_JUNIOR", Reputation = DriverReputation.PAY_DRIVER_SEASON };
            var rookie = new DriverResume { Id = "ROOKIE", Reputation = DriverReputation.YOUNG_TALENT };

            for (int i = 0; i < 30; i++)
            {
                Assert.AreEqual("OTHER_JUNIOR", _driverHirer.PickBestCandidate(new[] { rookie, junior }, DriverRole.FIRST_DRIVER, TeamReputation.MINNOW, false, "OTHER_SISTER", academies)?.Id);
                Assert.AreEqual("OTHER_JUNIOR", _driverHirer.PickWinner(rookie, junior, DriverRole.FIRST_DRIVER, TeamReputation.MINNOW, false, "OTHER_SISTER", academies).Id);
                Assert.AreEqual("OTHER_JUNIOR", _driverHirer.PickWinner(junior, rookie, DriverRole.FIRST_DRIVER, TeamReputation.MINNOW, false, "OTHER_SISTER", academies).Id);
            }

            // anywhere else the young talent's reputation wins
            Assert.AreEqual("ROOKIE", _driverHirer.PickWinner(junior, rookie, DriverRole.FIRST_DRIVER, TeamReputation.MINNOW, false, "OUTSIDE", academies).Id);
        }

        [TestMethod]
        public void FinalBallots_TieOnFit_TheJuniorIsDrawn()
        {
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);
            var academies = Context(("OTHER_JUNIOR", HiringYear - 19), ("ROOKIE", HiringYear - 20));

            for (int i = 0; i < 30; i++)
            {
                var ballot = SlotBallot("OTHER_SISTER", TeamReputation.MINNOW, 1, (null, default), ("ROOKIE", DriverReputation.YOUNG_TALENT), ("OTHER_JUNIOR", DriverReputation.PAY_DRIVER_SEASON));
                ballot.OriginalTeamHiring.Role = DriverRole.FIRST_DRIVER;

                Assert.AreEqual("OTHER_JUNIOR", movements.FinalBallotResults(new[] { ballot }, academies).Single().DriverId);
            }
        }

        [TestMethod]
        public void FinalBallots_AmongEqualTeams_ThePlayersAcademyTeamSettlesFirst()
        {
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);
            var player = ("PLAYER", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN);
            var veteran = ("VETERAN", DriverReputation.AGEING_CHAMPIONSHIP_LEVEL);

            // a top team outside his academy listed first, which picked the player himself; his
            // academy's parent team picked a veteran, and the player applied there
            TeamHiringBallot[] Ballots() => new[]
            {
                Ballot("OTHER_PARENT", TeamReputation.TOP_TEAM, player, veteran),
                Ballot("PARENT", TeamReputation.TOP_TEAM, veteran, player)
            };

            for (int i = 0; i < 30; i++)
            {
                var hirings = movements.FinalBallotResults(Ballots(), PlayerContext(HiringYear - 20)).ToList();
                Assert.AreEqual("PLAYER", hirings.Single(h => h.TeamId == "PARENT").DriverId);
                Assert.AreEqual("VETERAN", hirings.Single(h => h.TeamId == "OTHER_PARENT").DriverId);
            }

            // outside an academy, the list order decides as before
            Assert.AreEqual("PLAYER", movements.FinalBallotResults(Ballots()).Single(h => h.TeamId == "OTHER_PARENT").DriverId);
        }

        private static TeamHiringBallot SlotBallot(string teamId, TeamReputation teamReputation, int slot, (string Id, DriverReputation Reputation) pick, params (string Id, DriverReputation Reputation)[] candidates) => new()
        {
            OriginalTeamHiring = new TeamHiring
            {
                TeamId = teamId,
                TeamReputation = teamReputation,
                Role = DriverRole.SECOND_DRIVER,
                Slot = slot,
                DriverId = pick.Id,
                DriverReputation = pick.Reputation,
                ExcludeDriverIds = new HashSet<string>(),
                OtherPotentialCandidates = new List<DriverResume>()
            },
            Candidates = candidates.Select(c => new TeamHiringBallotCandidate { DriverId = c.Id, DriverReputation = c.Reputation }).ToList()
        };

        #endregion

        #region Final ballots

        [TestMethod]
        public void FinalBallot_JuniorLiftedToPerfectFit_BeatsGoodFitCandidatesAndAGoodFitPick()
        {
            // MIDFIELD sister team, second seat: YOUNG_TALENT and AGEING_MIDFIELD are both GoodFits
            var ballot = Ballot("SISTER", TeamReputation.MIDFIELD, pick: ("PICK", DriverReputation.AGEING_MIDFIELD),
                ("JUNIOR", DriverReputation.YOUNG_TALENT), ("RIVAL", DriverReputation.AGEING_MIDFIELD));
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);

            for (int i = 0; i < 50; i++)
            {
                var result = movements.FinalBallotResults(new[] { ballot }, Context(("JUNIOR", HiringYear - 20))).Single();
                Assert.AreEqual("JUNIOR", result.DriverId);
            }
        }

        [TestMethod]
        public void FinalBallot_YoungTalentLiftedToPerfectFit_BeatsAPerfectFitPickOfHigherReputation()
        {
            // PRIME_MIDFIELD is a PerfectFit on its own for a MIDFIELD team's second seat, and above
            // YOUNG_TALENT; level on fit, the sister team's junior wins the tie
            var ballot = Ballot("SISTER", TeamReputation.MIDFIELD, pick: ("PICK", DriverReputation.PRIME_MIDFIELD),
                ("JUNIOR", DriverReputation.YOUNG_TALENT));
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual("JUNIOR", movements.FinalBallotResults(new[] { ballot }, Context(("JUNIOR", HiringYear - 20))).Single().DriverId);
                Assert.AreEqual("PICK", movements.FinalBallotResults(new[] { ballot }).Single().DriverId);
            }
        }

        [TestMethod]
        public void FinalBallot_WithoutAcademies_IsDecidedAsBefore()
        {
            // a MINNOW second seat: the pay driver is the PerfectFit, a YOUNG_TALENT only a GoodFit.
            // As a sister-team junior the YOUNG_TALENT draws level on fit and wins on reputation
            var ballot = Ballot("SISTER", TeamReputation.MINNOW, pick: ("PICK", DriverReputation.PAY_DRIVER_SEASON),
                ("JUNIOR", DriverReputation.YOUNG_TALENT));
            var movements = new OffSeasonMovements(new DriverFirer(), _driverHirer);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual("PICK", movements.FinalBallotResults(new[] { ballot }).Single().DriverId);
                Assert.AreEqual("PICK", movements.FinalBallotResults(new[] { ballot }, null).Single().DriverId);
                Assert.AreEqual("JUNIOR", movements.FinalBallotResults(new[] { ballot }, Context(("JUNIOR", HiringYear - 20))).Single().DriverId);
            }
        }

        #endregion

        #region First refusal

        [TestMethod]
        public void TeamPicks_ReserveJuniorAtHisParentTeam_GetsFirstRefusalOnTheLeadersSeat()
        {
            // a top team's leader leaves; its reserve is a YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, normally
            // UnderQualified to lead it
            var (saveGame, nextSeason) = FirstRefusalSave();
            var manager = EndOfSeasonManager();
            var drops = new List<DropTeamResult>
            {
                new() { TeamId = "PARENT", DropDriver1 = DriverFirerOutcome.DROPPED_UNDERPERFORMING, DropDriver2 = DriverFirerOutcome.NOT_DROPPED }
            };

            var withAcademies = manager.TeamPicksPotentialReplacementsDrivers(HiringYear, saveGame, nextSeason.Teams, drops, null,
                AcademyContext.For(nextSeason, saveGame.Drivers, "PLAYER", null)).ToList();
            var promoted = withAcademies.Single(b => b.OriginalTeamHiring.TeamId == "PARENT" && b.OriginalTeamHiring.Slot == 1).OriginalTeamHiring;
            Assert.AreEqual("JUNIOR", promoted.DriverId);
            Assert.IsTrue(promoted.Guaranteed);

            var (saveGame2, nextSeason2) = FirstRefusalSave();
            var without = manager.TeamPicksPotentialReplacementsDrivers(HiringYear, saveGame2, nextSeason2.Teams, drops).ToList();
            Assert.IsFalse(without.Single(b => b.OriginalTeamHiring.TeamId == "PARENT" && b.OriginalTeamHiring.Slot == 1).OriginalTeamHiring.Guaranteed);
        }

        #endregion

        #region Market outlook

        [TestMethod]
        public void MarketOutlook_SeatKeptForAJunior_IsNotOnTheMarket()
        {
            var withAcademy = OutlookSave(academy: true);
            var withoutAcademy = OutlookSave(academy: false);

            // a PerfectFit at his sister team, the junior gets its seat before the market opens
            var seat = EstimateSeat(withAcademy.saveGame, withAcademy.nextSeason, "SISTER", 2);
            var control = EstimateSeat(withoutAcademy.saveGame, withoutAcademy.nextSeason, "SISTER", 2);

            Assert.IsNull(seat);
            Assert.IsNotNull(control);
            Assert.AreEqual(DriverPolicyFit.GoodFit, control.PlayerFit);
            CollectionAssert.Contains(control.SameFitRivals, "JUNIOR");
        }

        #endregion

        #region Reserve advice

        [TestMethod]
        public void ReserveAdvice_UnprovenJuniorAtHisParentTeam_WouldBePromoted_NotElsewhere_NorOnceAgedOut()
        {
            // the leader's contract runs out after next season, the wingman stays: the leader's seat
            // is the one opening, and a YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN is UnderQualified for it
            var parent = AdviceTeam("PARENT");
            var elsewhere = AdviceTeam("ELSEWHERE");
            var outlook = new MarketOutlook
            {
                ProjectedReputations = new Dictionary<string, DriverReputation>
                {
                    { "LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                    { "WINGMAN", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN }
                }
            };
            var reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;

            // 24 the season after next
            var young = PlayerContext(HiringYear + 1 - 24);
            // 24 in the season being hired for, 25 when the promotion is judged
            var agedOut = PlayerContext(HiringYear - 24);

            Assert.IsTrue(ReserveAdvice.For(parent, TeamReputation.TOP_TEAM, 16, 16, outlook, reputation, "PLAYER", young).WouldBePromoted);
            Assert.IsFalse(ReserveAdvice.For(elsewhere, TeamReputation.TOP_TEAM, 16, 16, outlook, reputation, "PLAYER", young).WouldBePromoted);
            Assert.IsFalse(ReserveAdvice.For(parent, TeamReputation.TOP_TEAM, 16, 16, outlook, reputation, "PLAYER", agedOut).WouldBePromoted);
            Assert.IsFalse(ReserveAdvice.For(parent, TeamReputation.TOP_TEAM, 16, 16, outlook, reputation).WouldBePromoted);
        }

        #endregion

        #region Data

        [TestMethod]
        public void Json_SeasonWithoutAcademies_LoadsAsNull_AndWritesNoKey()
        {
            var season = JsonSerializer.Deserialize<Ams2Season>("{\"year\":2020,\"teams\":[],\"races\":[]}", DefaultJsonSerializerOptions.Instance);
            Assert.IsNull(season.Academies);

            var json = JsonSerializer.Serialize<Ams2Season>(season, DefaultJsonSerializerOptions.Instance);
            Assert.IsFalse(json.Contains("academies"), json);
        }

        [TestMethod]
        public void Json_Academies_RoundTrip()
        {
            var season = new Ams2Season { Year = HiringYear, Teams = new List<ITeamEntry>(), Races = new List<Race>(), Academies = new List<Academy> { RedBull } };

            var json = JsonSerializer.Serialize(season, DefaultJsonSerializerOptions.Instance);
            var loaded = JsonSerializer.Deserialize<Ams2Season>(json, DefaultJsonSerializerOptions.Instance);

            StringAssert.Contains(json, "\"parent_teams\"");
            Assert.AreEqual("red_bull", loaded.Academies.Single().Id);
            CollectionAssert.AreEqual(RedBull.ParentTeams, loaded.Academies.Single().ParentTeams);
            CollectionAssert.AreEqual(RedBull.SisterTeams, loaded.Academies.Single().SisterTeams);
            CollectionAssert.AreEqual(RedBull.Drivers, loaded.Academies.Single().Drivers);
        }

        [TestMethod]
        public void BakedSeasons_HaveTheFourAcademies_FromTheModernEraOnly()
        {
            var baked = Path.Combine(RepoRoot(), "AMS2ChEd", "BakedSeasons");
            foreach (var folder in Directory.GetDirectories(baked))
            {
                if (!int.TryParse(Path.GetFileName(folder), out var year))
                    continue;

                var academies = LoadBaked(year).Season.Academies;
                if (ModernMarket.IsModern(year))
                    CollectionAssert.AreEquivalent(new[] { "red_bull", "ferrari", "mercedes", "alpine" }, academies.Select(a => a.Id).ToList(), year.ToString());
                else
                    Assert.IsNull(academies, year.ToString());
            }
        }

        [TestMethod]
        [DataRow(2016, "mercedes", "virgin")]
        [DataRow(2017, "ferrari", "haas")]
        [DataRow(2018, "ferrari", "haas,sauber")]
        [DataRow(2019, "mercedes", "williams")]
        [DataRow(2021, "mercedes", "williams")]
        [DataRow(2022, "mercedes", "")]
        [DataRow(2025, "alpine", "")]
        public void BakedAcademies_SisterTeamsFollowTheYears(int year, string academyId, string sisters)
        {
            var academy = LoadBaked(year).Season.Academies.Single(a => a.Id == academyId);
            var expected = sisters.Split(',', StringSplitOptions.RemoveEmptyEntries);
            CollectionAssert.AreEquivalent(expected, academy.SisterTeams);
        }

        [TestMethod]
        public void BakedAcademies_ListOnlyDriversUnder25ThatSeason()
        {
            for (int year = ModernMarket.Year; year <= 2025; year++)
            {
                var folder = Path.Combine(RepoRoot(), "AMS2ChEd", "BakedSeasons", year.ToString());
                using var drivers = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "drivers.json")));
                var birthYears = drivers.RootElement.GetProperty("drivers").EnumerateArray()
                    .ToDictionary(d => d.GetProperty("driver_id").GetString(), d => d.GetProperty("year_of_birth").GetInt32());

                foreach (var driverId in LoadBaked(year).Season.Academies.SelectMany(a => a.Drivers))
                    Assert.IsTrue(AcademyMembership.IsYoung(year, birthYears[driverId]), $"{year}: {driverId}");
            }
        }

        [TestMethod]
        public void BakedAcademies_PointAtTheSeasonsTeamsAndDrivers_WithoutOverlaps()
        {
            var baked = Path.Combine(RepoRoot(), "AMS2ChEd", "BakedSeasons");
            foreach (var folder in Directory.GetDirectories(baked))
            {
                if (!int.TryParse(Path.GetFileName(folder), out var year))
                    continue;

                var (season, driverIds) = LoadBaked(year);
                if (season.Academies == null)
                    continue;

                var teamIds = season.Teams.Select(t => t.TeamId).ToHashSet();
                var academyIds = new HashSet<string>();
                var academyTeams = new HashSet<string>();
                foreach (var academy in season.Academies)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(academy.Id), $"{year}: an academy without id");
                    Assert.IsTrue(academyIds.Add(academy.Id), $"{year}: academy {academy.Id} twice");

                    foreach (var teamId in (academy.ParentTeams ?? new()).Concat(academy.SisterTeams ?? new()))
                    {
                        Assert.IsTrue(teamIds.Contains(teamId), $"{year} {academy.Id}: no team {teamId}");
                        // in two academies, or parent and sister of the same one
                        Assert.IsTrue(academyTeams.Add(teamId), $"{year} {academy.Id}: team {teamId} listed twice");
                    }

                    foreach (var driverId in academy.Drivers ?? new())
                        Assert.IsTrue(driverIds.Contains(driverId), $"{year} {academy.Id}: no driver {driverId} in drivers.json");
                }
            }
        }

        #endregion

        #region Helpers

        private static AcademyContext Context(params (string DriverId, int YearOfBirth)[] drivers) =>
            new(new[] { RedBull, Ferrari }, HiringYear, drivers.ToDictionary(d => d.DriverId, d => d.YearOfBirth), "PLAYER", null);

        // the player, born then, as a member of the academy whose parent team is PARENT
        private static AcademyContext PlayerContext(int yearOfBirth) =>
            new(new[] { RedBull }, HiringYear, new Dictionary<string, int> { { "PLAYER", yearOfBirth } }, "PLAYER", "red_bull");

        private static TeamHiringBallot Ballot(string teamId, TeamReputation teamReputation, (string Id, DriverReputation Reputation) pick, params (string Id, DriverReputation Reputation)[] candidates)
        {
            return new TeamHiringBallot
            {
                OriginalTeamHiring = new TeamHiring
                {
                    TeamId = teamId,
                    TeamReputation = teamReputation,
                    Role = DriverRole.SECOND_DRIVER,
                    Slot = 2,
                    DriverId = pick.Id,
                    DriverReputation = pick.Reputation,
                    ExcludeDriverIds = new HashSet<string>(),
                    OtherPotentialCandidates = new List<DriverResume>()
                },
                Candidates = candidates.Select(c => new TeamHiringBallotCandidate { DriverId = c.Id, DriverReputation = c.Reputation }).ToList()
            };
        }

        private static EndOfSeasonManager EndOfSeasonManager()
        {
            var driverHirer = new DriverHirer();
            return new EndOfSeasonManager(new ReputationUpdater(), new OffSeasonMovements(new DriverFirer(), driverHirer), new Mock<IRandomDriverGenerator>().Object, driverHirer);
        }

        // 2024 ending. PARENT (TOP): LEADER first, WINGMAN second, JUNIOR its reserve; a free
        // champion-level driver on the market. 2025 has the academy
        private static (ISaveGame, ISeason) FirstRefusalSave()
        {
            var parent = new TeamEntry
            {
                TeamId = "PARENT",
                TeamName = "Parent",
                Reputation = TeamReputation.TOP_TEAM,
                Driver1Contract = new DriverContract { DriverId = "LEADER", Races = 10, Role = ContractRole.FIRST_DRIVER },
                Driver2Contract = new DriverContract { DriverId = "WINGMAN", Races = 10, Role = ContractRole.SECOND_DRIVER },
                ReserveContract = new DriverContract { DriverId = "JUNIOR", Races = 3 }
            };
            var saveGame = new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = HiringYear - 1,
                    Teams = new List<ITeamEntry> { parent },
                    Races = Enumerable.Range(1, 3).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList(),
                    Absences = new List<Absence>()
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "LEADER", Name = "Leader", YearOfBirth = 1990, Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                    new DriverData { DriverId = "WINGMAN", Name = "Wingman", YearOfBirth = 1990, Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN },
                    new DriverData { DriverId = "JUNIOR", Name = "Junior", YearOfBirth = HiringYear - 20, Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN },
                    new DriverData { DriverId = "FREE", Name = "Free", YearOfBirth = 1990, Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                    new DriverData { DriverId = "PLAYER", Name = "Player", YearOfBirth = 1990, Reputation = DriverReputation.PRIME_MIDFIELD }
                },
                RetiredDrivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Player" },
                Reprimands = new List<Reprimand>()
            };
            var nextSeason = new Season
            {
                Year = HiringYear,
                Teams = new List<ITeamEntry>
                {
                    new TeamEntry
                    {
                        TeamId = "PARENT",
                        TeamName = "Parent",
                        Reputation = TeamReputation.TOP_TEAM,
                        Driver1Contract = new DriverContract { DriverId = "LEADER", Races = 10 },
                        Driver2Contract = new DriverContract { DriverId = "WINGMAN", Races = 10 }
                    }
                },
                Races = saveGame.CurrentSeason.Races.ToList(),
                Absences = new List<Absence>(),
                Academies = new List<Academy> { RedBull }
            };
            return (saveGame, nextSeason);
        }

        // 2024 ending. PLAYER_TEAM (SUPER_MINNOW): the player and a pay driver, under contract.
        // SISTER (MINNOW): a PRIME_MIDFIELD leader under contract and a 50-year-old second driver
        // certain to retire. JUNIOR, a YOUNG_TALENT, is free; the academy lists him when `academy`
        private static (ISaveGame saveGame, ISeason nextSeason) OutlookSave(bool academy)
        {
            ITeamEntry Team(string id, TeamReputation reputation, string d1, string d2) => new TeamEntry
            {
                TeamId = id,
                TeamName = id,
                Reputation = reputation,
                Driver1Contract = new DriverContract { DriverId = d1, Races = 10 },
                Driver2Contract = new DriverContract { DriverId = d2, Races = 10 }
            };

            var saveGame = new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = HiringYear - 1,
                    Teams = new List<ITeamEntry> { Team("PLAYER_TEAM", TeamReputation.SUPER_MINNOW, "PLAYER", "MATE"), Team("SISTER", TeamReputation.MINNOW, "LEAD", "OLD") },
                    Races = Enumerable.Range(1, 3).Select(i => new Race { RaceId = i, RaceName = $"GP {i}" }).ToList(),
                    Absences = new List<Absence>()
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "PLAYER", Name = "Player", YearOfBirth = HiringYear - 21, Reputation = DriverReputation.YOUNG_TALENT },
                    new DriverData { DriverId = "MATE", Name = "Mate", YearOfBirth = 1995, Reputation = DriverReputation.PAY_DRIVER_SEASON },
                    new DriverData { DriverId = "LEAD", Name = "Lead", YearOfBirth = 1995, Reputation = DriverReputation.PRIME_MIDFIELD },
                    new DriverData { DriverId = "OLD", Name = "Old", YearOfBirth = HiringYear - 50, Reputation = DriverReputation.AGEING_MIDFIELD },
                    new DriverData { DriverId = "JUNIOR", Name = "Junior", YearOfBirth = HiringYear - 20, Reputation = DriverReputation.YOUNG_TALENT }
                },
                RetiredDrivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Player", TeamId = "PLAYER_TEAM" }
            };
            var nextSeason = new Season
            {
                Year = HiringYear,
                Teams = saveGame.CurrentSeason.Teams.Select(t => Team(t.TeamId, t.Reputation, t.Driver1Contract.DriverId, t.Driver2Contract.DriverId)).ToList(),
                Races = saveGame.CurrentSeason.Races,
                Absences = new List<Absence>(),
                Academies = academy
                    ? new List<Academy> { new() { Id = "red_bull", ParentTeams = new List<string>(), SisterTeams = new List<string> { "SISTER" }, Drivers = new List<string> { "JUNIOR" } } }
                    : null
            };
            return (saveGame, nextSeason);
        }

        private static SeatOutlook EstimateSeat(ISaveGame saveGame, ISeason nextSeason, string teamId, int slot)
        {
            // reputations stay as they are
            var endOfSeasonManager = new Mock<IEndOfSeasonManager>();
            endOfSeasonManager
                .Setup(m => m.ProjectNewReputations(It.IsAny<ISaveGame>(), It.IsAny<int>()))
                .Returns((ISaveGame s, int _) => s.Drivers.ToDictionary(d => d.DriverId, d => d.Reputation));

            var outlook = new MarketOutlookEstimator(endOfSeasonManager.Object, new DriverFirer(), new DriverHirer())
                .Estimate(saveGame, nextSeason, new Dictionary<string, IDriverData>());
            return outlook.Seats.SingleOrDefault(s => s.TeamId == teamId && s.Slot == slot);
        }

        private static TeamEntry AdviceTeam(string teamId) => new()
        {
            TeamId = teamId,
            TeamName = teamId,
            Reputation = TeamReputation.TOP_TEAM,
            // 16 races this season and 16 next: 32 runs out after next season, 48 runs longer
            Driver1Contract = new DriverContract { DriverId = "LEADER", Races = 32, Role = ContractRole.FIRST_DRIVER },
            Driver2Contract = new DriverContract { DriverId = "WINGMAN", Races = 48, Role = ContractRole.SECOND_DRIVER }
        };

        private static (Ams2Season Season, HashSet<string> DriverIds) LoadBaked(int year)
        {
            var folder = Path.Combine(RepoRoot(), "AMS2ChEd", "BakedSeasons", year.ToString());
            var season = JsonSerializer.Deserialize<Ams2Season>(File.ReadAllText(Path.Combine(folder, "season.json")), DefaultJsonSerializerOptions.Instance);
            using var drivers = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "drivers.json")));
            var driverIds = drivers.RootElement.GetProperty("drivers").EnumerateArray().Select(d => d.GetProperty("driver_id").GetString()).ToHashSet();
            return (season, driverIds);
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
