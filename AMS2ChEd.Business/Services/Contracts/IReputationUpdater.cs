using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.Services.Contracts
{
    public interface IReputationUpdater
    {
        DriverReputation GetNewReputationForInactiveDriver(DriverReputation currentReputation, int age, bool decays, int seasonYear);
        DriverReputation GetNewReputation(DriverReputation currentReputation, int age, int standings, int podiums, int dnfs, int races, int totalSeasonRaces, double averageRacePosition, int seasonYear);
    }
}
