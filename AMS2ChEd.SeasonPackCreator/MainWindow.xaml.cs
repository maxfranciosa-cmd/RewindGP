using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.SeasonPackEditor.Services;
using BCnEncoder.Decoder;
using BCnEncoder.ImageSharp;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class MainWindow : Window
    {
        private SeasonPackProject _currentProject;
        private string _staticAssetsSourceFolder;

        public MainWindow()
        {
            InitializeComponent();
            InitializeNewProject();
        }

        private void InitializeNewProject()
        {
            _currentProject = new SeasonPackProject
            {
                Season = new Ams2Season
                {
                    Year = 0,
                    PointsSystem = new Dictionary<string, int>
                    {
                        { "1", 10 }, { "2", 6 }, { "3", 4 }, { "4", 3 }, { "5", 2 }, { "6", 1 }
                    },
                    Races = new List<Race>(),
                    Teams = new List<ITeamEntry>(),
                    Absences = new List<Absence>()
                },
                Drivers = new List<Ams2DriverData>(),
                TextureFiles = new Dictionary<string, string>(),
                XmlFiles = new Dictionary<string, string>(),
                StaticAssetFiles = new List<StaticAssetFile>()
            };

            RefreshUI();
        }

        private void RefreshUI()
        {
            // General Tab
            YearTextBox.Text = _currentProject.Season.Year > 0 ? _currentProject.Season.Year.ToString() : "";
            Ams2ClassTextBox.Text = (_currentProject.Season as Ams2Season)?.Ams2Class ?? "";
            PointsForFastestLapTextBox.Text = _currentProject.Season.PointsForFastestLap?.ToString() ?? "0";

            // Points System
            var pointsList = _currentProject.Season.PointsSystem
                .OrderBy(p => int.Parse(p.Key))
                .Select(p => new PointsSystemEntry { Position = p.Key, Points = p.Value })
                .ToList();
            PointsSystemDataGrid.ItemsSource = pointsList;

            // Races
            RacesDataGrid.ItemsSource = _currentProject.Season.Races;

            // Drivers - create display models with reputation for current season
            var driverDisplayModels = _currentProject.Drivers.Select(d =>
            {
                var values = d.RatingValues;

                return new DriverDisplayModel
                {
                    Driver = d,
                    SeasonYear = _currentProject.Season.Year,
                    DriverId = d.DriverId,
                    Name = d.Name,
                    Nationality = d.Nationality,
                    YearOfBirth = d.YearOfBirth,
                    Reputation = d.Reputation.ToString() ?? "",
                    Aggression = GetRatingDisplayValue(values, "aggression"),
                    AvoidanceForcedMistakes = GetRatingDisplayValue(values, "avoidance_of_forced_mistakes"),
                    AvoidanceMistakes = GetRatingDisplayValue(values, "avoidance_of_mistakes"),
                    BlueFlagConceding = GetRatingDisplayValue(values, "blue_flag_conceding"),
                    Consistency = GetRatingDisplayValue(values, "consistency"),
                    Defending = GetRatingDisplayValue(values, "defending"),
                    FuelManagement = GetRatingDisplayValue(values, "fuel_management"),
                    QualifyingSkill = GetRatingDisplayValue(values, "qualifying_skill"),
                    RaceSkill = GetRatingDisplayValue(values, "race_skill"),
                    Stamina = GetRatingDisplayValue(values, "stamina"),
                    StartReactions = GetRatingDisplayValue(values, "start_reactions"),
                    TyreManagement = GetRatingDisplayValue(values, "tyre_management"),
                    VehicleReliability = GetRatingDisplayValue(values, "vehicle_reliability"),
                    WeatherTyreChanges = GetRatingDisplayValue(values, "weather_tyre_changes"),
                    WetSkill = GetRatingDisplayValue(values, "wet_skill")
                };
            }).ToList();
            DriversDataGrid.ItemsSource = driverDisplayModels;

            // Teams
            TeamsDataGrid.ItemsSource = null;
            TeamsDataGrid.ItemsSource = _currentProject.Season.Teams;

            // Absences
            var absencesWithFlag = _currentProject.Season.Absences.Select(a => new
            {
                a.RaceId,
                a.TeamId,
                a.DriverOut,
                a.DriverIn,
                HasChainedAbsence = a.ChainedAbsence != null
            }).ToList();
            AbsencesDataGrid.ItemsSource = absencesWithFlag;

            // Scenarios
            ScenariosDataGrid.ItemsSource = null;
            ScenariosDataGrid.ItemsSource = _currentProject.Scenarios ?? (_currentProject.Scenarios = new List<ScenarioEntry>());

            StatusTextBlock.Text = _currentProject.Season.Year > 0
                ? $"Season Pack: {_currentProject.Season.Year}"
                : "Season Pack: [Year Not Set]";
        }

        private string GetRatingDisplayValue(Dictionary<string, double> values, string key)
        {
            if (values == null || !values.ContainsKey(key))
                return "";

            return values[key].ToString("F3");
        }

        #region Menu Actions

        private void NewSeason_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to create a new season? Any unsaved changes will be lost.",
                "New Season",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                InitializeNewProject();
                StatusTextBlock.Text = "New season created";
            }
        }

        private void LoadSeason_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Load Season JSON"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var seasonPack = JsonSerializer.Deserialize<SeasonPackProject>(json, DefaultJsonSerializerOptions.Instance);

                    _currentProject = seasonPack;
                    RefreshUI();
                    StatusTextBlock.Text = $"Loaded season from {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading season: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveSeason_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = $"season_{_currentProject.Season.Year}.json",
                Title = "Save Season JSON"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    UpdateProjectFromUI();
                    var json = JsonSerializer.Serialize(_currentProject, DefaultJsonSerializerOptions.Instance);
                    File.WriteAllText(dialog.FileName, json);
                    StatusTextBlock.Text = $"Saved to {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving season: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Points System

        private void AddPointsPosition_Click(object sender, RoutedEventArgs e)
        {
            var currentPoints = _currentProject.Season.PointsSystem;
            var nextPosition = (currentPoints.Count + 1).ToString();
            currentPoints.Add(nextPosition, 0);
            RefreshUI();
        }

        private void RemovePointsPosition_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProject.Season.PointsSystem.Count > 0)
            {
                var lastPosition = _currentProject.Season.PointsSystem.Keys.OrderByDescending(k => int.Parse(k)).First();
                _currentProject.Season.PointsSystem.Remove(lastPosition);
                RefreshUI();
            }
        }

        #endregion

        #region Races

        private void RacesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/disable edit buttons based on selection
        }

        private void AddRace_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RaceEditorDialog(_currentProject.TextureFiles);
            if (dialog.ShowDialog() == true)
            {
                var races = _currentProject.Season.Races.ToList();
                races.Add(dialog.Race);
                _currentProject.Season.Races = races;
                RefreshUI();
            }
        }

        private void EditRace_Click(object sender, RoutedEventArgs e)
        {
            if (RacesDataGrid.SelectedItem is Race selectedRace)
            {
                var dialog = new RaceEditorDialog(_currentProject.TextureFiles, selectedRace);
                if (dialog.ShowDialog() == true)
                {
                    RefreshUI();
                }
            }
        }

        private void AddSprintRace_Click(object sender, RoutedEventArgs e)
        {
            if (RacesDataGrid.SelectedItem is Race selectedRace)
            {
                // Parse the race date to calculate the day before
                string sprintDate = selectedRace.RaceDate;
                if (DateTime.TryParse(selectedRace.RaceDate, out DateTime raceDateTime))
                {
                    var sprintDateTime = raceDateTime.AddDays(-1);
                    sprintDate = sprintDateTime.ToString("yyyy-MM-dd");
                }

                var newRaceId = _currentProject.Season.Races.Max(r => r.RaceId) + 1;

                // Create sprint race
                var sprintRace = new Race
                {
                    RaceId = newRaceId,
                    RaceName = selectedRace.RaceName + " (Sprint)",
                    RaceShortName = selectedRace.RaceShortName + "*",
                    RaceDate = sprintDate,
                    Circuit = selectedRace.Circuit,
                    CoverPictureUrl = selectedRace.CoverPictureUrl,
                    PointsForFastestLap = 0,
                    IgnoreForPositionsTally = true
                };

                if (_currentProject.Season.Year == 2019)
                {
                    sprintRace.PointsSystem = new Dictionary<string, int>
                    {
                        { "1", 3 },
                        { "2", 2 },
                        { "3", 1 }
                    };
                }
                else
                {
                    sprintRace.PointsSystem = new Dictionary<string, int>
                    {
                        { "1", 8 },
                        { "2", 7 },
                        { "3", 6 },
                        { "4", 5 },
                        { "5", 4 },
                        { "6", 3 },
                        { "7", 2 },
                        { "8", 1 }
                    };
                }

                // Insert the sprint race directly before the main race
                var races = _currentProject.Season.Races.ToList();
                var index = races.IndexOf(selectedRace);
                if (index >= 0)
                {
                    races.Insert(index, sprintRace);
                    _currentProject.Season.Races = races;
                    RefreshUI();
                }
            }
            else
            {
                MessageBox.Show("Please select a race first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RemoveRace_Click(object sender, RoutedEventArgs e)
        {
            if (RacesDataGrid.SelectedItem is Race selectedRace)
            {
                var result = MessageBox.Show(
                    $"Remove race '{selectedRace.RaceName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var races = _currentProject.Season.Races.ToList();
                    races.Remove(selectedRace);
                    _currentProject.Season.Races = races;
                    RefreshUI();
                }
            }
        }

        private List<Race> ParseWikipediaRaceTable(string clipboardText)
        {
            var races = new List<Race>();
            var lines = clipboardText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Dictionary for common GP short names
            var shortNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Australian", "AUS" },
                { "Bahrain", "BAH" },
                { "Chinese", "CHN" },
                { "Azerbaijan", "AZE" },
                { "Spanish", "ESP" },
                { "Monaco", "MON" },
                { "Canadian", "CAN" },
                { "French", "FRA" },
                { "Austrian", "AUT" },
                { "British", "GBR" },
                { "German", "GER" },
                { "Hungarian", "HUN" },
                { "Belgian", "BEL" },
                { "Italian", "ITA" },
                { "Singapore", "SIN" },
                { "Russian", "RUS" },
                { "Japanese", "JPN" },
                { "United States", "USA" },
                { "Mexican", "MEX" },
                { "Brazilian", "BRA" },
                { "Abu Dhabi", "ABU" },
                { "Argentine", "ARG" },
                { "San Marino", "SMR" },
                { "European", "EUR" },
                { "Portuguese", "POR" },
                { "Dutch", "NED" },
                { "Luxembourg", "LUX" },
                { "Swiss", "SUI" },
                { "Swedish", "SWE" },
                { "South African", "RSA" },
                { "Pacific", "PAC" },
                { "Turkish", "TUR" },
                { "Indian", "IND" },
                { "Korean", "KOR" },
                { "Malaysian", "MAL" },
                { "Saudi Arabian", "SAU" },
                { "Miami", "MIA" },
                { "Emilia Romagna", "EMI" },
                { "Emilia-Romagna", "EMI" },
                { "Styrian", "STY" },
                { "70th Anniversary", "70A" },
                { "Tuscany", "TUS" },
                { "Eifel", "EIF" },
                { "Sakhir", "SAK" },
                { "Las Vegas", "LV" },
                { "Qatar", "QAT" }
            };

            foreach (var line in lines)
            {
                try
                {
                    // Split by tab character (typical Wikipedia table format)
                    var parts = line.Split('\t').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

                    if (parts.Count < 4)
                        continue;

                    // First part should be the race number
                    if (!int.TryParse(parts[0], out int raceId))
                        continue;

                    // Second part is the race name (e.g., "Australian Grand Prix")
                    string raceName = parts[1];

                    // Find circuit - it's usually after the country
                    // Format: "Country Circuit Name, City"
                    string circuit = "";
                    string raceDate = "";

                    // Parts[2] is typically country, parts[3] is circuit, parts[4] is date
                    if (parts.Count >= 4)
                    {
                        circuit = parts[2]; // Take the full circuit string including country
                        raceDate = parts[parts.Count - 1]; // Last part is always the date

                        // Append season year to the date (e.g., "10 March" becomes "10 March 1996")

                        string[] formats = { "d MMMM yyyy", "dd MMMM yyyy" };
                        if (DateTime.TryParseExact(
                                            $"{raceDate} {_currentProject.Season.Year}",
                                            formats,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.None,
                                            out DateTime raceDateTime))
                        {
                            raceDate = raceDateTime.ToString("yyyy-MM-dd");
                        }
                    }

                    // Generate short name
                    string shortName = GenerateShortName(raceName, shortNames);

                    var race = new Race
                    {
                        RaceId = raceId,
                        RaceName = raceName,
                        RaceShortName = shortName,
                        Circuit = circuit,
                        RaceDate = raceDate,
                        CoverPictureUrl = "", // Not populated
                        PointsForFastestLap = _currentProject.Season.Year >= 2019 && _currentProject.Season.Year <= 2024 ? 1 : 0,
                        IgnoreForPositionsTally = false
                    };

                    races.Add(race);
                }
                catch
                {
                    // Skip malformed lines
                    continue;
                }
            }

            return races;
        }

        private string GenerateShortName(string raceName, Dictionary<string, string> shortNames)
        {
            // Remove "Grand Prix" from the name
            string nameWithoutGP = raceName.Replace("Grand Prix", "").Trim();

            // Check if we have a specific short name for this GP
            foreach (var kvp in shortNames)
            {
                if (nameWithoutGP.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // Fallback: take first 3 letters of the first word
            var words = nameWithoutGP.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                string firstWord = words[0];
                return firstWord.Length >= 3 ? firstWord.Substring(0, 3).ToUpper() : firstWord.ToUpper();
            }

            return "UNK";
        }

        #endregion

        #region Drivers

        private void DriversDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/disable edit buttons based on selection
        }

        private void AddDriver_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DriverEditorDialog(_currentProject.Season.Year, _currentProject.TextureFiles);
            if (dialog.ShowDialog() == true)
            {
                _currentProject.Drivers.Add(dialog.Driver);
                RefreshUI();
            }
        }

        private void EditDriver_Click(object sender, RoutedEventArgs e)
        {
            if (DriversDataGrid.SelectedItem is DriverDisplayModel selectedDisplayModel)
            {
                var dialog = new DriverEditorDialog(_currentProject.Season.Year, _currentProject.TextureFiles, selectedDisplayModel.Driver);
                if (dialog.ShowDialog() == true)
                {
                    RefreshUI();
                }
            }
        }

        private void RemoveDriver_Click(object sender, RoutedEventArgs e)
        {
            if (DriversDataGrid.SelectedItem is DriverDisplayModel selectedDisplayModel)
            {
                var result = MessageBox.Show(
                    $"Remove driver '{selectedDisplayModel.Name}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _currentProject.Drivers.Remove(selectedDisplayModel.Driver);
                    RefreshUI();
                }
            }
        }


        private void DriversContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            // Clear existing submenu items
            RemoveMalusMenuItem.Items.Clear();

            // Check if a driver is selected
            if (DriversDataGrid.SelectedItem is not DriverDisplayModel selectedDriver)
            {
                RemoveMalusMenuItem.IsEnabled = false;
                return;
            }

            RemoveMalusMenuItem.IsEnabled = true;

            // Get all teams from the current project
            var teams = (_currentProject.Season as Ams2Season)?.Teams;
            if (teams == null || !teams.Any())
            {
                var noTeamsItem = new MenuItem { Header = "(No teams available)", IsEnabled = false };
                RemoveMalusMenuItem.Items.Add(noTeamsItem);
                return;
            }

            // Create a submenu item for each team
            foreach (var team in teams.OrderBy(t => t.TeamId))
            {
                var menuItem = new MenuItem
                {
                    Header = $"{team.TeamId} - {team.TeamName}",
                    Tag = new { Driver = selectedDriver, Team = team }
                };
                menuItem.Click += RemoveMalusFromTeam_Click;
                RemoveMalusMenuItem.Items.Add(menuItem);
            }
        }

        private void RemoveMalusFromTeam_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.Tag == null)
                return;

            dynamic tag = menuItem.Tag;
            DriverDisplayModel driver = tag.Driver;
            Ams2TeamEntry team = tag.Team;

            try
            {


                var adjustedCount = 0;
                // Apply the inverse of each malus (add the malus value to remove its effect)
                foreach (var key in team.Ams2CarPerformanceMalus.Keys)
                {
                    if (driver.Driver.RatingValues.ContainsKey(key))
                    {
                        double newValue = driver.Driver.RatingValues[key] + team.Ams2CarPerformanceMalus[key];

                        // Clamp to 0.0 - 1.0
                        newValue = Math.Max(0.0, Math.Min(1.0, newValue));

                        // Round to avoid floating-point precision issues (e.g., 0.8200000000000001)
                        newValue = Math.Round(newValue, 10);

                        driver.Driver.RatingValues[key] = newValue;
                        adjustedCount++;
                    }
                }

                // Refresh the UI to show updated values
                RefreshUI();

                MessageBox.Show(
                    $"Removed malus from team '{team.TeamId}' for driver '{driver.Name}'.\n{adjustedCount} ratings adjusted.",
                    "Malus Removed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing malus: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
        #endregion

        #region Teams

        private void TeamsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/disable edit buttons based on selection
        }

        private void AddTeam_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TeamEditorDialog(_currentProject.Drivers);
            if (dialog.ShowDialog() == true)
            {
                var teams = _currentProject.Season.Teams.ToList();
                teams.Add(dialog.Team);
                _currentProject.Season.Teams = teams;
                RefreshUI();
            }
        }

        private void EditTeam_Click(object sender, RoutedEventArgs e)
        {
            if (TeamsDataGrid.SelectedItem is Ams2TeamEntry selectedTeam)
            {
                var dialog = new TeamEditorDialog(_currentProject.Drivers, selectedTeam);
                if (dialog.ShowDialog() == true)
                {
                    RefreshUI();
                }
            }
        }

        private void EditLiveries_Click(object sender, RoutedEventArgs e)
        {
            if (TeamsDataGrid.SelectedItem is Ams2TeamEntry selectedTeam)
            {
                var dialog = new LiveryEditorDialog(selectedTeam, _currentProject.Season.Races, _currentProject.TextureFiles, _currentProject.XmlFiles, _currentProject.Season.Year);
                if (dialog.ShowDialog() == true)
                {
                    RefreshUI();
                }
            }
            else
            {
                MessageBox.Show("Please select a team first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RemoveTeam_Click(object sender, RoutedEventArgs e)
        {
            if (TeamsDataGrid.SelectedItem is Ams2TeamEntry selectedTeam)
            {
                var result = MessageBox.Show(
                    $"Remove team '{selectedTeam.TeamName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var teams = _currentProject.Season.Teams.ToList();
                    teams.Remove(selectedTeam);
                    _currentProject.Season.Teams = teams;
                    RefreshUI();
                }
            }
        }

        #endregion

        #region Absences

        private void AbsencesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/disable edit buttons based on selection
        }

        private void AddAbsence_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AbsenceEditorDialog(
                _currentProject.Season.Races,
                _currentProject.Season.Teams,
                _currentProject.Drivers);

            if (dialog.ShowDialog() == true)
            {
                var absences = _currentProject.Season.Absences.ToList();
                absences.AddRange(dialog.Absences);
                _currentProject.Season.Absences = absences;
                RefreshUI();
            }
        }

        private void EditAbsence_Click(object sender, RoutedEventArgs e)
        {
            if (AbsencesDataGrid.SelectedItem != null)
            {
                var selectedItem = AbsencesDataGrid.SelectedItem;
                var raceId = (int)selectedItem.GetType().GetProperty("RaceId").GetValue(selectedItem);
                var teamId = (string)selectedItem.GetType().GetProperty("TeamId").GetValue(selectedItem);
                var driverOut = (string)selectedItem.GetType().GetProperty("DriverOut").GetValue(selectedItem);

                var absence = _currentProject.Season.Absences.FirstOrDefault(a =>
                    a.RaceId == raceId && a.TeamId == teamId && a.DriverOut == driverOut);

                if (absence != null)
                {
                    var dialog = new AbsenceEditorDialog(
                        _currentProject.Season.Races,
                        _currentProject.Season.Teams,
                        _currentProject.Drivers,
                        absence);

                    if (dialog.ShowDialog() == true)
                    {
                        RefreshUI();
                    }
                }
            }
        }

        private void RemoveAbsence_Click(object sender, RoutedEventArgs e)
        {
            if (AbsencesDataGrid.SelectedItem != null)
            {
                var result = MessageBox.Show(
                    "Remove this absence?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var selectedItem = AbsencesDataGrid.SelectedItem;
                    var raceId = (int)selectedItem.GetType().GetProperty("RaceId").GetValue(selectedItem);
                    var teamId = (string)selectedItem.GetType().GetProperty("TeamId").GetValue(selectedItem);
                    var driverOut = (string)selectedItem.GetType().GetProperty("DriverOut").GetValue(selectedItem);

                    var absence = _currentProject.Season.Absences.FirstOrDefault(a =>
                        a.RaceId == raceId && a.TeamId == teamId && a.DriverOut == driverOut);

                    if (absence != null)
                    {
                        var absences = _currentProject.Season.Absences.ToList();
                        absences.Remove(absence);
                        _currentProject.Season.Absences = absences;
                        RefreshUI();
                    }
                }
            }
        }

        #endregion

        #region Scenarios

        private void AddScenario_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ScenarioEditorDialog();
            if (dialog.ShowDialog() == true)
            {
                _currentProject.Scenarios ??= new List<ScenarioEntry>();
                _currentProject.Scenarios.Add(dialog.Scenario);
                RefreshUI();
            }
        }

        private void EditScenario_Click(object sender, RoutedEventArgs e)
        {
            if (ScenariosDataGrid.SelectedItem is ScenarioEntry selected)
            {
                var dialog = new ScenarioEditorDialog(selected);
                if (dialog.ShowDialog() == true)
                {
                    RefreshUI();
                }
            }
            else
            {
                MessageBox.Show("Please select a scenario first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BuildScenario_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProject.Season.Year <= 0)
            {
                MessageBox.Show("Please set a valid year before creating a scenario.", "Year Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ScenarioEntry target;
            bool isNew = false;

            if (ScenariosDataGrid.SelectedItem is ScenarioEntry selected)
            {
                target = selected;
            }
            else
            {
                // Create a new entry to populate
                target = new ScenarioEntry();
                isNew = true;
            }

            // Warn about stale config if editing
            if (target.SaveConfig != null)
            {
                var warnings = ScenarioSaveBuilder.GetWarnings(
                    target.SaveConfig, _currentProject.Season, _currentProject.Drivers);
                if (warnings.Any())
                {
                    var msg = "This scenario config may be out of sync with the current season:\n\n"
                            + string.Join("\n", warnings.Select(w => "• " + w))
                            + "\n\nOpen the wizard anyway?";
                    if (MessageBox.Show(msg, "Stale Config", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                            == MessageBoxResult.No)
                        return;
                }
            }

            var wizard = new ScenarioCreatorWizard(
                _currentProject.Season,
                _currentProject.Drivers,
                target.SaveConfig);

            wizard.Owner = this;

            if (wizard.ShowDialog() == true)
            {
                target.SaveConfig = wizard.ResultConfig;
                target.Name = wizard.ScenarioName;
                target.Description = wizard.ScenarioDescription;
                target.PictureFullPath = wizard.PictureFullPath;
                target.GameFileFullPath = null; // generated – clear any manual path

                if (isNew)
                {
                    _currentProject.Scenarios ??= new List<ScenarioEntry>();
                    _currentProject.Scenarios.Add(target);
                }

                RefreshUI();
            }
        }

        private void RemoveScenario_Click(object sender, RoutedEventArgs e)
        {
            if (ScenariosDataGrid.SelectedItem is ScenarioEntry selected)
            {
                var result = MessageBox.Show(
                    $"Remove scenario '{selected.Name}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _currentProject.Scenarios.Remove(selected);
                    RefreshUI();
                }
            }
            else
            {
                MessageBox.Show("Please select a scenario first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Static Assets

        private void BrowseStaticAssetsFolder_Click(object sender, RoutedEventArgs e)
        {
            // Use OpenFileDialog in folder selection mode (WPF compatible)
            var dialog = new OpenFileDialog
            {
                Title = "Select Static Assets Source Folder",
                // This is a trick to make OpenFileDialog select folders
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                Filter = "Folders|*.none",
                ValidateNames = false
            };

            if (dialog.ShowDialog() == true)
            {
                // Get the directory from the selected path
                _staticAssetsSourceFolder = Path.GetDirectoryName(dialog.FileName);
                StaticAssetsFolderTextBox.Text = _staticAssetsSourceFolder;

                // Load all files from the folder recursively
                LoadStaticAssetFiles();
            }
        }

        private void LoadStaticAssetFiles()
        {
            _currentProject.StaticAssetFiles = _currentProject.StaticAssetFiles ?? new List<StaticAssetFile>();
            _currentProject.StaticAssetFiles.Clear();

            if (string.IsNullOrWhiteSpace(_staticAssetsSourceFolder) || !Directory.Exists(_staticAssetsSourceFolder))
                return;

            try
            {
                var files = Directory.GetFiles(_staticAssetsSourceFolder, "*.*", SearchOption.AllDirectories)
                    .OrderBy(f => f)
                    .ToList();

                foreach (var file in files)
                {
                    var relativePath = file.Substring(_staticAssetsSourceFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var fileInfo = new FileInfo(file);

                    _currentProject.StaticAssetFiles.Add(new StaticAssetFile
                    {
                        FilePath = relativePath,
                        FullPath = file,
                        Size = fileInfo.Length,
                        SizeFormatted = FormatFileSize(fileInfo.Length)
                    });
                }

                // Refresh the DataGrid
                StaticAssetsDataGrid.ItemsSource = null;
                StaticAssetsDataGrid.ItemsSource = _currentProject.StaticAssetFiles;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddStaticAssetFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Files to Add to Static Assets"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    var fileName = Path.GetFileName(file);
                    var fileInfo = new FileInfo(file);

                    // Check if file already exists
                    if (_currentProject.StaticAssetFiles.Any(f => f.FilePath == fileName))
                    {
                        MessageBox.Show($"File '{fileName}' already exists in the list.", "Duplicate File", MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }

                    _currentProject.StaticAssetFiles.Add(new StaticAssetFile
                    {
                        FilePath = fileName,
                        FullPath = file,
                        Size = fileInfo.Length,
                        SizeFormatted = FormatFileSize(fileInfo.Length)
                    });
                }

                // Refresh the DataGrid
                StaticAssetsDataGrid.ItemsSource = null;
                StaticAssetsDataGrid.ItemsSource = _currentProject.StaticAssetFiles;
            }
        }

        private void RemoveStaticAssetFiles_Click(object sender, RoutedEventArgs e)
        {
            if (StaticAssetsDataGrid.SelectedItems.Count > 0)
            {
                var result = MessageBox.Show(
                    $"Remove {StaticAssetsDataGrid.SelectedItems.Count} selected file(s)?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var itemsToRemove = StaticAssetsDataGrid.SelectedItems.Cast<StaticAssetFile>().ToList();
                    foreach (var item in itemsToRemove)
                    {
                        _currentProject.StaticAssetFiles.Remove(item);
                    }

                    // Refresh the DataGrid
                    StaticAssetsDataGrid.ItemsSource = null;
                    StaticAssetsDataGrid.ItemsSource = _currentProject.StaticAssetFiles;
                }
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region Tab Navigation

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tabControl)
            {
                // Check if we're moving away from the General tab
                if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem removedTab)
                {
                    // Get the selected tab
                    if (tabControl.SelectedItem is TabItem selectedTab)
                    {
                        // If moving from General tab to any other tab, validate year is set
                        if (removedTab.Header?.ToString() == "GENERAL" && selectedTab.Header?.ToString() != "GENERAL")
                        {
                            if (!int.TryParse(YearTextBox.Text, out int year) || year <= 0)
                            {
                                MessageBox.Show(
                                    "Please set a valid year in the General tab before proceeding.",
                                    "Year Required",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);

                                // Revert to General tab
                                tabControl.SelectedItem = removedTab;
                                e.Handled = true;
                                return;
                            }

                            double pointsForFastestLap = 0;
                            double.TryParse(PointsForFastestLapTextBox.Text, out pointsForFastestLap);
                            _currentProject.Season.PointsForFastestLap = pointsForFastestLap;

                            // Update the project year if it's different
                            if (_currentProject.Season.Year != year)
                            {
                                _currentProject.Season.Year = year;
                            }
                            _currentProject.Season.Ams2Class = Ams2ClassTextBox.Text;
                        }
                    }
                }
            }
        }

        #endregion

        #region Export

        private async void ExportSeasonPack_Click(object sender, RoutedEventArgs e)
        {
            UpdateProjectFromUI();

            var dialog = new SaveFileDialog
            {
                Filter = "Rewind GP Season Pack (*.rwgp)|*.rwgp",
                FileName = $"SeasonPack_{_currentProject.Season.Year}.rwgp",
                Title = "Export Season Pack"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    StatusTextBlock.Text = "Exporting season pack...";
                    var task = new Task(() => ExportSeasonPack(dialog.FileName));
                    task.Start();
                    await Task.WhenAll(task);
                    StatusTextBlock.Text = $"Exported to {Path.GetFileName(dialog.FileName)}";
                    MessageBox.Show("Season pack exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting season pack: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusTextBlock.Text = "Export failed";
                }
            }
        }

        private void ExportSeasonPack(string zipFilePath)
        {
            var rootDir = Path.Combine(Path.GetTempPath(), $"SeasonPack_{Guid.NewGuid()}");
            var tempDir = Path.Combine(rootDir, _currentProject.Season.Year.ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Normalize race dates to yyyy-MM-dd format before export
                NormalizeRaceDates();

                // Export season JSON
                var seasonJson = JsonSerializer.Serialize(_currentProject.Season, DefaultJsonSerializerOptions.Instance);
                File.WriteAllText(Path.Combine(tempDir, $"season.json"), seasonJson);

                // Export drivers JSON
                var driversDb = new DriverRatingsDatabase { Drivers = _currentProject.Drivers };
                var driversJson = JsonSerializer.Serialize(driversDb, DefaultJsonSerializerOptions.Instance);
                File.WriteAllText(Path.Combine(tempDir, "drivers.json"), driversJson);

                // Copy texture files
                foreach (var textureFile in _currentProject.TextureFiles)
                {
                    var sourcePath = textureFile.Value;

                    //handle those files that are referenced from the app directory instead of the season directory
                    var realRelativePath = textureFile.Key.StartsWith("Seasons/1996", StringComparison.OrdinalIgnoreCase) ?
                                             Path.GetRelativePath($"Seasons/{_currentProject.Season.Year}", textureFile.Key) :
                                             textureFile.Key;

                    var destPath = Path.Combine(tempDir, realRelativePath);

                    var destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    if (File.Exists(sourcePath))
                    {
                        var hasSameExtensions = Path.GetExtension(sourcePath) == Path.GetExtension(destPath);
                        var isSourceDDS = Path.GetExtension(sourcePath) == ".dds";
                        var isDestPNG = Path.GetExtension(destPath) == ".png";

                        if (hasSameExtensions)
                        {
                            File.Copy(sourcePath, destPath, true);
                        }
                        else if (isSourceDDS && isDestPNG)
                        {
                            ExportDDSasPNG(sourcePath, destPath);
                        }
                        else if (Path.GetExtension(sourcePath) == ".png" && Path.GetExtension(destPath) == ".dds")
                        {
                            DdsTextureComposer.Compose(sourcePath, null, destPath);
                        }
                        else
                        {
                            MessageBox.Show($"{sourcePath} and {destPath} have incompatible extensions.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            throw new Exception($"{sourcePath} and {destPath} have incompatible extensions.");
                        }


                    }
                }

                // Export livery XML files
                var liveriesXmlDir = Path.Combine(tempDir, "liveries_xml");
                Directory.CreateDirectory(liveriesXmlDir);

                foreach (var xmlFile in _currentProject.XmlFiles)
                {
                    if (!string.IsNullOrWhiteSpace(xmlFile.Value))
                    {
                        var xmlFilePath = Path.Combine(liveriesXmlDir, $"{xmlFile.Key}.xml");
                        File.WriteAllText(xmlFilePath, xmlFile.Value);
                    }
                }

                // Copy static assets files
                if (_currentProject.StaticAssetFiles != null && _currentProject.StaticAssetFiles.Any())
                {
                    var staticAssetsDir = Path.Combine(tempDir, "static_assets");
                    Directory.CreateDirectory(staticAssetsDir);

                    foreach (var assetFile in _currentProject.StaticAssetFiles)
                    {
                        if (File.Exists(assetFile.FullPath))
                        {
                            var destPath = Path.Combine(staticAssetsDir, assetFile.FilePath);
                            var destDir = Path.GetDirectoryName(destPath);

                            if (!Directory.Exists(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }

                            File.Copy(assetFile.FullPath, destPath, true);
                        }
                    }
                }

                // Export scenarios
                if (_currentProject.Scenarios != null && _currentProject.Scenarios.Any())
                {
                    var scenariosDir = Path.Combine(tempDir, "Scenarios");
                    var picturesDir = Path.Combine(scenariosDir, "Pictures");
                    var savesDir = Path.Combine(scenariosDir, "Saves");
                    var helmetsDir = Path.Combine(scenariosDir, "Helmets");
                    Directory.CreateDirectory(scenariosDir);
                    Directory.CreateDirectory(picturesDir);
                    Directory.CreateDirectory(savesDir);
                    Directory.CreateDirectory(helmetsDir);

                    var jsonOptions = DefaultJsonSerializerOptions.Instance;

                    foreach (var scenario in _currentProject.Scenarios)
                    {
                        var safeScenarioName = string.Concat(
                            (scenario.Name ?? "scenario").Split(Path.GetInvalidFileNameChars()))
                            .Replace(" ", "_");

                        // ── Picture ──
                        string picFileName = "";
                        if (!string.IsNullOrWhiteSpace(scenario.PictureFullPath) && File.Exists(scenario.PictureFullPath))
                        {
                            picFileName = Path.GetFileName(scenario.PictureFullPath);
                            File.Copy(scenario.PictureFullPath, Path.Combine(picturesDir, picFileName), true);
                        }

                        // ── Save file ──
                        string saveFileName = $"{safeScenarioName}.json";

                        if (scenario.SaveConfig != null)
                        {
                            // Validate before generating
                            var errors = ScenarioSaveBuilder.ValidateConfig(
                                scenario.SaveConfig, _currentProject.Season, _currentProject.Drivers);
                            if (errors.Any())
                            {
                                throw new InvalidOperationException(
                                    $"Scenario '{scenario.Name}' has errors:\n" + string.Join("\n", errors));
                            }

                            // Copy team-level fields from current season into config (non-destructive)
                            // This is handled inside ScenarioSaveBuilder.Build() → DeepCopySeason()

                            // Copy new player driver helmet/visor files if present
                            if (scenario.SaveConfig.NewPlayerDriver != null)
                            {
                                var nd = scenario.SaveConfig.NewPlayerDriver;
                                void CopyHelmet(string path)
                                {
                                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                                        File.Copy(path, Path.Combine(helmetsDir, Path.GetFileName(path)), true);
                                }
                                CopyHelmet(nd.BaseHelmetFileFullPath);
                                CopyHelmet(nd.BaseVisorFileFullPath);
                                CopyHelmet(nd.BaseHelmetFile90sFullPath);
                                CopyHelmet(nd.BaseHelmetFile80sFullPath);
                                CopyHelmet(nd.BaseVisorFile80sFullPath);
                                CopyHelmet(nd.BaseHelmetFile70sFullPath);
                                CopyHelmet(nd.BaseVisorFile70sFullPath);
                            }

                            // Generate save game
                            var saveGame = ScenarioSaveBuilder.Build(
                                _currentProject.Season,
                                _currentProject.Drivers,
                                scenario.SaveConfig);

                            var saveJson = JsonSerializer.Serialize(saveGame, jsonOptions);
                            File.WriteAllText(Path.Combine(savesDir, saveFileName), saveJson);
                        }
                        else if (!string.IsNullOrWhiteSpace(scenario.GameFileFullPath) && File.Exists(scenario.GameFileFullPath))
                        {
                            // Legacy: manually provided save file
                            saveFileName = Path.GetFileName(scenario.GameFileFullPath);
                            File.Copy(scenario.GameFileFullPath, Path.Combine(savesDir, saveFileName), true);
                        }
                        else
                        {
                            saveFileName = "";
                        }

                        // ── Scenario metadata JSON ──
                        var exportedScenario = new
                        {
                            name = scenario.Name,
                            description = scenario.Description,
                            picture = string.IsNullOrWhiteSpace(picFileName)
                                            ? ""
                                            : $"Seasons/{_currentProject.Season.Year}/Scenarios/Pictures/{picFileName}",
                            game_file = string.IsNullOrWhiteSpace(saveFileName)
                                            ? ""
                                            : $"Seasons/{_currentProject.Season.Year}/Scenarios/Saves/{saveFileName}"
                        };

                        var scenarioJson = JsonSerializer.Serialize(exportedScenario, jsonOptions);
                        File.WriteAllText(Path.Combine(scenariosDir, $"{safeScenarioName}.json"), scenarioJson);
                    }
                }

                // Create ZIP file
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }
                ZipFile.CreateFromDirectory(rootDir, zipFilePath);
            }
            finally
            {
                // Clean up temp directory
                if (Directory.Exists(rootDir))
                {
                    Directory.Delete(rootDir, true);
                }
            }
        }

        private void GenerateTestSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProject?.Season == null)
            {
                MessageBox.Show("No season loaded.", "Test Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build a flat list of all contracted drivers for the picker
            var contractedDrivers = _currentProject.Season.Teams
                .OfType<Ams2TeamEntry>()
                .SelectMany(t => new[]
                {
            t.Driver1Contract?.DriverId,
            t.Driver2Contract?.DriverId
                })
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .Select(id =>
                {
                    var driver = _currentProject.Drivers.FirstOrDefault(d => d.DriverId == id);
                    return new { DriverId = id, DisplayName = driver?.Name ?? id };
                })
                .OrderBy(d => d.DisplayName)
                .ToList();

            if (!contractedDrivers.Any())
            {
                MessageBox.Show("No contracted drivers found in the season.", "Test Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Simple driver picker dialog
            var picker = new Window
            {
                Title = "Select Player Driver",
                Width = 320,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1a1a"))
            };

            var stack = new StackPanel { Margin = new Thickness(16) };

            stack.Children.Add(new TextBlock
            {
                Text = "Select the driver to play as:",
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var listBox = new ListBox
            {
                Height = 280,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2a2a")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"))
            };

            foreach (var d in contractedDrivers)
                listBox.Items.Add(new ListBoxItem { Content = d.DisplayName, Tag = d.DriverId });

            listBox.SelectedIndex = 0;
            stack.Children.Add(listBox);

            var confirmBtn = new Button
            {
                Content = "CONFIRM",
                Margin = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(0, 8, 0, 8),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#c41e3a")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold
            };

            string selectedDriverId = null;
            confirmBtn.Click += (_, __) =>
            {
                if (listBox.SelectedItem is ListBoxItem item)
                {
                    selectedDriverId = item.Tag as string;
                    picker.DialogResult = true;
                }
            };

            stack.Children.Add(confirmBtn);
            picker.Content = stack;

            if (picker.ShowDialog() != true || string.IsNullOrEmpty(selectedDriverId))
                return;

            var saveDialog = new SaveFileDialog
            {
                Title = "Save Test Season File",
                Filter = "JSON files (*.json)|*.json",
                FileName = $"season_{_currentProject.Season.Year}_test.json"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                SeasonPackTestSaveService.GenerateTestSave(_currentProject, selectedDriverId, saveDialog.FileName);
                MessageBox.Show($"Test save written to:\n{saveDialog.FileName}", "Test Save", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate test save:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportDDSasPNG(string sourcePath, string destPath)
        {
            using (var fs = File.OpenRead(sourcePath))
            {
                var decoder = new BcDecoder();
                using (var image = decoder.DecodeToImageRgba32(fs))
                {
                    image.SaveAsPng(destPath);
                }
            }
        }

        private void NormalizeRaceDates()
        {
            // Format all race dates to yyyy-MM-dd
            foreach (var race in _currentProject.Season.Races)
            {
                if (!string.IsNullOrWhiteSpace(race.RaceDate))
                {
                    if (DateTime.TryParse(race.RaceDate, out DateTime dateTime))
                    {
                        race.RaceDate = dateTime.ToString("yyyy-MM-dd");
                    }
                }
            }
        }

        private void UpdateProjectFromUI()
        {
            // Update general info
            if (int.TryParse(YearTextBox.Text, out int year))
            {
                _currentProject.Season.Year = year;
            }

            if (_currentProject.Season is Ams2Season ams2Season)
            {
                ams2Season.Ams2Class = Ams2ClassTextBox.Text;
            }

            // Update points system from grid
            if (PointsSystemDataGrid.ItemsSource is IEnumerable<PointsSystemEntry> points)
            {
                _currentProject.Season.PointsSystem = points.ToDictionary(p => p.Position, p => p.Points);
            }
        }

        #endregion

        #region Helper Classes

        public class SeasonPackProject
        {
            public Ams2Season Season { get; set; }
            public List<Ams2DriverData> Drivers { get; set; }
            public Dictionary<string, string> TextureFiles { get; set; } // Relative path -> Absolute path
            public Dictionary<string, string> XmlFiles { get; set; } // Team ID -> XML content
            public List<StaticAssetFile> StaticAssetFiles { get; set; } // Files to copy to static_assets folder
            public List<ScenarioEntry> Scenarios { get; set; } = new List<ScenarioEntry>();
        }

        /// <summary>
        /// Editor-side wrapper that stores the full (absolute) paths of picture
        /// and game-save so we can copy them on export without re-asking the user.
        /// </summary>
        public class ScenarioEntry
        {
            public string Name { get; set; }
            public string Description { get; set; }

            /// <summary>Full absolute path to the picture chosen by the user.</summary>
            public string PictureFullPath { get; set; }

            /// <summary>
            /// Full absolute path to a pre-existing game-save JSON.
            /// Null/empty when the save is generated programmatically from SaveConfig.
            /// </summary>
            public string GameFileFullPath { get; set; }

            /// <summary>
            /// When non-null, the save file is generated at export time from this config.
            /// This takes priority over GameFileFullPath.
            /// </summary>
            public ScenarioSaveConfig SaveConfig { get; set; }

            [System.Text.Json.Serialization.JsonIgnore]
            public string PictureUrl => string.IsNullOrWhiteSpace(PictureFullPath)
                ? "" : System.IO.Path.GetFileName(PictureFullPath);

            [System.Text.Json.Serialization.JsonIgnore]
            public string GameFile => SaveConfig != null
                ? "(generated)"
                : (string.IsNullOrWhiteSpace(GameFileFullPath) ? "" : System.IO.Path.GetFileName(GameFileFullPath));

            [System.Text.Json.Serialization.JsonIgnore]
            public bool HasSaveConfig => SaveConfig != null;
        }

        public class PointsSystemEntry
        {
            public string Position { get; set; }
            public int Points { get; set; }
        }

        public class DriverDisplayModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            public Ams2DriverData Driver { get; set; }  // Reference to actual driver
            public int SeasonYear { get; set; }  // Track which season year these ratings are for

            public string DriverId { get; set; }
            public string Name { get; set; }
            public string Nationality { get; set; }
            public int YearOfBirth { get; set; }
            public string Reputation { get; set; }      // Extracted from current season

            // Individual rating values for current season
            private string _aggression;
            public string Aggression
            {
                get => _aggression;
                set
                {
                    _aggression = value;
                    UpdateDriverRating("aggression", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Aggression)));
                }
            }

            private string _avoidanceForcedMistakes;
            public string AvoidanceForcedMistakes
            {
                get => _avoidanceForcedMistakes;
                set
                {
                    _avoidanceForcedMistakes = value;
                    UpdateDriverRating("avoidance_of_forced_mistakes", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvoidanceForcedMistakes)));
                }
            }

            private string _avoidanceMistakes;
            public string AvoidanceMistakes
            {
                get => _avoidanceMistakes;
                set
                {
                    _avoidanceMistakes = value;
                    UpdateDriverRating("avoidance_of_mistakes", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvoidanceMistakes)));
                }
            }

            private string _blueFlagConceding;
            public string BlueFlagConceding
            {
                get => _blueFlagConceding;
                set
                {
                    _blueFlagConceding = value;
                    UpdateDriverRating("blue_flag_conceding", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BlueFlagConceding)));
                }
            }

            private string _consistency;
            public string Consistency
            {
                get => _consistency;
                set
                {
                    _consistency = value;
                    UpdateDriverRating("consistency", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Consistency)));
                }
            }

            private string _defending;
            public string Defending
            {
                get => _defending;
                set
                {
                    _defending = value;
                    UpdateDriverRating("defending", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Defending)));
                }
            }

            private string _fuelManagement;
            public string FuelManagement
            {
                get => _fuelManagement;
                set
                {
                    _fuelManagement = value;
                    UpdateDriverRating("fuel_management", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FuelManagement)));
                }
            }

            private string _qualifyingSkill;
            public string QualifyingSkill
            {
                get => _qualifyingSkill;
                set
                {
                    _qualifyingSkill = value;
                    UpdateDriverRating("qualifying_skill", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QualifyingSkill)));
                }
            }

            private string _raceSkill;
            public string RaceSkill
            {
                get => _raceSkill;
                set
                {
                    _raceSkill = value;
                    UpdateDriverRating("race_skill", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RaceSkill)));
                }
            }

            private string _stamina;
            public string Stamina
            {
                get => _stamina;
                set
                {
                    _stamina = value;
                    UpdateDriverRating("stamina", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stamina)));
                }
            }

            private string _startReactions;
            public string StartReactions
            {
                get => _startReactions;
                set
                {
                    _startReactions = value;
                    UpdateDriverRating("start_reactions", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartReactions)));
                }
            }

            private string _tyreManagement;
            public string TyreManagement
            {
                get => _tyreManagement;
                set
                {
                    _tyreManagement = value;
                    UpdateDriverRating("tyre_management", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TyreManagement)));
                }
            }

            private string _vehicleReliability;
            public string VehicleReliability
            {
                get => _vehicleReliability;
                set
                {
                    _vehicleReliability = value;
                    UpdateDriverRating("vehicle_reliability", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VehicleReliability)));
                }
            }

            private string _weatherTyreChanges;
            public string WeatherTyreChanges
            {
                get => _weatherTyreChanges;
                set
                {
                    _weatherTyreChanges = value;
                    UpdateDriverRating("weather_tyre_changes", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WeatherTyreChanges)));
                }
            }

            private string _wetSkill;
            public string WetSkill
            {
                get => _wetSkill;
                set
                {
                    _wetSkill = value;
                    UpdateDriverRating("wet_skill", value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WetSkill)));
                }
            }

            private void UpdateDriverRating(string ratingKey, string value)
            {
                if (Driver == null || string.IsNullOrWhiteSpace(value))
                    return;

                // Parse the value
                if (!double.TryParse(value, out double ratingValue))
                    return;

                // Clamp between 0.0 and 1.0
                ratingValue = Math.Max(0.0, Math.Min(1.0, ratingValue));

                ratingValue = Math.Round(ratingValue, 10);

                // Update the rating value
                if (Driver.RatingValues == null)
                    Driver.RatingValues = new Dictionary<string, double>();

                Driver.RatingValues[ratingKey] = ratingValue;
            }
        }

        public class StaticAssetFile
        {
            public string FilePath { get; set; }
            public string FullPath { get; set; }
            public long Size { get; set; }
            public string SizeFormatted { get; set; }
        }

        #endregion

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            int year = 0;
            if (!int.TryParse(YearTextBox.Text, out year)) return;

            var service = new JolpicaF1Service();
            var result = await service.ImportSeasonAsync(year);

            if (result != null)
            {
                var converter = new JolpicaDataConverter();
                var convertedResult = converter.ConvertToAppModels(result);
                _currentProject.Drivers = convertedResult.Drivers;
                _currentProject.Season.Races = convertedResult.Races;
                _currentProject.Season.Teams = convertedResult.Teams;
                _currentProject.Season.Year = convertedResult.Year;
                RefreshUI();
            }
        }
    }
}