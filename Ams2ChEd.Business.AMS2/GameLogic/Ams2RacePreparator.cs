using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Settings.Contracts;

namespace Ams2ChEd.Business.AMS2.GameLogic
{
    public class Ams2RacePreparator : IRacePreparator
    {
        private IGameInstallSettingsStorage _installSettingsStorage;
        private ICarModelCapacityLoader _carModelCapacityLoader;
        public Ams2RacePreparator(IGameInstallSettingsStorage installSettingsStorage, ICarModelCapacityLoader carModelCapacityLoader)
        {
            _installSettingsStorage = installSettingsStorage;
            _carModelCapacityLoader = carModelCapacityLoader;
        }

        public void PrepareRace(int raceId, IEnumerable<EntryListEntry> raceEntryList, IEnumerable<IDriverData> drivers, ISeason season)
        {
            var (liveryService, seasonFileDirectory, ams2InstallationFolder) = BuildLiveryService(drivers, season);
            liveryService.GenerateRaceFiles(raceId, raceEntryList.ToList(), seasonFileDirectory, ams2InstallationFolder);
        }

        public void PrepareCustomAi(int raceId, IEnumerable<EntryListEntry> raceEntryList, IEnumerable<IDriverData> drivers, ISeason season)
        {
            var (liveryService, _, ams2InstallationFolder) = BuildLiveryService(drivers, season);
            liveryService.GenerateCustomAiOnly(raceId, raceEntryList.ToList(), ams2InstallationFolder);
        }

        public void PrepareLiveries(int raceId, IEnumerable<EntryListEntry> raceEntryList, IEnumerable<IDriverData> drivers, ISeason season)
        {
            var (liveryService, seasonFileDirectory, ams2InstallationFolder) = BuildLiveryService(drivers, season);
            liveryService.GenerateLiveriesOnly(raceId, raceEntryList.ToList(), seasonFileDirectory, ams2InstallationFolder);
        }

        private (Ams2LiveryService liveryService, string seasonFileDirectory, string ams2InstallationFolder) BuildLiveryService(IEnumerable<IDriverData> drivers, ISeason season)
        {
            var seasonFilePath = StoragePaths.SeasonFilePath(season.OriginalYear ?? season.Year);
            var seasonFileDirectory = Path.GetDirectoryName(seasonFilePath);
            var ams2InstallationFolder = _installSettingsStorage.LoadSettings().GameInstallFolder;
            var ams2Class = ((Ams2Season)season).Ams2Class;

            var modelCapacities = _carModelCapacityLoader.GetModelsForClass(ams2Class);

            var liveryService = new Ams2LiveryService(
                season.Year,
                ams2Class,
                drivers.Cast<Ams2DriverData>(),
                season.Teams.Cast<Ams2TeamEntry>(),
                modelCapacities);

            return (liveryService, seasonFileDirectory, ams2InstallationFolder);
        }

    }
}
