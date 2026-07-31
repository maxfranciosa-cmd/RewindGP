using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Settings;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.Settings.Contracts;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.SeasonPackEditor.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.Data;
using System.Windows;

namespace AMS2ChEd.SeasonPackEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        public static ServiceProvider Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);

            // Register Windows
            services.AddTransient<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider;
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // ********* JSON LOADERS ************
            services.AddSingleton<IDriversLoader<Ams2DriverData>, DriversLoader>();
            services.AddSingleton<ISeasonLoader<Ams2Season>, SeasonLoader>();
            services.AddSingleton<ITeamsLoader, TeamsLoader>();
            // ********** STORAGE FACTORY **************
            // GameStorage/AccoladesLoader are required by Ams2StorageFactory's constructor but are
            // never actually exercised from SeasonPackCreator. IAms2AppSettingsStorage's Ams2Folder is
            // used by the in-sim calibration feature (PerformanceCalibrationDialog) to write CustomAI
            // roster/livery files directly into a real AMS2 install - see Ams2SettingsStorage.
            services.AddSingleton<IGameStorage, GameStorage>();
            services.AddSingleton<IAccoladesLoader, AccoladesLoader>();
            services.AddSingleton<IAms2AppSettingsStorage, Ams2SettingsStorage>();
            // Ams2StorageFactory's constructor requires IGameInstallSettingsStorage (post game-module
            // decoupling refactor) - separate from IAms2AppSettingsStorage above, which older
            // SeasonPackCreator dialogs still consume directly.
            services.AddSingleton<IGameInstallSettingsStorage, Ams2GameInstallSettingsStorage>();
            services.AddTransient<Ams2StorageFactory>();
        }
    }
}
