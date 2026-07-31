using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Settings.Contracts;
using AMS2ChEd.Business.Storage.Contracts;

namespace Ams2ChEd.Business.AMS2.DependencyInjection
{
    public class Ams2StorageFactory : StorageFactory<Ams2DriverData, Ams2Season>
    {
        public IGameInstallSettingsStorage InstallSettingsStorage { get; private set; }

        public Ams2StorageFactory(
            IDriversLoader<Ams2DriverData> driversLoader,
            ITeamsLoader teamsLoader,
            ISeasonLoader<Ams2Season> seasonLoader,
            IGameStorage gameStorage,
            IAccoladesLoader accoladesLoader,
            IGameInstallSettingsStorage installSettingsStorage) : base(driversLoader, teamsLoader, seasonLoader, gameStorage, accoladesLoader)
        {
            InstallSettingsStorage = installSettingsStorage;
        }
    }
}
