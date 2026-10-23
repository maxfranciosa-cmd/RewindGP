using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class ReserveAdviceTests
    {
        // 16 races this season and 16 next: a contract of 32 runs out after next season
        private const int RacesThisSeason = 16;
        private const int RacesNextSeason = 16;
        private const int EndsThisWinter = 16;
        private const int EndsAfterNextSeason = 32;
        private const int RunsLonger = 48;

        [TestMethod]
        public void OfficialSeatLikelyThisWinter_SaysSo()
        {
            var team = Team(("LEADER", RunsLonger, ContractRole.FIRST_DRIVER), ("WINGMAN", EndsThisWinter, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_STRONG_MIDFIELD));
            outlook.Seats.Add(new SeatOutlook { TeamId = "T", Slot = 2, Band = MarketChance.Likely });

            var advice = Advise(team, outlook, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN);

            Assert.IsTrue(advice.OfficialSeatNow);
        }

        [TestMethod]
        public void OfficialSeatOnlyPossible_OrTheTeamsReserveRow_IsNoOfficialSeat()
        {
            var team = Team(("LEADER", RunsLonger, ContractRole.FIRST_DRIVER), ("WINGMAN", EndsThisWinter, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_STRONG_MIDFIELD));
            outlook.Seats.Add(new SeatOutlook { TeamId = "T", Slot = 2, Band = MarketChance.Possible });
            outlook.Seats.Add(new SeatOutlook { TeamId = "T", Slot = TeamHiring.ReserveSlot, Band = MarketChance.VeryLikely });

            var advice = Advise(team, outlook, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN);

            Assert.IsFalse(advice.OfficialSeatNow);
        }

        [TestMethod]
        public void SecondSeatEndingAfterNextSeason_PerfectFit_WouldBePromoted()
        {
            var team = Team(("LEADER", RunsLonger, ContractRole.FIRST_DRIVER), ("WINGMAN", EndsAfterNextSeason, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN));

            // PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN is a PerfectFit for a top team's second seat
            var advice = Advise(team, outlook, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN);

            Assert.AreEqual(FutureSeats.SecondDriver, advice.FutureSeats);
            Assert.IsTrue(advice.WouldBePromoted);
        }

        [TestMethod]
        public void SecondSeatEndingAfterNextSeason_NotAPerfectFit_WouldNotBePromoted()
        {
            var team = Team(("LEADER", RunsLonger, ContractRole.FIRST_DRIVER), ("WINGMAN", EndsAfterNextSeason, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN));

            var advice = Advise(team, outlook, DriverReputation.PRIME_MIDFIELD);

            Assert.AreEqual(FutureSeats.SecondDriver, advice.FutureSeats);
            Assert.IsFalse(advice.WouldBePromoted);
        }

        [TestMethod]
        public void SeatEndingThisWinter_IsNotAFutureSeat_AndTheLineupIsUnknown()
        {
            // the market fills it this winter, before a reserve could be promoted to it
            var team = Team(("LEADER", RunsLonger, ContractRole.FIRST_DRIVER), ("WINGMAN", EndsThisWinter, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN));

            var advice = Advise(team, outlook, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN);

            Assert.AreEqual(FutureSeats.None, advice.FutureSeats);
            Assert.IsFalse(advice.WouldBePromoted);
            Assert.AreEqual(ReserveRaceTime.Unknown, advice.RaceTime);
        }

        [TestMethod]
        public void BothSeatsEndingAfterNextSeason_FirstDriverMaterial_WouldBePromoted()
        {
            var team = Team(("LEADER", EndsAfterNextSeason, ContractRole.FIRST_DRIVER), ("WINGMAN", EndsAfterNextSeason, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN));

            var advice = Advise(team, outlook, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL);

            Assert.AreEqual(FutureSeats.Both, advice.FutureSeats);
            Assert.IsTrue(advice.WouldBePromoted);
        }

        [TestMethod]
        public void BothSeatsUnderLongContracts_NoFutureSeat_NoPromotion()
        {
            var team = Team(("LEADER", RunsLonger, ContractRole.FIRST_DRIVER), ("WINGMAN", RunsLonger, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN));

            var advice = Advise(team, outlook, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN);

            Assert.AreEqual(FutureSeats.None, advice.FutureSeats);
            Assert.IsFalse(advice.WouldBePromoted);
        }

        [TestMethod]
        public void BothContracted_OneLevelApart_MayClash()
        {
            // PRIME_CHAMPIONSHIP_LEVEL (6) and PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN (5)
            var team = Team(("LEADER", RunsLonger, ContractRole.FIRST_DRIVER), ("WINGMAN", RunsLonger, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN));

            var advice = Advise(team, outlook, DriverReputation.PRIME_MIDFIELD);

            Assert.AreEqual(ReserveRaceTime.PossibleClash, advice.RaceTime);
        }

        [TestMethod]
        public void BothContracted_TwoLevelsApart_UnlikelyToClash()
        {
            // PRIME_CHAMPIONSHIP_LEVEL (6) and PRIME_STRONG_MIDFIELD (4)
            var team = Team(("LEADER", RunsLonger, ContractRole.FIRST_DRIVER), ("WINGMAN", RunsLonger, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_STRONG_MIDFIELD));

            var advice = Advise(team, outlook, DriverReputation.PRIME_MIDFIELD);

            Assert.AreEqual(ReserveRaceTime.UnlikelyClash, advice.RaceTime);
        }

        [TestMethod]
        public void RaceTime_ReadsNextSeasonsProjectedReputations()
        {
            // the wingman is projected to drop two levels: next season's pair is far apart
            var team = Team(("LEADER", RunsLonger, ContractRole.FIRST_DRIVER), ("WINGMAN", RunsLonger, ContractRole.SECOND_DRIVER));
            var outlook = Outlook(("LEADER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), ("WINGMAN", DriverReputation.PRIME_MIDFIELD));

            var advice = Advise(team, outlook, DriverReputation.PRIME_MIDFIELD);

            Assert.AreEqual(ReserveRaceTime.UnlikelyClash, advice.RaceTime);
        }

        private static ReserveAdvice Advise(TeamEntry team, MarketOutlook outlook, DriverReputation playerReputation)
        {
            return ReserveAdvice.For(team, team.Reputation, RacesThisSeason, RacesNextSeason, outlook, playerReputation);
        }

        private static TeamEntry Team((string DriverId, int Races, ContractRole Role) driver1, (string DriverId, int Races, ContractRole Role) driver2)
        {
            return new TeamEntry
            {
                TeamId = "T",
                TeamName = "Team T",
                Reputation = TeamReputation.TOP_TEAM,
                Driver1Contract = new DriverContract { DriverId = driver1.DriverId, Races = driver1.Races, Role = driver1.Role },
                Driver2Contract = new DriverContract { DriverId = driver2.DriverId, Races = driver2.Races, Role = driver2.Role }
            };
        }

        private static MarketOutlook Outlook(params (string DriverId, DriverReputation Reputation)[] projected)
        {
            var reputations = new Dictionary<string, DriverReputation>();
            foreach (var (driverId, reputation) in projected)
                reputations[driverId] = reputation;

            return new MarketOutlook { ProjectedReputations = reputations };
        }
    }
}
