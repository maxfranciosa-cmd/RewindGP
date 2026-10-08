using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Settings.Contracts;
using AMS2ChEd.Business.Storage;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Models;
using AMS2ChEd.Commands;
using AMS2ChEd.Extensions;
using AMS2ChEd.Resources;
using AMS2ChEd.Services;
using AMS2ChEd.Views;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace AMS2ChEd
{
    public class ReputationItem
    {
        public DriverReputation Reputation { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public partial class MainWindow : Window
    {
        private List<ReputationItem> reputationList;
        private Storyboard fadeInStoryboard;

        private IGameDataFactory _ams2StorageFactory;
        private IGameInstallSettingsStorage _installSettingsStorage;
        private GameLogicFactory _gameLogicFactory;
        private SaveGameSeasonChecker _seasonChecker;
        private SeasonManifestService _manifest;
        private DeveloperModeSettings _developerModeSettings;

        // Scenario-related fields
        private List<Scenario> _scenarios;
        private List<CosmeticsOptionDisplay> _defaultHelmets;
        private IPlayerCosmeticsEditor _cosmeticsEditor;
        private IOffSeasonOrchestrator _offSeasonOrchestrator;
        private IPrerequisiteWindow _prerequisiteWindow;
        public InstallSeasonModCommandAsync InstallSeasonCommand { get; set; }

        public MainWindow(
            IGameDataFactory ams2StorageFactory,
            IGameInstallSettingsStorage installSettingsStorage,
            GameLogicFactory gameLogicFactory,
            SeasonManifestService manifest,
            SaveGameSeasonChecker seasonChecker,
            DeveloperModeSettings developerModeSettings,
            IExternalLiveriesInstaller externalLiveriesInstaller,
            IExternalLiveriesPrompt externalLiveriesPrompt,
            ISeasonPackInstaller seasonPackInstaller,
            IOffSeasonOrchestrator offSeasonOrchestrator,
            IPlayerCosmeticsEditor cosmeticsEditor = null,
            IPrerequisiteWindow prerequisiteWindow = null)
        {
            InitializeComponent();
            _ams2StorageFactory = ams2StorageFactory;
            _installSettingsStorage = installSettingsStorage;
            _gameLogicFactory = gameLogicFactory;
            _seasonChecker = seasonChecker;
            _manifest = manifest;
            _developerModeSettings = developerModeSettings;
            _cosmeticsEditor = cosmeticsEditor;
            _offSeasonOrchestrator = offSeasonOrchestrator;
            _prerequisiteWindow = prerequisiteWindow;

            InstallSeasonCommand = new InstallSeasonModCommandAsync(seasonPackInstaller, externalLiveriesInstaller, externalLiveriesPrompt);
            InstallSeasonCommand.SeasonInstalled += OnSeasonModInstalled;

            InitializeGameLogic();
            InitializeAnimations();
            InitializeReputations();
            LoadSeasons();
            _scenarios = new List<Scenario>();

            HelmetSelectionLabel.Visibility = _cosmeticsEditor == null ? Visibility.Collapsed : Visibility.Visible;
            HelmetSelectionBorder.Visibility = _cosmeticsEditor == null ? Visibility.Collapsed : Visibility.Visible;

            DeveloperToolsButton.Visibility = _developerModeSettings.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        // Must be called only after this window has been shown (e.g. right after MainWindow.Show()) —
        // Window.Owner cannot be set to a window that hasn't been shown yet (its HWND doesn't exist).
        public void ShowPrerequisiteIfNeeded()
        {
            // One-time, game-specific "here's what you need to set up before racing" prompt (e.g.
            // AMS2's telemetry/borderless-window requirements for the race-launch overlay). No-op
            // if already dismissed with "don't show again", or if the active game module has none.
            _prerequisiteWindow?.ShowIfNeeded(this);
        }


        public void OnSeasonModInstalled(object sender, SeasonInstalledEventArgs e)
        {
            LoadSeasons();
        }

        private void InitializeGameLogic()
        {
            // Subscribe to events
            _gameLogicFactory.GameEngine.GameStateChanged += OnGameStateChanged;
            _gameLogicFactory.GameEngine.SeasonProgressed += OnSeasonProgressed;
            _gameLogicFactory.GameEngine.ErrorOccurred += OnErrorOccurred;
            InstallSeasonCommand.SeasonInstalled -= OnSeasonModInstalled;
        }

        private void InitializeAnimations()
        {
            // Get the fade-in storyboard from resources
            fadeInStoryboard = (Storyboard)this.Resources["FadeInStoryboard"];
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Unsubscribe from events to prevent memory leaks
            _gameLogicFactory.GameEngine.GameStateChanged -= OnGameStateChanged;
            _gameLogicFactory.GameEngine.SeasonProgressed -= OnSeasonProgressed;
            _gameLogicFactory.GameEngine.ErrorOccurred -= OnErrorOccurred;

            // The app's default ShutdownMode is OnLastWindowClose, not OnMainWindowClose - without
            // this, closing MainWindow while any other window (RaceWeekendWindow, EntryListWindow,
            // overlays, dialogs, etc.) is still open would leave those dangling with no way back to
            // MainWindow instead of ending the app. Close everything else too; once the last one
            // goes, the app shuts down on its own as usual.
            foreach (var window in System.Windows.Application.Current.Windows.OfType<Window>().Where(w => w != this).ToList())
            {
                window.Close();
            }
        }

        private void OnGameStateChanged(object sender, GameStateChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.NewState == GameState.SeasonOverview)
                {
                    // Game was created successfully - it will be handled in CreateGameButton_Click
                }
            });
        }

        private void OnSeasonProgressed(object sender, SeasonProgressionEventArgs e)
        {
            // Handle season progression if needed
        }

        private void OnErrorOccurred(object sender, string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(errorMessage, Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void InitializeReputations()
        {
            // Static gameplay copy, not part of the deferred procedural news/letter generators
            // (those stay English-only for now - see the localization plan).
            reputationList = new List<ReputationItem>
            {
                new ReputationItem { Reputation = DriverReputation.PAY_DRIVER_WILD_CARD, Name = Strings.MainWindow_Reputation_PayDriverWildCard_Name, Description = Strings.MainWindow_Reputation_PayDriverWildCard_Description },
                new ReputationItem { Reputation = DriverReputation.PAY_DRIVER_SEASON, Name = Strings.MainWindow_Reputation_PayDriverSeason_Name, Description = Strings.MainWindow_Reputation_PayDriverSeason_Description },
                new ReputationItem { Reputation = DriverReputation.YOUNG_TALENT, Name = Strings.MainWindow_Reputation_YoungTalent_Name, Description = Strings.MainWindow_Reputation_YoungTalent_Description },
                new ReputationItem { Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, Name = Strings.MainWindow_Reputation_YoungChampionshipUnproven_Name, Description = Strings.MainWindow_Reputation_YoungChampionshipUnproven_Description },
                new ReputationItem { Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, Name = Strings.MainWindow_Reputation_YoungChampionship_Name, Description = Strings.MainWindow_Reputation_YoungChampionship_Description },
                new ReputationItem { Reputation = DriverReputation.PRIME_MIDFIELD, Name = Strings.MainWindow_Reputation_PrimeMidfield_Name, Description = Strings.MainWindow_Reputation_PrimeMidfield_Description },
                new ReputationItem { Reputation = DriverReputation.PRIME_STRONG_MIDFIELD, Name = Strings.MainWindow_Reputation_PrimeStrongMidfield_Name, Description = Strings.MainWindow_Reputation_PrimeStrongMidfield_Description },
                new ReputationItem { Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, Name = Strings.MainWindow_Reputation_PrimeChampionshipUnproven_Name, Description = Strings.MainWindow_Reputation_PrimeChampionshipUnproven_Description },
                new ReputationItem { Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, Name = Strings.MainWindow_Reputation_PrimeChampionship_Name, Description = Strings.MainWindow_Reputation_PrimeChampionship_Description },
                new ReputationItem { Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, Name = Strings.MainWindow_Reputation_PrimeChampionshipWashed_Name, Description = Strings.MainWindow_Reputation_PrimeChampionshipWashed_Description },
                new ReputationItem { Reputation = DriverReputation.AGEING_MIDFIELD, Name = Strings.MainWindow_Reputation_AgeingMidfield_Name, Description = Strings.MainWindow_Reputation_AgeingMidfield_Description },
                new ReputationItem { Reputation = DriverReputation.AGEING_STRONG_MIDFIELD, Name = Strings.MainWindow_Reputation_AgeingStrongMidfield_Name, Description = Strings.MainWindow_Reputation_AgeingStrongMidfield_Description },
                new ReputationItem { Reputation = DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, Name = Strings.MainWindow_Reputation_AgeingChampionship_Name, Description = Strings.MainWindow_Reputation_AgeingChampionship_Description },
                new ReputationItem { Reputation = DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED, Name = Strings.MainWindow_Reputation_AgeingChampionshipWashed_Name, Description = Strings.MainWindow_Reputation_AgeingChampionshipWashed_Description },
                new ReputationItem { Reputation = DriverReputation.JUST_ONE_LAST_DANCE, Name = Strings.MainWindow_Reputation_JustOneLastDance_Name, Description = Strings.MainWindow_Reputation_JustOneLastDance_Description }
            };

            UpdateReputationComboBox();
        }

        private void LoadDefaultHelmets()
        {
            if (_cosmeticsEditor == null)
            {
                _defaultHelmets = new List<CosmeticsOptionDisplay>();
                return;
            }

            string season = ((ComboBoxItem)SeasonComboBox.SelectedItem).Content.ToString();
            int seasonYear = int.Parse(season);
            _defaultHelmets = _cosmeticsEditor.GetDefaultCosmeticsOptions(seasonYear)
                .Select(o => new CosmeticsOptionDisplay { Id = o.Id, PreviewImagePath = o.PreviewImagePath })
                .ToList();

            HelmetSelectionItemsControl.ItemsSource = _defaultHelmets;

            if (_defaultHelmets.Any())
            {
                _defaultHelmets[0].IsSelected = true;
            }
        }

        private void UpdateReputationComboBox()
        {
            ReputationComboBox.Items.Clear();

            // Get available reputations based on age
            IEnumerable<DriverReputation> availableReputations;
            if (int.TryParse(DriverAgeTextBox.Text, out int age) && age > 0)
            {
                availableReputations = ReputationUpdater.AvailableReputationForAge(age);
            }
            else
            {
                // If no valid age, show all reputations
                availableReputations = reputationList.Select(r => r.Reputation);
            }

            // Filter and add items
            var filteredReputations = reputationList.Where(r => availableReputations.Contains(r.Reputation)).ToList();

            foreach (var item in filteredReputations)
            {
                ReputationComboBox.Items.Add(new ComboBoxItem { Content = item.Name, Tag = item });
            }

            if (ReputationComboBox.Items.Count > 0)
            {
                ReputationComboBox.SelectedIndex = 0;
            }
        }
        public void RefreshSeasonComboBoxes()
        {
            LoadSeasons();
            LoadScenarios();
        }


        private void LoadSeasons()
        {
            try
            {
                var seasonFolders = _manifest.GetSeasonCatalog();

                if (seasonFolders.Count() > 0)
                {
                    SeasonComboBox.Items.Clear();
                    foreach (var season in seasonFolders)
                    {
                        SeasonComboBox.Items.Add(new ComboBoxItem { Content = season.Year });
                    }
                    SeasonComboBox.SelectedIndex = 0;
                }
                else
                {
                    SeasonComboBox.Items.Add(new ComboBoxItem { Content = Strings.MainWindow_NoSeasonsAvailable });
                    SeasonComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_LoadSeasonsError_Message, ex.Message), Strings.MainWindow_GenericError_Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SeasonComboBox.Items.Add(new ComboBoxItem { Content = Strings.MainWindow_ErrorLoadingSeasonsItem });
                SeasonComboBox.SelectedIndex = 0;
            }
        }

        private void DeveloperToolsButton_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPanel.Visibility = Visibility.Collapsed;
            DeveloperToolsPanel.Visibility = Visibility.Visible;
            LoadDevToolsSeasons();
        }

        private void DevToolsBackButton_Click(object sender, RoutedEventArgs e)
        {
            DeveloperToolsPanel.Visibility = Visibility.Collapsed;
            MainMenuPanel.Visibility = Visibility.Visible;
        }

        private void LoadDevToolsSeasons()
        {
            DevSeasonComboBox.Items.Clear();
            foreach (var season in _manifest.GetSeasonCatalog())
            {
                DevSeasonComboBox.Items.Add(new ComboBoxItem { Content = season.Year });
            }
            if (DevSeasonComboBox.Items.Count > 0)
                DevSeasonComboBox.SelectedIndex = 0;
        }

        private void DevSeasonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DevRaceComboBox.Items.Clear();
            if (DevSeasonComboBox.SelectedItem == null) return;

            int year = int.Parse(((ComboBoxItem)DevSeasonComboBox.SelectedItem).Content.ToString());
            var season = _ams2StorageFactory.SeasonLoader.LoadBaseSeason(year);

            foreach (var race in season.Races)
            {
                DevRaceComboBox.Items.Add(new ComboBoxItem { Content = race.RaceName, Tag = race });
            }
            if (DevRaceComboBox.Items.Count > 0)
                DevRaceComboBox.SelectedIndex = 0;
        }

        private void DevExportCustomAiButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildDevExportContext(out var raceId, out var entryList, out var drivers, out var season, out var error))
            {
                System.Windows.MessageBox.Show(error, Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _gameLogicFactory.RacePreparator.PrepareCustomAi(raceId, entryList, drivers, season);
                System.Windows.MessageBox.Show(Strings.MainWindow_CustomAiExported_Message, Strings.MainWindow_DeveloperMode_Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_ExportCustomAiError_Message, ex.Message), Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DevExportLiveriesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildDevExportContext(out var raceId, out var entryList, out var drivers, out var season, out var error))
            {
                System.Windows.MessageBox.Show(error, Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _gameLogicFactory.RacePreparator.PrepareLiveries(raceId, entryList, drivers, season);
                System.Windows.MessageBox.Show(Strings.MainWindow_LiveriesExported_Message, Strings.MainWindow_DeveloperMode_Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_ExportLiveriesError_Message, ex.Message), Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryBuildDevExportContext(out int raceId, out List<EntryListEntry> entryList, out IEnumerable<IDriverData> drivers, out ISeason season, out string error)
        {
            raceId = 0; entryList = null; drivers = null; season = null; error = null;

            if (DevSeasonComboBox.SelectedItem == null || DevRaceComboBox.SelectedItem == null)
            {
                error = Strings.MainWindow_SelectSeasonAndRaceFirst_Message;
                return false;
            }

            int year = int.Parse(((ComboBoxItem)DevSeasonComboBox.SelectedItem).Content.ToString());
            var race = (Race)((ComboBoxItem)DevRaceComboBox.SelectedItem).Tag;

            season = _ams2StorageFactory.SeasonLoader.LoadBaseSeason(year);
            drivers = _ams2StorageFactory.DriversLoader.LoadDriversBase(year).Values;

            // No save game in play here, so build a plain entry list straight from the season's team rosters
            // (reputation isn't read by livery/CustomAI generation, so it's left out unlike EntryListGenerator).
            entryList = season.Teams.Select(t => new EntryListEntry
            {
                TeamId = t.TeamId,
                Driver1Id = t.Driver1Contract.DriverId,
                Driver1Number = t.Driver1Contract.DriverNumber,
                Driver2Id = t.Driver2Contract.DriverId,
                Driver2Number = t.Driver2Contract.DriverNumber
            }).ToList();

            raceId = race.RaceId;
            return true;
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPanel.Visibility = Visibility.Collapsed;
            NewGamePanel.Visibility = Visibility.Visible;

            // no picture here: just the name and description of the selected reputation
            SetInfoTextOnlyLayout(true);

            ReplaceDriverPanel.Visibility = Visibility.Collapsed;
            CustomDriverPanel.Visibility = Visibility.Visible;
            SetImportedDriverMode(false);

            //trigger the selection change (so the description to be updated)
            ReputationComboBox_SelectionChanged(null, null);

            ReputationNameText.Visibility = Visibility.Visible;
            ReputationParagraphText.Visibility = Visibility.Visible;
        }

        #region Driver from another season

        private class ImportableDriver
        {
            public IDriverData Driver { get; set; }
            public int SourceYear { get; set; }
        }

        private bool _importedDriverMode;
        private readonly Dictionary<int, Dictionary<string, IDriverData>> _seasonDriversCache = new();

        // same form as the custom driver, but name, age and helmet come from the picked driver
        private void NewGameImportedDriverButton_Click(object sender, RoutedEventArgs e)
        {
            NewGameButton_Click(sender, e);
            SetImportedDriverMode(true);
            LoadImportableDrivers();
        }

        private void SetImportedDriverMode(bool enabled)
        {
            _importedDriverMode = enabled;

            ImportedDriverPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            CustomDriverFieldsPanel.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;

            var showHelmets = !enabled && _cosmeticsEditor != null;
            HelmetSelectionLabel.Visibility = showHelmets ? Visibility.Visible : Visibility.Collapsed;
            HelmetSelectionBorder.Visibility = showHelmets ? Visibility.Visible : Visibility.Collapsed;

            if (!enabled)
            {
                DriverAgeTextBox.Clear();
            }
        }

        private bool TryGetSelectedSeasonYear(out int seasonYear)
        {
            seasonYear = 0;
            return SeasonComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out seasonYear);
        }

        private Dictionary<string, IDriverData> LoadSeasonDriversCached(int year)
        {
            if (!_seasonDriversCache.TryGetValue(year, out var drivers))
            {
                drivers = _ams2StorageFactory.DriversLoader.LoadDriversBase(year);
                _seasonDriversCache[year] = drivers;
            }
            return drivers;
        }

        // every driver of the other installed seasons who isn't part of the selected one,
        // each in the version of the season closest to it
        private void LoadImportableDrivers()
        {
            ImportedDriverComboBox.Items.Clear();
            UpdateImportedDriverVisual(null, 0);

            if (!TryGetSelectedSeasonYear(out var seasonYear))
                return;

            try
            {
                var installedYears = _ams2StorageFactory.SeasonLoader.GetAvailableSeasons()
                    .Select(s => int.TryParse(s, out var y) ? y : 0)
                    .Where(y => y > 0)
                    .ToList();

                var driversInSelectedSeason = installedYears.Contains(seasonYear)
                    ? LoadSeasonDriversCached(seasonYear).Keys.ToHashSet()
                    : new HashSet<string>();

                var importableDrivers = installedYears
                    .Where(y => y != seasonYear)
                    .OrderBy(y => Math.Abs(y - seasonYear))
                    .ThenBy(y => y)
                    .SelectMany(y => LoadSeasonDriversCached(y).Values.Select(d => new ImportableDriver { Driver = d, SourceYear = y }))
                    .Where(d => d.Driver.YearOfBirth > 0 && !driversInSelectedSeason.Contains(d.Driver.DriverId))
                    .GroupBy(d => d.Driver.DriverId)
                    .Select(g => g.First())
                    .OrderBy(d => d.Driver.Name);

                foreach (var driver in importableDrivers)
                {
                    ImportedDriverComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = string.Format(Strings.MainWindow_ImportedDriverListItem_Format, driver.Driver.Name, driver.SourceYear),
                        Tag = driver
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_LoadSeasonsError_Message, ex.Message), Strings.MainWindow_GenericError_Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (ImportedDriverComboBox.Items.Count > 0)
            {
                ImportedDriverComboBox.SelectedIndex = 0;
            }
        }

        private void ImportedDriverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (ImportedDriverComboBox.SelectedItem as ComboBoxItem)?.Tag as ImportableDriver;
            TryGetSelectedSeasonYear(out var seasonYear);
            UpdateImportedDriverVisual(selected, seasonYear);
        }

        private void UpdateImportedDriverVisual(ImportableDriver selected, int seasonYear)
        {
            ImportedDriverPhoto.LoadPhoto(selected?.Driver.PictureUrl, ImportedDriverPhotoPlaceholder);
            ImportedDriverNameText.Text = selected?.Driver.Name?.ToUpper();

            if (selected == null)
            {
                ImportedDriverInfoText.Text = _importedDriverMode ? Strings.MainWindow_NoImportableDrivers_Message : null;
                return;
            }

            var age = seasonYear - selected.Driver.YearOfBirth;
            ImportedDriverInfoText.Text = string.Format(Strings.MainWindow_ImportedDriverInfo_Format, selected.Driver.YearOfBirth, age, seasonYear, selected.SourceYear);

            // the reputations on offer follow the age, as for a custom driver
            DriverAgeTextBox.Text = age.ToString();

            // start from the driver's own reputation, or its equivalent for the age they'd have this season
            var reputation = selected.Driver.Reputation;
            if (!SelectReputation(reputation))
            {
                SelectReputation(new ReputationUpdater().GetNewReputationForInactiveDriver(reputation, age));
            }
        }

        private bool SelectReputation(DriverReputation reputation)
        {
            var item = ReputationComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => (i.Tag as ReputationItem)?.Reputation == reputation);

            if (item == null)
                return false;

            ReputationComboBox.SelectedItem = item;
            return true;
        }

        #endregion

        private void LoadGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog()
                {
                    Title = Strings.MainWindow_LoadGame_DialogTitle,
                    InitialDirectory = AppPaths.SavesFolder,
                    Filter = "json files (*.json)|*.json"
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    LoadSaveAndOpenSeasonOverview(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_LoadGameError_Message, ex.Message),
                    Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_latestSavePath == null)
                return;

            try
            {
                LoadSaveAndOpenSeasonOverview(_latestSavePath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_LoadGameError_Message, ex.Message),
                    Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadSaveAndOpenSeasonOverview(string filePath)
        {
            var saveGame = _ams2StorageFactory.GameStorage.LoadGame(filePath);

            var result = _seasonChecker.CheckIfSaveGameNeedsRefresh(saveGame);

            if (result == SaveGameSeasonCheckerResult.NeedsRefresh)
            {
                System.Windows.MessageBox.Show(Strings.MainWindow_ModOutOfDate_Message, Strings.MainWindow_ModOutOfDate_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                _gameLogicFactory.GameEngine.UpdateSeasonInsideSave(saveGame);
            }

            _gameLogicFactory.GameEngine.LoadGame(saveGame);

            // Open Season Overview window
            var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _installSettingsStorage, _gameLogicFactory, saveGame, _cosmeticsEditor, _offSeasonOrchestrator);
            seasonOverviewWindow.Owner = this.Owner;
            seasonOverviewWindow.Show();
        }

        #region Cover story (latest save)

        private enum CoverStyle
        {
            NextGrandPrix,
            PlayerPortrait
        }

        // TEMPORARY: both cover options are in, clicking the cover photo switches between them
        private CoverStyle _coverStyle = CoverStyle.NextGrandPrix;

        private string _latestSavePath;
        private DateTime _latestSaveWriteTime;
        private ISaveGame _latestSave;

        // the cover and the "continue" line follow the most recently written save
        private void RefreshLatestSave()
        {
            string path = null;
            var writeTime = DateTime.MinValue;

            try
            {
                var latestFile = _ams2StorageFactory.GameStorage.GetSaveFiles()
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();

                if (latestFile != null)
                {
                    path = latestFile.FullName;
                    writeTime = latestFile.LastWriteTime;
                }

                if (path == _latestSavePath && writeTime == _latestSaveWriteTime)
                    return;

                _latestSave = path == null ? null : _ams2StorageFactory.GameStorage.LoadGame(path);
            }
            catch
            {
                // an unreadable save just means a cover with no story
                path = null;
                _latestSave = null;
            }

            _latestSavePath = _latestSave == null ? null : path;
            _latestSaveWriteTime = writeTime;

            UpdateContinuePanel();
            UpdateCover();
        }

        private void UpdateContinuePanel()
        {
            if (_latestSave == null)
            {
                ContinuePanel.Visibility = Visibility.Collapsed;
                return;
            }

            var season = _latestSave.CurrentSeason;
            var racesCount = season.Races.Count();
            var savedOn = _latestSaveWriteTime.ToString("g");

            ContinueDescriptionText.Text = _latestSave.NextGpIndex < racesCount
                ? string.Format(Strings.MainWindow_ContinueDescription_Format, _latestSave.PlayerData.Name, season.Year, _latestSave.NextGpIndex + 1, racesCount, savedOn)
                : string.Format(Strings.MainWindow_ContinueDescriptionSeasonOver_Format, _latestSave.PlayerData.Name, season.Year, savedOn);
            ContinuePanel.Visibility = Visibility.Visible;
        }

        private void UpdateCover()
        {
            string photo = null;
            string headline = null;
            string subline = null;

            if (_latestSave != null)
            {
                var hasNextGrandPrixCover = TryBuildNextGrandPrixCover(out var gpPhoto, out var gpHeadline, out var gpSubline);
                var hasPortraitCover = TryBuildPlayerPortraitCover(out var portraitPhoto, out var portraitHeadline, out var portraitSubline);

                // fall back to the other option when the chosen one has nothing to show
                var useNextGrandPrix = hasNextGrandPrixCover && (_coverStyle == CoverStyle.NextGrandPrix || !hasPortraitCover);
                if (useNextGrandPrix)
                {
                    (photo, headline, subline) = (gpPhoto, gpHeadline, gpSubline);
                }
                else if (hasPortraitCover)
                {
                    (photo, headline, subline) = (portraitPhoto, portraitHeadline, portraitSubline);
                }
            }

            var photoLoaded = WelcomeImage.LoadPhoto(photo);

            CoverHeadlineText.Text = headline;
            CoverSublineText.Text = subline;
            CoverHeadlineText.Visibility = photoLoaded && !string.IsNullOrEmpty(headline) ? Visibility.Visible : Visibility.Collapsed;
            CoverSublineText.Visibility = photoLoaded && !string.IsNullOrEmpty(subline) ? Visibility.Visible : Visibility.Collapsed;
        }

        // option 1: the poster of the next grand prix
        private bool TryBuildNextGrandPrixCover(out string photo, out string headline, out string subline)
        {
            photo = headline = subline = null;

            var season = _latestSave.CurrentSeason;
            var races = season.Races.ToList();
            if (_latestSave.NextGpIndex < 0 || _latestSave.NextGpIndex >= races.Count)
                return false;

            var nextRace = races[_latestSave.NextGpIndex];
            if (!PictureUrlLoaderExtension.TryLoadBitmap(nextRace.CoverPictureUrl, out _))
                return false;

            photo = nextRace.CoverPictureUrl;
            headline = string.Format(Strings.MainWindow_Cover_NextRace_Headline_Format, nextRace.RaceName?.ToUpper());
            subline = string.Format(Strings.MainWindow_Cover_NextRace_Subline_Format, _latestSave.NextGpIndex + 1, races.Count, season.Year);
            return true;
        }

        // option 2: the portrait of the player's driver, with a headline on how their season is going
        private bool TryBuildPlayerPortraitCover(out string photo, out string headline, out string subline)
        {
            photo = headline = subline = null;

            var player = _latestSave.PlayerData;
            var playerDriver = _latestSave.Drivers?.FirstOrDefault(d => d.DriverId == player.DriverId);
            if (!PictureUrlLoaderExtension.TryLoadBitmap(playerDriver?.PictureUrl, out _))
                return false;

            var season = _latestSave.CurrentSeason;
            var surname = (player.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToUpper();
            var standing = _latestSave.CurrentDriverStandings?.FirstOrDefault(s => s.DriverId == player.DriverId);
            var roundsDone = _latestSave.NextGpIndex;

            if (roundsDone <= 0 || standing == null || standing.Position <= 0)
                headline = string.Format(Strings.MainWindow_Cover_SeasonPreview_Headline_Format, season.Year);
            else if (standing.Position == 1)
                headline = string.Format(Strings.MainWindow_Cover_Leads_Headline_Format, surname, roundsDone);
            else
                headline = string.Format(Strings.MainWindow_Cover_Position_Headline_Format, surname, standing.Position, roundsDone);

            var teamName = season.Teams.FirstOrDefault(t => t.TeamId == player.TeamId)?.TeamName;
            subline = string.IsNullOrEmpty(teamName) ? player.Name : $"{player.Name} - {teamName}";

            photo = playerDriver.PictureUrl;
            return true;
        }

        private void WelcomeImageBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _coverStyle = _coverStyle == CoverStyle.NextGrandPrix ? CoverStyle.PlayerPortrait : CoverStyle.NextGrandPrix;
            UpdateCover();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            // saves are written while the other windows are open
            RefreshLatestSave();
        }

        #endregion

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            _installSettingsStorage.ShowEditor(this);
        }

        private void NationalityTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            NationalityTextBox.Text = NationalityTextBox.Text.ToUpper();
        }

        private void ReputationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReputationComboBox.SelectedItem != null)
            {
                var selectedItem = (ComboBoxItem)ReputationComboBox.SelectedItem;
                var reputationItem = (ReputationItem)selectedItem.Tag;

                // Update visual panel
                UpdateReputationVisual(reputationItem);
            }
        }

        private void UpdateReputationVisual(ReputationItem reputationItem)
        {
            // Update the paragraph text (description)
            ReputationParagraphText.Text = reputationItem.Description;

            // Update the reputation name in red
            ReputationNameText.Text = reputationItem.Name.ToUpper();

            // Reset opacity to 0 before animating
            ReputationInfoPanel.Opacity = 0;

            // Trigger fade-in animation
            if (fadeInStoryboard != null)
            {
                Storyboard.SetTarget(fadeInStoryboard, ReputationInfoPanel);
                fadeInStoryboard.Begin();
            }
        }

        private void DriverAgeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void DriverAgeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateReputationComboBox();
        }

        private async void CreateGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // a driver taken from another season brings their own name, age and numbers
                var importedDriver = _importedDriverMode
                    ? ((ImportedDriverComboBox.SelectedItem as ComboBoxItem)?.Tag as ImportableDriver)?.Driver
                    : null;

                string driverName = importedDriver != null ? importedDriver.Name : DriverNameTextBox.Text.Trim();
                string nationality = importedDriver != null ? importedDriver.Nationality : NationalityTextBox.Text.Trim();

                if ((_importedDriverMode && importedDriver == null) ||
                    (!_importedDriverMode && (string.IsNullOrEmpty(driverName) || string.IsNullOrEmpty(nationality))) ||
                    string.IsNullOrEmpty(DriverAgeTextBox.Text.Trim()))
                {
                    System.Windows.MessageBox.Show(Strings.MainWindow_RequiredFieldsMissing_Message, Strings.MainWindow_ValidationError_Title,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int driverAge = int.Parse(DriverAgeTextBox.Text);
                int[] favouriteNumbers = importedDriver != null
                    ? (importedDriver.FavouriteNumbers ?? Enumerable.Empty<int>()).ToArray()
                    : FavouriteNumbersTextBox.Text.Split(",").Select(n => int.Parse(n)).ToArray();
                string playerDriverId = importedDriver != null
                    ? importedDriver.DriverId
                    : $"player_{driverName.ToLower().Replace(" ", "_")}";

                string season = ((ComboBoxItem)SeasonComboBox.SelectedItem).Content.ToString();
                var selectedReputation = (ComboBoxItem)ReputationComboBox.SelectedItem;
                var reputationItem = (ReputationItem)selectedReputation.Tag;
                var seasonYear = int.Parse(season);

                await DownloadSeasonIfNeeded(seasonYear);

                if (!_ams2StorageFactory.SeasonLoader.GetAvailableSeasons().Any(s => s == season))
                {
                    System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_SeasonNotInstalled_Message, season), Strings.MainWindow_ValidationError_Title,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Load season drivers
                var seasonDrivers = _ams2StorageFactory.DriversLoader.LoadDriversBase(seasonYear);
                var seasonData = _ams2StorageFactory.SeasonLoader.LoadBaseSeason(seasonYear);
                DriverHirer.AssignUndefinedRoles(seasonData, seasonDrivers.Values);

                // the season may have only just been downloaded, so the driver list couldn't be filtered before
                if (importedDriver != null && seasonDrivers.ContainsKey(importedDriver.DriverId))
                {
                    System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_ImportedDriverAlreadyInSeason_Message, importedDriver.Name, season), Strings.MainWindow_ValidationError_Title,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    _seasonDriversCache.Remove(seasonYear);
                    LoadImportableDrivers();
                    return;
                }

                ISaveGame CreateGame(string selectedTeamId, string replacedDriverId)
                {
                    if (importedDriver != null)
                    {
                        return _gameLogicFactory.GameEngine.CreateNewGameWithImportedDriver(
                            importedDriver: importedDriver,
                            playerReputation: reputationItem.Reputation,
                            season: seasonData,
                            selectedTeamId: selectedTeamId,
                            replacedDriverId: replacedDriverId,
                            seasonDrivers: seasonDrivers.Values.ToList());
                    }

                    var newGame = _gameLogicFactory.GameEngine.CreateNewGame(
                        playerName: driverName,
                        playerNationality: nationality,
                        playerAge: driverAge,
                        playerReputation: reputationItem.Reputation,
                        favouriteNumbers: favouriteNumbers,
                        season: seasonData,
                        selectedTeamId: selectedTeamId,
                        replacedDriverId: replacedDriverId,
                        seasonDrivers: seasonDrivers.Values.ToList());

                    // add selected helmet design
                    SetPlayerHelmetDesign(newGame);
                    return newGame;
                }

                // NEW: Check if Pay Driver Wild Card is selected
                if (reputationItem.Reputation == DriverReputation.PAY_DRIVER_WILD_CARD)
                {
                    if (driverAge < 18 || driverAge > 42)
                    {
                        System.Windows.MessageBox.Show(Strings.MainWindow_PayDriverAgeInvalid_Message, Strings.MainWindow_ValidationError_Title,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Show GenerateAbsenceWindow
                    var payDriverWindow = new GenerateAbsenceWindow(GenerateAbsenceWindowType.PayDriverAtGameStart);
                    payDriverWindow.Owner = this;

                    if (payDriverWindow.ShowDialog() == true)
                    {

                        var createFictionalAbsence = payDriverWindow.CreateFictionalAbsence;

                        var saveGame = CreateGame(null, null);

                        if (createFictionalAbsence)
                        {
                            // if there are no absences at the first GP of the season
                            var firstRaceId = saveGame.CurrentSeason.Races.First().RaceId;
                            if (!saveGame.CurrentSeason.Absences.Any(a => a.RaceId == firstRaceId))
                            {
                                // create a new random absence in a midfield (or lower) team
                                var possibleTeams = saveGame
                                                .CurrentSeason
                                                .Teams
                                                .Where(t => t.Reputation <= TeamReputation.MIDFIELD)
                                                .ToList();

                                var selectedTeam = possibleTeams.ElementAt(Random.Shared.Next(possibleTeams.Count));

                                var driverOut = selectedTeam.PickRandomDriverFromTheTeam();

                                saveGame.CurrentSeason.Absences = saveGame.CurrentSeason.Absences.Concat(new[]
                                {
                                new Absence
                                {
                                    DriverOut = driverOut.DriverId,
                                    RaceId = firstRaceId,
                                    DriverIn = saveGame.PlayerData.DriverId,
                                    TeamId = selectedTeam.TeamId,
                                }
                            });
                            }
                        }

                        // Save the game
                        string saveName = $"{driverName}_{seasonYear}".Replace(" ", "_");
                        string savedPath = _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                        // Open Season Overview window
                        var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _installSettingsStorage, _gameLogicFactory, saveGame, _cosmeticsEditor, _offSeasonOrchestrator);
                        seasonOverviewWindow.Owner = this.Owner;
                        seasonOverviewWindow.Show();

                        return;

                    }
                    else
                    {
                        // User closed the window without making a choice, abort
                        return;
                    }
                }

                // Open Team Selection window
                var teamSelectionWindow = new TeamSelectionWindow(_ams2StorageFactory, int.Parse(season), false, seasonDrivers);
                teamSelectionWindow.Owner = this;

                bool teamSelected = false;
                while (!teamSelected)
                {
                    if (teamSelectionWindow.ShowDialog() == true)
                    {
                        var selectedDriver = teamSelectionWindow.SelectedDriver;
                        var selectedTeamName = teamSelectionWindow.SelectedTeamName;
                        var selectedTeamPrincipal = teamSelectionWindow.SelectedTeamPrincipal;
                        // Get replaced driver's reputation
                        var replacedDriverData = seasonDrivers.ContainsKey(selectedDriver.DriverId) ? seasonDrivers[selectedDriver.DriverId] : null;
                        var replacedDriverReputation = replacedDriverData.Reputation;

                        // NEW: Use ContractLetterWindow but pass the game engine
                        var contractLetterWindow = new ContractLetterWindow(
                            _gameLogicFactory,
                            selectedTeamName,
                            teamSelectionWindow.SelectedTeamId,
                            selectedTeamPrincipal,
                            driverName,
                            nationality,
                            driverAge,
                            playerDriverId,
                            favouriteNumbers,
                            reputationItem.Reputation,
                            selectedDriver.Name,
                            selectedDriver.DriverId,
                            replacedDriverReputation,
                            selectedDriver.RoleName,
                            seasonData
                        );
                        contractLetterWindow.Owner = this;

                        if (contractLetterWindow.ShowDialog() == true)
                        {
                            // Player was hired - game has been created by the engine
                            teamSelected = true;

                            var saveGame = CreateGame(teamSelectionWindow.SelectedTeamId, selectedDriver.DriverId);

                            // Save the game
                            string saveName = $"{driverName}_{seasonYear}".Replace(" ", "_");
                            string savedPath = _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                            // Open Season Overview window
                            var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _installSettingsStorage, _gameLogicFactory, saveGame, _cosmeticsEditor, _offSeasonOrchestrator);
                            seasonOverviewWindow.Owner = this.Owner;
                            seasonOverviewWindow.Show();

                            return;

                        }
                        else
                        {
                            // Player was rejected - create new team selection window for retry
                            teamSelectionWindow = new TeamSelectionWindow(_ams2StorageFactory, int.Parse(season), false, seasonDrivers);
                            teamSelectionWindow.Owner = this;
                        }
                    }
                    else
                    {
                        // User cancelled team selection
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_CreateGameError_Message, ex.Message),
                    Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            } 
        }

        private void SetPlayerHelmetDesign(ISaveGame saveGame)
        {
            if (_cosmeticsEditor == null) return;

            var playerDriverData = saveGame.Drivers.First(d => d.DriverId == saveGame.PlayerData.DriverId);
            var selectedHelmet = _defaultHelmets.FirstOrDefault(h => h.IsSelected);
            if (selectedHelmet == null) return;

            _cosmeticsEditor.ApplySelectedCosmetics(playerDriverData, selectedHelmet.Id, saveGame.CurrentSeason.Year);
        }

        private async Task<bool> DownloadSeasonIfNeeded(int seasonYear)
        {
            return await _gameLogicFactory.SeasonUpdaterOrchestrator.PrepareSeasonAsync(seasonYear);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPanel.Visibility = Visibility.Visible;
            NewGamePanel.Visibility = Visibility.Collapsed;

            // Switch back to welcome image
            ReputationNameText.Visibility = Visibility.Hidden;
            ReputationParagraphText.Visibility = Visibility.Hidden;
            WelcomeImageBorder.Visibility = Visibility.Visible;
            ReputationImageBorder.Visibility = Visibility.Collapsed;
            ReputationInfoPanel.Opacity = 0; // Hide reputation info

            SetInfoTextOnlyLayout(false);

            // Hide scenario panel and show season controls
            ScenarioPanel.Visibility = Visibility.Collapsed;
            SeasonComboBox.Visibility = Visibility.Visible;

            // Show season label again
            SelectSeasonLabel.Visibility = Visibility.Visible;

            // Clear fields
            DriverNameTextBox.Clear();
            NationalityTextBox.Clear();
            DriverAgeTextBox.Clear();
            FavouriteNumbersTextBox.Clear();
            SeasonComboBox.SelectedIndex = 0;
            ReputationComboBox.SelectedIndex = 0;
        }
        // with no picture on the right column, the title and description take its top;
        // otherwise they sit below the picture
        private void SetInfoTextOnlyLayout(bool textOnly)
        {
            if (textOnly)
            {
                WelcomeImageBorder.Visibility = Visibility.Collapsed;
                ReputationImageBorder.Visibility = Visibility.Collapsed;
            }

            Grid.SetRow(ReputationInfoPanel, textOnly ? 0 : 1);
            ReputationInfoPanel.Margin = textOnly ? new Thickness(0) : new Thickness(0, 20, 0, 0);
            ReputationInfoPanel.Height = textOnly ? double.NaN : 150;
            ReputationInfoPanel.VerticalAlignment = textOnly ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        }

        private void NewGameReplaceDriverButton_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPanel.Visibility = Visibility.Collapsed;
            NewGamePanel.Visibility = Visibility.Visible;

            // Switch to welcome image
            WelcomeImageBorder.Visibility = Visibility.Visible;
            ReputationImageBorder.Visibility = Visibility.Collapsed;

            ReplaceDriverPanel.Visibility = Visibility.Visible;
            CustomDriverPanel.Visibility = Visibility.Collapsed;

            ReputationNameText.Visibility = Visibility.Visible;
            ReputationParagraphText.Visibility = Visibility.Visible;
        }

        private async void CreateGameReplaceDriverButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string season = ((ComboBoxItem)SeasonComboBox.SelectedItem).Content.ToString();
                int seasonYear = int.Parse(season);

                await DownloadSeasonIfNeeded(seasonYear);

                if (!_ams2StorageFactory.SeasonLoader.GetAvailableSeasons().Any(s => s == season))
                {
                    System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_SeasonNotInstalled_Message, season), Strings.MainWindow_ValidationError_Title,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var seasonDrivers = _ams2StorageFactory.DriversLoader.LoadDriversBase(seasonYear);
                // Open Team Selection window
                var teamSelectionWindow = new TeamSelectionWindow(_ams2StorageFactory, seasonYear, true, seasonDrivers, true);
                teamSelectionWindow.Owner = this;

                if (teamSelectionWindow.ShowDialog() == true)
                {
                    var selectedDriver = teamSelectionWindow.SelectedDriver;
                    var selectedTeamId = teamSelectionWindow.SelectedTeamId;

                    var replacedDriverData = seasonDrivers.ContainsKey(selectedDriver.DriverId) ? seasonDrivers[selectedDriver.DriverId] : null;
                    var seasonData = _ams2StorageFactory.SeasonLoader.LoadBaseSeason(int.Parse(season));

                    // NEW: Use GameEngine to create the game With existing driver
                    var saveGame = _gameLogicFactory.GameEngine.CreateNewGameWithExistingDriver(
                                                                                                season: seasonData,
                                                                                                selectedTeamId: selectedTeamId,
                                                                                                driverId: selectedDriver.DriverId,
                                                                                                seasonDrivers: seasonDrivers.Values.ToList());

                    // Save the game
                    string saveName = $"{saveGame.PlayerData.Name}_{season}".Replace(" ", "_");
                    string savedPath = _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                    // Open Season Overview window
                    var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _installSettingsStorage, _gameLogicFactory, saveGame, _cosmeticsEditor, _offSeasonOrchestrator);
                    seasonOverviewWindow.Owner = this.Owner;
                    seasonOverviewWindow.Show();

                    return;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_CreateGameError_Message, ex.Message),
                    Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

        }

        private void NewGameScenarioModeButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide main menu
            MainMenuPanel.Visibility = Visibility.Collapsed;

            // Show New Game panel
            NewGamePanel.Visibility = Visibility.Visible;

            // Hide other sub-panels and show Scenario panel
            CustomDriverPanel.Visibility = Visibility.Collapsed;
            ReplaceDriverPanel.Visibility = Visibility.Collapsed;
            ScenarioPanel.Visibility = Visibility.Visible;

            // Hide season selection (not needed for scenarios)
            SeasonComboBox.Visibility = Visibility.Collapsed;
            // Hide the season label
            SelectSeasonLabel.Visibility = Visibility.Collapsed;

            // scenarios have no cover picture: their title and description take the top of the column
            SetInfoTextOnlyLayout(true);

            // Load scenarios
            LoadScenarios();

        }

        private void LoadScenarios()
        {
            _scenarios.Clear();
            ScenarioComboBox.Items.Clear();

           foreach(var seasonFolder in Directory.GetDirectories(AppPaths.SeasonsFolder))
           {
                var scenarioFolder = Path.Combine(seasonFolder, "scenarios");

                if (Directory.Exists(scenarioFolder))
                {
                    var scenarioFiles = Directory.GetFiles(scenarioFolder, "*.json");

                    foreach (var file in scenarioFiles)
                    {
                        try
                        {
                            var jsonContent = File.ReadAllText(file);
                            var scenario = System.Text.Json.JsonSerializer.Deserialize<Scenario>(jsonContent, DefaultJsonSerializerOptions.Instance);

                            if (scenario != null)
                            {
                                _scenarios.Add(scenario);
                                ScenarioComboBox.Items.Add(string.Format(Strings.MainWindow_ScenarioListItem_Format, Path.GetFileName(seasonFolder), scenario.Name));
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_LoadScenarioError_Message, Path.GetFileName(file), ex.Message),
                                Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
           }

            if (ScenarioComboBox.Items.Count > 0)
            {
                ScenarioComboBox.SelectedIndex = 0;
            }
            else
            {
                System.Windows.MessageBox.Show(Strings.MainWindow_NoScenariosAvailable_Message, Strings.MainWindow_Information_Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        private void ScenarioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScenarioComboBox.SelectedIndex < 0 || ScenarioComboBox.SelectedIndex >= _scenarios.Count)
            {
                CreateGameScenarioButton.IsEnabled = false;
                return;
            }

            var selectedScenario = _scenarios[ScenarioComboBox.SelectedIndex];

            // Enable the start button
            CreateGameScenarioButton.IsEnabled = true;

            // Display scenario name and description
            ReputationNameText.Text = selectedScenario.Name;
            ReputationNameText.Visibility = Visibility.Visible;

            ReputationParagraphText.Text = selectedScenario.Description;
            ReputationParagraphText.Visibility = Visibility.Visible;

            // Animate the info panel
            fadeInStoryboard.Begin(ReputationInfoPanel);
        }

        private void CreateGameScenarioButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ScenarioComboBox.SelectedIndex < 0 || ScenarioComboBox.SelectedIndex >= _scenarios.Count)
                {
                    System.Windows.MessageBox.Show(Strings.MainWindow_SelectScenarioFirst_Message, Strings.MainWindow_GenericError_Title,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedScenario = _scenarios[ScenarioComboBox.SelectedIndex];

                var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, selectedScenario.GameFile);
                var jsonContent = File.ReadAllText(fileName);
                var saveGame = System.Text.Json.JsonSerializer.Deserialize<SaveGame>(jsonContent, DefaultJsonSerializerOptions.Instance);

                var result = _seasonChecker.CheckIfSaveGameNeedsRefresh(saveGame);

                if (result == SaveGameSeasonCheckerResult.NeedsRefresh)
                {
                    System.Windows.MessageBox.Show(Strings.MainWindow_ModOutOfDate_Message, Strings.MainWindow_ModOutOfDate_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    _gameLogicFactory.GameEngine.UpdateSeasonInsideSave(saveGame);
                }

                // Save the game
                string saveName = $"{saveGame.PlayerData.Name}_{saveGame.CurrentSeason.Year}".Replace(" ", "_");
                string savedPath = _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                // Open Season Overview window
                var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _installSettingsStorage, _gameLogicFactory, saveGame, _cosmeticsEditor, _offSeasonOrchestrator);
                seasonOverviewWindow.Owner = this.Owner;
                seasonOverviewWindow.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.MainWindow_CreateGameError_Message, ex.Message),
                    Strings.MainWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void HelmetPreview_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is CosmeticsOptionDisplay clickedDesign)
            {
                // Deselect all helmets
                foreach (var helmet in _defaultHelmets)
                {
                    helmet.IsSelected = false;
                }

                // Select the clicked helmet
                clickedDesign.IsSelected = true;
            }
        }

        private void SeasonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadDefaultHelmets();

            if (_importedDriverMode)
            {
                LoadImportableDrivers();
            }
        }

        private void InstallSeasonModButto_Click()
        {

        }
    }
}
