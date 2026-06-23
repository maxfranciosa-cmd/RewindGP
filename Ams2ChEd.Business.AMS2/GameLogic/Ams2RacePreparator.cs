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
            var seasonFilePath = StoragePaths.SeasonFilePath(season.OriginalYear ?? season.Year);
            var seasonFileDirectory = Path.GetDirectoryName(seasonFilePath);
            var ams2InstallationFolder = _ams2AppSettingsStorage.LoadSettings().Ams2Folder;

            var liveryService = new Ams2LiveryService(
                season.Year,
                ((Ams2Season)season).Ams2Class,
                drivers.Cast<Ams2DriverData>(),
                season.Teams.Cast<Ams2TeamEntry>());

            var ams2Folder = _ams2AppSettingsStorage;
           
            liveryService.GenerateRaceFiles(raceId, raceEntryList.ToList(), seasonFileDirectory, ams2InstallationFolder);
        }

    }
}
