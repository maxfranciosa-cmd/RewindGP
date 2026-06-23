using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.SeasonPackEditor.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class TeamEditorDialog : Window
    {
        public Ams2TeamEntry Team { get; private set; }
        private List<Ams2DriverData> _availableDrivers;
        private List<TeamOption> _availableTeams;

        public TeamEditorDialog(List<Ams2DriverData> drivers, Ams2TeamEntry team = null)
        {
            InitializeComponent();
            _availableDrivers = drivers;

            // Load teams from teams.json
            LoadTeamsFromJson();

            ReputationComboBox.ItemsSource = Enum.GetValues(typeof(TeamReputation)).Cast<TeamReputation>();
            Driver1ComboBox.ItemsSource = _availableDrivers.Select(d => d.DriverId).ToList();
            Driver2ComboBox.ItemsSource = _availableDrivers.Select(d => d.DriverId).ToList();

            if (team == null)
            {
                Team = new Ams2TeamEntry
                {
                    Driver1Contract = new DriverContract(),
                    Driver2Contract = new DriverContract(),
                    Ams2CarPerformanceMalus = new Dictionary<string, double>
                    {
                        { "consistency", 0.000 },
                        { "defending", 0.000 },
                        { "fuel_management", 0.000 },
                        { "qualifying_skill", 0.000 },
                        { "race_skill", 0.000 },
                        { "tyre_management", 0.000 },
                        { "vehicle_reliability", 0.000 },
                        { "weight_scalar", 0.000 },
                        { "power_scalar", 0.000 },
                        { "drag_scalar", 0.000 }
                    }
                };
            }
            else
            {
                Team = team;

                // Ensure Ams2CarPerformanceMalus is initialized
                if (Team.Ams2CarPerformanceMalus == null)
                {
                    Team.Ams2CarPerformanceMalus = new Dictionary<string, double>
                    {
                        { "consistency", 0.000 },
                        { "defending", 0.000 },
                        { "fuel_management", 0.000 },
                        { "qualifying_skill", 0.000 },
                        { "race_skill", 0.000 },
                        { "tyre_management", 0.000 },
                        { "vehicle_reliability", 0.000 },
                        { "weight_scalar", 0.000 },
                        { "power_scalar", 0.000 },
                        { "drag_scalar", 0.000 }
                    };
                }

                LoadTeamData();
            }
        }

        private void LoadTeamsFromJson()
        {
            try
            {
                string teamsJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "teams.json");

                if (!File.Exists(teamsJsonPath))
                {
                    MessageBox.Show($"teams.json file not found at: {teamsJsonPath}\n\nPlease ensure teams.json is in the application directory.",
                        "Teams File Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _availableTeams = new List<TeamOption>();
                    TeamIdComboBox.ItemsSource = _availableTeams;
                    return;
                }

                string json = File.ReadAllText(teamsJsonPath);
                var teamsData = JsonSerializer.Deserialize<TeamsData>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                _availableTeams = teamsData?.Teams ?? new List<TeamOption>();

                TeamIdComboBox.ItemsSource = _availableTeams;
                TeamIdComboBox.DisplayMemberPath = "TeamName";
                TeamIdComboBox.SelectedValuePath = "TeamId";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading teams.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _availableTeams = new List<TeamOption>();
                TeamIdComboBox.ItemsSource = _availableTeams;
            }
        }

        private void LoadTeamData()
        {
            // Find and select the team in the ComboBox
            TeamIdComboBox.SelectedValue = Team.TeamId;

            TeamNameTextBox.Text = Team.TeamName;
            TeamPrincipalTextBox.Text = Team.TeamPrincipal;
            TeamColorTextBox.Text = Team.Color;
            ReputationComboBox.SelectedItem = Team.Reputation;
            Driver1ComboBox.SelectedItem = Team.Driver1Contract?.DriverId;
            Driver1NumberTextBox.Text = Team.Driver1Contract?.DriverNumber.ToString();
            Driver1RacesContractTextBox.Text = Team.Driver2Contract?.Races.ToString();
            Driver2ComboBox.SelectedItem = Team.Driver2Contract?.DriverId;
            Driver2NumberTextBox.Text = Team.Driver2Contract?.DriverNumber.ToString();
            Driver2RacesContractTextBox.Text = Team.Driver2Contract?.Races.ToString();
            DefaultPreQualiCheckBox.IsChecked = Team.DefaultPrequalifying;

            // Load performance malus values
            MalusWeightScalarTextBox.Text = GetMalusValue("weight_scalar");
            MalusPowerScalarTextBox.Text = GetMalusValue("power_scalar");
            MalusDragScalarTextBox.Text = GetMalusValue("drag_scalar");
            MalusConsistencyTextBox.Text = GetMalusValue("consistency");
            MalusDefendingTextBox.Text = GetMalusValue("defending");
            MalusFuelManagementTextBox.Text = GetMalusValue("fuel_management");
            MalusQualifyingSkillTextBox.Text = GetMalusValue("qualifying_skill");
            MalusRaceSkillTextBox.Text = GetMalusValue("race_skill");
            MalusTyreManagementTextBox.Text = GetMalusValue("tyre_management");
            MalusVeichleReliabilityTextBox.Text = GetMalusValue("vehicle_reliability");
            WetSkillTextBox.Text = GetMalusValue("wet_skill");
        }

        private string GetMalusValue(string key)
        {
            if (Team.Ams2CarPerformanceMalus != null && Team.Ams2CarPerformanceMalus.ContainsKey(key))
            {
                return Team.Ams2CarPerformanceMalus[key].ToString("F3");
            }
            return "0.000";
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            Team.TeamId = TeamIdComboBox.SelectedValue?.ToString();
            Team.TeamName = TeamNameTextBox.Text;
            Team.TeamPrincipal = TeamPrincipalTextBox.Text;
            Team.Color = TeamColorTextBox.Text;
            Team.Reputation = (TeamReputation)ReputationComboBox.SelectedItem;
            Team.Driver1Contract.DriverId = Driver1ComboBox.SelectedItem?.ToString();
            Team.Driver1Contract.DriverNumber = int.Parse(Driver1NumberTextBox.Text);
            Team.Driver1Contract.Races = int.Parse(Driver1RacesContractTextBox.Text);
            Team.Driver2Contract.DriverId = Driver2ComboBox.SelectedItem?.ToString();
            Team.Driver2Contract.DriverNumber = int.Parse(Driver2NumberTextBox.Text);
            Team.Driver2Contract.Races = int.Parse(Driver2RacesContractTextBox.Text);
            Team.DefaultPrequalifying = DefaultPreQualiCheckBox.IsChecked ?? false;

            // Update performance malus
            Team.Ams2CarPerformanceMalus = new Dictionary<string, double>
            {
                { "consistency", ParseMalus(MalusConsistencyTextBox.Text) },
                { "defending", ParseMalus(MalusDefendingTextBox.Text) },
                { "fuel_management", ParseMalus(MalusFuelManagementTextBox.Text) },
                { "qualifying_skill", ParseMalus(MalusQualifyingSkillTextBox.Text) },
                { "race_skill", ParseMalus(MalusRaceSkillTextBox.Text) },
                { "tyre_management", ParseMalus(MalusTyreManagementTextBox.Text) },
                { "vehicle_reliability", ParseMalus(MalusVeichleReliabilityTextBox.Text) },
                { "wet_skill",  ParseMalus(WetSkillTextBox.Text) },
                { "weight_scalar", ParseMalus(MalusWeightScalarTextBox.Text) },
                { "power_scalar", ParseMalus(MalusPowerScalarTextBox.Text) },
                { "drag_scalar",  ParseMalus(MalusDragScalarTextBox.Text) }
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private double ParseMalus(string text)
        {
            if (double.TryParse(text, out double value))
            {
                // Clamp between -1 and 1 (though typically these are 0 or negative)
                return value;
            }
            return 0.0;
        }

        private void GeneratePerformance_Click(object sender, RoutedEventArgs e)
        {
            if (ReputationComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a reputation first.", "No Reputation Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var reputation = (TeamReputation)ReputationComboBox.SelectedItem;
            var ratings = DriverPerformanceGenerator.Generate(reputation);

            // Update all rating textboxes
            MalusWeightScalarTextBox.Text = ratings["weight_scalar"].ToString("F3");
            MalusDragScalarTextBox.Text = ratings["drag_scalar"].ToString("F3");
            MalusPowerScalarTextBox.Text = ratings["power_scalar"].ToString("F3");
        }

        private bool ValidateInput()
        {
            if (TeamIdComboBox.SelectedValue == null)
            {
                MessageBox.Show("Team ID is required. Please select a team from the list.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!int.TryParse(Driver1NumberTextBox.Text, out _) || !int.TryParse(Driver2NumberTextBox.Text, out _))
            {
                MessageBox.Show("Driver numbers must be valid integers.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!int.TryParse(Driver1RacesContractTextBox.Text, out _) || !int.TryParse(Driver2RacesContractTextBox.Text, out _))
            {
                MessageBox.Show("Driver numbers must be valid integers.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validate malus fields
            var malusFields = new[] { MalusConsistencyTextBox, MalusDefendingTextBox, MalusFuelManagementTextBox,
                                     MalusQualifyingSkillTextBox, MalusRaceSkillTextBox, MalusTyreManagementTextBox, MalusVeichleReliabilityTextBox, WetSkillTextBox , MalusWeightScalarTextBox, MalusDragScalarTextBox, MalusPowerScalarTextBox};

            foreach (var field in malusFields)
            {
                if (!double.TryParse(field.Text, out double value))
                {
                    MessageBox.Show("All performance malus values must be valid numbers", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }
    }

    // Helper classes for JSON deserialization
    public class TeamsData
    {
        public List<TeamOption> Teams { get; set; }
    }

    public class TeamOption
    {
        public string TeamId { get; set; }
        public string TeamName { get; set; }
    }
}