using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Tests.Business.Services
{
    [TestClass]
    public class DriverHirerAssignUndefinedRolesTests
    {
        [TestMethod]
        public void AssignUndefinedRoles_Driver1HasBetterReputation_Driver1Leads()
        {
            var team = Team("D1", ContractRole.UNDEFINED, "D2", ContractRole.UNDEFINED);

            Assign(team,
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_MIDFIELD));

            Assert.AreEqual(ContractRole.FIRST_DRIVER, team.Driver1Contract.Role);
            Assert.AreEqual(ContractRole.SECOND_DRIVER, team.Driver2Contract.Role);
        }

        [TestMethod]
        public void AssignUndefinedRoles_Driver2HasBetterReputation_Driver2Leads()
        {
            var team = Team("D1", ContractRole.UNDEFINED, "D2", ContractRole.UNDEFINED);

            Assign(team,
                Driver("D1", DriverReputation.PRIME_MIDFIELD),
                Driver("D2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL));

            Assert.AreEqual(ContractRole.SECOND_DRIVER, team.Driver1Contract.Role);
            Assert.AreEqual(ContractRole.FIRST_DRIVER, team.Driver2Contract.Role);
        }

        [TestMethod]
        public void AssignUndefinedRoles_SameReputation_BothEqual()
        {
            var team = Team("D1", ContractRole.UNDEFINED, "D2", ContractRole.UNDEFINED);

            Assign(team,
                Driver("D1", DriverReputation.PRIME_MIDFIELD),
                Driver("D2", DriverReputation.PRIME_MIDFIELD));

            Assert.AreEqual(ContractRole.EQUAL, team.Driver1Contract.Role);
            Assert.AreEqual(ContractRole.EQUAL, team.Driver2Contract.Role);
        }

        [TestMethod]
        public void AssignUndefinedRoles_OneDriverTeam_DriverIsFirstDriver()
        {
            var team = Team("D1", ContractRole.UNDEFINED, "", ContractRole.UNDEFINED);

            Assign(team, Driver("D1", DriverReputation.PAY_DRIVER_SEASON));

            Assert.AreEqual(ContractRole.FIRST_DRIVER, team.Driver1Contract.Role);
            Assert.AreEqual(ContractRole.UNDEFINED, team.Driver2Contract.Role);
        }

        [TestMethod]
        public void AssignUndefinedRoles_RolesAlreadyDefined_AreLeftUntouched()
        {
            var equalTeam = Team("D1", ContractRole.EQUAL, "D2", ContractRole.EQUAL);
            var reversedTeam = Team("D1", ContractRole.SECOND_DRIVER, "D2", ContractRole.FIRST_DRIVER);

            Assign(equalTeam,
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_MIDFIELD));
            Assign(reversedTeam,
                Driver("D1", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL),
                Driver("D2", DriverReputation.PRIME_MIDFIELD));

            Assert.AreEqual(ContractRole.EQUAL, equalTeam.Driver1Contract.Role);
            Assert.AreEqual(ContractRole.EQUAL, equalTeam.Driver2Contract.Role);
            Assert.AreEqual(ContractRole.SECOND_DRIVER, reversedTeam.Driver1Contract.Role);
            Assert.AreEqual(ContractRole.FIRST_DRIVER, reversedTeam.Driver2Contract.Role);
        }

        [TestMethod]
        public void AssignUndefinedRoles_DriverNotInList_IsTreatedAsPrimeMidfield()
        {
            var team = Team("D1", ContractRole.UNDEFINED, "UNKNOWN", ContractRole.UNDEFINED);

            Assign(team, Driver("D1", DriverReputation.PRIME_STRONG_MIDFIELD));

            Assert.AreEqual(ContractRole.FIRST_DRIVER, team.Driver1Contract.Role);
            Assert.AreEqual(ContractRole.SECOND_DRIVER, team.Driver2Contract.Role);
        }

        private static void Assign(ITeamEntry team, params IDriverData[] drivers)
        {
            var season = new Season { Year = 2024, Teams = new List<ITeamEntry> { team } };
            DriverHirer.AssignUndefinedRoles(season, drivers);
        }

        private static ITeamEntry Team(string driver1Id, ContractRole driver1Role, string driver2Id, ContractRole driver2Role)
        {
            return new TeamEntry
            {
                TeamId = "T1",
                Reputation = TeamReputation.MIDFIELD,
                Driver1Contract = new DriverContract { DriverId = driver1Id, Role = driver1Role },
                Driver2Contract = new DriverContract { DriverId = driver2Id, Role = driver2Role }
            };
        }

        private static IDriverData Driver(string id, DriverReputation reputation)
        {
            return new DriverData { DriverId = id, Name = $"Driver {id}", Reputation = reputation };
        }
    }
}
