using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Views;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace AMS2ChEd.Commands
{
    public class SeasonInstalledEventArgs : EventArgs
    {
        public int SeasonYear { get; set; }
        public bool IsUpdate { get; set; }
    }

    public class InstallSeasonModCommandAsync : ICommand
    {
        private readonly SeasonModInstaller _installer;
        private readonly ExternalLiveriesInstaller _externalLiveriesInstaller;
        private readonly IExternalLiveriesPrompt _externalLiveriesPrompt;
        private bool _isExecuting;

        public event EventHandler<SeasonInstalledEventArgs> SeasonInstalled;

        public InstallSeasonModCommandAsync(
            Ams2StorageFactory storageFactory,
            ExternalLiveriesInstaller externalLiveriesInstaller,
            IExternalLiveriesPrompt externalLiveriesPrompt)
        {
            var driversLoader = storageFactory.DriversLoader;
            var teamsLoader = storageFactory.TeamsLoader;
            _installer = new SeasonModInstaller(driversLoader, teamsLoader);
            _externalLiveriesInstaller = externalLiveriesInstaller;
            _externalLiveriesPrompt = externalLiveriesPrompt;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return !_isExecuting;
        }

        public async void Execute(object parameter)
        {
            ExecuteAsync(parameter);
        }

        public async Task ExecuteAsync(object parameter)
        {
            if (_isExecuting)
                return;

            try
            {
                var zipFilePath = parameter as string;

                if (string.IsNullOrEmpty(zipFilePath) || Path.GetExtension(zipFilePath) == ".rwgp")
                {
                    // Open file dialog
                    var openFileDialog = new OpenFileDialog
                    {
                        Title = "Select Season Mod Package",
                        Filter = "Rewind GP Season Pack (*.rwgp)|*.rwgp",
                        CheckFileExists = true,
                        CheckPathExists = true
                    };
                    if (openFileDialog.ShowDialog() != DialogResult.OK) return;

                    zipFilePath = openFileDialog.FileName;
                }

                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();

                // Show progress window
                var progressWindow = new ProgressWindow();
                progressWindow.Show();

                try
                {
                    // Install the mod asynchronously
                    var result = await Task.Run(() => _installer.InstallSeasonMod(zipFilePath));

                    progressWindow.Close();

                    // Show result
                    if (result.Success)
                    {
                        if (_externalLiveriesInstaller.HasExternalLiveries(result.SeasonYear))
                        {
                            var liveriesInstalled = await _externalLiveriesInstaller.InstallAsync(result.SeasonYear, _externalLiveriesPrompt);
                            if (!liveriesInstalled)
                                result.CleanupWarning = (string.IsNullOrEmpty(result.CleanupWarning) ? "" : result.CleanupWarning + "\n")
                                    + "External livery pack was not downloaded. Reinstall the season pack to be prompted again.";
                        }

                        SeasonInstalled?.Invoke(this, new SeasonInstalledEventArgs
                        {
                            SeasonYear = result.SeasonYear,
                            IsUpdate = result.IsUpdate
                        });

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

                    _isExecuting = false;
                    CommandManager.InvalidateRequerySuggested();
                }

            }
            catch (Exception ex)
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();

                MessageBox.Show(
                    $"An error occurred while installing the season mod:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}