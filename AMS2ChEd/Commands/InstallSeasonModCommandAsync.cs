using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Updater;
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
        private readonly ISeasonPackInstaller _installer;
        private readonly IExternalLiveriesInstaller _externalLiveriesInstaller;
        private readonly IExternalLiveriesPrompt _externalLiveriesPrompt;
        private readonly SkinPackImportFlow _skinPackImport;
        private bool _isExecuting;

        public event EventHandler<SeasonInstalledEventArgs> SeasonInstalled;

        /// <summary>Raised after a skin pack archive was imported (artwork only).</summary>
        public event EventHandler SkinPackImported;

        public InstallSeasonModCommandAsync(
            ISeasonPackInstaller installer,
            IExternalLiveriesInstaller externalLiveriesInstaller,
            IExternalLiveriesPrompt externalLiveriesPrompt,
            SkinPackImportFlow skinPackImport = null)
        {
            _installer = installer;
            _externalLiveriesInstaller = externalLiveriesInstaller;
            _externalLiveriesPrompt = externalLiveriesPrompt;
            _skinPackImport = skinPackImport;
        }

        private bool IsSkinPackArchive(string path) =>
            _skinPackImport != null && !string.IsNullOrEmpty(path)
            && !string.Equals(Path.GetExtension(path), _installer.PackFileExtension, StringComparison.OrdinalIgnoreCase);

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

                // a file handed over (dropped) is taken as it is; otherwise ask for one
                if (string.IsNullOrEmpty(zipFilePath))
                {
                    // Open file dialog: season packs, and community skin packs when the game has them
                    var openFileDialog = new OpenFileDialog
                    {
                        Title = _skinPackImport == null ? "Select Season Mod Package" : "Select a season pack or a livery pack",
                        Filter = _skinPackImport?.FileFilter ?? $"{_installer.PackFileFilterLabel} (*{_installer.PackFileExtension})|*{_installer.PackFileExtension}",
                        CheckFileExists = true,
                        CheckPathExists = true
                    };
                    if (openFileDialog.ShowDialog() != DialogResult.OK) return;

                    zipFilePath = openFileDialog.FileName;
                }

                if (IsSkinPackArchive(zipFilePath))
                {
                    _isExecuting = true;
                    CommandManager.InvalidateRequerySuggested();
                    try
                    {
                        if (await _skinPackImport.ImportFileAsync(zipFilePath, System.Windows.Application.Current?.MainWindow))
                            SkinPackImported?.Invoke(this, EventArgs.Empty);
                    }
                    finally
                    {
                        _isExecuting = false;
                        CommandManager.InvalidateRequerySuggested();
                    }
                    return;
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