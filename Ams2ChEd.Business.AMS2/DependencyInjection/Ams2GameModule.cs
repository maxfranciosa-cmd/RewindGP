using AMS2ChEd.Business.AMS2.GameLogic;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Business.Services.Mocks;
using AMS2ChEd.Business.Settings.Contracts;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
using Ams2ChEd.Business.AMS2.GameLogic;
using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.PakPatching;
using Ams2ChEd.Business.AMS2.PakPatching.Contracts;
using Ams2ChEd.Business.AMS2.Services;
using Ams2ChEd.Business.AMS2.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Ams2ChEd.Business.AMS2.DependencyInjection
{
    public class Ams2GameModule : IGameModule
    {
        public string GameId => "AMS2";

        public string DisplayName => "Automobilista 2";

        public string SeasonsManifestFileName => "seasons_manifest.json";

        public void RegisterServices(IServiceCollection services, GameModuleStartupOptions options)
        {
            // ********* JSON LOADERS ************
            services.AddSingleton<IDriversLoader<Ams2DriverData>, DriversLoader>();
            services.AddSingleton<ISeasonLoader<Ams2Season>, SeasonLoader>();
            services.AddSingleton<ISeasonLoader, SeasonLoader>();
            services.AddSingleton<ITeamsLoader, TeamsLoader>();
            services.AddSingleton<IAccoladesLoader, AccoladesLoader>();
            services.AddSingleton<ICarModelCapacityLoader, CarModelCapacityLoader>();
            services.AddSingleton<IGameStorage, GameStorage>();
            services.AddSingleton<IGameInstallSettingsStorage, Ams2GameInstallSettingsStorage>();
            services.AddSingleton<IVehicleLiverySlotPatcher, Ams2VehicleLiverySlotPatcher>();

            // ********** STORAGE FACTORY **************
            services.AddTransient<Ams2StorageFactory>();
            services.AddTransient<IGameDataFactory>(sp => sp.GetRequiredService<Ams2StorageFactory>());

            // ************ GAME LOGIC *******************
            services.AddTransient<IGameEngine, Ams2GameEngine>();
            services.AddTransient<IRandomDriverGenerator, Ams2RandomDriverGenerator>();
            services.AddTransient<IRaceSetupAdvisor, Ams2RaceSetupAdvisor>();
            services.AddTransient<IPlayerCosmeticsEditor, Ams2PlayerCosmeticsEditor>();
            services.AddTransient<IRacePreparator, Ams2RacePreparator>();
            services.AddTransient<IRaceDataService, Ams2RaceDataService>();

            // ************* RACE LAUNCH / OVERLAY *******************
            services.AddTransient<IPrerequisiteWindow, Ams2PrerequisiteWindow>();
            services.AddTransient<IRaceLaunchAssistant, Ams2RaceLaunchAssistant>();
            services.AddSingleton<ITrackMappingLoader, Ams2TrackMappingLoader>();
            services.AddSingleton<IAms2HashCatalogProvider, Ams2InteropHashCatalogLoader>();
            services.AddTransient<IAms2DlcOwnershipChecker, Ams2DlcOwnershipChecker>();
            services.AddTransient<IAms2GrandPrixTrackResolver, Ams2GrandPrixTrackResolver>();

            // ************* MOD PACK / EXTERNAL CONTENT ***********
            services.AddTransient<ISeasonPackInstaller, SeasonModInstaller>();
            services.AddTransient<SeasonModInstaller>();
            services.AddSingleton<IExternalLiveriesInstaller, ExternalLiveriesInstaller>();
        }
    }
}
