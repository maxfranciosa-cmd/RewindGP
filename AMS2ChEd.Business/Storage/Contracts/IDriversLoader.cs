using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface IDriversLoader
    {
        Dictionary<string, IDriverData> LoadDriversBase(int seasonYear);
    }

    public interface IDriversLoader<TDriverData> : IDriversLoader
        where TDriverData : IDriverData
    {
        Dictionary<string, TDriverData> LoadDrivers(int seasonYear);
    }
}
