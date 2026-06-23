using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Settings;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using AMS2ChEd.Business.DependencyInjection;
using System.IO;
using System.Windows;

namespace AMS2ChEd
{
    public partial class OptionsWindow : Window
    {
        private Ams2StorageFactory _ams2StorageFactory;

        public OptionsWindow(Ams2StorageFactory storageFactory)
        {
            InitializeComponent();
            _ams2StorageFactory = storageFactory;
            LoadSettings();
        }

        private void LoadSettings()
        {
            AMS2FolderTextBox.Text = _ams2StorageFactory.Ams2AppSettingsStorage.LoadSettings()?.Ams2Folder;
            AMS2PlayerNameTextBox.Text = _ams2StorageFactory.Ams2AppSettingsStorage.LoadSettings()?.Ams2InGameName;
        }



        private void SaveSettings(string path, string inGameDriverName)
        {
            try
            {
                _ams2StorageFactory.Ams2AppSettingsStorage.SaveSettings(new Ams2AppSettings { Ams2Folder = path, Ams2InGameName = inGameDriverName });
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
    }
}