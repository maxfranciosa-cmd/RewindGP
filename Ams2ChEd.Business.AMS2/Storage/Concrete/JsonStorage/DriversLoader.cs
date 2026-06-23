using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class DriversLoader : IDriversLoader<Ams2DriverData>
    {
        public Dictionary<string, Ams2DriverData> LoadDrivers(int seasonYear)
        {
            try
            {
                if (!File.Exists(StoragePaths.DriversFilePath(seasonYear)))
                {
                    throw new FileNotFoundException($"Drivers database not found at: {StoragePaths.DriversFilePath(seasonYear)}");
                }

                string json = File.ReadAllText(StoragePaths.DriversFilePath(seasonYear));
                var driversDb = JsonSerializer.Deserialize<DriverRatingsDatabase>(json, DefaultJsonSerializerOptions.Instance);
                return driversDb.Drivers.ToDictionary(d => d.DriverId, d => (Ams2DriverData)d);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading drivers database: {ex.Message}", ex);
            }
        }
    }
}
