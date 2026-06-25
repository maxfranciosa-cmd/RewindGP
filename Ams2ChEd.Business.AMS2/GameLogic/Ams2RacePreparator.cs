using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Services;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace Ams2ChEd.Business.AMS2.GameLogic
{
    public class Ams2RacePreparator : IRacePreparator
    {
        private IAms2AppSettingsStorage _ams2AppSettingsStorage;
        public Ams2RacePreparator(IAms2AppSettingsStorage ams2AppSettingsStorage)
        {
            _ams2AppSettingsStorage = ams2AppSettingsStorage;
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
            var ams2InstallationFolder = _ams2AppSettingsStorage.LoadSettings().Ams2Folder;

            var liveryService = new Ams2LiveryService(
                season.Year,
                ((Ams2Season)season).Ams2Class,
                drivers.Cast<Ams2DriverData>(),
                season.Teams.Cast<Ams2TeamEntry>());

            return (liveryService, seasonFileDirectory, ams2InstallationFolder);
        }

    }
}
