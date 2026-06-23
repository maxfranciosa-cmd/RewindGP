using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class ScenarioCreatorWizard : Window
    {
        // ── Inputs ──────────────────────────────────────────────────────────
        private readonly Ams2Season _season;
        private readonly List<Ams2DriverData> _allDrivers;

        // ── Wizard state ─────────────────────────────────────────────────────
        private int _currentStep = 0;
        private readonly List<Grid> _stepPanels;
        private readonly List<Button> _stepButtons;

        // ── Roster VM ───────────────────────────────────────────────────────
        private ObservableCollection<RosterRowVm> _rosterRows = new();

        // ── Absences (cloned from season) ────────────────────────────────────
        private ObservableCollection<AbsenceRowVm> _absenceRows = new();

        // ── Results ──────────────────────────────────────────────────────────
        // List of all active driver IDs in roster order (used to build result grid rows)
        private List<ResultRowVm> _resultRows = new();
        // Race columns (from season calendar)
        private List<Race> _races = new();

        // ── Output ───────────────────────────────────────────────────────────
        /// <summary>Set after Finish is confirmed. Caller reads this.</summary>
        public ScenarioSaveConfig ResultConfig { get; private set; }
        public string ScenarioName { get; private set; }
        public string ScenarioDescription { get; private set; }
        public string PictureFullPath { get; private set; }

        // ──────────────────────────────────────────────────────────────────────

        public ScenarioCreatorWizard(Ams2Season season, List<Ams2DriverData> allDrivers,
                                     ScenarioSaveConfig existingConfig = null)
        {
            InitializeComponent();
            _season = season;
            _allDrivers = allDrivers;
            _races = season.Races?.OrderBy(r => r.RaceId).ToList() ?? new List<Race>();

            _stepPanels = new List<Grid> { Step1Panel, Step2Panel, Step3Panel, Step4Panel, Step5Panel };
            _stepButtons = new List<Button> { Step1Button, Step2Button, Step3Button, Step4Button, Step5Button };

            InitRoster(existingConfig);
            InitAbsences(existingConfig);
            InitResults(existingConfig);

            if (existingConfig != null)
            {
                ScenarioNameBox.Text = "";
                ScenarioDescriptionBox.Text = "";
            }

            // Stale-detection warnings
            if (existingConfig != null)
            {
                var warnings = ScenarioSaveBuilder.GetWarnings(existingConfig, season, allDrivers);
                if (warnings.Any())
                {
                    RosterWarningText.Text = "⚠ " + string.Join("\n⚠ ", warnings);
                    RosterWarningText.Visibility = Visibility.Visible;
                }
            }

            NavigateTo(0);
        }

        // ════════════════════════════════════════════════════════════════════
        // STEP 1 – ROSTER
        // ════════════════════════════════════════════════════════════════════

        private void InitRoster(ScenarioSaveConfig existing)
        {
            _rosterRows.Clear();

            // Build list of drivers available for assignment:
            // all drivers that have a rating for this year OR are already in a team slot
            var seasonYear = _season.Year.ToString();
            var driverPool = _allDrivers
                .Select(d => new DriverOption { DriverId = d.DriverId, Name = d.Name })
                .OrderBy(d => d.Name)
                .ToList();

            foreach (var team in _season.Teams)
            {
                // Slot 1
                var s1config = existing?.DriverSlots.FirstOrDefault(
                    s => s.TeamId == team.TeamId && s.Slot == 1);
                var c1 = team.Driver1Contract;

                _rosterRows.Add(new RosterRowVm
                {
                    TeamId = team.TeamId,
                    Slot = 1,
                    DriverId = s1config?.DriverId ?? c1?.DriverId,
                    CarNumber = s1config?.CarNumber ?? c1?.DriverNumber ?? 0,
                    Races = s1config?.Races ?? c1?.Races ?? 0,
                    AvailableDrivers = driverPool,
                    IsPlayer = existing != null && existing.PlayerDriverId == (s1config?.DriverId ?? c1?.DriverId)
                });

                // Slot 2
                var s2config = existing?.DriverSlots.FirstOrDefault(
                    s => s.TeamId == team.TeamId && s.Slot == 2);
                var c2 = team.Driver2Contract;
                if (c2 != null || s2config != null)
                {
                    _rosterRows.Add(new RosterRowVm
                    {
                        TeamId = team.TeamId,
                        Slot = 2,
                        DriverId = s2config?.DriverId ?? c2?.DriverId,
                        CarNumber = s2config?.CarNumber ?? c2?.DriverNumber ?? 0,
                        Races = s2config?.Races ?? c2?.Races ?? 0,
                        AvailableDrivers = driverPool,
                        IsPlayer = existing != null && existing.PlayerDriverId == (s2config?.DriverId ?? c2?.DriverId)
                    });
                }
            }

            RosterDataGrid.ItemsSource = _rosterRows;

            // Restore new-driver fields
            if (existing?.NewPlayerDriver != null)
            {
                NewDriverExpander.IsExpanded = true;
                NewDriverFirstName.Text = existing.NewPlayerDriver.FirstName;
                NewDriverLastName.Text = existing.NewPlayerDriver.LastName;
                NewDriverNationality.Text = existing.NewPlayerDriver.Nationality;
                BaseHelmetFileBox.Text = existing.NewPlayerDriver.BaseHelmetFileFullPath ?? "";
                BaseVisorFileBox.Text = existing.NewPlayerDriver.BaseVisorFileFullPath ?? "";
                BaseHelmetFile90sBox.Text = existing.NewPlayerDriver.BaseHelmetFile90sFullPath ?? "";
                BaseHelmetFile80sBox.Text = existing.NewPlayerDriver.BaseHelmetFile80sFullPath ?? "";
                BaseVisorFile80sBox.Text = existing.NewPlayerDriver.BaseVisorFile80sFullPath ?? "";
                BaseHelmetFile70sBox.Text = existing.NewPlayerDriver.BaseHelmetFile70sFullPath ?? "";
                BaseVisorFile70sBox.Text = existing.NewPlayerDriver.BaseVisorFile70sFullPath ?? "";
            }
        }

        private void RosterDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void RosterDriverCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Keep IsPlayer consistent when driver is changed for a row that was the player
        }

        private void PlayerRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Only one row can be player – enforce by unchecking all others
            if (sender is RadioButton rb && rb.DataContext is RosterRowVm vm)
            {
                foreach (var row in _rosterRows)
                    row.IsPlayer = row == vm;
            }
        }

        private void NewDriverExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            NewDriverFirstName.Text = "";
            NewDriverLastName.Text = "";
            NewDriverNationality.Text = "";
            BaseHelmetFileBox.Text = "";
            BaseVisorFileBox.Text = "";
            BaseHelmetFile90sBox.Text = "";
            BaseHelmetFile80sBox.Text = "";
            BaseVisorFile80sBox.Text = "";
            BaseHelmetFile70sBox.Text = "";
            BaseVisorFile70sBox.Text = "";
        }

        private void BrowseDdsFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var dlg = new OpenFileDialog { Filter = "DDS files (*.dds)|*.dds|All files (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;

            // Tag contains the x:Name of the target TextBox
            var targetName = btn.Tag?.ToString();
            var tb = targetName switch
            {
                "BaseHelmetFileBox" => BaseHelmetFileBox,
                "BaseVisorFileBox" => BaseVisorFileBox,
                "BaseHelmetFile90sBox" => BaseHelmetFile90sBox,
                "BaseHelmetFile80sBox" => BaseHelmetFile80sBox,
                "BaseVisorFile80sBox" => BaseVisorFile80sBox,
                "BaseHelmetFile70sBox" => BaseHelmetFile70sBox,
                "BaseVisorFile70sBox" => BaseVisorFile70sBox,
                _ => null
            };
            if (tb != null) tb.Text = dlg.FileName;
        }

        private bool ValidateRoster(out string error)
        {
            error = null;
            var playerRow = _rosterRows.FirstOrDefault(r => r.IsPlayer);
            bool usingNewDriver = NewDriverExpander.IsExpanded &&
                                  !string.IsNullOrWhiteSpace(NewDriverFirstName.Text);

            if (playerRow == null && !usingNewDriver)
            {
                error = "Please select a player driver (use the Player radio button or create a new driver).";
                return false;
            }

            if (usingNewDriver)
            {
                if (string.IsNullOrWhiteSpace(NewDriverFirstName.Text) || string.IsNullOrWhiteSpace(NewDriverLastName.Text))
                { error = "New driver requires first and last name."; return false; }
                if (string.IsNullOrWhiteSpace(BaseHelmetFileBox.Text))
                { error = "New player driver requires at least a default helmet file."; return false; }
                if (string.IsNullOrWhiteSpace(BaseVisorFileBox.Text))
                { error = "New player driver requires at least a default visor file."; return false; }
            }
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        // STEP 2 – ABSENCES
        // ════════════════════════════════════════════════════════════════════

        private void InitAbsences(ScenarioSaveConfig existing)
        {
            _absenceRows.Clear();

            // Pre-load from config if editing, otherwise from original season
            var source = existing?.Absences ?? _season.Absences?.ToList() ?? new List<Absence>();
            foreach (var a in source)
                _absenceRows.Add(new AbsenceRowVm(a));

            AbsencesDataGrid.ItemsSource = _absenceRows;
        }

        private void AddAbsence_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AbsenceEditorDialog(_season.Races, _season.Teams, _allDrivers);
            if (dlg.ShowDialog() == true)
            {
                foreach (var absence in dlg.Absences)
                    _absenceRows.Add(new AbsenceRowVm(absence));
                RefreshAbsences();
            }
        }

        private void EditAbsence_Click(object sender, RoutedEventArgs e)
        {
            if (AbsencesDataGrid.SelectedItem is AbsenceRowVm selected)
            {
                var dlg = new AbsenceEditorDialog(_season.Races, _season.Teams, _allDrivers, selected.Source);
                if (dlg.ShowDialog() == true)
                {
                    // Edit mode always returns exactly one absence (the mutated original)
                    var idx = _absenceRows.IndexOf(selected);
                    _absenceRows[idx] = new AbsenceRowVm(dlg.Absences[0]);
                    RefreshAbsences();
                }
            }
            else MessageBox.Show("Please select an absence first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveAbsence_Click(object sender, RoutedEventArgs e)
        {
            if (AbsencesDataGrid.SelectedItem is AbsenceRowVm selected)
            {
                _absenceRows.Remove(selected);
                RefreshAbsences();
            }
            else MessageBox.Show("Please select an absence first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshAbsences()
        {
            AbsencesDataGrid.ItemsSource = null;
            AbsencesDataGrid.ItemsSource = _absenceRows;
        }

        // ════════════════════════════════════════════════════════════════════
        // STEP 3 – RESULTS
        // ════════════════════════════════════════════════════════════════════

        private void InitResults(ScenarioSaveConfig existing)
        {
            // Store existing predefined results for later use when RebuildResultRows runs.
            // We don't build rows here because _rosterRows may not be fully settled yet
            // (new player driver from expander isn't known until navigation).
            _existingResultsConfig = existing;
        }

        // Stored so RebuildResultRows can pre-populate values from a reopened config
        private ScenarioSaveConfig _existingResultsConfig;

        private string FormatResultValue(ScenarioDriverResult r)
        {
            if (r == null) return "";
            if (r.DidNotPreQualify) return "DNQ";
            if (r.DNF) return "DNF";
            if (r.Position > 0) return r.Position.ToString();
            return "";
        }

        private void BuildResultsGrid()
        {
            ResultsDataGrid.Columns.Clear();

            // Driver name column
            var driverCol = new DataGridTextColumn
            {
                Header = "Driver",
                Binding = new System.Windows.Data.Binding("DisplayName"),
                Width = new DataGridLength(160),
                IsReadOnly = true
            };
            ResultsDataGrid.Columns.Add(driverCol);

            // One pair of columns per race
            foreach (var race in _races)
            {
                var raceId = race.RaceId;
                var shortName = race.RaceShortName ?? $"R{raceId}";

                // Position column
                var posCol = new DataGridTemplateColumn
                {
                    Header = shortName,
                    Width = new DataGridLength(52)
                };
                var posFactory = new FrameworkElementFactory(typeof(TextBox));
                var posBinding = new System.Windows.Data.Binding($"Cells[{raceId}].Value")
                {
                    Mode = System.Windows.Data.BindingMode.TwoWay,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus
                };
                posFactory.SetBinding(TextBox.TextProperty, posBinding);
                posFactory.SetValue(TextBox.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)));
                posFactory.SetValue(TextBox.ForegroundProperty, Brushes.White);
                posFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
                posFactory.SetValue(TextBox.FontSizeProperty, 11.0);
                posFactory.SetValue(TextBox.TextAlignmentProperty, TextAlignment.Center);
                posFactory.AddHandler(TextBox.LostFocusEvent, new RoutedEventHandler((s, e) => ResultCell_LostFocus(s, e, raceId)));
                posCol.CellTemplate = new DataTemplate { VisualTree = posFactory };
                ResultsDataGrid.Columns.Add(posCol);

                // BL column
                var blCol = new DataGridTemplateColumn
                {
                    Header = "BL",
                    Width = new DataGridLength(30)
                };
                var blFactory = new FrameworkElementFactory(typeof(CheckBox));
                var blBinding = new System.Windows.Data.Binding($"Cells[{raceId}].IsBL")
                {
                    Mode = System.Windows.Data.BindingMode.TwoWay,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
                };
                blFactory.SetBinding(CheckBox.IsCheckedProperty, blBinding);
                blFactory.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                blFactory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
                blFactory.AddHandler(CheckBox.CheckedEvent, new RoutedEventHandler((s, e) => BLChecked(s, e, raceId)));
                blFactory.AddHandler(CheckBox.UncheckedEvent, new RoutedEventHandler((s, e) => { }));
                blCol.CellTemplate = new DataTemplate { VisualTree = blFactory };
                ResultsDataGrid.Columns.Add(blCol);
            }

            ResultsDataGrid.ItemsSource = _resultRows;
        }

        private void ResultCell_LostFocus(object sender, RoutedEventArgs e, int raceId)
        {
            if (sender is TextBox tb && tb.DataContext is ResultRowVm row)
            {
                var val = tb.Text.Trim().ToUpper();
                var cell = row.Cells.TryGetValue(raceId, out var c) ? c : null;
                if (cell == null) return;

                // Validate
                if (!string.IsNullOrWhiteSpace(val) && val != "DNF" && val != "DNQ")
                {
                    if (!int.TryParse(val, out _))
                    {
                        tb.Background = new SolidColorBrush(Color.FromRgb(0x5A, 0x10, 0x10));
                        return;
                    }
                }

                // Disable BL for DNQ
                if (val == "DNQ") cell.IsBL = false;

                tb.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
            }
        }

        private void BLChecked(object sender, RoutedEventArgs e, int raceId)
        {
            if (sender is CheckBox cb && cb.IsChecked == true && cb.DataContext is ResultRowVm checkedRow)
            {
                // Radio semantics: uncheck BL for all other drivers in this race
                foreach (var row in _resultRows)
                {
                    if (row != checkedRow && row.Cells.TryGetValue(raceId, out var cell))
                        cell.IsBL = false;
                }
            }
        }

        private void ResultsDataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e) { }
        private void ResultsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) { }

        // ════════════════════════════════════════════════════════════════════
        // STEP 4 – STANDINGS (calculated when leaving step 3)
        // ════════════════════════════════════════════════════════════════════

        private void RecalculateStandings()
        {
            try
            {
                var config = BuildConfigFromWizard();
                var saveGame = ScenarioSaveBuilder.Build(_season, _allDrivers, config);
                var manager = new StandingsManager();

                var driverDisplay = manager.GetDriverStandingsDisplay(saveGame);
                DriverStandingsGrid.ItemsSource = driverDisplay;

                var teamNames = _season.Teams.ToDictionary(t => t.TeamId, t => t.TeamName ?? t.TeamId);
                var constructorDisplay = manager.GetConstructorStandingsDisplay(saveGame, teamNames);
                ConstructorStandingsGrid.ItemsSource = constructorDisplay;
            }
            catch (Exception ex)
            {
                DriverStandingsGrid.ItemsSource = null;
                ConstructorStandingsGrid.ItemsSource = null;
                MessageBox.Show($"Could not calculate standings:\n{ex.Message}", "Standings Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // STEP 5 – DETAILS
        // ════════════════════════════════════════════════════════════════════

        private void BrowsePicture_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Scenario Picture",
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                PicturePathBox.Text = dlg.FileName;
                UpdatePathPreviews();
            }
        }

        private void UpdatePathPreviews()
        {
            var year = _season.Year;
            var safeName = MakeSafeName(ScenarioNameBox.Text);
            var picFile = string.IsNullOrWhiteSpace(PicturePathBox.Text)
                              ? "(no picture)"
                              : System.IO.Path.GetFileName(PicturePathBox.Text);

            SavePathPreview.Text = $"Save:     Seasons/{year}/Scenarios/Saves/{safeName}.json";
            ScenarioPathPreview.Text = $"Scenario: Seasons/{year}/Scenarios/{safeName}.json";
            PicturePathPreview.Text = $"Picture:  Seasons/{year}/Scenarios/Pictures/{picFile}";
        }

        // ════════════════════════════════════════════════════════════════════
        // NAVIGATION
        // ════════════════════════════════════════════════════════════════════

        private void StepButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int target))
                TryNavigateTo(target);
        }

        private void Prev_Click(object sender, RoutedEventArgs e) => TryNavigateTo(_currentStep - 1);

        private void Next_Click(object sender, RoutedEventArgs e) => TryNavigateTo(_currentStep + 1);

        private void TryNavigateTo(int target)
        {
            // Validate current step before leaving
            if (_currentStep == 0)
            {
                if (!ValidateRoster(out var err)) { MessageBox.Show(err, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            }

            // Leaving step 3 → recalculate standings
            if (_currentStep == 2 && target != 2)
                RecalculateStandings();

            // Always rebuild result rows when entering step 3 from any direction
            if (target == 2)
                RebuildResultRows();

            NavigateTo(target);
        }

        private void NavigateTo(int step)
        {
            _currentStep = step;
            for (int i = 0; i < _stepPanels.Count; i++)
                _stepPanels[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;

            for (int i = 0; i < _stepButtons.Count; i++)
                _stepButtons[i].Style = i == step
                    ? (Style)FindResource("StepButtonActive")
                    : (Style)FindResource("StepButton");

            PrevButton.IsEnabled = step > 0;
            NextButton.Visibility = step < 4 ? Visibility.Visible : Visibility.Collapsed;
            FinishButton.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

            if (step == 4) UpdatePathPreviews();
        }

        private void RebuildResultRows()
        {
            // Preserve existing entered values
            var old = _resultRows.ToDictionary(r => r.DriverId);

            // ── Base roster slots ──
            var activeSlots = _rosterRows
                .Where(r => !string.IsNullOrWhiteSpace(r.DriverId))
                .Select(r => (DriverId: r.DriverId, TeamId: r.TeamId, DisplayName: GetDriverName(r.DriverId)))
                .ToList();

            // ── New player driver ──
            bool usingNewDriver = NewDriverExpander.IsExpanded &&
                                  !string.IsNullOrWhiteSpace(NewDriverFirstName.Text) &&
                                  !string.IsNullOrWhiteSpace(NewDriverLastName.Text);
            if (usingNewDriver)
            {
                var newId = $"player_{NewDriverFirstName.Text.Trim()}_{NewDriverLastName.Text.Trim()}";
                var newName = $"{NewDriverFirstName.Text.Trim()} {NewDriverLastName.Text.Trim()}";
                var playerRow = _rosterRows.FirstOrDefault(r => r.IsPlayer);
                var teamId = playerRow?.TeamId ?? "";

                if (playerRow != null)
                {
                    var idx = activeSlots.FindIndex(s => s.DriverId == playerRow.DriverId);
                    if (idx >= 0) activeSlots[idx] = (newId, teamId, newName);
                    else activeSlots.Add((newId, teamId, newName));
                }
                else activeSlots.Add((newId, teamId, newName));
            }

            // ── Absence lookups ──
            // driverId → raceIds where they are OUT
            var absentForRaces = _absenceRows
                .Where(a => !string.IsNullOrWhiteSpace(a.Source.DriverOut))
                .GroupBy(a => a.Source.DriverOut, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(a => a.Source.RaceId).ToHashSet(),
                              StringComparer.OrdinalIgnoreCase);

            // driverId → list of (RaceId, TeamId) where they substitute IN
            var substitutesIn = _absenceRows
                .Where(a => !string.IsNullOrWhiteSpace(a.Source.DriverIn))
                .GroupBy(a => a.Source.DriverIn, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key,
                              g => g.Select(a => (RaceId: a.Source.RaceId, TeamId: a.Source.TeamId ?? "")).ToList(),
                              StringComparer.OrdinalIgnoreCase);

            // ── Add pure-substitute drivers (not already in roster) ──
            var rosterIds = activeSlots.Select(s => s.DriverId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in substitutesIn)
            {
                if (!rosterIds.Contains(kvp.Key))
                {
                    var teamId = kvp.Value.First().TeamId;
                    activeSlots.Add((kvp.Key, teamId, $"{GetDriverName(kvp.Key)} (sub)"));
                }
            }

            // ── Build rows ──
            _resultRows = activeSlots
                .Select(slot =>
                {
                    old.TryGetValue(slot.DriverId, out var existing);
                    var vm = existing ?? new ResultRowVm { DriverId = slot.DriverId, TeamId = slot.TeamId, DisplayName = slot.DisplayName };
                    vm.TeamId = slot.TeamId;
                    vm.DisplayName = slot.DisplayName;

                    bool isPureSubstitute = !rosterIds.Contains(slot.DriverId);

                    foreach (var race in _races)
                    {
                        // Resolve the correct team for this driver in this specific race:
                        // - For regular drivers: their normal team, UNLESS they are absent and
                        //   someone is covering for them (team stays the same — the absent driver's team)
                        // - For substitutes: the team from the absence record for this race
                        string cellTeamId = slot.TeamId;
                        if (substitutesIn.TryGetValue(slot.DriverId, out var subRaces))
                        {
                            var match = subRaces.FirstOrDefault(s => s.RaceId == race.RaceId);
                            if (match.TeamId != null)
                                cellTeamId = match.TeamId;
                        }

                        if (!vm.Cells.ContainsKey(race.RaceId))
                        {
                            var predefined = _existingResultsConfig?.PredefinedResults.FirstOrDefault(pr => pr.RaceId == race.RaceId);
                            var driverResult = predefined?.Results.FirstOrDefault(dr => dr.DriverId == slot.DriverId);
                            vm.Cells[race.RaceId] = new ResultCell
                            {
                                RaceId = race.RaceId,
                                DriverId = slot.DriverId,
                                TeamId = cellTeamId,
                                Value = FormatResultValue(driverResult),
                                IsBL = driverResult?.FastestLap ?? false
                            };
                        }
                        else
                        {
                            // Update TeamId in case absences changed since last build
                            vm.Cells[race.RaceId].TeamId = cellTeamId;
                        }

                        // Regular driver absent this race
                        bool isAbsent = absentForRaces.TryGetValue(slot.DriverId, out var absentSet)
                                        && absentSet.Contains(race.RaceId);

                        // Substitute driver but NOT covering this specific race
                        bool isSubNotCovering = isPureSubstitute
                                                && (!substitutesIn.TryGetValue(slot.DriverId, out var subRaces2)
                                                    || !subRaces2.Any(s => s.RaceId == race.RaceId));

                        vm.Cells[race.RaceId].Disabled = isAbsent || isSubNotCovering;
                        if (isAbsent || isSubNotCovering)
                        {
                            vm.Cells[race.RaceId].Value = "";
                            vm.Cells[race.RaceId].IsBL = false;
                        }
                    }
                    return vm;
                }).ToList();

            BuildResultsGrid();
        }

        private string GetDriverName(string driverId)
        {
            var driver = _allDrivers.FirstOrDefault(d => d.DriverId == driverId);
            return driver?.Name ?? driverId;
        }

        // ════════════════════════════════════════════════════════════════════
        // FINISH
        // ════════════════════════════════════════════════════════════════════

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ScenarioNameBox.Text))
            {
                MessageBox.Show("Please enter a scenario name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var config = BuildConfigFromWizard();

            // Export-time validation
            var errors = ScenarioSaveBuilder.ValidateConfig(config, _season, _allDrivers);
            if (errors.Any())
            {
                MessageBox.Show("Cannot finish – please fix the following:\n\n" + string.Join("\n", errors),
                    "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultConfig = config;
            ScenarioName = ScenarioNameBox.Text.Trim();
            ScenarioDescription = ScenarioDescriptionBox.Text.Trim();
            PictureFullPath = PicturePathBox.Text.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Discard scenario changes?", "Cancel", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                DialogResult = false;
        }

        // ════════════════════════════════════════════════════════════════════
        // BUILD CONFIG FROM WIZARD STATE
        // ════════════════════════════════════════════════════════════════════

        private ScenarioSaveConfig BuildConfigFromWizard()
        {
            // ── Driver slots ──
            var slots = _rosterRows
                .Where(r => !string.IsNullOrWhiteSpace(r.DriverId))
                .Select(r => new ScenarioDriverSlot
                {
                    TeamId = r.TeamId,
                    Slot = r.Slot,
                    DriverId = r.DriverId,
                    CarNumber = r.CarNumber,
                    Races = r.Races
                }).ToList();

            // ── Player ──
            var playerRow = _rosterRows.FirstOrDefault(r => r.IsPlayer);
            bool newDriver = NewDriverExpander.IsExpanded && !string.IsNullOrWhiteSpace(NewDriverFirstName.Text);
            ScenarioNewDriver newPlayerDriver = null;
            string playerDriverId;

            if (newDriver)
            {
                newPlayerDriver = new ScenarioNewDriver
                {
                    FirstName = NewDriverFirstName.Text.Trim(),
                    LastName = NewDriverLastName.Text.Trim(),
                    Nationality = NewDriverNationality.Text.Trim(),
                    BaseHelmetFileFullPath = BaseHelmetFileBox.Text,
                    BaseVisorFileFullPath = BaseVisorFileBox.Text,
                    BaseHelmetFile90sFullPath = BaseHelmetFile90sBox.Text,
                    BaseHelmetFile80sFullPath = BaseHelmetFile80sBox.Text,
                    BaseVisorFile80sFullPath = BaseVisorFile80sBox.Text,
                    BaseHelmetFile70sFullPath = BaseHelmetFile70sBox.Text,
                    BaseVisorFile70sFullPath = BaseVisorFile70sBox.Text,
                };
                playerDriverId = newPlayerDriver.DriverId;

                // Add new driver as slot for their team
                if (playerRow != null)
                {
                    var existing = slots.FirstOrDefault(s => s.TeamId == playerRow.TeamId && s.Slot == playerRow.Slot);
                    if (existing != null) existing.DriverId = playerDriverId;
                }
            }
            else
            {
                playerDriverId = playerRow?.DriverId ?? "";
            }

            // ── Absences ──
            var absences = _absenceRows.Select(a => a.Source).ToList();

            // ── Predefined results ──
            var predefined = new List<ScenarioRaceResult>();
            foreach (var race in _races)
            {
                var raceResults = _resultRows
                    .Where(row => row.Cells.TryGetValue(race.RaceId, out var cell) && !string.IsNullOrWhiteSpace(cell.Value))
                    .Select(row =>
                    {
                        var cell = row.Cells[race.RaceId];
                        var upper = cell.Value.Trim().ToUpper();
                        return new ScenarioDriverResult
                        {
                            DriverId = row.DriverId,
                            TeamId = cell.TeamId,   // per-race team from the cell
                            DNF = upper == "DNF",
                            DidNotPreQualify = upper == "DNQ",
                            Position = int.TryParse(upper, out int pos) ? pos : 0,
                            FastestLap = cell.IsBL
                        };
                    }).ToList();

                if (raceResults.Any())
                {
                    var enteredIds = raceResults.Select(r => r.DriverId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var absentOut = _absenceRows.Where(a => a.Source.RaceId == race.RaceId)
                                                    .Select(a => a.Source.DriverOut)
                                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var substitutingIn = _absenceRows.Where(a => a.Source.RaceId == race.RaceId && !string.IsNullOrWhiteSpace(a.Source.DriverIn))
                                                    .Select(a => a.Source.DriverIn)
                                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var rosterIds = _rosterRows.Select(r => r.DriverId).ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var row in _resultRows.Where(r => !enteredIds.Contains(r.DriverId)))
                    {
                        if (absentOut.Contains(row.DriverId)) continue;

                        bool isPureSub = !rosterIds.Contains(row.DriverId);
                        if (isPureSub && !substitutingIn.Contains(row.DriverId)) continue;

                        // Use per-race TeamId from the cell
                        var cellTeamId = row.Cells.TryGetValue(race.RaceId, out var cell)
                            ? cell.TeamId
                            : row.TeamId;

                        raceResults.Add(new ScenarioDriverResult
                        {
                            DriverId = row.DriverId,
                            TeamId = cellTeamId,
                            DNF = true,
                            Position = 0
                        });
                    }

                    predefined.Add(new ScenarioRaceResult { RaceId = race.RaceId, Results = raceResults });
                }
            }

            return new ScenarioSaveConfig
            {
                SeasonSyncedAt = DateTime.UtcNow.ToString("O"),
                DriverSlots = slots,
                PlayerDriverId = playerDriverId,
                NewPlayerDriver = newPlayerDriver,
                Absences = absences,
                PredefinedResults = predefined
            };
        }

        private static string MakeSafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "scenario";
            return string.Concat(name.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(" ", "_");
        }

        // ════════════════════════════════════════════════════════════════════
        // VIEW MODELS
        // ════════════════════════════════════════════════════════════════════

        public class DriverOption
        {
            public string DriverId { get; set; }
            public string Name { get; set; }
        }

        public class RosterRowVm : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            public string TeamId { get; set; }
            public int Slot { get; set; }
            public string SlotLabel => Slot == 1 ? "D1" : "D2";

            private string _driverId;
            public string DriverId
            {
                get => _driverId;
                set { _driverId = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DriverId))); }
            }

            public int CarNumber { get; set; }
            public int Races { get; set; }

            private bool _isPlayer;
            public bool IsPlayer
            {
                get => _isPlayer;
                set { _isPlayer = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlayer))); }
            }

            public List<DriverOption> AvailableDrivers { get; set; }
        }

        public class AbsenceRowVm
        {
            public Absence Source { get; }
            public int RaceId => Source.RaceId;
            public string TeamId => Source.TeamId;
            public string DriverOut => Source.DriverOut;
            public string DriverIn => Source.DriverIn ?? "-";
            public string HasChain => Source.ChainedAbsence != null ? "Yes" : "No";

            public AbsenceRowVm(Absence a) { Source = a; }
        }

        public class ResultRowVm : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public string DriverId { get; set; }
            public string TeamId { get; set; }
            public string DisplayName { get; set; }
            public Dictionary<int, ResultCell> Cells { get; } = new();
        }

        public class ResultCell : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            public int RaceId { get; set; }
            public string DriverId { get; set; }
            /// <summary>
            /// Team for this specific race. For regular drivers this matches the row's TeamId.
            /// For substitutes it reflects the team they're covering for in this race.
            /// </summary>
            public string TeamId { get; set; }
            public bool Disabled { get; set; }

            private string _value = "";
            public string Value
            {
                get => _value;
                set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
            }

            private bool _isBL;
            public bool IsBL
            {
                get => _isBL;
                set { _isBL = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBL))); }
            }
        }
    }
}