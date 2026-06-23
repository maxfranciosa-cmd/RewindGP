using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Services.Contracts
{
    public interface IRandomDriverGenerator
    {
        IDriverData GenerateDriver(IEnumerable<IDriverData> existingDrivers, int year);
    }
}
