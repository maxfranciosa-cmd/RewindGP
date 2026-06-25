using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Services;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using AMS2ChEd.Business.AMS2.GameLogic;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.RaceNumberSystem.Factory;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Models;
using AMS2ChEd.Commands;
using AMS2ChEd.Extensions;
using AMS2ChEd.Services;
using AMS2ChEd.ViewModels;
using AMS2ChEd.Views;
using System.IO;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using Label = System.Windows.Controls.Label;

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
        private Dictionary<DriverReputation, string> reputationImages;
        private Storyboard fadeInStoryboard;

        private Ams2StorageFactory _ams2StorageFactory;
        private GameLogicFactory _gameLogicFactory;
        private SaveGameSeasonChecker _seasonChecker;
        private SeasonManifestService _manifest;
        private DeveloperModeSettings _developerModeSettings;

        // Scenario-related fields
        private List<Scenario> _scenarios;
        private List<DefaultHelmetDesign> _defaultHelmets;
        public InstallSeasonModCommandAsync InstallSeasonCommand { get; set; }

        public MainWindow(
            Ams2StorageFactory ams2StorageFactory,
            GameLogicFactory gameLogicFactory,
            SeasonManifestService manifest,
            SaveGameSeasonChecker seasonChecker,
            DeveloperModeSettings developerModeSettings)
        {
            InitializeComponent();
            _ams2StorageFactory = ams2StorageFactory;
            _gameLogicFactory = gameLogicFactory;
            _seasonChecker = seasonChecker;
            _manifest = manifest;
            _developerModeSettings = developerModeSettings;

            InstallSeasonCommand = new InstallSeasonModCommandAsync(ams2StorageFactory);
            InstallSeasonCommand.SeasonInstalled += OnSeasonModInstalled;

            InitializeGameLogic();
            InitializeReputationImages();
            InitializeAnimations();
            InitializeReputations();
            LoadSeasons();
            _scenarios = new List<Scenario>();

            DeveloperToolsButton.Visibility = _developerModeSettings.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
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

        private void InitializeReputationImages()
        {
            // Map each reputation to an image filename
            // Images should be placed in an Images folder in the project
            reputationImages = new Dictionary<DriverReputation, string>
            {
                { DriverReputation.PAY_DRIVER_WILD_CARD, "/Images/reputation_paydriver_wildcard.png" },
                { DriverReputation.PAY_DRIVER_SEASON, "/Images/reputation_paydriver_season.png" },
                { DriverReputation.YOUNG_TALENT, "/Images/reputation_young_talent.png" },
                { DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, "/Images/reputation_young_unproven.png" },
                { DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, "/Images/reputation_young_champion.png" },
                { DriverReputation.PRIME_MIDFIELD, "/Images/reputation_midfield.png" },
                { DriverReputation.PRIME_STRONG_MIDFIELD, "/Images/reputation_high_midfield.png" },
                { DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, "/Images/reputation_unproven_champion.png" },
                { DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, "/Images/reputation_champion.png" },
                { DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, "/Images/reputation_washed_champion.png" },
                { DriverReputation.AGEING_MIDFIELD, "/Images/reputation_veteran_midfield.png" },
                { DriverReputation.AGEING_STRONG_MIDFIELD, "/Images/reputation_veteran_high_midfield.png" },
                { DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, "/Images/reputation_veteran_champion.png" },
                { DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED, "/Images/reputation_veteran_washed.png" },
                { DriverReputation.JUST_ONE_LAST_DANCE, "/Images/reputation_just_one_last_dance.png" }
            };
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
                System.Windows.MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void InitializeReputations()
        {
            reputationList = new List<ReputationItem>
            {
                new ReputationItem { Reputation = DriverReputation.PAY_DRIVER_WILD_CARD, Name = "Pay Driver - Wild Card", Description = "You are a pay driver, but without enough budget for the whole season. You will substitute existing drivers when they cannot participate in a race. This will be your opportunity to show your talent and hopefully get a full-time seat for the next season." },
                new ReputationItem { Reputation = DriverReputation.PAY_DRIVER_SEASON, Name = "Pay Driver - Full Season", Description = "You are a pay driver with enough budget to cover the season. You can race in F1 but can't be too picky about which team you join. Prove your talent to gain a drive for better teams." },
                new ReputationItem { Reputation = DriverReputation.YOUNG_TALENT, Name = "Young Talent", Description = "You're a potential young star, but still a rough diamond. Teams hiring you are betting on your raw talent, taking into account you might make some mistakes. Bigger teams are still not ready to bet on you." },
                new ReputationItem { Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, Name = "Young Unproven Championship Level Driver", Description = "You've demonstrated that in the right car, you can get wins, but you've not been proven a champion yet." },
                new ReputationItem { Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, Name = "Young Championship Level Driver", Description = "You are an accomplished young driver, able to fight for a championship." },
                new ReputationItem { Reputation = DriverReputation.PRIME_MIDFIELD, Name = "Midfield Driver", Description = "You're a reliable midfield driver, able to consistently bring the car to the finish line, but without showing any great flashes." },
                new ReputationItem { Reputation = DriverReputation.PRIME_STRONG_MIDFIELD, Name = "High Midfield Driver", Description = "You are a solid midfield driver, able to fight consistently for points and podiums in the right situation. You might also be able to fight for a spot in a top team (if they're really in need)." },
                new ReputationItem { Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, Name = "Unproven Championship Level Driver", Description = "You've demonstrated you're able to fight for wins consistently, but you're not in the fight for championship yet." },
                new ReputationItem { Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, Name = "Championship Level Driver", Description = "You are an accomplished champion that consistently fights for championships." },
                new ReputationItem { Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, Name = "Washed Championship Level Driver", Description = "You are a former champion that has fallen a bit, but you're eager to be back for a title fight." },
                new ReputationItem { Reputation = DriverReputation.AGEING_MIDFIELD, Name = "Veteran Midfield Driver", Description = "For a midfield team, you're a safe bet. An experienced driver that can bring solid results." },
                new ReputationItem { Reputation = DriverReputation.AGEING_STRONG_MIDFIELD, Name = "Veteran High Midfield Driver", Description = "You are a solid and reliable veteran driver that can fight consistently for points." },
                new ReputationItem { Reputation = DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, Name = "Veteran Championship Level Driver", Description = "You are an experienced driver that can still fight for the title." },
                new ReputationItem { Reputation = DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED, Name = "Washed Veteran Championship Level Driver", Description = "You are a former champion that has fallen from grace, but you're still hungry for success." },
                new ReputationItem { Reputation = DriverReputation.JUST_ONE_LAST_DANCE, Name = "Just One Last Dance", Description = "You are a legendary former champion well past your prime, but the fire still burns. This is your final chance to add one more chapter to an already storied career. Teams know you're a risk, but your name still carries weight." }
            };

            UpdateReputationComboBox();
        }

        private void LoadDefaultHelmets()
        {
            string season = ((ComboBoxItem)SeasonComboBox.SelectedItem).Content.ToString();
            int seasonYear = int.Parse(season);
            _defaultHelmets = HelmetPicker.LoadGenericHelmetDesignsPerYear(seasonYear).Select(h => new DefaultHelmetDesign { HelmetDesign = h }).ToList();

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
                    SeasonComboBox.Items.Add(new ComboBoxItem { Content = "No Seasons Available" });
                    SeasonComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading seasons: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SeasonComboBox.Items.Add(new ComboBoxItem { Content = "Error Loading Seasons" });
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
            var season = _ams2StorageFactory.SeasonLoader.LoadSeason(year);

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
                System.Windows.MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _gameLogicFactory.RacePreparator.PrepareCustomAi(raceId, entryList, drivers, season);
                System.Windows.MessageBox.Show("Custom AI exported.", "Developer Mode", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error exporting Custom AI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DevExportLiveriesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildDevExportContext(out var raceId, out var entryList, out var drivers, out var season, out var error))
            {
                System.Windows.MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _gameLogicFactory.RacePreparator.PrepareLiveries(raceId, entryList, drivers, season);
                System.Windows.MessageBox.Show("Liveries exported.", "Developer Mode", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error exporting liveries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryBuildDevExportContext(out int raceId, out List<EntryListEntry> entryList, out IEnumerable<IDriverData> drivers, out ISeason season, out string error)
        {
            raceId = 0; entryList = null; drivers = null; season = null; error = null;

            if (DevSeasonComboBox.SelectedItem == null || DevRaceComboBox.SelectedItem == null)
            {
                error = "Select a season and a race first.";
                return false;
            }

            int year = int.Parse(((ComboBoxItem)DevSeasonComboBox.SelectedItem).Content.ToString());
            var race = (Race)((ComboBoxItem)DevRaceComboBox.SelectedItem).Tag;

            season = _ams2StorageFactory.SeasonLoader.LoadSeason(year);
            drivers = _ams2StorageFactory.DriversLoader.LoadDrivers(year).Values.Cast<IDriverData>();

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

            // Switch from welcome image to reputation display
            WelcomeImageBorder.Visibility = Visibility.Collapsed;
            ReputationImageBorder.Visibility = Visibility.Visible;

            ReplaceDriverPanel.Visibility = Visibility.Collapsed;
            CustomDriverPanel.Visibility = Visibility.Visible;

            //trigger the selection change (so the description to be updated)
            ReputationComboBox_SelectionChanged(null, null);

            ReputationNameText.Visibility = Visibility.Visible;
            ReputationParagraphText.Visibility = Visibility.Visible;
        }

        private void LoadGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog()
                {
                    Title = "Select Save File",
                    InitialDirectory = StoragePaths.SavesFolder,
                    Filter = "json files (*.json)|*.json"
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // load the file
                    var saveGame = _ams2StorageFactory.GameStorage.LoadGame(dialog.FileName);

                    var result = _seasonChecker.CheckIfSaveGameNeedsRefresh(saveGame);

                    if (result == SaveGameSeasonCheckerResult.NeedsRefresh)
                    {
                        UpdateSave(saveGame);
                    }                    

                    _gameLogicFactory.GameEngine.LoadGame(saveGame);

                    // Open Season Overview window
                    var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _gameLogicFactory, saveGame);
                    seasonOverviewWindow.Owner = this.Owner;
                    seasonOverviewWindow.Show();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading the game: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateSave(ISaveGame saveGame)
        {
            System.Windows.MessageBox.Show("it seems that you've installed a new version of this season. we will update driver ratings, liveries, team names and driver names for the curren season.", "Mod out of date", MessageBoxButton.OK, MessageBoxImage.Warning);

            var driversDict = _ams2StorageFactory.DriversLoader.LoadDrivers(saveGame.CurrentSeason.Year);
            var updatedSeason = _ams2StorageFactory.SeasonLoader.LoadSeason(saveGame.CurrentSeason.Year);
            var raceInfo = updatedSeason.Races.ToDictionary(r => r.RaceId);
            var newDrivers = new List<IDriverData>();
            var newTeamEntries = new List<ITeamEntry>();


            foreach (var driverInSave in saveGame.Drivers)
            {
                var driverInDatabase = driversDict.GetValueOrDefault(driverInSave.DriverId);

                if (driverInDatabase != null)
                {
                    var clonedDriver = driverInDatabase.DeepClone();

                    // clone the driver but USE THE REPUTATION FROM CURRENT SAVE.
                    clonedDriver.Reputation = driverInSave.Reputation;
                    
                    if (clonedDriver.DriverId == saveGame.PlayerData.DriverId)
                    {
                        saveGame.PlayerData.Name = clonedDriver.Name;
                        saveGame.PlayerData.Nationality = clonedDriver.Nationality;
                    }

                    newDrivers.Add(clonedDriver);
                }
                else
                {
                    newDrivers.Add(driverInSave);
                }
            }

            ((Ams2Season)saveGame.CurrentSeason).Ams2Class = ((Ams2Season)updatedSeason).Ams2Class;
            var teamEntryDict = updatedSeason.Teams.DeepClone().ToDictionary(t => t.TeamId);

            foreach (var teamEntry in saveGame.CurrentSeason.Teams)
            {
                var teamFromUpdatedSeason = teamEntryDict.GetValueOrDefault(teamEntry.TeamId);

                if (teamFromUpdatedSeason != null)
                {
                    // just port the driver contracts.
                    var driver1Contract = teamEntry.Driver1Contract;
                    var driver2Contract = teamEntry.Driver2Contract;

                    teamFromUpdatedSeason.Driver1Contract = teamEntry.Driver1Contract;
                    teamFromUpdatedSeason.Driver2Contract = teamEntry.Driver2Contract;

                    //update the flag "default prequalifying" (in case it was updated on merit)
                    teamFromUpdatedSeason.DefaultPrequalifying = teamEntry.DefaultPrequalifying;

                    newTeamEntries.Add(teamFromUpdatedSeason);

                }
                else
                {
                    newTeamEntries.Add(teamEntry);
                }

            }

            saveGame.Drivers = newDrivers;
            saveGame.CurrentSeason.Teams = newTeamEntries;

            var newDriverNamesDict = newDrivers.ToDictionary(d => d.DriverId, d => d.Name);

            // update driver names in historical standings
            if(saveGame.HistoricalDriverStandings != null)
            {
                foreach (var year in saveGame.HistoricalDriverStandings)
                {
                    foreach (var driver in year.Standing)
                    {
                        if (newDriverNamesDict.ContainsKey(driver.DriverId))
                        {
                            driver.DriverName = newDriverNamesDict[driver.DriverId];
                        }       
                    }
                }
            }

            foreach(var race in saveGame.CurrentSeason.Races)
            {
                var raceFromNewSeason = raceInfo.GetValueOrDefault(race.RaceId);

                if(raceFromNewSeason != null) {
                
                    race.CoverPictureUrl = raceFromNewSeason.CoverPictureUrl ?? race.CoverPictureUrl;
                    race.RaceDate = raceFromNewSeason.RaceDate ?? race.RaceDate;
                    race.Circuit = raceFromNewSeason.Circuit;
                    race.RaceName = raceFromNewSeason.RaceName;
                    race.RaceShortName = raceFromNewSeason.RaceShortName ?? race.RaceShortName;
                }
            }
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var optionsWindow = new OptionsWindow(_ams2StorageFactory);
            optionsWindow.Owner = this;
            optionsWindow.ShowDialog();
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

            // Update the image
            if (reputationImages.ContainsKey(reputationItem.Reputation))
            {
                ReputationImage.LoadPhoto(reputationImages[reputationItem.Reputation]);
            }

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
                string driverName = DriverNameTextBox.Text.Trim();
                string nationality = NationalityTextBox.Text.Trim();

                if (string.IsNullOrEmpty(driverName) || string.IsNullOrEmpty(nationality) || string.IsNullOrEmpty(DriverAgeTextBox.Text.Trim()))
                {
                    System.Windows.MessageBox.Show("Please fill in all required fields!", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int driverAge = int.Parse(DriverAgeTextBox.Text);
                int[] favouriteNumbers = FavouriteNumbersTextBox.Text.Split(",").Select(n => int.Parse(n)).ToArray();

                string season = ((ComboBoxItem)SeasonComboBox.SelectedItem).Content.ToString();
                var selectedReputation = (ComboBoxItem)ReputationComboBox.SelectedItem;
                var reputationItem = (ReputationItem)selectedReputation.Tag;
                var seasonYear = int.Parse(season);

                await DownloadSeasonIfNeeded(seasonYear);

                if (!_ams2StorageFactory.SeasonLoader.GetAvailableSeasons().Any(s => s == season))
                {
                    System.Windows.MessageBox.Show($"Season {season} not installed, cannot proceed.", "Validation Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Load season drivers
                var seasonDrivers = _ams2StorageFactory.DriversLoader.LoadDrivers(seasonYear);
                var seasonData = _ams2StorageFactory.SeasonLoader.LoadSeason(seasonYear);

                // NEW: Check if Pay Driver Wild Card is selected
                if (reputationItem.Reputation == DriverReputation.PAY_DRIVER_WILD_CARD)
                {
                    if (driverAge < 18 || driverAge > 42)
                    {
                        System.Windows.MessageBox.Show("for a pay driver, you must be 18 AND at most 42 years old.", "Validation Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Show GenerateAbsenceWindow
                    var payDriverWindow = new GenerateAbsenceWindow(GenerateAbsenceWindowType.PayDriverAtGameStart);
                    payDriverWindow.Owner = this;

                    if (payDriverWindow.ShowDialog() == true)
                    {

                        var createFictionalAbsence = payDriverWindow.CreateFictionalAbsence;

                        // NEW: Use GameEngine to create the game
                        var saveGame = _gameLogicFactory.GameEngine.CreateNewGame(
                            playerName: driverName,
                            playerNationality: nationality,
                            playerAge: driverAge,
                            playerReputation: reputationItem.Reputation,
                            favouriteNumbers: favouriteNumbers,
                            season: seasonData,
                            selectedTeamId: null,
                            replacedDriverId: null,
                            seasonDrivers: seasonDrivers.Select(d => d.Value).Cast<IDriverData>().ToList());

                        if (createFictionalAbsence)
                        {
                            // if there are no absences at the first GP of the season
                            var firstRaceId = saveGame.CurrentSeason.Races.First().RaceId;
                            if (!saveGame.CurrentSeason.Absences.Any(a => a.RaceId == firstRaceId))
                            {
                                // create a new random absence in a midfield (or lower) team
                                var random = new Random();
                                var possibleTeams = saveGame
                                                .CurrentSeason
                                                .Teams
                                                .Where(t => t.Reputation <= TeamReputation.MIDFIELD)
                                                .ToList();

                                var selectedTeam = possibleTeams.ElementAt(random.Next(possibleTeams.Count));

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

                        // add selected helmet design
                        SetPlayerHelmetDesign(saveGame);

                        // Save the game
                        string saveName = $"{driverName}_{seasonYear}".Replace(" ", "_");
                        string savedPath = _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                        // Open Season Overview window
                        var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _gameLogicFactory, saveGame);
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
                var teamSelectionWindow = new TeamSelectionWindow(_ams2StorageFactory, int.Parse(season), false, seasonDrivers.ToDictionary(d => d.Key, d => (IDriverData)d.Value));
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
                            _ams2StorageFactory,
                            _gameLogicFactory,
                            selectedTeamName,
                            teamSelectionWindow.SelectedTeamId,
                            selectedTeamPrincipal,
                            driverName,
                            nationality,
                            driverAge,
                            $"player_{driverName.ToLower().Replace(" ", "_")}",
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

                            // NEW: Use GameEngine to create the game
                            var saveGame = _gameLogicFactory.GameEngine.CreateNewGame(
                                playerName: driverName,
                                playerNationality: nationality,
                                playerAge: driverAge,
                                playerReputation: reputationItem.Reputation,
                                favouriteNumbers: favouriteNumbers,
                                season: seasonData,
                                selectedTeamId: teamSelectionWindow.SelectedTeamId,
                                replacedDriverId: selectedDriver.DriverId,
                                seasonDrivers: seasonDrivers.Select(d => d.Value).Cast<IDriverData>().ToList());

                            // add selected helmet design
                            SetPlayerHelmetDesign(saveGame);

                            // Save the game
                            string saveName = $"{driverName}_{seasonYear}".Replace(" ", "_");
                            string savedPath = _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                            // Open Season Overview window
                            var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _gameLogicFactory, saveGame);
                            seasonOverviewWindow.Owner = this.Owner;
                            seasonOverviewWindow.Show();

                            return;

                        }
                        else
                        {
                            // Player was rejected - create new team selection window for retry
                            teamSelectionWindow = new TeamSelectionWindow(_ams2StorageFactory, int.Parse(season), false, seasonDrivers.ToDictionary(d => d.Key, d => (IDriverData)d.Value));
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
                System.Windows.MessageBox.Show($"Error creating the game: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            } 
        }

        private void SetPlayerHelmetDesign(ISaveGame saveGame)
        {
            var playerDriverData = saveGame.Drivers.First(d => d.DriverId == saveGame.PlayerData.DriverId) as Ams2DriverData;
            var selectedHelmet = _defaultHelmets.FirstOrDefault(h => h.IsSelected);

            if(saveGame.CurrentSeason.Year >= HelmetPicker.HELMET_MODERN_EARLIEST_YEAR)
            {
                playerDriverData.BaseHelmetFile = selectedHelmet.HelmetDesign.HelmetFile;
                playerDriverData.BaseVisorFile = selectedHelmet.HelmetDesign.VisorFile;
            }
            else if (saveGame.CurrentSeason.Year >= HelmetPicker.HELMET_90s_EARLIEST_YEAR)
            {
                playerDriverData.BaseHelmetFile90s = selectedHelmet.HelmetDesign.HelmetFile;
            }
            else if (saveGame.CurrentSeason.Year >= HelmetPicker.HELMET_80s_EARLIEST_YEAR)
            {
                playerDriverData.BaseHelmetFile80s = selectedHelmet.HelmetDesign.HelmetFile;
                playerDriverData.BaseVisorFile80s = selectedHelmet.HelmetDesign.VisorFile;
            }
            else
            {
                playerDriverData.BaseHelmetFile70s = selectedHelmet.HelmetDesign.HelmetFile;
                playerDriverData.BaseVisorFile70s = selectedHelmet.HelmetDesign.VisorFile;
            }
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

            // Hide scenario panel and show season controls
            ScenarioPanel.Visibility = Visibility.Collapsed;
            SeasonComboBox.Visibility = Visibility.Visible;

            // Show season label again
            foreach (var child in ((StackPanel)NewGamePanel).Children)
            {
                if (child is Label label && label.Content.ToString() == "SELECT SEASON:")
                {
                    label.Visibility = Visibility.Visible;
                    break;
                }
            }

            // Clear fields
            DriverNameTextBox.Clear();
            NationalityTextBox.Clear();
            DriverAgeTextBox.Clear();
            FavouriteNumbersTextBox.Clear();
            SeasonComboBox.SelectedIndex = 0;
            ReputationComboBox.SelectedIndex = 0;
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
                    System.Windows.MessageBox.Show($"Season {season} not installed, cannot proceed.", "Validation Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var seasonDrivers = _ams2StorageFactory.DriversLoader.LoadDrivers(seasonYear);
                // Open Team Selection window
                var teamSelectionWindow = new TeamSelectionWindow(_ams2StorageFactory, seasonYear, true, seasonDrivers.ToDictionary(d => d.Key, d => (IDriverData)d.Value), true);
                teamSelectionWindow.Owner = this;

                if (teamSelectionWindow.ShowDialog() == true)
                {
                    var selectedDriver = teamSelectionWindow.SelectedDriver;
                    var selectedTeamId = teamSelectionWindow.SelectedTeamId;

                    var replacedDriverData = seasonDrivers.ContainsKey(selectedDriver.DriverId) ? seasonDrivers[selectedDriver.DriverId] : null;
                    var seasonData = _ams2StorageFactory.SeasonLoader.LoadSeason(int.Parse(season));

                    // NEW: Use GameEngine to create the game With existing driver
                    var saveGame = _gameLogicFactory.GameEngine.CreateNewGameWithExistingDriver(
                                                                                                season: seasonData,
                                                                                                selectedTeamId: selectedTeamId,
                                                                                                driverId: selectedDriver.DriverId,
                                                                                                seasonDrivers: seasonDrivers.Select(d => d.Value).Cast<IDriverData>().ToList());

                    // Save the game
                    string saveName = $"{saveGame.PlayerData.Name}_{season}".Replace(" ", "_");
                    string savedPath = _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                    // Open Season Overview window
                    var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _gameLogicFactory, saveGame);
                    seasonOverviewWindow.Owner = this.Owner;
                    seasonOverviewWindow.Show();

                    return;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error creating the game: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            // Find and hide the season label
            foreach (var child in ((StackPanel)NewGamePanel).Children)
            {
                if (child is Label label && label.Content.ToString() == "SELECT SEASON:")
                {
                    label.Visibility = Visibility.Collapsed;
                    break;
                }
            }

            // Load scenarios
            LoadScenarios();

        }

        private void LoadScenarios()
        {
            _scenarios.Clear();
            ScenarioComboBox.Items.Clear();

           foreach(var seasonFolder in Directory.GetDirectories(StoragePaths.SeasonsFolder))
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
                                ScenarioComboBox.Items.Add($"Season {Path.GetFileName(seasonFolder)}: \"{scenario.Name}\"");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"Error loading scenario from {Path.GetFileName(file)}: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                System.Windows.MessageBox.Show("No scenarios available.", "Information",
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

            // Load and display scenario picture if available
            if (!string.IsNullOrEmpty(selectedScenario.PictureUrl))
            {
                var picturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, selectedScenario.PictureUrl);

                if (ReputationImage.LoadPhoto(picturePath))
                {
                    // Show the image border
                    WelcomeImageBorder.Visibility = Visibility.Collapsed;
                    ReputationImageBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    // No picture or picture not found - show welcome image
                    ReputationImageBorder.Visibility = Visibility.Collapsed;
                    WelcomeImageBorder.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // No picture specified - show welcome image
                ReputationImageBorder.Visibility = Visibility.Collapsed;
                WelcomeImageBorder.Visibility = Visibility.Visible;
            }

            // Animate the info panel
            fadeInStoryboard.Begin(ReputationInfoPanel);
        }

        private void CreateGameScenarioButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ScenarioComboBox.SelectedIndex < 0 || ScenarioComboBox.SelectedIndex >= _scenarios.Count)
                {
                    System.Windows.MessageBox.Show("Please select a scenario first.", "Error",
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
                    UpdateSave(saveGame);
                }
                
                // Save the game
                string saveName = $"{saveGame.PlayerData.Name}_{saveGame.CurrentSeason.Year}".Replace(" ", "_");
                string savedPath = _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                // Open Season Overview window
                var seasonOverviewWindow = new SeasonOverviewWindow(_ams2StorageFactory, _gameLogicFactory, saveGame);
                seasonOverviewWindow.Owner = this.Owner;
                seasonOverviewWindow.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error creating the game: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void HelmetPreview_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is DefaultHelmetDesign clickedDesign)
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
        }

        private void InstallSeasonModButto_Click()
        {

        }
    }
}