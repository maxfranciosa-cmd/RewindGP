using AMS2ChEd.Business.AMS2.Models;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Storage.Contracts;

namespace Ams2ChEd.Business.AMS2.DependencyInjection
{
    public class Ams2StorageFactory : StorageFactory<Ams2DriverData, Ams2Season>
    {
        public IAms2AppSettingsStorage Ams2AppSettingsStorage { get; private set; }

        public Ams2StorageFactory(
            IDriversLoader<Ams2DriverData> driversLoader,
            ITeamsLoader teamsLoader,
            ISeasonLoader<Ams2Season> seasonLoader,
            IGameStorage gameStorage,
            IAccoladesLoader accoladesLoader,
            IAms2AppSettingsStorage ams2AppSettingsStorage) : base(driversLoader, teamsLoader, seasonLoader, gameStorage, accoladesLoader)
        {
            Ams2AppSettingsStorage = ams2AppSettingsStorage;
        }
    }
}
