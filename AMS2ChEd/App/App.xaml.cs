using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Business.Settings;
using AMS2ChEd.Business.Storage;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Services;
using AMS2ChEd.Commands;
using AMS2ChEd.Dialogs;
using AMS2ChEd.Services;
using AMS2ChEd.Updater;
using AMS2ChEd.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
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

        private void ConfigureServices(ServiceCollection services, bool forceAppUpdate, bool forceSeasonsUpdate, bool developerMode)
        {
            services.AddSingleton(new DeveloperModeSettings(developerMode));

            // ************ GAME LOGIC (game-agnostic) *******************
            services.AddTransient<IAbsenceManager, AbsenceManager>();
            services.AddTransient<IContractNegotiationEngine, ContractNegotiationEngine>();
            services.AddTransient<IEntryListGenerator, EntryListGenerator>();
            services.AddTransient<IStandingsManager, StandingsManager>();
            services.AddTransient<IEndOfSeasonManager, EndOfSeasonManager>();
            services.AddTransient<IReputationUpdater, ReputationUpdater>();
            services.AddTransient<IOffSeasonMovements, OffSeasonMovements>();
            services.AddTransient<IPreQualiPoolResolver, PreQualiPoolResolver>();
            services.AddTransient<IOffSeasonOrchestrator, OffSeasonOrchestrator>();

            // ************ GAME LOGIC FACTORY **************
            services.AddTransient<GameLogicFactory>();

            // ************* OTHER DEPENDENCIES ***********
            services.AddTransient<DriverHirer>();
            services.AddTransient<DriverFirer>();
            services.AddSingleton<IExternalLiveriesPrompt, WpfExternalLiveriesPrompt>();
            // ********************************************

            // ************* GAME MODULE (only place that touches Ams2ChEd.Business.AMS2) ***********
            IGameModule gameModule = new Ams2GameModule();
            services.AddSingleton(gameModule);
            gameModule.RegisterServices(services, new GameModuleStartupOptions
            {
                DeveloperMode = developerMode
            });
            // ********************************************

            // Register Windows
            services.AddTransient<MainWindow>();

            SetupUpdater(services, gameModule, forceAppUpdate, forceSeasonsUpdate);
        }

        private void SetupUpdater(ServiceCollection services, IGameModule gameModule, bool forceAppUpdate, bool forceSeasonsUpdate)
        {

            var versionCheckStore = new JsonCurrentVersionCheckStore(AppPaths.CurrentVersionCheckPath);
            services.AddSingleton<SeasonUpdaterOrchestrator>();
            services.AddSingleton<ISeasonDownloadPrompt>((serviceProvider) => new WpfSeasonDownloadPrompt(
                downloadUrlFormat,
                serviceProvider.GetService<ISeasonPackInstaller>(),
                serviceProvider.GetService<IExternalLiveriesInstaller>(),
                serviceProvider.GetService<IExternalLiveriesPrompt>()));
            services.AddSingleton((serviceProvider) => new SeasonManifestService(AppPaths.SeasonsFolder, AppPaths.SeasonsManifestPath(gameModule.SeasonsManifestFileName), serviceProvider.GetService<ISeasonLoader>(), File.ReadAllText, forceSeasonsUpdate));
            services.AddSingleton(versionCheckStore);
            services.AddSingleton<ICurrentVersionCheckStore>(versionCheckStore);
            services.AddSingleton<SaveGameSeasonChecker>();
            services.AddSingleton((serviceProvider) => new VersionCheckService(versionCheckUrl, versionCheckStore, forceAppUpdate));
        }


        protected override void OnStartup(StartupEventArgs e)
        {
            ApplyCulture(e.Args);

            var exePath = Process.GetCurrentProcess().MainModule!.FileName;
            FileAssociationHelper.Register(exePath, exePath);
            var services = new ServiceCollection();
            ConfigureServices(services, e.Args.Contains("--forceupdate"), e.Args.Contains("--forceseasonsupdate"), e.Args.Contains("--developermode"));
            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider; // Make it static for easy access
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            mainWindow.ShowPrerequisiteIfNeeded();

            var shuttingDown = false;
            Application.Current.Exit += (s, _) => shuttingDown = true;

            var args = e.Args;

            _ = RunStartupChecksAsync(mainWindow, _serviceProvider.GetService<VersionCheckService>(), args)
        .ContinueWith(_ =>
        {
            if (shuttingDown) return Task.CompletedTask;

            var seasonPackInstaller = _serviceProvider.GetService<ISeasonPackInstaller>();

            if (args.Length > 0
                && args[0].EndsWith(seasonPackInstaller.PackFileExtension, StringComparison.OrdinalIgnoreCase)
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
                            seasonPackInstaller.InstallSeasonMod(args[0]));

                        progressWindow.Close();

                        if (result.Success)
                        {
                            var externalLiveries = _serviceProvider.GetService<IExternalLiveriesInstaller>();
                            if (externalLiveries.HasExternalLiveries(result.SeasonYear))
                            {
                                var liveriesInstalled = await externalLiveries.InstallAsync(
                                    result.SeasonYear,
                                    _serviceProvider.GetService<IExternalLiveriesPrompt>());
                                if (!liveriesInstalled)
                                    result.CleanupWarning = (string.IsNullOrEmpty(result.CleanupWarning) ? "" : result.CleanupWarning + "\n")
                                        + "External livery pack was not downloaded. Reinstall the season pack to be prompted again.";
                            }

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

        /// <summary>
        /// Sets the UI culture for the whole process, before any window is constructed. Language
        /// switching is restart-based (no live-switching library, e.g. WPFLocalizeExtension, is
        /// used), so this is the only place culture ever gets set. The saved preference lives in
        /// AppLanguageSettings (game-agnostic - a UI language isn't an AMS2-specific concern), with
        /// a "--culture=xx" arg override for fast iteration while converting windows.
        /// </summary>
        private static void ApplyCulture(string[] args)
        {
            const string cultureArgPrefix = "--culture=";
            var cultureArg = args.FirstOrDefault(a => a.StartsWith(cultureArgPrefix, StringComparison.OrdinalIgnoreCase));
            var languageCode = cultureArg != null
                ? cultureArg.Substring(cultureArgPrefix.Length)
                : AppLanguageSettings.LoadLanguageCode();

            CultureInfo culture;
            try
            {
                culture = new CultureInfo(languageCode);
            }
            catch (CultureNotFoundException)
            {
                culture = new CultureInfo("en");
            }

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // FrameworkElement.Language defaults to en-US regardless of thread culture unless
            // overridden - several windows use culture-sensitive StringFormat bindings that read it.
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
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
