using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface IDriversLoader
    {
        Dictionary<string, IDriverData> LoadDriversBase(int seasonYear);

        /// <summary>Several seasons' drivers at once, per year: a loader may share the work between them.</summary>
        Dictionary<int, Dictionary<string, IDriverData>> LoadDriversBase(IEnumerable<int> seasonYears)
            => seasonYears.Distinct().ToDictionary(y => y, LoadDriversBase);
    }

    public interface IDriversLoader<TDriverData> : IDriversLoader
        where TDriverData : IDriverData
    {
        Dictionary<string, TDriverData> LoadDrivers(int seasonYear);
    }
}
