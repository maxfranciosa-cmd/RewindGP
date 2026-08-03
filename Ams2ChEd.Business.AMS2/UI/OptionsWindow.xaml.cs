using Ams2ChEd.Business.AMS2.PakPatching.Contracts;
using Ams2ChEd.Business.AMS2.Settings;
using AMS2ChEd.Business.Settings.Contracts;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Ams2ChEd.Business.AMS2.UI
{
    public partial class OptionsWindow : Window
    {
        private Ams2GameInstallSettingsStorage _settingsStorage;
        private IVehicleLiverySlotPatcher _vehicleLiverySlotPatcher;

        public OptionsWindow(Ams2GameInstallSettingsStorage settingsStorage, IVehicleLiverySlotPatcher vehicleLiverySlotPatcher)
        {
            InitializeComponent();
            _settingsStorage = settingsStorage;
            _vehicleLiverySlotPatcher = vehicleLiverySlotPatcher;
            LoadSettings();
        }

        private void LoadSettings()
        {
            AMS2FolderTextBox.Text = _settingsStorage.LoadSettings()?.GameInstallFolder;
            AMS2PlayerNameTextBox.Text = _settingsStorage.LoadInGameName();
        }

        private void SaveSettings(string path, string inGameDriverName)
        {
            try
            {
                _settingsStorage.SaveSettings(new GameInstallSettings { GameInstallFolder = path });
                _settingsStorage.SaveInGameName(inGameDriverName);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select AMS2 Installation Folder",
                ShowNewFolderButton = false
            };

            if (!string.IsNullOrEmpty(AMS2FolderTextBox.Text))
            {
                dialog.SelectedPath = AMS2FolderTextBox.Text;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AMS2FolderTextBox.Text = dialog.SelectedPath;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = AMS2FolderTextBox.Text.Trim();

            if (string.IsNullOrEmpty(folderPath))
            {
                System.Windows.MessageBox.Show("Please specify the AMS2 folder path.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                var result = System.Windows.MessageBox.Show(
                    "The specified folder does not exist. Do you want to save it anyway?",
                    "Folder Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            string inGamePlayerName = AMS2PlayerNameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(inGamePlayerName))
            {
                System.Windows.MessageBox.Show("Please specify your in-game driver Name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save to configuration
            SaveSettings(folderPath, inGamePlayerName);

            System.Windows.MessageBox.Show("Settings saved successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
            this.DialogResult = true;
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void RestoreVehicleFilesButton_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = AMS2FolderTextBox.Text.Trim();

            if (!_vehicleLiverySlotPatcher.HasBackups(folderPath))
            {
                System.Windows.MessageBox.Show(
                    "No patched vehicle files were found for this install - nothing to restore.",
                    "Nothing To Restore", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                "This will restore every vehicle pak file Rewind GP has patched this season back to its original state. Continue?",
                "Restore Original Vehicle Files", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            var progressWindow = new Ams2ProgressWindow("Restoring original vehicle files...");
            progressWindow.Owner = this;
            progressWindow.Show();

            RestoreResult result;
            try
            {
                result = await Task.Run(() => _vehicleLiverySlotPatcher.RestoreAll(folderPath));
            }
            finally
            {
                progressWindow.Close();
            }

            if (result.Success)
            {
                System.Windows.MessageBox.Show($"Restored {result.FilesRestored} file(s) to their original state.",
                    "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show($"Restore did not fully complete: {result.Message}",
                    "Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
