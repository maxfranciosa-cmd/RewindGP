using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Mocks;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace AMS2ChEd.Views
{
    // Converter to make player name bold
    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? FontWeights.Bold : FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EntryDisplay
    {
        public int DriverNumber { get; set; }
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string Nationality { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public bool IsPlayer { get; set; }
    }

    public partial class EntryListWindow : Window
    {
        private ISaveGame saveGame;
        private List<EntryDisplay> _currentEntryDisplay;
        private Ams2StorageFactory _ams2StorageFactory;
        private GameLogicFactory _gameLogicFactory;
        public event EventHandler<ISaveGame> RaceWeekendCompleted;

        public EntryListWindow(Ams2StorageFactory storageFactory, GameLogicFactory gameLogicFactory, ISaveGame saveGame)
        {
            InitializeComponent();
            _ams2StorageFactory = storageFactory;
            _gameLogicFactory = gameLogicFactory;
            this.saveGame = saveGame;
            LoadEntryList();
        }

        private void LoadEntryList()
        {
            bool isPreQuali = saveGame.PreQualiStatus == PreQualiStatus.Pending;

            EntryListTitleText.Text = isPreQuali
                ? "PRE-QUALIFYING ENTRY LIST"
                : "OFFICIAL ENTRY LIST";

            PreQualiBanner.Visibility = isPreQuali
                ? Visibility.Visible
                : Visibility.Collapsed;

            ContinueButton.Content = isPreQuali
                ? "PROCEED TO PRE-QUALIFYING"
                : "CONTINUE TO RACE WEEKEND";

            // Set Grand Prix name and load race poster
            if (saveGame.NextGpIndex < saveGame.CurrentSeason.Races.Count())
            {
                var nextRace = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex);
                GrandPrixText.Text = nextRace.RaceName.ToUpper();
                DateText.Text = (DateTime.ParseExact(nextRace.RaceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToString("dddd, MMMM dd, yyyy");

                // Load race poster image
                RacePosterImage.LoadPhoto(nextRace.CoverPictureUrl);

                FooterText.Text = isPreQuali
                                    ? $"Pre-qualifying entry list for the {saveGame.CurrentSeason.Year} {nextRace.RaceName}."
                                    : $"This document is the official entry list for the {saveGame.CurrentSeason.Year} season.";
            }

            // Set footer
            FooterText.Text = $"This document is the official entry list for the {saveGame.CurrentSeason.Year} season.";

            // Build entry display list
            var displayList = new List<EntryDisplay>();

            var entriesToDisplay = isPreQuali
                                        ? saveGame.PreQualiPoolEntries
                                        : saveGame.NextGpEntryList;

            if (entriesToDisplay != null && entriesToDisplay.Any())
            {
                // Sort by team and driver number
                var sortedEntries = entriesToDisplay
                    .OrderByDescending(e => !string.IsNullOrEmpty(e.Driver1Id) && !string.IsNullOrEmpty(e.Driver2Id))
                    .ThenBy(e => e.Driver1Number + e.Driver2Number)
                    .ToList();

                foreach (var entry in sortedEntries)
                {
                    var teamName = GetTeamName(entry.TeamId);

                    // Add Driver 1
                    if (!string.IsNullOrEmpty(entry.Driver1Id))
                    {
                        var nameAndNationality1 = GetDriverNameAdNationality(entry.Driver1Id);
                        displayList.Add(new EntryDisplay
                        {
                            DriverNumber = entry.Driver1Number,
                            DriverId = entry.Driver1Id,
                            DriverName = nameAndNationality1[0],
                            Nationality = nameAndNationality1[1],
                            TeamId = entry.TeamId,
                            TeamName = teamName,
                            IsPlayer = entry.Driver1Id == saveGame.PlayerData.DriverId
                        });
                    }

                    if (!string.IsNullOrEmpty(entry.Driver2Id))
                    {
                        // Add Driver 2
                        var nameAndNationality2 = GetDriverNameAdNationality(entry.Driver2Id);
                        displayList.Add(new EntryDisplay
                        {
                            DriverNumber = entry.Driver2Number,
                            DriverId = entry.Driver2Id,
                            DriverName = nameAndNationality2[0],
                            Nationality = nameAndNationality2[1],
                            TeamId = entry.TeamId,
                            TeamName = teamName,
                            IsPlayer = entry.Driver2Id == saveGame.PlayerData.DriverId
                        });
                    }
                }
            }
            _currentEntryDisplay = displayList;
            EntryListItems.ItemsSource = displayList;
        }

        private string[] GetDriverNameAdNationality(string driverId)
        {
            if (driverId == saveGame.PlayerData.DriverId)
                return new[] { saveGame.PlayerData.Name, saveGame.PlayerData.Nationality };

            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);
            return new[] { driver.Name, driver.Nationality };
        }

        private string GetTeamName(string teamId)
        {
            var teamsCache = _ams2StorageFactory.TeamsLoader.LoadTeams();
            var teamEntry = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            var team = teamsCache.ContainsKey(teamId) ? teamsCache[teamId] : null;

            var teamName = teamEntry?.TeamName ?? team?.TeamName ?? "Unknown Team";
            return teamName;
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (saveGame.PreQualiStatus == PreQualiStatus.Pending)
            {
                await RunPreQualiPhase();
                return;
            }

            // Check if player is participating in this race
            bool playerParticipating = saveGame.NextGpEntryList?.Any(entry =>
                entry.Driver1Id == saveGame.PlayerData.DriverId ||
                entry.Driver2Id == saveGame.PlayerData.DriverId) ?? false;

            if (playerParticipating)
            {
                // REAL RACE - Apply liveries and run actual race
                var loadingWindow = new ProgressWindow("Applying liveries and Custom AI...");
                loadingWindow.Show();

                try
                {
                    await Task.Run(() => ApplyLiveries(_currentEntryDisplay));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error applying liveries: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    loadingWindow.Close();
                    return;
                }

                loadingWindow.Close();

                // check if the "driver name" setting is populated
                var settings = _ams2StorageFactory.Ams2AppSettingsStorage.LoadSettings();

                if (string.IsNullOrEmpty(settings?.Ams2InGameName))
                {
                    bool? optionDialogResult;
                    do
                    {
                        var optionsWindow = new OptionsWindow(_ams2StorageFactory);
                        MessageBox.Show($"Please indicate your in-game driver name in Automobilista 2 (usually your steam name)", "Add Driver Name",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        optionsWindow.Owner = this;
                        optionDialogResult = optionsWindow.ShowDialog();
                    } while (!(optionDialogResult ?? false));
                }

            }


            // Use real race data service
            var participants = GetParticipants(_currentEntryDisplay);
            var raceDataService = _gameLogicFactory.RaceDataService;
            raceDataService.InitializeRaceWeekend(participants);

            var simulatedRaceDataService = new SimulatedRaceDataService(saveGame);
            simulatedRaceDataService.InitializeRaceWeekend(participants);

            CleanUpExistingRaceWeekendWindowInstances();

            var raceWeekendWindow = new RaceWeekendWindow(_gameLogicFactory, saveGame, simulatedRaceDataService, simulateRace: !playerParticipating);
            raceWeekendWindow.RaceCompleted += OnRaceCompleted;
            raceWeekendWindow.Show();


            this.Close();
        }

        private void CleanUpExistingRaceWeekendWindowInstances()
        {
            // Close all existing RaceWeekendWindow instances (from any source)
            var existingWindows = Application.Current.Windows
                .OfType<RaceWeekendWindow>()
                .ToList(); // ToList() to avoid collection modification during enumeration

            foreach (var window in existingWindows)
            {
                window.Close();
            }
        }

        private async Task RunPreQualiPhase()
        {
            bool playerInPreQuali = saveGame.PreQualiPoolEntries.Any(e =>
                e.Driver1Id == saveGame.PlayerData.DriverId ||
                e.Driver2Id == saveGame.PlayerData.DriverId);

            List<ParticipantData> results = playerInPreQuali
                ? await RunPlayerDrivenPreQuali()
                : RunSimulatedPreQuali();

            await FinalisePreQualiResults(results, playerInPreQuali);
        }

        private List<ParticipantData> RunSimulatedPreQuali()
        {
            var participants = BuildParticipantsFromEntryList(saveGame.PreQualiPoolEntries);
            var simulatedService = new SimulatedRaceDataService(saveGame);
            simulatedService.InitializeRaceWeekend(participants);
            return simulatedService.SimulateQualifyingOnly();
        }

        private async Task<List<ParticipantData>> RunPlayerDrivenPreQuali()
        {
            // Apply liveries for pool drivers only.
            var loadingWindow = new ProgressWindow("Applying Pre-Qualifying liveries and Custom AI...");
            loadingWindow.Show();

            var raceId = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex).RaceId;
            var normalizedSeason = NormalisePreQualiPoolEntries(saveGame.PreQualiPoolEntries);

            try
            {
                await Task.Run(() =>
                    _gameLogicFactory.RacePreparator.PrepareRace(
                        raceId, saveGame.PreQualiPoolEntries, saveGame.Drivers, normalizedSeason));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying liveries: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                loadingWindow.Close();
                return RunSimulatedPreQuali();
            }

            loadingWindow.Close();

            // Resolve player's car and livery name for the instructions window.
            var playerEntry = saveGame.PreQualiPoolEntries.First(e =>
                e.Driver1Id == saveGame.PlayerData.DriverId ||
                e.Driver2Id == saveGame.PlayerData.DriverId);
            var playerTeamData = saveGame.CurrentSeason.Teams
                .Cast<Ams2TeamEntry>()
                .FirstOrDefault(t => t.TeamId == playerEntry.TeamId);
            var playerNumber = playerEntry.Driver1Id == saveGame.PlayerData.DriverId
                ? playerEntry.Driver1Number
                : playerEntry.Driver2Number;

            // Which driver slot (1 or 2) the player occupies - determines which car/malus applies
            var playerDriverSlot = playerEntry.Driver1Id == saveGame.PlayerData.DriverId ? 1 : 2;

            var difficultyDelta = CalculatePreQualiDifficulty(normalizedSeason, playerEntry.TeamId, playerDriverSlot);

            var instructionsWindow = RaceInstructionsWindow.CreatePreQualiWindow(
                saveGame.PlayerData.Name,
                carName: playerTeamData?.GetAms2Car(playerDriverSlot) ?? "",
                liveryName: $"#{playerNumber} {playerTeamData?.TeamName} - {saveGame.PlayerData.Name}",
                opponentsNumber: saveGame.PreQualiPoolEntries.DriverCount() - 1,
                suggestedDifficulty: difficultyDelta);

            instructionsWindow.Owner = this;
            instructionsWindow.ShowDialog();

            // Initialise real race data service in pre-qualifying mode.
            var participants = BuildParticipantsFromEntryList(saveGame.PreQualiPoolEntries);
            var raceDataService = _gameLogicFactory.RaceDataService;
            raceDataService.IsPreQualiSession = true;
            raceDataService.InitializeRaceWeekend(participants);

            // TaskCompletionSource lets us await the session finished event.
            var tcs = new TaskCompletionSource<List<ParticipantData>>();

            EventHandler<SessionFinishedEventArgs> preQualiHandler = null;
            preQualiHandler = (s, e) =>
            {
                raceDataService.PreQualiSessionFinished -= preQualiHandler;
                tcs.TrySetResult(e.FinalStandings);
            };
            raceDataService.PreQualiSessionFinished += preQualiHandler;

            // SimulatedRaceDataService is required by the constructor but won't be used
            // (simulateRace: false, preQualiMode: true).
            var simulatedService = new SimulatedRaceDataService(saveGame);
            simulatedService.InitializeRaceWeekend(participants);

            CleanUpExistingRaceWeekendWindowInstances();

            var raceWeekendWindow = new RaceWeekendWindow(
                _gameLogicFactory,
                saveGame,
                simulatedService,
                simulateRace: false,
                preQualiMode: true);

            // Handle early closure — cancel the task so the UI doesn't hang.
            raceWeekendWindow.Closed += (s, e) =>
            {
                raceDataService.PreQualiSessionFinished -= preQualiHandler;
                tcs.TrySetCanceled();
            };

            raceWeekendWindow.Owner = this;
            raceWeekendWindow.Show();

            try
            {
                var results = await tcs.Task;
                raceWeekendWindow.Close();
                return results;
            }
            catch (TaskCanceledException)
            {
                // Player closed the window before qualifying finished — fall back to simulation.
                return RunSimulatedPreQuali();
            }
        }
        private int CalculatePreQualiDifficulty(Ams2Season normalisedSeason, string playerTeamId, int playerDriverSlot)
        {
            var playerTeam = normalisedSeason.Teams
                .OfType<Ams2TeamEntry>()
                .FirstOrDefault(t => t.TeamId == playerTeamId);

            double playerMalus = playerTeam?
                .GetAms2CarPerformanceMalus(playerDriverSlot)?
                .GetValueOrDefault("qualifying_skill", 0.0) ?? 0.0;

            // +5 difficulty points per 0.1 malus gap from the fastest car (which is now at 0).
            return (int)Math.Round(playerMalus / 0.1) * 5;
        }

        private Ams2Season NormalisePreQualiPoolEntries(List<EntryListEntry> poolEntries)
        {
            var normalisedSeason = ((Ams2Season)saveGame.CurrentSeason).DeepClone();

            // Collect the malus dictionaries relevant to each driver slot actually present in the pool.
            // A team with no second car defined returns the same dictionary instance for both slots.
            var relevantMalusDicts = new List<Dictionary<string, double>>();

            foreach (var entry in poolEntries)
            {
                var team = normalisedSeason.Teams
                    .OfType<Ams2TeamEntry>()
                    .FirstOrDefault(t => t.TeamId == entry.TeamId);
                if (team == null) continue;

                if (!string.IsNullOrEmpty(entry.Driver1Id))
                {
                    var malus1 = team.GetAms2CarPerformanceMalus(1);
                    if (malus1 != null) relevantMalusDicts.Add(malus1);
                }

                if (!string.IsNullOrEmpty(entry.Driver2Id))
                {
                    var malus2 = team.GetAms2CarPerformanceMalus(2);
                    if (malus2 != null) relevantMalusDicts.Add(malus2);
                }
            }

            // Find the minimum qualifying_skill malus across every car represented in the pool.
            var dictsWithQualifyingSkill = relevantMalusDicts
                .Where(m => m.ContainsKey("qualifying_skill"))
                .ToList();

            if (!dictsWithQualifyingSkill.Any())
            {
                return normalisedSeason;
            }

            double minQualifyingSkill = dictsWithQualifyingSkill.Min(m => m["qualifying_skill"]);

            // Shift all qualifying_skill values down by the minimum so the fastest car lands at 0.
            // De-duplicate by reference so a dictionary shared between driver1/driver2
            // (i.e. no second car defined) is only shifted once.
            var shiftedDicts = new HashSet<Dictionary<string, double>>();
            foreach (var malus in dictsWithQualifyingSkill)
            {
                if (!shiftedDicts.Add(malus)) continue;

                malus["qualifying_skill"] -= minQualifyingSkill;
            }

            return normalisedSeason;
        }

        private async Task FinalisePreQualiResults(List<ParticipantData> results, bool playerWasInPreQuali)
        {
            var survivorsToPick = (saveGame.CurrentSeason.MaxDriversPerRace ?? 26) - (saveGame.CurrentSeason.Teams.DriverCount() - saveGame.PreQualiPoolEntries.DriverCount());

            var survivorIds = results
                .Take(survivorsToPick)
                .Select(p => p.DriverId)
                .ToHashSet();

            bool playerSurvived = survivorIds.Contains(saveGame.PlayerData.DriverId);

            // Merge survivors into the committed entry list.
            var survivorEntries = saveGame.PreQualiPoolEntries
                .Where(e => survivorIds.Contains(e.Driver1Id) || survivorIds.Contains(e.Driver2Id))
                .Select(e =>
                {
                    var entry = e.DeepClone();
                    if (!survivorIds.Contains(entry.Driver1Id))
                    {
                        entry.Driver1Id = null;
                        entry.Driver1Number = 0;
                    }
                    if (!survivorIds.Contains(entry.Driver2Id))
                    {
                        entry.Driver2Id = null;
                        entry.Driver2Number = 0;
                    }
                    return entry;
                })
                .ToList();

            saveGame.NextGpEntryList = saveGame.NextGpEntryList
                .Concat(survivorEntries)
                .ToList();

            saveGame.CurrentPreQualiDnpqResults = saveGame.PreQualiPoolEntries
                .SelectMany(e =>
                {
                    var result = new List<ParticipantData>();

                    // Driver1 did not pre-qualify if not in survivors and ID is present.
                    if (!survivorIds.Contains(e.Driver1Id) && !string.IsNullOrEmpty(e.Driver1Id))
                    {
                        var nameNat1 = GetDriverNameAdNationality(e.Driver1Id);
                        result.Add(new ParticipantData
                        {
                            DriverId = e.Driver1Id,
                            DriverName = nameNat1[0],
                            TeamId = e.TeamId,
                            TeamName = GetTeamName(e.TeamId),
                            Number = e.Driver1Number,
                            Position = 0,
                            BestLapTime = "--:--.---",
                            DNF = false,
                            DidNotPreQualify = true
                        });
                    }

                    // Driver2 did not pre-qualify if not in survivors and ID is present.
                    if (!survivorIds.Contains(e.Driver2Id) && !string.IsNullOrEmpty(e.Driver2Id))
                    {
                        var nameNat2 = GetDriverNameAdNationality(e.Driver2Id);
                        result.Add(new ParticipantData
                        {
                            DriverId = e.Driver2Id,
                            DriverName = nameNat2[0],
                            TeamId = e.TeamId,
                            TeamName = GetTeamName(e.TeamId),
                            Number = e.Driver2Number,
                            Position = 0,
                            BestLapTime = "--:--.---",
                            DNF = false,
                            DidNotPreQualify = true
                        });
                    }

                    return result;
                })
                .ToList();

            saveGame.PreQualiStatus = PreQualiStatus.Completed;

            string saveName = $"{saveGame.PlayerData.Name}_{saveGame.CurrentSeason.Year}".Replace(" ", "_");
            _ams2StorageFactory.GameStorage.SaveGame(saveGame, saveName);

            // Inform player if they did not pre-qualify.
            if (playerWasInPreQuali && !playerSurvived)
            {
                var raceName = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex).RaceName;
                MessageBox.Show(
                    $"You did not pre-qualify for the {raceName}.\n\n" +
                    "Your result has been recorded as Did Not Qualify (DNQ). " +
                    "The race weekend will be simulated without you.",
                    "Did Not Qualify",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            // Show results window.
            var nextRace = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex);
            var preQualiResultsWindow = new PreQualiResultsWindow(
                results,
                passCount: survivorsToPick,
                grandPrixName: nextRace.RaceName,
                seasonYear: saveGame.CurrentSeason.Year);

            preQualiResultsWindow.Owner = this;
            preQualiResultsWindow.ShowDialog();

            // Refresh entry list — now shows full roster with normal titles.
            LoadEntryList();
        }

        private IEnumerable<ParticipantData> BuildParticipantsFromEntryList(List<EntryListEntry> entries)
        {
            var displayList = new List<EntryDisplay>();

            foreach (var entry in entries)
            {
                var teamName = GetTeamName(entry.TeamId);

                if (!string.IsNullOrEmpty(entry.Driver1Id))
                {
                    var nameNat1 = GetDriverNameAdNationality(entry.Driver1Id);
                    displayList.Add(new EntryDisplay
                    {
                        DriverNumber = entry.Driver1Number,
                        DriverId = entry.Driver1Id,
                        DriverName = nameNat1[0],
                        Nationality = nameNat1[1],
                        TeamId = entry.TeamId,
                        TeamName = teamName,
                        IsPlayer = entry.Driver1Id == saveGame.PlayerData.DriverId
                    });
                }

                if (!string.IsNullOrEmpty(entry.Driver2Id))
                {
                    var nameNat2 = GetDriverNameAdNationality(entry.Driver2Id);
                    displayList.Add(new EntryDisplay
                    {
                        DriverNumber = entry.Driver2Number,
                        DriverId = entry.Driver2Id,
                        DriverName = nameNat2[0],
                        Nationality = nameNat2[1],
                        TeamId = entry.TeamId,
                        TeamName = teamName,
                        IsPlayer = entry.Driver2Id == saveGame.PlayerData.DriverId
                    });
                }
            }

            return GetParticipants(displayList);
        }

        private void ApplyLiveries(List<EntryDisplay> entryDisplay)
        {
            // Your livery application implementation here
            var raceId = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex).RaceId;
            _gameLogicFactory.RacePreparator.PrepareRace(raceId, saveGame.NextGpEntryList, saveGame.Drivers, saveGame.CurrentSeason);
        }

        private void OnRaceCompleted(object sender, ISaveGame updatedSaveGame)
        {
            // Pass the updated saveGame back up the chain
            RaceWeekendCompleted?.Invoke(this, updatedSaveGame);
        }

        private IEnumerable<ParticipantData> GetParticipants(List<EntryDisplay> entryDisplay)
        {
            return entryDisplay.Select((e, i) => new ParticipantData { DriverName = e.DriverName, DriverId = e.DriverId, TeamId = e.TeamId, TeamName = e.TeamName, Position = i + 1, Number = e.DriverNumber, IsPlayer = e.IsPlayer });
        }
    }
}