using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Business.Settings.Contracts;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Extensions;
using AMS2ChEd.Resources;
using AMS2ChEd.Views;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using static AMS2ChEd.Business.Services.OffSeasonMovements;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace AMS2ChEd
{
    public class DriverStandingDisplay
    {
        public int Position { get; set; }
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string TeamId { get; set; }
        public double Points { get; set; }
        public bool IsPlayer { get; set; }
        public int? RaceNumber { get; set; }
        public bool IsEven { get; set; }
        public SolidColorBrush BadgeColor { get; set; }
        public SolidColorBrush BadgeTextColor { get; set; }
    }

    public class ConstructorStandingDisplay
    {
        public int Position { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public double Points { get; set; }
        public bool IsPlayerTeam { get; set; }
        public SolidColorBrush TeamColor { get; set; }
        public bool IsEven { get; set; }
    }

    public enum OffSeasonPhase
    {
        NOT_STARTED,
        REPUTATIONS_AND_RATINGS_UPDATED,
        TEAM_DROPPED_DRIVERS,
        PLAYER_SHOWN_RENEW_PROPOSAL,
        TEAM_PICKED_NEW_DRIVERS,
        PLAYER_CHOOSED_TEAMS,
        TEAM_HIRED_DRIVERS,
        OFFSEASON_COMPLETED,
    }

    public partial class SeasonOverviewWindow : Window
    {
        private ISaveGame saveGame;
        private IGameDataFactory _ams2StorageFactory;
        private IGameInstallSettingsStorage _settingsStorage;
        private GameLogicFactory _gameLogicFactory;
        private IPlayerCosmeticsEditor _cosmeticsEditor;
        private IOffSeasonOrchestrator _offSeasonOrchestrator;

        public SeasonOverviewWindow(IGameDataFactory storageFactory, IGameInstallSettingsStorage settingsStorage, GameLogicFactory gameLogicFactory, ISaveGame saveGame, IPlayerCosmeticsEditor cosmeticsEditor = null, IOffSeasonOrchestrator offSeasonOrchestrator = null)
        {
            InitializeComponent();
            _ams2StorageFactory = storageFactory;
            _settingsStorage = settingsStorage;
            _gameLogicFactory = gameLogicFactory;
            this.saveGame = saveGame;
            _cosmeticsEditor = cosmeticsEditor;
            _offSeasonOrchestrator = offSeasonOrchestrator;
            _gameLogicFactory.AbsenceManager.AbsenceOpportunityAvailable += OnAbsenceOpportunityAvailable;
            _gameLogicFactory.AbsenceManager.AbsenceDecisionMade += OnAbsenceDecisionMade;
            LoadOverview();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Unsubscribe from events to prevent memory leaks
            _gameLogicFactory.AbsenceManager.AbsenceOpportunityAvailable -= OnAbsenceOpportunityAvailable;
            _gameLogicFactory.AbsenceManager.AbsenceDecisionMade -= OnAbsenceDecisionMade;
        }

        private void LoadOverview()
        {

            if (saveGame.NextGpIndex == 0 && saveGame.CurrentSeason.Races.Any())
            {
                ShowRaceCalendarSelection();
            }

            // Set season and next GP
            SeasonText.Text = string.Format(Strings.SeasonOverviewWindow_SeasonText_Format, saveGame.CurrentSeason.Year);

            // Load player data
            LoadPlayerData();

            // Load driver standings
            LoadDriverStandings();

            // Load constructor standings
            LoadConstructorStandings();

            if (saveGame.NextGpIndex < saveGame.CurrentSeason.Races.Count())
            {
                var nextRace = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex);
                NextGPText.Text = nextRace.RaceName.ToUpper();

                // Format race info with round number and date
                DateTime raceDate = DateTime.ParseExact(nextRace.RaceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                RaceInfoText.Text = string.Format(Strings.SeasonOverviewWindow_RoundInfo_Format, saveGame.NextGpIndex + 1, raceDate.ToString("d MMMM yyyy")).ToUpper();
            }
            else
            {
                NextGPText.Text = Strings.SeasonOverviewWindow_SeasonComplete;
                RaceInfoText.Text = "";
            }
        }

        private void LoadPlayerData()
        {
            // Set player name
            PlayerNameText.Text = saveGame.PlayerData.Name;

            // Set player team
            var playerTeam = saveGame.CurrentSeason.Teams.FirstOrDefault(t =>
                t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            PlayerTeamText.Text = playerTeam?.TeamName ?? Strings.SeasonOverviewWindow_NoTeam;

            // Set player reputation
            var playerReputation = GetPlayerReputation();
            PlayerReputationText.Text = FormatReputation(playerReputation);

            // Load player photo 
            LoadPlayerPhoto();
        }


        private DriverReputation GetPlayerReputation()
        {
            var playerDriver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == saveGame.PlayerData.DriverId);
            if (playerDriver != null)
            {
                return playerDriver.Reputation;
            }

            // Default fallback
            return DriverReputation.PRIME_MIDFIELD;
        }

        // Reuses MainWindow's reputation display-name keys (same semantic content shown in both
        // windows, must stay consistent app-wide) rather than duplicating separate translations.
        private string FormatReputation(DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD => Strings.MainWindow_Reputation_PayDriverWildCard_Name,
                DriverReputation.PAY_DRIVER_SEASON => Strings.MainWindow_Reputation_PayDriverSeason_Name,
                DriverReputation.AGEING_MIDFIELD => Strings.MainWindow_Reputation_AgeingMidfield_Name,
                DriverReputation.YOUNG_TALENT => Strings.MainWindow_Reputation_YoungTalent_Name,
                DriverReputation.PRIME_MIDFIELD => Strings.MainWindow_Reputation_PrimeMidfield_Name,
                DriverReputation.AGEING_STRONG_MIDFIELD => Strings.MainWindow_Reputation_AgeingStrongMidfield_Name,
                DriverReputation.JUST_ONE_LAST_DANCE => Strings.MainWindow_Reputation_JustOneLastDance_Name,
                DriverReputation.PRIME_STRONG_MIDFIELD => Strings.MainWindow_Reputation_PrimeStrongMidfield_Name,
                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED => Strings.MainWindow_Reputation_AgeingChampionshipWashed_Name,
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED => Strings.MainWindow_Reputation_PrimeChampionshipWashed_Name,
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN => Strings.MainWindow_Reputation_PrimeChampionshipUnproven_Name,
                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN => Strings.MainWindow_Reputation_YoungChampionshipUnproven_Name,
                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL => Strings.MainWindow_Reputation_AgeingChampionship_Name,
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL => Strings.MainWindow_Reputation_PrimeChampionship_Name,
                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL => Strings.MainWindow_Reputation_YoungChampionship_Name,
                _ => Strings.SeasonOverviewWindow_UnknownReputation
            };
        }

        private void LoadPlayerPhoto()
        {
            bool photoLoaded = false;
            var playerDriver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == saveGame.PlayerData.DriverId);
            PlayerPhotoImage.LoadPhoto(playerDriver?.PictureUrl, PlayerPhotoPlaceholder);
        }

        private void ShowRaceCalendarSelection()
        {
            var calendarWindow = new RaceCalendarSelectionWindow(
                saveGame.CurrentSeason.Races,
                saveGame.CurrentSeason.Year);

            if (this.IsLoaded)
                calendarWindow.Owner = this;

            bool? result = calendarWindow.ShowDialog();
            
            if (result == true && calendarWindow.RacesToRemove.Any())
            {
                // Remove selected races from the calendar
                var racesToKeep = saveGame.CurrentSeason.Races
                    .Where(r => !calendarWindow.RacesToRemove.Contains(r.RaceId))
                    .ToList();

                // remove the amount of races from drivers' contracts
                foreach (var teamEntry in saveGame.CurrentSeason.Teams)
                {
                    teamEntry.Driver1Contract.Races -= calendarWindow.RacesToRemove.Count();
                    teamEntry.Driver2Contract.Races -= calendarWindow.RacesToRemove.Count();
                }

                // Update the season's race list
                saveGame.CurrentSeason.Races = racesToKeep;
            }
        }

        private void LoadDriverStandings()
        {
            var displayList = new List<DriverStandingDisplay>();

            // Load from save game standings using the Drivers list
            int index = 0;
            foreach (var standing in saveGame.CurrentDriverStandings.OrderBy(s => s.Position))
            {
                var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == standing.DriverId);
                string driverName = driver?.Name ?? Strings.SeasonOverviewWindow_UnknownDriver;

                // Get race number for this driver from their team contract
                int raceNumber = GetDriverRaceNumber(standing.DriverId);
                SolidColorBrush badgeColor = GetTeamColor(standing.TeamId);

                displayList.Add(new DriverStandingDisplay
                {
                    Position = standing.Position,
                    DriverId = standing.DriverId,
                    DriverName = driverName,
                    Points = standing.Points,
                    IsPlayer = standing.DriverId == saveGame.PlayerData.DriverId,
                    TeamId = standing.TeamId,
                    RaceNumber = standing.TeamId != null ? raceNumber : null,
                    IsEven = index % 2 == 1,
                    BadgeColor = badgeColor,
                    BadgeTextColor = GetContrastingTextColor(badgeColor.Color)
                });
                index++;
            }

            DriverStandingsItems.ItemsSource = displayList;
        }

        private int GetDriverRaceNumber(string driverId)
        {
            // Find which team this driver is on
            var teamEntry = saveGame.CurrentSeason.Teams.FirstOrDefault(t =>
                t.Driver1Contract.DriverId == driverId ||
                t.Driver2Contract.DriverId == driverId);

            if (teamEntry == null) return 0;

            // Determine if driver 1 or driver 2 and return their number
            if (teamEntry.Driver1Contract.DriverId == driverId)
                return teamEntry.Driver1Contract.DriverNumber;
            else
                return teamEntry.Driver2Contract.DriverNumber;
        }

        private void LoadConstructorStandings()
        {
            var teamsCache = _ams2StorageFactory.TeamsLoader.LoadTeams();
            var displayList = new List<ConstructorStandingDisplay>();

            // Load from save game standings
            int index = 0;
            foreach (var standing in saveGame.CurrentConstructorStandings.OrderBy(s => s.Position))
            {
                var teamEntry = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == standing.TeamId);
                var team = teamsCache.ContainsKey(standing.TeamId)
                    ? teamsCache[standing.TeamId]
                    : null;

                string teamName = teamEntry?.TeamName ?? team?.TeamName ?? Strings.SeasonOverviewWindow_UnknownTeam;
                SolidColorBrush teamColor = GetTeamColor(standing.TeamId);

                displayList.Add(new ConstructorStandingDisplay
                {
                    Position = standing.Position,
                    TeamId = standing.TeamId,
                    TeamName = teamName,
                    Points = standing.Points,
                    IsPlayerTeam = standing.TeamId == saveGame.PlayerData.TeamId,
                    TeamColor = teamColor,
                    IsEven = index % 2 == 1
                });
                index++;
            }

            ConstructorStandingsItems.ItemsSource = displayList;
        }

        private SolidColorBrush GetTeamColor(string teamId)
        {
            // Get team color directly from season data
            var teamEntry = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);

            if (teamEntry != null && !string.IsNullOrEmpty(teamEntry.Color))
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(teamEntry.Color));
                }
                catch
                {
                    // If color conversion fails, use default
                }
            }

            // Default color if no match found or color is invalid
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
        }

        private SolidColorBrush GetContrastingTextColor(Color color)
        {
            // Perceived brightness (YIQ formula)
            double brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000.0;
            return brightness >= 128
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.White);
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (saveGame.NextGpIndex >= saveGame.CurrentSeason.Races.Count())
                {
                    // Season is over - start off-season process
                    await StartOffSeasonProcess();
                    return;
                }

                GenerateEntryListForTheNextRace();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.SeasonOverviewWindow_PrepareGpError_Message, ex.Message), Strings.SeasonOverviewWindow_GenericError_Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateEntryListForTheNextRace()
        {
            // NEW: Use EntryListGenerator to create entry list
            var entryList = _gameLogicFactory.EntryListGenerator.GenerateEntryList(saveGame);
            var absences = _gameLogicFactory.EntryListGenerator.GetAbsencesForGrandPrix(saveGame);

            // NEW: Use AbsenceManager to process absences
            if (absences.Any())
            {
                var decisionProvider = new WpfAbsenceDecisionProvider(this, saveGame, _ams2StorageFactory);
                _gameLogicFactory.AbsenceManager.ProcessAbsences(entryList, absences, saveGame, decisionProvider);
            }

            var preQualiPool = _gameLogicFactory.PreQualiPoolResolver.Resolve(
                saveGame, saveGame.NextGpIndex);

            if (preQualiPool.IsApplicable)
            {
                var poolTeamIds = preQualiPool.PoolTeams.Select(t => t.TeamId).ToHashSet();

                saveGame.PreQualiPoolEntries = entryList
                    .Where(e => poolTeamIds.Contains(e.TeamId))
                    .ToList();

                saveGame.NextGpEntryList = entryList
                    .Where(e => !poolTeamIds.Contains(e.TeamId))
                    .ToList();

                saveGame.PreQualiStatus = PreQualiStatus.Pending;
            }
            else
            {
                saveGame.NextGpEntryList = entryList;
                saveGame.PreQualiPoolEntries = null;
                saveGame.PreQualiStatus = PreQualiStatus.NotApplicable;
            }

            // Save the updated game state
            string saveName = $"{saveGame.PlayerData.Name}_{saveGame.CurrentSeason.Year}".Replace(" ", "_");
            _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

            // Show entry list window
            var entryListWindow = new Views.EntryListWindow(_ams2StorageFactory, _settingsStorage, _gameLogicFactory, saveGame);
            entryListWindow.RaceWeekendCompleted += OnRaceWeekendCompleted;

            entryListWindow.ShowDialog();
        }

        private void OnRaceWeekendCompleted(object sender, ISaveGame updatedSaveGame)
        {
            Dispatcher.Invoke(() =>
            {
                // Replace our saveGame with the updated one
                this.saveGame = updatedSaveGame;

                saveGame.PreQualiStatus = PreQualiStatus.NotApplicable;
                saveGame.PreQualiPoolEntries = null;
                saveGame.CurrentPreQualiDnpqResults = null;

                // save the game on disk
                string saveName = $"{saveGame.PlayerData.Name}_{saveGame.CurrentSeason.Year}".Replace(" ", "_");
                _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                // Refresh UI
                LoadOverview();

            });
        }

        private void OnAbsenceOpportunityAvailable(object sender, AbsenceOpportunityEventArgs e)
        {
            // This is called when an absence opportunity becomes available
            // The WpfAbsenceDecisionProvider will handle showing UI
        }

        private void OnAbsenceDecisionMade(object sender, AbsenceDecisionEventArgs e)
        {
            var absenceM = new WpfAbsenceDecisionProvider(this, saveGame, _ams2StorageFactory);
            absenceM.ShowAbsenceDecisionAnnouncement(e);
        }

        private async Task StartOffSeasonProcess()
        {
            try
            {
                var uiCallbacks = new WpfOffSeasonUiCallbacks(this, _ams2StorageFactory);
                await _offSeasonOrchestrator.RunAsync(saveGame, uiCallbacks);
                LoadOverview();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.SeasonOverviewWindow_OffSeasonError_Message, ex.Message, ex.StackTrace), Strings.SeasonOverviewWindow_GenericError_Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DriverGridButton_Click(object sender, RoutedEventArgs e)
        {
            var gridWindow = new DriverStandingsGridWindow(saveGame);
            gridWindow.Owner = this;
            gridWindow.ShowDialog();
        }

        private void ConstructorGridButton_Click(object sender, RoutedEventArgs e)
        {
            var gridWindow = new ConstructorStandingsGridWindow(saveGame);
            gridWindow.Owner = this;
            gridWindow.ShowDialog();
        }

        private void DriverRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is System.Windows.Controls.Border b && b.DataContext is DriverStandingDisplay d)
            {
                var driver = saveGame.Drivers.FirstOrDefault(x => x.DriverId == d.DriverId);
                var teamName = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == d.TeamId)?.TeamName;
                var window = new Views.DriverAccoladesWindow(saveGame, d.DriverId, driver?.Name ?? d.DriverName, teamName ?? "No Team", driver?.PictureUrl);
                window.Owner = this;
                window.ShowDialog();
            }
        }

        private void ConstructorRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is System.Windows.Controls.Border b && b.DataContext is ConstructorStandingDisplay d)
            {
                var teamEntry = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == d.TeamId);
                var window = new Views.ConstructorAccoladesWindow(saveGame, d.TeamId, d.TeamName, teamEntry?.Color);
                window.Owner = this;
                window.ShowDialog();
            }
        }

        private void EditPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cosmeticsEditor == null) return;

            bool result = _cosmeticsEditor.ShowEditor(saveGame.PlayerData, saveGame, this);

            if (result)
            {
                // save the game on disk
                string saveName = $"{saveGame.PlayerData.Name}_{saveGame.CurrentSeason.Year}".Replace(" ", "_");
                _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

                // Reload player data display
                LoadPlayerData();
            }
        }

        private void HistoricalStandingsButton_Click(object sender, RoutedEventArgs e)
        {
            var historicalWindow = new HistoricalStandingsWindow(saveGame);
            historicalWindow.Owner = this;
            historicalWindow.ShowDialog();
        }
    }

    public class WpfAbsenceDecisionProvider : IAbsenceDecisionProvider
    {
        private readonly Window _owner;
        private readonly ISaveGame _saveGame;
        private readonly IGameDataFactory _ams2StorageFactory;

        public WpfAbsenceDecisionProvider(Window owner, ISaveGame saveGame, IGameDataFactory ams2StorageFactory)
        {
            _owner = owner;
            _saveGame = saveGame;
            _ams2StorageFactory = ams2StorageFactory;
        }

        public bool DoesPlayerWantToApply(AbsenceOpportunity opportunity, bool playerAlreadySteppedIn)
        {
            var gp = GetGrandPrix(opportunity.RaceId);
            var driverOutName = GetDriverName(opportunity.DriverOut);
            var teamName = GetTeamName(opportunity.TeamId);
            var driverInName = GetDriverName(opportunity.DriverIn);
            var isConsecutive = WasThisDriverAbsenteThePreviousRace(_saveGame, opportunity.DriverOut);

            // Show absence announcement window
            var newsWindow = new AbsenceAnnouncementWindow(
                driverOutName,
                teamName,
                gp?.RaceName ?? Strings.SeasonOverviewWindow_GrandPrixFallback,
                driverInName,
                DateTime.ParseExact(gp.RaceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                askPlayerToApply: !playerAlreadySteppedIn && opportunity.DriverIn != _saveGame.PlayerData.DriverId,
                isConsecutive);

            newsWindow.Owner = _owner;
            bool? result = newsWindow.ShowDialog();

            return newsWindow.PlayerWantsToApply == true;
        }

        public void ShowAbsenceDecisionAnnouncement(AbsenceDecisionEventArgs e)
        {
            var isConsecutive = WasThisDriverAbsenteThePreviousRace(_saveGame, e.Decision.Absence.DriverOut);

            if (e.Decision.DecisionType == AbsenceDecisionType.TeamRefused)
            {
                // Player's team won't let them go - show newspaper announcement
                var newsWindow = AbsenceAnnouncementWindow.CreateTeamRefusedWindow(
                    GetDriverName(e.Decision.Absence.DriverOut),
                    GetTeamName(e.Decision.Absence.TeamId),
                    GetGrandPrix(e.Decision.Absence.RaceId).RaceName,
                    GetDriverName(e.Decision.Absence.DriverIn),
                    GetDriverName(_saveGame.PlayerData.DriverId),
                    GetTeamName(_saveGame.PlayerData.TeamId),
                    isConsecutive
                );

                newsWindow.Owner = _owner;
                newsWindow.ShowDialog();
            }
            if (e.Decision.DecisionType == AbsenceDecisionType.PlayerRefused)
            {
                // Team prefers the proposed driver - show newspaper announcement
                var newsWindow = AbsenceAnnouncementWindow.CreateRefusedWindow(
                    GetDriverName(e.Decision.Absence.DriverOut),
                    GetTeamName(e.Decision.Absence.TeamId),
                    GetGrandPrix(e.Decision.Absence.RaceId).RaceName,
                    GetDriverName(e.Decision.Absence.DriverIn),
                    GetTeamName(_saveGame.PlayerData.TeamId),
                    isConsecutive
                );

                newsWindow.Owner = _owner;
                newsWindow.ShowDialog();
            }
            else if (e.Decision.DecisionType == AbsenceDecisionType.PlayerAccepted)
            {
                // Show newspaper announcement of player getting the position
                var newsWindow = AbsenceAnnouncementWindow.CreateAcceptedWindow(
                    GetDriverName(e.Decision.Absence.DriverOut),
                    GetTeamName(e.Decision.Absence.TeamId),
                    GetGrandPrix(e.Decision.Absence.RaceId).RaceName,
                    GetDriverName(_saveGame.PlayerData.DriverId),
                    GetTeamName(_saveGame.PlayerData.TeamId),
                    isConsecutive
                );
                newsWindow.Owner = _owner;
                newsWindow.ShowDialog();
            }
        }

        private bool WasThisDriverAbsenteThePreviousRace(ISaveGame saveGame, string driverOut)
        {
            if (saveGame.NextGpIndex == 0)
                return false;

            var previousRaceid = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex - 1).RaceId;

            return saveGame.CurrentSeason.Absences.Any(a => a.RaceId == previousRaceid && a.DriverOut == driverOut);
        }

        public bool DoesPlayerTeamAllowLeave(string playerTeamId, Absence proposedAbsence)
        {
            if (string.IsNullOrEmpty(playerTeamId))
                return true;

            var playerTeam = _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == _saveGame.PlayerData.TeamId);
            var proposedTeam = _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == proposedAbsence.TeamId);
            var proposedDriverReputation = GetDriverReputation(proposedAbsence.DriverIn, _saveGame.CurrentSeason.Year);
            var playerReputation = GetDriverReputation(_saveGame.PlayerData.DriverId, _saveGame.CurrentSeason.Year);
            return true;
        }


        private DriverReputation GetDriverReputation(string driverId, int season)
        {
            var driversCache = _saveGame.Drivers.ToDictionary(d => d.DriverId, d => d);

            if (driversCache.ContainsKey(driverId))
            {
                var driverData = driversCache[driverId];
                // If no exact season match, use first available
                if (driverData != null)
                {
                    return driverData.Reputation;
                }
            }

            // Default fallback
            return DriverReputation.PRIME_MIDFIELD;
        }

        private string GetDriverName(string driverId)
        {
            if (driverId == _saveGame.PlayerData.DriverId)
                return _saveGame.PlayerData.Name;

            var driver = _saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);
            return driver?.Name ?? Strings.SeasonOverviewWindow_UnknownDriver;
        }

        private string GetTeamName(string teamId)
        {
            if (teamId == null) return string.Empty;
            var teamsCache = _ams2StorageFactory.TeamsLoader.LoadTeams();
            var teamEntry = _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            var team = teamsCache.ContainsKey(teamId) ? teamsCache[teamId] : null;

            return teamEntry?.TeamName ?? team?.TeamName ?? Strings.SeasonOverviewWindow_UnknownTeam;
        }

        private Race GetGrandPrix(int raceId)
        {
            return _saveGame.CurrentSeason.Races.FirstOrDefault(r => r.RaceId == raceId);
        }

    }

    public class WpfOffSeasonUiCallbacks : IOffSeasonUiCallbacks
    {
        private readonly Window _owner;
        private readonly IGameDataFactory _dataFactory;

        public WpfOffSeasonUiCallbacks(Window owner, IGameDataFactory dataFactory)
        {
            _owner = owner;
            _dataFactory = dataFactory;
        }

        public Task ShowChampionshipCelebrationAsync(ISaveGame saveGame)
        {
            var celebrationWindow = new ChampionshipCelebrationWindow(saveGame);
            celebrationWindow.Owner = _owner;
            celebrationWindow.ShowDialog();
            return Task.CompletedTask;
        }

        public Task ShowSeasonUnavailableWarningAsync(int nextSeasonYear)
        {
            System.Windows.MessageBox.Show(string.Format(Strings.SeasonOverviewWindow_SeasonUnavailable_Message, nextSeasonYear), Strings.SeasonOverviewWindow_GenericError_Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        public Task<bool> ShowContractLetterAsync(ISaveGame saveGame, IEnumerable<ITeamEntry> nextSeasonTeamEntries, DriverFirerOutcome dropOutcome, DriverReputation playerReputation)
        {
            var contractWindow = new OffSeasonContractWindow(saveGame, nextSeasonTeamEntries, dropOutcome, playerReputation);
            contractWindow.Owner = _owner;
            contractWindow.ShowDialog();
            return Task.FromResult(contractWindow.PlayerAcceptedContract);
        }

        public Task ShowRetirementNewsAsync(ISaveGame saveGame, IDriverData retiredDriver, string lastTeamId)
        {
            var retirementWindow = new RetirementNewsWindow(saveGame, retiredDriver, lastTeamId);
            retirementWindow.Owner = _owner;
            retirementWindow.ShowDialog();
            return Task.CompletedTask;
        }

        public Task<IEnumerable<TeamHiringBallot>> ShowTeamApplicationAsync(ISaveGame saveGame, IEnumerable<TeamHiringBallot> ballots, List<DropTeamResult> dropResults, DriverReputation newPlayerReputation, IEnumerable<ITeamEntry> nextSeasonTeamEntries)
        {
            var applicationWindow = new TeamApplicationWindow(saveGame, ballots, dropResults, newPlayerReputation, nextSeasonTeamEntries);
            applicationWindow.Owner = _owner;
            applicationWindow.ShowDialog();
            return Task.FromResult(applicationWindow.UpdatedBallots ?? ballots);
        }

        public Task ShowNewSeasonRosterAsync(ISaveGame saveGame, ISeason newSeason)
        {
            var rosterWindow = new NewSeasonRosterWindow(saveGame, _dataFactory, newSeason);
            rosterWindow.Owner = _owner;
            rosterWindow.ShowDialog();
            return Task.CompletedTask;
        }

        public Task<bool> AskCreateFictionalAbsenceAsync()
        {
            var generateAbsenceWindow = new GenerateAbsenceWindow(GenerateAbsenceWindowType.NoTeamForNextSeason);
            generateAbsenceWindow.Owner = _owner;
            generateAbsenceWindow.ShowDialog();
            return Task.FromResult(generateAbsenceWindow.CreateFictionalAbsence);
        }
    }
}