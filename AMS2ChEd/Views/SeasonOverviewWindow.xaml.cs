using Ams2ChEd.Business.AMS2.DependencyInjection;
using AMS2ChEd.Business.AMS2.GameLogic;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Extensions;
using AMS2ChEd.Views;
using System.Globalization;
using System.IO;
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
        public string DriverName { get; set; }
        public double Points { get; set; }
        public bool IsPlayer { get; set; }
        public int? RaceNumber { get; set; }
        public bool IsEven { get; set; }
    }

    public class ConstructorStandingDisplay
    {
        public int Position { get; set; }
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
        private Ams2StorageFactory _ams2StorageFactory;
        private GameLogicFactory _gameLogicFactory;

        public SeasonOverviewWindow(Ams2StorageFactory storageFactory, GameLogicFactory gameLogicFactory, ISaveGame saveGame)
        {
            InitializeComponent();
            _ams2StorageFactory = storageFactory;
            _gameLogicFactory = gameLogicFactory;
            this.saveGame = saveGame;
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
            SeasonText.Text = $"Season: {saveGame.CurrentSeason.Year}";

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
                RaceInfoText.Text = $"ROUND {saveGame.NextGpIndex + 1} - {raceDate:d MMMM yyyy}".ToUpper();
            }
            else
            {
                NextGPText.Text = "SEASON COMPLETE";
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

            PlayerTeamText.Text = playerTeam?.TeamName ?? "No Team";

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

        private string FormatReputation(DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD => "Pay Driver - Wild Card",
                DriverReputation.PAY_DRIVER_SEASON => "Pay Driver - Full Season",
                DriverReputation.AGEING_MIDFIELD => "Veteran Midfield Driver",
                DriverReputation.YOUNG_TALENT => "Young Talent",
                DriverReputation.PRIME_MIDFIELD => "Midfield Driver",
                DriverReputation.AGEING_STRONG_MIDFIELD => "Veteran High Midfield Driver",
                DriverReputation.JUST_ONE_LAST_DANCE => "Just One Last Dance",
                DriverReputation.PRIME_STRONG_MIDFIELD => "High Midfield Driver",
                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED => "Washed Veteran Championship Level Driver",
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED => "Washed Championship Level Driver",
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN => "Unproven Championship Level Driver",
                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN => "Young Unproven Championship Level Driver",
                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL => "Veteran Championship Level Driver",
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL => "Championship Level Driver",
                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL => "Young Championship Level Driver",
                _ => "Unknown"
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
                string driverName = driver?.Name ?? "Unknown Driver";

                // Get race number for this driver from their team contract
                int raceNumber = GetDriverRaceNumber(standing.DriverId);

                displayList.Add(new DriverStandingDisplay
                {
                    Position = standing.Position,
                    DriverName = driverName,
                    Points = standing.Points,
                    IsPlayer = standing.DriverId == saveGame.PlayerData.DriverId,
                    RaceNumber = standing.TeamId != null ? raceNumber : null,
                    IsEven = index % 2 == 1
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

                string teamName = teamEntry?.TeamName ?? team?.TeamName ?? "Unknown Team";
                SolidColorBrush teamColor = GetTeamColor(standing.TeamId);

                displayList.Add(new ConstructorStandingDisplay
                {
                    Position = standing.Position,
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
                System.Windows.MessageBox.Show($"Error preparing GP: {ex.Message}", "Error",
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
            var entryListWindow = new Views.EntryListWindow(_ams2StorageFactory, _gameLogicFactory, saveGame);
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
                int nextSeasonYear = saveGame.CurrentSeason.Year + 1;
                
                // STEP 1: Show championship celebration newspaper
                var celebrationWindow = new ChampionshipCelebrationWindow(saveGame);
                celebrationWindow.Owner = this;
                celebrationWindow.ShowDialog();

                await _gameLogicFactory.SeasonUpdaterOrchestrator.PrepareSeasonAsync(nextSeasonYear);

                var isNextSeasonAvailable = _ams2StorageFactory.SeasonLoader.GetAvailableSeasons().Contains(nextSeasonYear.ToString());

                if (!isNextSeasonAvailable)
                {
                    System.Windows.MessageBox.Show($"Season {nextSeasonYear} Not Available - we will re-use the current teams, liveries and GP as base", "Error", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                var originalNewSeason = isNextSeasonAvailable ? LoadNewSeason(nextSeasonYear) : DuplicateCurrentSeasonAsNextSeason(saveGame.CurrentSeason, nextSeasonYear);

                // Load driver ratings database
                var newDriversSeason = isNextSeasonAvailable ? _ams2StorageFactory.DriversLoader.LoadDrivers(nextSeasonYear) : new Dictionary<string, Ams2DriverData>();

                // load the new season's data   
                _gameLogicFactory.EndOfSeasonManager.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, newDriversSeason.ToDictionary(d => d.Key, d => (IDriverData)d.Value));

                // Execute team drops
                var dropResults = _gameLogicFactory.EndOfSeasonManager
                    .ExecuteTeamDrops(saveGame, originalNewSeason)
                    .ToList();


                // STEP 2: Show player contract letter
                var playerDropResult = GetPlayerDropResult(dropResults);
                var playerReputation = GetPlayerCurrentReputation();

                bool playerAcceptedContract = false;

                //if the player is employed by a team
                if (!string.IsNullOrEmpty(saveGame.PlayerData.TeamId))
                {
                    var contractWindow = new OffSeasonContractWindow(saveGame, originalNewSeason.Teams, _ams2StorageFactory, playerDropResult, playerReputation);
                    contractWindow.Owner = this;
                    bool? contractResult = contractWindow.ShowDialog();

                    playerAcceptedContract = contractWindow.PlayerAcceptedContract;
                }

                // Update drop results if player rejected contract
                if (!playerAcceptedContract && !playerDropResult.IsDropped())
                {
                    UpdateDropResultsForPlayerRejection(dropResults);
                }

                // STEP 3: Generate potential team picks and driver proposals

                var newSeasonTeamEntries = originalNewSeason.Teams;
                var ballots = _gameLogicFactory.EndOfSeasonManager
                    .TeamPicksPotentialReplacementsDrivers(nextSeasonYear, saveGame, newSeasonTeamEntries, dropResults)
                    .ToList();

                // STEP 4: If player needs to apply, show team selection window
                IEnumerable<TeamHiringBallot> finalBallots = ballots;

                if (!playerAcceptedContract)
                {
                    var newPlayerReputation = saveGame.Drivers.First(d => d.DriverId == saveGame.PlayerData.DriverId).Reputation;
                    var applicationWindow = new TeamApplicationWindow(saveGame, _ams2StorageFactory, ballots, dropResults, newPlayerReputation, originalNewSeason.Teams);
                    applicationWindow.Owner = this;
                    applicationWindow.ShowDialog();
                    
                    finalBallots = applicationWindow.UpdatedBallots ?? ballots;
                }

                // STEP 5: Generate new season with hirings
                var actualNewSeason = _gameLogicFactory.EndOfSeasonManager
                    .GenerateNewSeasonWithNewHirings(saveGame, originalNewSeason, finalBallots);


                // STEP 6: Show final roster newspaper
                var rosterWindow = new NewSeasonRosterWindow(saveGame, _ams2StorageFactory, actualNewSeason);
                rosterWindow.Owner = this;
                rosterWindow.ShowDialog();

                // STEP 7: if the player STILL hasn't got a team, ask if he'd like to create an absence
                if (string.IsNullOrEmpty(saveGame.PlayerData.TeamId))
                {
                    var generateAbsenceWindow = new GenerateAbsenceWindow(GenerateAbsenceWindowType.NoTeamForNextSeason);
                    generateAbsenceWindow.Owner = this;

                    generateAbsenceWindow.ShowDialog();

                    if (generateAbsenceWindow.CreateFictionalAbsence)
                    {
                        // if there are no absences at the first GP of the season
                        var firstRaceId = actualNewSeason.Races.First().RaceId;
                        if (!actualNewSeason.Absences.Any(a => a.RaceId == firstRaceId))
                        {
                            // create a new random absence in a midfield (or lower) team
                            var random = new Random();
                            var possibleTeams = actualNewSeason
                                            .Teams
                                            .Where(t => t.Reputation <= TeamReputation.MIDFIELD)
                                            .ToList();

                            var selectedTeam = possibleTeams.ElementAt(random.Next(possibleTeams.Count));

                            var driverOut = selectedTeam.PickRandomDriverFromTheTeam();

                            actualNewSeason.Absences = actualNewSeason.Absences.Concat(new[]
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
                }

                // STEP 8: Start new season
                _gameLogicFactory.EndOfSeasonManager.StartNewSeason(saveGame, actualNewSeason);

                // Update player team ID if changed
                UpdatePlayerTeamId(actualNewSeason);

                // Refresh the overview
                LoadOverview();

                // Save the game
                string saveName = $"{saveGame.PlayerData.Name}_{saveGame.CurrentSeason.Year}".Replace(" ", "_");
                _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error during off-season: {ex.Message}\n\n{ex.StackTrace}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ISeason DuplicateCurrentSeasonAsNextSeason(ISeason currentSeason, int nextSeasonYear)
        {
            var result = LoadNewSeason(currentSeason.OriginalYear ?? currentSeason.Year).DeepClone();

            result.OriginalYear = currentSeason.OriginalYear ?? currentSeason.Year;
            result.Year = nextSeasonYear;

            foreach(var race in result.Races)
            {
                var raceDate = DateTime.ParseExact(race.RaceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                race.RaceDate = GetClosestSundayNextYear(raceDate).ToString("yyyy-MM-dd");
            }

            return result;
        }

        private DateTime GetClosestSundayNextYear(DateTime date)
        {
            var d = date.AddYears(1);
            int next = ((int)DayOfWeek.Sunday - (int)d.DayOfWeek + 7) % 7;
            int prev = next == 0 ? 0 : 7 - next;
            return d.AddDays(next <= prev ? next : -prev);
        }

        // Get player's drop status from results
        private DriverFirerOutcome GetPlayerDropResult(List<DropTeamResult> dropResults)
        {
            var playerTeam = saveGame.CurrentSeason.Teams.FirstOrDefault(t =>
                t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            if (playerTeam == null)
                return DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED;

            var teamDropResult = dropResults.FirstOrDefault(d => d.TeamId == playerTeam.TeamId);
            if (teamDropResult == null)
                return DriverFirerOutcome.NOT_DROPPED;

            // Check which driver the player is
            if (playerTeam.Driver1Contract.DriverId == saveGame.PlayerData.DriverId)
                return teamDropResult.DropDriver1;
            else
                return teamDropResult.DropDriver2;
        }

        // Get player's current reputation
        private DriverReputation GetPlayerCurrentReputation()
        {
            var playerDriver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == saveGame.PlayerData.DriverId);
            if (playerDriver != null)
            {
                return playerDriver.Reputation;
            }
            return DriverReputation.PRIME_MIDFIELD;
        }

        // Update drop results if player rejects
        private void UpdateDropResultsForPlayerRejection(List<DropTeamResult> dropResults)
        {
            var playerTeam = saveGame.CurrentSeason.Teams.FirstOrDefault(t =>
                t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            if (playerTeam == null) return;

            var teamDropResult = dropResults.FirstOrDefault(d => d.TeamId == playerTeam.TeamId);
            if (teamDropResult == null) return;

            if (playerTeam.Driver1Contract.DriverId == saveGame.PlayerData.DriverId)
                teamDropResult.DropDriver1 = DriverFirerOutcome.DROPPED_PLAYER_REJECTING;
            else
                teamDropResult.DropDriver2 = DriverFirerOutcome.DROPPED_PLAYER_REJECTING;
        }

        // Load teams for next season
        private ISeason LoadNewSeason(int seasonYear)
        {
            var teamsCache = _ams2StorageFactory.TeamsLoader.LoadTeams();
            var seasonLoader = _ams2StorageFactory.SeasonLoader;
            var newSeasonData = seasonLoader.LoadSeason(seasonYear);
            return newSeasonData;
        }

        // Update player's team ID after moves
        private void UpdatePlayerTeamId(ISeason newSeason)
        {
            var playerTeam = newSeason.Teams.FirstOrDefault(t =>
                t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            if (playerTeam != null)
            {
                saveGame.PlayerData.TeamId = playerTeam.TeamId;
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

        private void EditPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new EditPlayerDetailsWindow(saveGame.PlayerData, saveGame);
            editWindow.Owner = this;
            bool? result = editWindow.ShowDialog();

            if (result == true)
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
        private readonly Ams2StorageFactory _ams2StorageFactory;

        public WpfAbsenceDecisionProvider(Window owner, ISaveGame saveGame, Ams2StorageFactory ams2StorageFactory)
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
                gp?.RaceName ?? "Grand Prix",
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
            return driver?.Name ?? "Unknown Driver";
        }

        private string GetTeamName(string teamId)
        {
            if (teamId == null) return string.Empty;
            var teamsCache = _ams2StorageFactory.TeamsLoader.LoadTeams();
            var teamEntry = _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            var team = teamsCache.ContainsKey(teamId) ? teamsCache[teamId] : null;

            return teamEntry?.TeamName ?? team?.TeamName ?? "Unknown Team";
        }

        private Race GetGrandPrix(int raceId)
        {
            return _saveGame.CurrentSeason.Races.FirstOrDefault(r => r.RaceId == raceId);
        }

    }
}