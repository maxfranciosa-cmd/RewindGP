using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Services;
using Ams2ChEd.Business.AMS2.Settings;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Services;
using AMS2ChEd.SeasonPackEditor.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using static AMS2ChEd.SeasonPackEditor.MainWindow;

namespace AMS2ChEd.SeasonPackEditor
{
    /// <summary>
    /// Drives the in-sim calibration loop: fetch the real-world target score per team, export one
    /// neutral-ratings AI car per team directly into the configured AMS2 install (step 2), capture a
    /// live AMS2 session's lap times for those exported cars, and suggest corrected power/weight
    /// scalars instead of hand-guessing new numbers after every test.
    /// </summary>
    public partial class PerformanceCalibrationDialog : Window
    {
        private class ResultRow
        {
            public string TeamId { get; set; }
            public string TeamName { get; set; }
            public double TargetScore { get; set; }
            public double? ActualScore { get; set; }
            public Dictionary<string, double> CurrentMalus { get; set; }
            public Dictionary<string, double> NewMalus { get; set; }

            public string TargetScoreText => TargetScore.ToString("0.00");
            public string ActualScoreText => ActualScore.HasValue ? ActualScore.Value.ToString("0.00") : "-";
            public string ErrorText => ActualScore.HasValue ? (TargetScore - ActualScore.Value).ToString("+0.00;-0.00") : "-";
            public string CurrentPowerText => FormatMalus(CurrentMalus, "power_scalar");
            public string CurrentWeightText => FormatMalus(CurrentMalus, "weight_scalar");
            public string NewPowerText => FormatMalus(NewMalus, "power_scalar");
            public string NewWeightText => FormatMalus(NewMalus, "weight_scalar");

            private static string FormatMalus(Dictionary<string, double> malus, string key) =>
                malus != null && malus.TryGetValue(key, out var value) ? value.ToString("0.000") : "-";
        }

        private readonly SeasonPackProject _project;
        private readonly List<Ams2TeamEntry> _teams;
        private readonly ObservableCollection<ResultRow> _rows = new();
        private readonly IAms2AppSettingsStorage _ams2AppSettingsStorage;
        private readonly Ams2StorageFactory _storageFactory;
        private Ams2RaceDataService _raceDataService;
        private List<PerformanceCalibrationService.CalibrationEntry> _calibrationEntries;
        private int _iteration;

        public PerformanceCalibrationDialog(
            SeasonPackProject project,
            IAms2AppSettingsStorage ams2AppSettingsStorage,
            Ams2StorageFactory storageFactory)
        {
            InitializeComponent();
            _project = project;
            _ams2AppSettingsStorage = ams2AppSettingsStorage;
            _storageFactory = storageFactory;
            _teams = project.Season.Teams.OfType<Ams2TeamEntry>().ToList();
            ResultsDataGrid.ItemsSource = _rows;

            foreach (var team in _teams)
            {
                _rows.Add(new ResultRow
                {
                    TeamId = team.TeamId,
                    TeamName = team.TeamName,
                    CurrentMalus = team.Ams2CarPerformanceMalus ?? new Dictionary<string, double>()
                });
            }

            Ams2FolderTextBox.Text = _ams2AppSettingsStorage.LoadSettings().Ams2Folder;
        }

        private async void FetchTargetScores_Click(object sender, RoutedEventArgs e)
        {
            if (_project.Season.Year <= 0)
            {
                MessageBox.Show("Please set a valid year before fetching target scores.",
                    "Year Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusTextBlock.Text = "Fetching actual championship results...";
            try
            {
                var jolpica = new JolpicaF1Service();
                var constructorStandings = await jolpica.GetConstructorStandingsAsync(_project.Season.Year);
                var targetScores = TeamTargetScoreService.ComputeTargetScores(_teams, constructorStandings);

                int matched = 0;
                foreach (var row in _rows)
                {
                    if (targetScores.TryGetValue(row.TeamId, out var target) && target.Matched)
                    {
                        row.TargetScore = target.Score;
                        matched++;
                    }
                }

                ResultsDataGrid.Items.Refresh();
                StatusTextBlock.Text = $"Fetched target scores for {matched} of {_rows.Count} teams.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch championship results: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Ready.";
            }
        }

        private void ExportCalibrationCustomAi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _calibrationEntries = PerformanceCalibrationService.BuildCalibrationEntries(_teams, _project.Drivers);
                if (_calibrationEntries.Count == 0)
                {
                    MessageBox.Show("No team has a contracted seat-1 driver to calibrate with.", "Nothing To Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                PerformanceCalibrationService.GenerateCalibrationCustomAi(_project, _calibrationEntries, Ams2FolderTextBox.Text);

                PersistAms2Folder();

                var teamNames = string.Join(", ", _calibrationEntries.Select(c => c.TeamName));
                StatusTextBlock.Text = $"Exported calibration AI/liveries for {_calibrationEntries.Count} teams ({teamNames}). " +
                    "In AMS2, drive any car whose name doesn't end in '(Calibration)', then run Start Listening below.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export calibration CustomAI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseAms2Folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                InitialDirectory = Directory.Exists(Ams2FolderTextBox.Text) ? Ams2FolderTextBox.Text : null
            };

            if (dialog.ShowDialog() == true)
            {
                Ams2FolderTextBox.Text = dialog.FolderName;
                PersistAms2Folder();
            }
        }

        private void Ams2FolderTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            PersistAms2Folder();
        }

        private void PersistAms2Folder()
        {
            _ams2AppSettingsStorage.SaveSettings(new Ams2AppSettings
            {
                Ams2Folder = Ams2FolderTextBox.Text,
                Ams2InGameName = string.Empty
            });
        }

        private void ListenToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_raceDataService != null && _raceDataService.IsRunning)
            {
                StopListening();
                return;
            }

            if (_calibrationEntries == null || _calibrationEntries.Count == 0)
            {
                MessageBox.Show("Export calibration CustomAI first (step 2) before listening for a session.", "Nothing To Listen For", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _raceDataService = new Ams2RaceDataService(_storageFactory);
                _raceDataService.InitializeRaceWeekend(PerformanceCalibrationService.BuildParticipantRoster(_calibrationEntries));
                _raceDataService.SessionFinished += OnSessionFinished;
                _raceDataService.Start();

                ListenToggleButton.Content = "Stop Listening";
                StatusTextBlock.Text = "Listening for the AMS2 session to finish (make sure a Practice/Qualifying session with the " +
                    "exported cars is running)...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to AMS2 shared memory: {ex.Message}\n\nMake sure AMS2 is running.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopListening()
        {
            if (_raceDataService == null)
                return;

            _raceDataService.SessionFinished -= OnSessionFinished;
            _raceDataService.Stop();
            _raceDataService = null;
            ListenToggleButton.Content = "3. Start Listening For Session";
            StatusTextBlock.Text = "Stopped listening.";
        }

        private void OnSessionFinished(object sender, SessionFinishedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var actualScores = PerformanceCalibrationService.ComputeActualScores(
                    e.FinalStandings, _calibrationEntries.Select(c => c.TeamId));

                foreach (var row in _rows)
                {
                    if (actualScores.TryGetValue(row.TeamId, out var actual))
                    {
                        row.ActualScore = actual;
                        row.NewMalus = PerformanceCalibrationService.CorrectScalars(row.CurrentMalus, row.TargetScore, actual);
                    }
                }

                ResultsDataGrid.Items.Refresh();
                StatusTextBlock.Text = $"Session captured ({e.CompletedSession}). Review the suggested corrections below, then Apply.";
            });
        }

        private void ApplyCorrections_Click(object sender, RoutedEventArgs e)
        {
            int applied = 0;
            foreach (var row in _rows)
            {
                if (row.NewMalus == null)
                    continue;

                var team = _teams.First(t => t.TeamId == row.TeamId);
                team.Ams2CarPerformanceMalus = row.NewMalus;
                row.CurrentMalus = row.NewMalus;
                row.NewMalus = null;
                row.ActualScore = null;
                applied++;
            }

            if (applied == 0)
            {
                MessageBox.Show("No corrections to apply yet - capture a session first.", "Nothing To Apply", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _iteration++;
            ResultsDataGrid.Items.Refresh();
            IterationHistoryTextBlock.Text += $"Iteration {_iteration}: applied corrections to {applied} teams.\n";
            StatusTextBlock.Text = "Corrections applied. Export calibration CustomAI again to prepare the next session.";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            StopListening();
            DialogResult = true;
            Close();
        }
    }
}
