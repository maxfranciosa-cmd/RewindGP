using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Tests
{
    // The academies window: every member of the current season's academies, his seat, his FP1 form
    // and his season, in the academy's pecking order.
    [TestClass]
    public class AcademyStandingsTests
    {
        private const int Year = 2024;

        [TestMethod]
        public void Build_WithoutAcademies_IsEmpty()
        {
            var saveGame = Save();
            saveGame.CurrentSeason.Academies = null;

            Assert.AreEqual(0, AcademyStandings.Build(saveGame).Count);
        }

        [TestMethod]
        public void Build_GivesEachMemberHisSeat()
        {
            var saveGame = Save(
                Driver("AT_PARENT", 20, DriverReputation.YOUNG_TALENT),
                Driver("AT_SISTER", 20, DriverReputation.YOUNG_TALENT),
                Driver("RESERVE", 20, DriverReputation.YOUNG_TALENT),
                Driver("SEATLESS", 20, DriverReputation.YOUNG_TALENT));

            var members = AcademyStandings.Build(saveGame).Single().Members.ToDictionary(m => m.DriverId);

            Assert.AreEqual(AcademyMemberSeat.PARENT_SEAT, members["AT_PARENT"].Seat);
            Assert.AreEqual("PARENT", members["AT_PARENT"].TeamId);
            Assert.AreEqual(AcademyMemberSeat.SISTER_SEAT, members["AT_SISTER"].Seat);
            Assert.AreEqual(AcademyMemberSeat.RESERVE, members["RESERVE"].Seat);
            Assert.AreEqual("PARENT", members["RESERVE"].TeamId);
            Assert.AreEqual(AcademyMemberSeat.NO_SEAT, members["SEATLESS"].Seat);
            Assert.IsNull(members["SEATLESS"].TeamId);
        }

        [TestMethod]
        public void Build_TooOldDriver_IsNoMember_AndTheLastYoungSeasonAgesOut()
        {
            var saveGame = Save(
                // still young this season, too old for the academy next season
                Driver("SEATLESS", ReputationUpdater.YOUNG_DRIVER_AGE - 1, DriverReputation.YOUNG_TALENT),
                Driver("RESERVE", ReputationUpdater.YOUNG_DRIVER_AGE + 5, DriverReputation.PRIME_MIDFIELD));

            var members = AcademyStandings.Build(saveGame).Single().Members;

            Assert.AreEqual("SEATLESS", members.Single().DriverId);
            Assert.IsTrue(members.Single().AgesOut);
        }

        [TestMethod]
        public void Build_RanksByReputation_ThenFp1Average_ThenPoints()
        {
            var saveGame = Save(
                Driver("AT_SISTER", 20, DriverReputation.YOUNG_TALENT),
                Driver("SEATLESS", 20, DriverReputation.YOUNG_TALENT),
                Driver("RESERVE", 20, DriverReputation.YOUNG_TALENT),
                Driver("AT_PARENT", 21, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN));
            saveGame.PracticeOutings = new List<PracticeOuting>
            {
                Outing("SEATLESS", 3), Outing("SEATLESS", 1),
                Outing("RESERVE", 1),
                Outing("RESERVE", null), // not run yet: doesn't count
                new() { Year = Year - 1, DriverId = "AT_SISTER", Score = 3, TeamId = "SISTER" } // last season: doesn't count
            };
            saveGame.CurrentDriverStandings = new List<HistoricalDriverStandingEntry>
            {
                new() { DriverId = "AT_SISTER", Position = 9, Points = 12 }
            };

            var members = AcademyStandings.Build(saveGame).Single().Members;

            CollectionAssert.AreEqual(new[] { "AT_PARENT", "SEATLESS", "RESERVE", "AT_SISTER" }, members.Select(m => m.DriverId).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, members.Select(m => m.Rank).ToArray());

            var seatless = members.Single(m => m.DriverId == "SEATLESS");
            Assert.AreEqual(2, seatless.PracticeOutings);
            Assert.AreEqual(2.0, seatless.PracticeAverage);
            Assert.AreEqual(PracticeReward.YoungChampionshipLevelUnproven, seatless.PracticeReward);

            var reserve = members.Single(m => m.DriverId == "RESERVE");
            Assert.AreEqual(1, reserve.PracticeOutings);

            var sister = members.Single(m => m.DriverId == "AT_SISTER");
            Assert.AreEqual(0, sister.PracticeOutings);
            Assert.IsNull(sister.PracticeAverage);
            Assert.AreEqual(12, sister.Points);
            Assert.AreEqual(9, sister.ChampionshipPosition);
        }

        [TestMethod]
        public void Build_ListedDriverWhoSignedOutside_IsAFormerMember()
        {
            var saveGame = Save(Driver("ELSEWHERE", 20, DriverReputation.YOUNG_TALENT));

            var academy = AcademyStandings.Build(saveGame).Single();

            Assert.AreEqual(0, academy.Members.Count);
            var former = academy.FormerMembers.Single();
            Assert.AreEqual("ELSEWHERE", former.DriverId);
            Assert.AreEqual("OUTSIDE", former.TeamId);
        }

        [TestMethod]
        public void Build_PlayersAcademyComesFirst_AndHeIsInIt()
        {
            var saveGame = Save(Driver("PLAYER", 19, DriverReputation.YOUNG_TALENT));
            saveGame.CurrentSeason.Academies.Add(new Academy { Id = "ferrari", ParentTeams = new() { "OUTSIDE" }, SisterTeams = new(), Drivers = new() });
            saveGame.PlayerData.AcademyId = "ferrari";

            var academies = AcademyStandings.Build(saveGame);

            Assert.AreEqual("ferrari", academies[0].Academy.Id);
            Assert.IsTrue(academies[0].IsPlayersAcademy);
            Assert.IsFalse(academies[1].IsPlayersAcademy);
            var player = academies[0].Members.Single();
            Assert.IsTrue(player.IsPlayer);
            Assert.AreEqual(AcademyMemberSeat.NO_SEAT, player.Seat);
        }

        private static PracticeOuting Outing(string driverId, int? score) =>
            new() { Year = Year, DriverId = driverId, TeamId = "PARENT", Score = score };

        private static IDriverData Driver(string id, int age, DriverReputation reputation) =>
            new DriverData { DriverId = id, Name = id, Nationality = "GBR", YearOfBirth = Year - age, Reputation = reputation };

        private static TeamEntry Team(string id, string driver1, string driver2, string reserve = null) => new()
        {
            TeamId = id,
            TeamName = id + " Racing",
            Color = "#123456",
            Driver1Contract = new DriverContract { DriverId = driver1, DriverNumber = 1 },
            Driver2Contract = new DriverContract { DriverId = driver2, DriverNumber = 2 },
            ReserveContract = reserve == null ? null : new DriverContract { DriverId = reserve }
        };

        private static SaveGame Save(params IDriverData[] drivers)
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = Year,
                    Races = new List<Race>(),
                    Absences = new List<Absence>(),
                    Teams = new List<ITeamEntry>
                    {
                        Team("PARENT", "AT_PARENT", "VETERAN", reserve: "RESERVE"),
                        Team("SISTER", "AT_SISTER", "VETERAN_2"),
                        Team("OUTSIDE", "ELSEWHERE", "VETERAN_3")
                    },
                    Academies = new List<Academy>
                    {
                        new()
                        {
                            Id = "red_bull",
                            ParentTeams = new List<string> { "PARENT" },
                            SisterTeams = new List<string> { "SISTER" },
                            Drivers = new List<string> { "AT_PARENT", "AT_SISTER", "RESERVE", "SEATLESS", "ELSEWHERE" }
                        }
                    }
                },
                Drivers = drivers.ToList(),
                RetiredDrivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Player" }
            };
        }
    }
}
