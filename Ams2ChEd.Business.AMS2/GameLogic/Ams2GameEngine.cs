using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.AMS2.GameLogic
{
    public class Ams2GameEngine : GameEngine
    {
        private readonly Ams2StorageFactory _storageFactory;

        public Ams2GameEngine(Ams2StorageFactory storageFactory)
        {
            _storageFactory = storageFactory;
        }

        protected override HistoricalAccolades LoadAccoladesForNewGame(int seasonYear)
            => _storageFactory.AccoladesLoader.LoadAccolades(seasonYear);

        protected override IDriverData InitializeConcretePlayerDriverData(IDriverData provisionalDriverData, IPlayerData playerData, ISeason season)
        {
            var ams2PlayerDriverData = provisionalDriverData.ConvertToChild<IDriverData, Ams2DriverData>();

            ams2PlayerDriverData.BaseHelmetFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet.png");
            ams2PlayerDriverData.BaseVisorFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor.png");

            ams2PlayerDriverData.BaseHelmetFile90s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_90s.png");

            ams2PlayerDriverData.BaseHelmetFile80s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_80s.png");
            ams2PlayerDriverData.BaseVisorFile80s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_80s.png");

            ams2PlayerDriverData.BaseHelmetFile70s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_70s.png");
            ams2PlayerDriverData.BaseVisorFile70s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_70s.png");

            return ams2PlayerDriverData;
        }
    }
}