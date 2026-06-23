using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface IDriversLoader<TDriverData> 
        where TDriverData : IDriverData
    {
        Dictionary<string, TDriverData> LoadDrivers(int seasonYear);
    }
}
