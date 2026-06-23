using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class AbsenceEditorDialog : Window
    {
        public List<Absence> Absences { get; private set; }
        private readonly bool _isEditMode;
        private readonly List<Race> _races;

        public AbsenceEditorDialog(IEnumerable<Race> races, IEnumerable<ITeamEntry> teams, List<Ams2DriverData> drivers, Absence absence = null)
        {
            InitializeComponent();

            _races = races.ToList();
            _isEditMode = absence != null;

            // Setup team and driver dropdowns
            TeamComboBox.ItemsSource = teams.Select(t => t.TeamId).ToList();
            DriverOutComboBox.ItemsSource = drivers.Select(d => d.DriverId).ToList();
            DriverInComboBox.ItemsSource = drivers.Select(d => d.DriverId).ToList();

            if (_isEditMode)
            {
                // Edit mode: use single ComboBox for race selection
                RaceComboBox.ItemsSource = _races.Select(r => new { r.RaceId, Display = $"{r.RaceId} - {r.RaceName}" });
                RaceComboBox.DisplayMemberPath = "Display";
                RaceComboBox.SelectedValuePath = "RaceId";
                RaceComboBox.Visibility = Visibility.Visible;
                RaceCheckBoxContainer.Visibility = Visibility.Collapsed;

                // Initialize with single absence
                Absences = new List<Absence> { absence };
                LoadAbsenceData(absence);
            }
            else
            {
                // Add mode: use checkboxes for multiple race selection
                RaceComboBox.Visibility = Visibility.Collapsed;
                RaceCheckBoxContainer.Visibility = Visibility.Visible;
                PopulateRaceCheckBoxes();

                // Initialize empty list
                Absences = new List<Absence>();
            }
        }

        private void PopulateRaceCheckBoxes()
        {
            RaceCheckBoxPanel.Children.Clear();

            foreach (var race in _races)
            {
                var checkBox = new CheckBox
                {
                    Content = $"{race.RaceId} - {race.RaceName}",
                    Tag = race.RaceId,
                    Style = (Style)FindResource("ModernCheckBox")
                };
                RaceCheckBoxPanel.Children.Add(checkBox);
            }
        }

        private void LoadAbsenceData(Absence absence)
        {
            RaceComboBox.SelectedValue = absence.RaceId;
            TeamComboBox.SelectedItem = absence.TeamId;
            DriverOutComboBox.SelectedItem = absence.DriverOut;
            DriverInComboBox.SelectedItem = absence.DriverIn;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            if (_isEditMode)
            {
                // Edit mode: update single absence
                var absence = Absences[0];
                absence.RaceId = (int)RaceComboBox.SelectedValue;
                absence.TeamId = TeamComboBox.SelectedItem?.ToString();
                absence.DriverOut = DriverOutComboBox.SelectedItem?.ToString();
                absence.DriverIn = DriverInComboBox.SelectedItem?.ToString();
            }
            else
            {
                // Add mode: create absence for each selected race
                var selectedRaceIds = RaceCheckBoxPanel.Children
                    .OfType<CheckBox>()
                    .Where(cb => cb.IsChecked == true)
                    .Select(cb => (int)cb.Tag)
                    .ToList();

                foreach (var raceId in selectedRaceIds)
                {
                    Absences.Add(new Absence
                    {
                        RaceId = raceId,
                        TeamId = TeamComboBox.SelectedItem?.ToString(),
                        DriverOut = DriverOutComboBox.SelectedItem?.ToString(),
                        DriverIn = DriverInComboBox.SelectedItem?.ToString()
                    });
                }
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            if (_isEditMode)
            {
                // Edit mode: validate single race selection
                if (RaceComboBox.SelectedValue == null)
                {
                    MessageBox.Show("Please select a race.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            else
            {
                // Add mode: validate at least one race is selected
                var anySelected = RaceCheckBoxPanel.Children
                    .OfType<CheckBox>()
                    .Any(cb => cb.IsChecked == true);

                if (!anySelected)
                {
                    MessageBox.Show("Please select at least one race.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            if (TeamComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a team.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}