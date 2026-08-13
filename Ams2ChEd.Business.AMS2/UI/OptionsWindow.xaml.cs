using Ams2ChEd.Business.AMS2.PakPatching.Contracts;
using Ams2ChEd.Business.AMS2.Settings;
using AMS2ChEd.Business.Settings;
using AMS2ChEd.Business.Settings.Contracts;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Strings = Ams2ChEd.Business.AMS2.Resources.Strings;

namespace Ams2ChEd.Business.AMS2.UI
{
    public partial class OptionsWindow : Window
    {
        private Ams2GameInstallSettingsStorage _settingsStorage;
        private IVehicleLiverySlotPatcher _vehicleLiverySlotPatcher;
        private string _initialLanguageCode;

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

            var raceLength = _settingsStorage.LoadRaceLength();
            foreach (ComboBoxItem item in RaceLengthComboBox.Items)
            {
                if ((string)item.Tag == raceLength.ToString())
                {
                    RaceLengthComboBox.SelectedItem = item;
                    break;
                }
            }
            RaceLengthComboBox.SelectedItem ??= RaceLengthComboBox.Items[0];

            _initialLanguageCode = AppLanguageSettings.LoadLanguageCode();
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if ((string)item.Tag == _initialLanguageCode)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }
            LanguageComboBox.SelectedItem ??= LanguageComboBox.Items[0];
        }

        private void SaveSettings(string path, string inGameDriverName)
        {
            try
            {
                _settingsStorage.SaveSettings(new GameInstallSettings { GameInstallFolder = path });
                _settingsStorage.SaveInGameName(inGameDriverName);

                if (RaceLengthComboBox.SelectedItem is ComboBoxItem selected
                    && Enum.TryParse<Ams2RaceLength>((string)selected.Tag, out var raceLength))
                {
                    _settingsStorage.SaveRaceLength(raceLength);
                }

                if (LanguageComboBox.SelectedItem is ComboBoxItem selectedLanguage)
                {
                    AppLanguageSettings.SaveLanguageCode((string)selectedLanguage.Tag);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.OptionsWindow_SaveError_Message, ex.Message),
                    Strings.OptionsWindow_SaveError_Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = Strings.OptionsWindow_BrowseDialog_Description,
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
                System.Windows.MessageBox.Show(Strings.OptionsWindow_FolderPathRequired_Message, Strings.OptionsWindow_FolderPathRequired_Title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                var result = System.Windows.MessageBox.Show(
                    Strings.OptionsWindow_FolderNotFound_Message,
                    Strings.OptionsWindow_FolderNotFound_Title,
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
                System.Windows.MessageBox.Show(Strings.OptionsWindow_PlayerNameRequired_Message, Strings.OptionsWindow_PlayerNameRequired_Title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save to configuration
            SaveSettings(folderPath, inGamePlayerName);

            System.Windows.MessageBox.Show(Strings.OptionsWindow_SettingsSaved_Message, Strings.OptionsWindow_SettingsSaved_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);

            var selectedLanguageCode = (string)((ComboBoxItem)LanguageComboBox.SelectedItem).Tag;
            if (selectedLanguageCode != _initialLanguageCode)
            {
                // Language is restart-based (no live-switching) - nothing else in this UI signals
                // restart-required behavior, so call it out explicitly here.
                System.Windows.MessageBox.Show(Strings.OptionsWindow_LanguageChangeRequiresRestart_Message,
                    Strings.OptionsWindow_LanguageChangeRequiresRestart_Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }

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
                    Strings.OptionsWindow_NothingToRestore_Message,
                    Strings.OptionsWindow_NothingToRestore_Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                Strings.OptionsWindow_RestoreConfirm_Message,
                Strings.OptionsWindow_RestoreConfirm_Title, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            var progressWindow = new Ams2ProgressWindow(Strings.OptionsWindow_RestoringFiles_Message);
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
                System.Windows.MessageBox.Show(string.Format(Strings.OptionsWindow_RestoreComplete_Message, result.FilesRestored),
                    Strings.OptionsWindow_RestoreComplete_Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(string.Format(Strings.OptionsWindow_RestoreFailed_Message, result.Message),
                    Strings.OptionsWindow_RestoreFailed_Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
