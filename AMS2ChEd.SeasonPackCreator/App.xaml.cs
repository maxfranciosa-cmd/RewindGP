using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.Storage.Contracts;
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
            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider;
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // Register Windows
            services.AddTransient<MainWindow>();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // ********* JSON LOADERS ************
            services.AddSingleton<IDriversLoader<Ams2DriverData>, DriversLoader>();
            services.AddSingleton<ISeasonLoader<Ams2Season>, SeasonLoader>();
            services.AddSingleton<ITeamsLoader, TeamsLoader>();
            // ********** STORAGE FACTORY **************
            services.AddTransient<Ams2StorageFactory>();
        }
    }
}
