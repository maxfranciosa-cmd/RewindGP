using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.GameLogic;
using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Services;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using AMS2ChEd.Business.AMS2.DataLoaders.Mocks;
using AMS2ChEd.Business.AMS2.GameLogic;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Business.Services.Mocks;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Services;
using AMS2ChEd.Commands;
using AMS2ChEd.Dialogs;
using AMS2ChEd.Services;
using AMS2ChEd.Updater;
using AMS2ChEd.ViewModels;
using AMS2ChEd.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace AMS2ChEd
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private ServiceProvider _serviceProvider;
        private readonly string versionCheckUrl = "https://www.overtake.gg/downloads/rewind-gp.82303";
        private readonly string downloadUrlFormat = "https://www.overtake.gg/downloads/{0}";
        public static ServiceProvider Services { get; private set; }

        private void ConfigureServices(ServiceCollection services, bool scenarioCreatorMode, bool forceAppUpdate, bool forceSeasonsUpdate, bool developerMode)
        {
            services.AddSingleton(new DeveloperModeSettings(developerMode));

            // ********* JSON LOADERS ************
            services.AddSingleton<IDriversLoader<Ams2DriverData>, DriversLoader>();
            services.AddSingleton<ISeasonLoader<Ams2Season>, SeasonLoader>();
            services.AddSingleton<ISeasonLoader, SeasonLoader>();
            services.AddSingleton<ITeamsLoader, TeamsLoader>();
            services.AddSingleton<IAccoladesLoader, AccoladesLoader>();
            // ********* MOCK LOADERS ************
            //services.AddSingleton<IDriversLoader<Ams2DriverData>, LoaderMocks199697>();
            //services.AddSingleton<ISeasonLoader<Ams2Season>, LoaderMocks199697>();
            //services.AddSingleton<ISeasonLoader, LoaderMocks199697>();
            //services.AddSingleton<ITeamsLoader, LoaderMocks199697>();
            // ***********************************
            services.AddSingleton<IGameStorage, GameStorage>();
            services.AddSingleton<IAms2AppSettingsStorage, SettingsStorage>();
            // ********** STORAGE FACTORY **************
            services.AddTransient<Ams2StorageFactory>();
            
            // ************ GAME LOGIC *******************
            services.AddTransient<IAbsenceManager, AbsenceManager>();
            services.AddTransient<IContractNegotiationEngine, ContractNegotiationEngine>();
            services.AddTransient<IEntryListGenerator, EntryListGenerator>();
            services.AddTransient<IStandingsManager, StandingsManager>();
            services.AddTransient<IEndOfSeasonManager, EndOfSeasonManager>();
            services.AddTransient<IReputationUpdater, ReputationUpdater>();
            services.AddTransient<IOffSeasonMovements, OffSeasonMovements>();
            services.AddTransient<IGameEngine, Ams2GameEngine>();
            services.AddTransient<IRandomDriverGenerator, Ams2RandomDriverGenerator>();
            services.AddTransient<IPreQualiPoolResolver, PreQualiPoolResolver>();

            if (scenarioCreatorMode)
            {
                services.AddTransient<IRacePreparator, StubRacePreparator>();
                services.AddTransient<IRaceDataService, MockUserControlledRaceDataService>();
            }
            else
            {
                services.AddTransient<IRacePreparator, Ams2RacePreparator>();
                services.AddTransient<IRaceDataService, Ams2RaceDataService>();
            }

            // ************ GAME LOGIC FACTORY **************
            services.AddTransient<GameLogicFactory>();

            // ************* OTHER DEPENDENCIES ***********
            services.AddTransient<DriverHirer>();
            services.AddTransient<DriverFirer>();
            services.AddTransient<SeasonModInstaller>();
            // ********************************************

            // Register Windows
            services.AddTransient<MainWindow>();

            SetupUpdater(services, forceAppUpdate, forceSeasonsUpdate);
        }

        private void SetupUpdater(ServiceCollection services, bool forceAppUpdate, bool forceSeasonsUpdate)
        {
            
            var versionCheckStore = new JsonCurrentVersionCheckStore(StoragePaths.CurrentVersionCheckPath);
            services.AddSingleton<SeasonUpdaterOrchestrator>();
            services.AddSingleton<ISeasonDownloadPrompt>((serviceProvider) => new WpfSeasonDownloadPrompt(downloadUrlFormat, serviceProvider.GetService<SeasonModInstaller>()));
            services.AddSingleton((serviceProvider) => new SeasonManifestService(StoragePaths.SeasonsFolder, StoragePaths.SeasonsManifestPath, serviceProvider.GetService<ISeasonLoader>(), File.ReadAllText, forceSeasonsUpdate));
            services.AddSingleton(versionCheckStore);
            services.AddSingleton<SaveGameSeasonChecker>();
            services.AddSingleton((serviceProvider) => new VersionCheckService(versionCheckUrl, versionCheckStore, forceAppUpdate));
        }


        protected override void OnStartup(StartupEventArgs e)
        {
            var exePath = Process.GetCurrentProcess().MainModule!.FileName; 
            FileAssociationHelper.Register(exePath, exePath);
            var services = new ServiceCollection();
            ConfigureServices(services, e.Args.Contains("--scenariocreatormode"), e.Args.Contains("--forceupdate"), e.Args.Contains("--forceseasonsupdate"), e.Args.Contains("--developermode"));
            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider; // Make it static for easy access
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            var shuttingDown = false;
            Application.Current.Exit += (s, _) => shuttingDown = true;

            var args = e.Args;

            _ = RunStartupChecksAsync(mainWindow, _serviceProvider.GetService<VersionCheckService>(), args)
        .ContinueWith(_ =>
        {
            if (shuttingDown) return Task.CompletedTask;

            if (args.Length > 0
                && args[0].EndsWith(".rwgp", StringComparison.OrdinalIgnoreCase)
                && File.Exists(args[0]))
            {
                // Marshal everything UI-related back to the UI thread
                return mainWindow.Dispatcher.InvokeAsync(async () =>
                {
                    var progressWindow = new ProgressWindow();
                    progressWindow.Show();

                    try
                    {
                        var result = await Task.Run(() =>
                            _serviceProvider.GetService<SeasonModInstaller>().InstallSeasonMod(args[0]));

                        progressWindow.Close();

                        if (result.Success)
                        {
                            MessageBox.Show(
                                result.GetDetailedReport(),
                                "Season Mod Installed Successfully",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show(
                                $"{result.Message}\n\n{result.Exception?.Message}",
                                "Season Mod Installation Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                    finally
                    {
                        if (progressWindow.IsLoaded)
                            progressWindow.Close();
                        CommandManager.InvalidateRequerySuggested();
                    }
                }).Task;
            }

            return Task.CompletedTask;

        }, TaskScheduler.Default);
        }

        private async Task RunStartupChecksAsync(MainWindow mainWindow, VersionCheckService versionCheck, string[] originalArgs)
        {
            var result = await versionCheck.CheckAsync();
            if (!result.IsUpdateAvailable) return;

            mainWindow.Dispatcher.Invoke(() =>
            {
                var dialog = new UpdateAvailableDialog(result, originalArgs);
                dialog.ShowDialog();

            });
        }


        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }

}
