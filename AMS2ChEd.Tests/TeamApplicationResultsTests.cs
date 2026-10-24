using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class TeamApplicationResultsTests
    {
        [TestMethod]
        public void BuildTeamApplicationResults_WonFirstDriverAdButTeammateLeads_ReportsBothRolesAndTeammate()
        {
            // Arrange - the player wins the first driver ad in car 1, but the stronger driver hired
            // for car 2 is given the lead
            var saveGame = CreateSaveGame();
            var ballots = new[] { CreateBallot("WILLIAMS", 1, DriverRole.FIRST_DRIVER) };
            var newSeason = CreateNewSeason("PLAYER", ContractRole.SECOND_DRIVER, "SUZUKI", ContractRole.FIRST_DRIVER);

            // Act
            var result = OffSeasonOrchestrator.BuildTeamApplicationResults(saveGame, ballots, newSeason).Single();

            // Assert
            Assert.IsTrue(result.PlayerHired);
            Assert.AreEqual(DriverRole.FIRST_DRIVER, result.AdvertisedRole);
            Assert.AreEqual(DriverRole.SECOND_DRIVER, result.Role);
            Assert.AreEqual("SUZUKI", result.TeammateId);
        }

        [TestMethod]
        public void BuildTeamApplicationResults_RoleAsAdvertised_RolesMatch()
        {
            var saveGame = CreateSaveGame();
            var ballots = new[] { CreateBallot("WILLIAMS", 2, DriverRole.SECOND_DRIVER) };
            var newSeason = CreateNewSeason("SUZUKI", ContractRole.FIRST_DRIVER, "PLAYER", ContractRole.SECOND_DRIVER);

            var result = OffSeasonOrchestrator.BuildTeamApplicationResults(saveGame, ballots, newSeason).Single();

            Assert.IsTrue(result.PlayerHired);
            Assert.AreEqual(result.AdvertisedRole, result.Role);
            Assert.AreEqual("SUZUKI", result.TeammateId);
        }

        [TestMethod]
        public void BuildTeamApplicationResults_OneCarTeam_HasNoTeammate()
        {
            var saveGame = CreateSaveGame();
            var ballots = new[] { CreateBallot("WILLIAMS", 1, DriverRole.FIRST_DRIVER) };
            var newSeason = CreateNewSeason("PLAYER", ContractRole.EQUAL, "", ContractRole.UNDEFINED);

            var result = OffSeasonOrchestrator.BuildTeamApplicationResults(saveGame, ballots, newSeason).Single();

            Assert.IsTrue(result.PlayerHired);
            Assert.IsNull(result.TeammateId);
            // an EQUAL role leaves the advertised one in place: no role change to report
            Assert.AreEqual(result.AdvertisedRole, result.Role);
        }

        [TestMethod]
        public void BuildTeamApplicationResults_TeamsPickSignedForAnotherTeam_IsNotReportedAsBeaten()
        {
            // Arrange - Williams wanted Senna, but Ferrari's ballot ran first and signed him
            var saveGame = CreateSaveGame();
            var ballot = CreateBallot("WILLIAMS", 1, DriverRole.FIRST_DRIVER);
            ballot.OriginalTeamHiring.DriverId = "SENNA";
            var newSeason = CreateNewSeason("PLAYER", ContractRole.FIRST_DRIVER, "SUZUKI", ContractRole.SECOND_DRIVER);
            newSeason.Teams = newSeason.Teams.Append(new TeamEntry
            {
                TeamId = "FERRARI",
                Reputation = TeamReputation.TOP_TEAM,
                Driver1Contract = new DriverContract { DriverId = "SENNA", Role = ContractRole.FIRST_DRIVER },
                Driver2Contract = new DriverContract { DriverId = "BERGER", Role = ContractRole.SECOND_DRIVER }
            }).ToList();

            // Act
            var result = OffSeasonOrchestrator.BuildTeamApplicationResults(saveGame, new[] { ballot }, newSeason).Single();

            // Assert
            Assert.IsTrue(result.PlayerHired);
            Assert.AreEqual("SENNA", result.OtherDriverId);
            Assert.IsTrue(result.OtherDriverSignedElsewhere);
        }

        [TestMethod]
        public void BuildTeamApplicationResults_TeamPickedPlayer_RivalIsNeverACandidateWhoSignedElsewhere()
        {
            // Arrange - the team had picked the player already; Senna applied too but Ferrari
            // signed him first, so the rival named is the best candidate still without a seat
            var saveGame = CreateSaveGame();
            var ballot = CreateBallot("WILLIAMS", 1, DriverRole.FIRST_DRIVER);
            ballot.OriginalTeamHiring.DriverId = "PLAYER";
            ballot.Candidates = new List<TeamHiringBallotCandidate>
            {
                new TeamHiringBallotCandidate { DriverId = "SENNA", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new TeamHiringBallotCandidate { DriverId = "MODENA", DriverReputation = DriverReputation.YOUNG_TALENT }
            };
            var newSeason = CreateNewSeason("PLAYER", ContractRole.FIRST_DRIVER, "SUZUKI", ContractRole.SECOND_DRIVER);
            newSeason.Teams = newSeason.Teams.Append(new TeamEntry
            {
                TeamId = "FERRARI",
                Reputation = TeamReputation.TOP_TEAM,
                Driver1Contract = new DriverContract { DriverId = "SENNA", Role = ContractRole.FIRST_DRIVER },
                Driver2Contract = new DriverContract { DriverId = "BERGER", Role = ContractRole.SECOND_DRIVER }
            }).ToList();

            // Act
            var result = OffSeasonOrchestrator.BuildTeamApplicationResults(saveGame, new[] { ballot }, newSeason).Single();

            // Assert
            Assert.IsTrue(result.PlayerHired);
            Assert.AreEqual("MODENA", result.OtherDriverId);
        }

        [TestMethod]
        public void BuildTeamApplicationResults_PlayerBeatTeamsPickHeadToHead_IsReportedAsBeaten()
        {
            // Arrange - the team's pick lost to the player and is left without a seat
            var saveGame = CreateSaveGame();
            var ballot = CreateBallot("WILLIAMS", 1, DriverRole.FIRST_DRIVER);
            var newSeason = CreateNewSeason("PLAYER", ContractRole.FIRST_DRIVER, "SUZUKI", ContractRole.SECOND_DRIVER);

            // Act
            var result = OffSeasonOrchestrator.BuildTeamApplicationResults(saveGame, new[] { ballot }, newSeason).Single();

            // Assert
            Assert.AreEqual("SOMEONE", result.OtherDriverId);
            Assert.IsFalse(result.OtherDriverSignedElsewhere);
        }

        [TestMethod]
        public void BuildTeamApplicationResults_TeamsPickTookItsOtherSeat_NamesNoRival()
        {
            var saveGame = CreateSaveGame();
            var ballot = CreateBallot("WILLIAMS", 1, DriverRole.FIRST_DRIVER);
            ballot.OriginalTeamHiring.DriverId = "SUZUKI";
            var newSeason = CreateNewSeason("PLAYER", ContractRole.SECOND_DRIVER, "SUZUKI", ContractRole.FIRST_DRIVER);

            var result = OffSeasonOrchestrator.BuildTeamApplicationResults(saveGame, new[] { ballot }, newSeason).Single();

            Assert.IsNull(result.OtherDriverId);
            Assert.IsFalse(result.OtherDriverSignedElsewhere);
        }

        private static ISaveGame CreateSaveGame()
        {
            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = 1988,
                    Teams = new List<ITeamEntry>
                    {
                        new TeamEntry
                        {
                            TeamId = "WILLIAMS",
                            Reputation = TeamReputation.TOP_TEAM,
                            Driver1Contract = new DriverContract { DriverId = "MANSELL", Role = ContractRole.FIRST_DRIVER },
                            Driver2Contract = new DriverContract { DriverId = "PATRESE", Role = ContractRole.SECOND_DRIVER }
                        }
                    }
                },
                Drivers = new List<IDriverData>
                {
                    new DriverData { DriverId = "PLAYER", Name = "Player", Reputation = DriverReputation.YOUNG_TALENT },
                    new DriverData { DriverId = "SUZUKI", Name = "Aguri Suzuki", Reputation = DriverReputation.PRIME_MIDFIELD }
                },
                PlayerData = new PlayerData { DriverId = "PLAYER" }
            };
        }

        private static TeamHiringBallot CreateBallot(string teamId, int slot, DriverRole role)
        {
            return new TeamHiringBallot
            {
                OriginalTeamHiring = new TeamHiring
                {
                    TeamId = teamId,
                    Slot = slot,
                    Role = role,
                    TeamReputation = TeamReputation.TOP_TEAM,
                    DriverId = "SOMEONE",
                    DriverReputation = DriverReputation.YOUNG_TALENT
                },
                Candidates = new List<TeamHiringBallotCandidate>
                {
                    new TeamHiringBallotCandidate { DriverId = "PLAYER", DriverReputation = DriverReputation.YOUNG_TALENT }
                }
            };
        }

        private static ISeason CreateNewSeason(string driver1Id, ContractRole driver1Role, string driver2Id, ContractRole driver2Role)
        {
            return new Season
            {
                Year = 1989,
                Teams = new List<ITeamEntry>
                {
                    new TeamEntry
                    {
                        TeamId = "WILLIAMS",
                        Reputation = TeamReputation.TOP_TEAM,
                        Driver1Contract = new DriverContract { DriverId = driver1Id, Role = driver1Role },
                        Driver2Contract = new DriverContract { DriverId = driver2Id, Role = driver2Role }
                    }
                }
            };
        }
    }
}
