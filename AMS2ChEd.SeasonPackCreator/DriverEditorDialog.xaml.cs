using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.SeasonPackCreator.Services;
using AMS2ChEd.SeasonPackEditor.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class DriverEditorDialog : Window
    {
        public Ams2DriverData Driver { get; private set; }
        private int _seasonYear;
        private Dictionary<string, string> _textureFiles;

        public DriverEditorDialog(int seasonYear, Dictionary<string, string> textureFiles, Ams2DriverData driver = null)
        {
            InitializeComponent();
            _seasonYear = seasonYear;
            _textureFiles = textureFiles;

            ReputationComboBox.ItemsSource = Enum.GetValues(typeof(DriverReputation)).Cast<DriverReputation>();

            if (driver == null)
            {
                Driver = new Ams2DriverData
                {
                    RatingValues = new Dictionary<string, double>()
                };
            }
            else
            {
                Driver = driver;
                LoadDriverData();
            }
        }

        private void LoadDriverData()
        {
            DriverIdTextBox.Text = Driver.DriverId;
            DriverNameTextBox.Text = Driver.Name;
            NationalityTextBox.Text = Driver.Nationality;
            YearOfBirthTextBox.Text = Driver.YearOfBirth.ToString();
            PictureTextBox.Text = Driver.PictureUrl ?? "";


            ReputationComboBox.SelectedItem = Driver.Reputation;
            PictureTextBox.Text = Driver.PictureUrl ?? PictureTextBox.Text;
            // Load ratings values
            if (Driver.RatingValues != null)
            {
                AggressionTextBox.Text = GetRatingValue(Driver.RatingValues, "aggression");
                AvoidanceForcedMistakesTextBox.Text = GetRatingValue(Driver.RatingValues, "avoidance_of_forced_mistakes");
                AvoidanceMistakesTextBox.Text = GetRatingValue(Driver.RatingValues, "avoidance_of_mistakes");
                BlueFlagConcedingTextBox.Text = GetRatingValue(Driver.RatingValues, "blue_flag_conceding");
                ConsistencyTextBox.Text = GetRatingValue(Driver.RatingValues, "consistency");
                DefendingTextBox.Text = GetRatingValue(Driver.RatingValues, "defending");
                FuelManagementTextBox.Text = GetRatingValue(Driver.RatingValues, "fuel_management");
                QualifyingSkillTextBox.Text = GetRatingValue(Driver.RatingValues, "qualifying_skill");
                RaceSkillTextBox.Text = GetRatingValue(Driver.RatingValues, "race_skill");
                StaminaTextBox.Text = GetRatingValue(Driver.RatingValues, "stamina");
                StartReactionsTextBox.Text = GetRatingValue(Driver.RatingValues, "start_reactions");
                TyreManagementTextBox.Text = GetRatingValue(Driver.RatingValues, "tyre_management");
                VehicleReliabilityTextBox.Text = GetRatingValue(Driver.RatingValues, "vehicle_reliability");
                WeatherTyreChangesTextBox.Text = GetRatingValue(Driver.RatingValues, "weather_tyre_changes");
                WetSkillTextBox.Text = GetRatingValue(Driver.RatingValues, "wet_skill");
            }

            BaseHelmetTextBox.Text = Driver.BaseHelmetFile;
            UpdateHelmetSourceLabel();

            BaseVisorTextBox.Text = Driver.BaseVisorFile;
            UpdateVisorSourceLabel();

            BaseHelmet90sTextBox.Text = Driver.BaseHelmetFile90s ?? "";
            UpdateSourceLabel(BaseHelmet90sTextBox.Text, BaseHelmet90sSourceLabel);

            BaseHelmet80sTextBox.Text = Driver.BaseHelmetFile80s ?? "";
            UpdateSourceLabel(BaseHelmet80sTextBox.Text, BaseHelmet80sSourceLabel);

            BaseVisor80sTextBox.Text = Driver.BaseVisorFile80s ?? "";
            UpdateSourceLabel(BaseVisor80sTextBox.Text, BaseVisor80sSourceLabel);

            BaseHelmet70sTextBox.Text = Driver.BaseHelmetFile70s ?? "";
            UpdateSourceLabel(BaseHelmet70sTextBox.Text, BaseHelmet70sSourceLabel);

            BaseVisor70sTextBox.Text = Driver.BaseVisorFile70s ?? "";
            UpdateSourceLabel(BaseVisor70sTextBox.Text, BaseVisor70sSourceLabel);

        }

        private void UpdateHelmetSourceLabel()
        {
            UpdateSourceLabel(BaseHelmetTextBox.Text, BaseHelmetSourceLabel);
        }

        private void UpdateVisorSourceLabel()
        {
            UpdateSourceLabel(BaseVisorTextBox.Text, BaseVisorSourceLabel);
        }

        private void UpdateSourceLabel(string key, TextBlock label)
        {
            if (!string.IsNullOrWhiteSpace(key) && _textureFiles.ContainsKey(key))
                label.Text = $"Source: {_textureFiles[key]}";
            else
                label.Text = "";
        }

        private string GetRatingValue(Dictionary<string, double> values, string key)
        {
            return values.ContainsKey(key) ? values[key].ToString("F3") : "0.500";
        }

        private void BrowseBaseHelmet_Click(object sender, RoutedEventArgs e)
        {
            if (BrowseHelmetVisorFile("Select Base Helmet Design", out string relativePath, $"{DriverIdTextBox.Text}.png"))
            {
                BaseHelmetTextBox.Text = relativePath;
                UpdateHelmetSourceLabel();
            }
        }

        private void BrowseBaseVisor_Click(object sender, RoutedEventArgs e)
        {
            if (BrowseHelmetVisorFile("Select Base Visor Design", out string relativePath, $"{DriverIdTextBox.Text}_visor.png"))
            {
                BaseVisorTextBox.Text = relativePath;
                UpdateVisorSourceLabel();
            }
        }

        private void BrowseBaseHelmet90s_Click(object sender, RoutedEventArgs e)
        {
            if (BrowseHelmetVisorFile("Select Base Helmet Design (90s)", out string relativePath, $"{DriverIdTextBox.Text}_90s.png"))
            {
                BaseHelmet90sTextBox.Text = relativePath;
                UpdateSourceLabel(relativePath, BaseHelmet90sSourceLabel);
            }
        }

        private void BrowseBaseHelmet80s_Click(object sender, RoutedEventArgs e)
        {
            if (BrowseHelmetVisorFile("Select Base Helmet Design (80s)", out string relativePath, $"{DriverIdTextBox.Text}_80s.png"))
            {
                BaseHelmet80sTextBox.Text = relativePath;
                UpdateSourceLabel(relativePath, BaseHelmet80sSourceLabel);
            }
        }

        private void BrowseBaseVisor80s_Click(object sender, RoutedEventArgs e)
        {
            if (BrowseHelmetVisorFile("Select Base Visor Design (80s)", out string relativePath, $"{DriverIdTextBox.Text}_visor_80s.png"))
            {
                BaseVisor80sTextBox.Text = relativePath;
                UpdateSourceLabel(relativePath, BaseVisor80sSourceLabel);
            }
        }

        private void BrowseBaseHelmet70s_Click(object sender, RoutedEventArgs e)
        {
            if (BrowseHelmetVisorFile("Select Base Helmet Design (70s)", out string relativePath, $"{DriverIdTextBox.Text}_70s.png"))
            {
                BaseHelmet70sTextBox.Text = relativePath;
                UpdateSourceLabel(relativePath, BaseHelmet70sSourceLabel);
            }
        }

        private void BrowseBaseVisor70s_Click(object sender, RoutedEventArgs e)
        {
            if (BrowseHelmetVisorFile("Select Base Visor Design (70s)", out string relativePath, $"{DriverIdTextBox.Text}_visor_70s.png"))
            {
                BaseVisor70sTextBox.Text = relativePath;
                UpdateSourceLabel(relativePath, BaseVisor70sSourceLabel);
            }
        }

        private bool BrowseHelmetVisorFile(string title, out string relativePath, string filename)
        {
            relativePath = null;
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                Title = title
            };

            if (dialog.ShowDialog() == true)
            {
                relativePath = $"../{_seasonYear}/helmet_liveries/{filename}";
                _textureFiles[relativePath] = dialog.FileName;
                return true;
            }
            return false;
        }

        private void BrowsePicture_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PNG images (*.png)|*.png|All files (*.*)|*.*",
                Title = "Select Driver Picture"
            };

            if (dialog.ShowDialog() == true)
            {
                var filename = System.IO.Path.GetFileName(dialog.FileName);

                // Store the picture with the season-specific path
                var relativePath = $"Seasons/{_seasonYear}/portraits/{DriverIdTextBox.Text}.png";
                PictureTextBox.Text = relativePath;

                // Track the file for export
                _textureFiles[relativePath] = dialog.FileName;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            Driver.DriverId = DriverIdTextBox.Text;
            Driver.Name = DriverNameTextBox.Text;
            Driver.Nationality = NationalityTextBox.Text;
            Driver.YearOfBirth = int.Parse(YearOfBirthTextBox.Text);
            Driver.Reputation = (DriverReputation)ReputationComboBox.SelectedItem;
            Driver.PictureUrl = PictureTextBox.Text?.Trim() ?? "";
            Driver.BaseVisorFile = BaseVisorTextBox.Text ?? "";
            Driver.BaseHelmetFile = BaseHelmetTextBox.Text ?? "";
            Driver.BaseHelmetFile90s = BaseHelmet90sTextBox.Text?.Trim();
            Driver.BaseHelmetFile80s = BaseHelmet80sTextBox.Text?.Trim();
            Driver.BaseVisorFile80s = BaseVisor80sTextBox.Text?.Trim();
            Driver.BaseHelmetFile70s = BaseHelmet70sTextBox.Text?.Trim();
            Driver.BaseVisorFile70s = BaseVisor70sTextBox.Text?.Trim();

            var ratingValues = new Dictionary<string, double>
            {
                { "aggression", ParseRating(AggressionTextBox.Text) },
                { "avoidance_of_forced_mistakes", ParseRating(AvoidanceForcedMistakesTextBox.Text) },
                { "avoidance_of_mistakes", ParseRating(AvoidanceMistakesTextBox.Text) },
                { "blue_flag_conceding", ParseRating(BlueFlagConcedingTextBox.Text) },
                { "consistency", ParseRating(ConsistencyTextBox.Text) },
                { "defending", ParseRating(DefendingTextBox.Text) },
                { "fuel_management", ParseRating(FuelManagementTextBox.Text) },
                { "qualifying_skill", ParseRating(QualifyingSkillTextBox.Text) },
                { "race_skill", ParseRating(RaceSkillTextBox.Text) },
                { "stamina", ParseRating(StaminaTextBox.Text) },
                { "start_reactions", ParseRating(StartReactionsTextBox.Text) },
                { "tyre_management", ParseRating(TyreManagementTextBox.Text) },
                { "vehicle_reliability", ParseRating(VehicleReliabilityTextBox.Text) },
                { "weather_tyre_changes", ParseRating(WeatherTyreChangesTextBox.Text) },
                { "wet_skill", ParseRating(WetSkillTextBox.Text) }
            };

            Driver.RatingValues = ratingValues;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private double ParseRating(string text)
        {
            if (double.TryParse(text, out double value))
            {
                // Clamp between 0 and 1
                return Math.Max(0.0, Math.Min(1.0, value));
            }
            return 0.5;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(DriverIdTextBox.Text))
            {
                MessageBox.Show("Driver ID is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(DriverNameTextBox.Text))
            {
                MessageBox.Show("Driver Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!int.TryParse(YearOfBirthTextBox.Text, out _))
            {
                MessageBox.Show("Year of Birth must be a valid number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validate all rating fields are valid numbers between 0 and 1
            var ratingFields = new[] {
                AggressionTextBox, AvoidanceForcedMistakesTextBox, AvoidanceMistakesTextBox,
                BlueFlagConcedingTextBox, ConsistencyTextBox, DefendingTextBox,
                FuelManagementTextBox, QualifyingSkillTextBox, RaceSkillTextBox,
                StaminaTextBox, StartReactionsTextBox, TyreManagementTextBox,
                VehicleReliabilityTextBox, WeatherTyreChangesTextBox, WetSkillTextBox
            };

            foreach (var field in ratingFields)
            {
                if (!double.TryParse(field.Text, out double value) || value < 0 || value > 1)
                {
                    MessageBox.Show("All rating values must be numbers between 0.0 and 1.0", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private void DriverNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            DriverIdTextBox.Text = DriverIdGenerator.GenerateDriverId(((TextBox)sender).Text);
        }

        private void GeneratePerformance_Click(object sender, RoutedEventArgs e)
        {
            if (ReputationComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a reputation first.", "No Reputation Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var reputation = (DriverReputation)ReputationComboBox.SelectedItem;
            var ratings = DriverPerformanceGenerator.Generate(reputation);

            // Update all rating textboxes
            AggressionTextBox.Text = ratings["aggression"].ToString("F3");
            AvoidanceForcedMistakesTextBox.Text = ratings["avoidance_of_forced_mistakes"].ToString("F3");
            AvoidanceMistakesTextBox.Text = ratings["avoidance_of_mistakes"].ToString("F3");
            BlueFlagConcedingTextBox.Text = ratings["blue_flag_conceding"].ToString("F3");
            ConsistencyTextBox.Text = ratings["consistency"].ToString("F3");
            DefendingTextBox.Text = ratings["defending"].ToString("F3");
            FuelManagementTextBox.Text = ratings["fuel_management"].ToString("F3");
            QualifyingSkillTextBox.Text = ratings["qualifying_skill"].ToString("F3");
            RaceSkillTextBox.Text = ratings["race_skill"].ToString("F3");
            StaminaTextBox.Text = ratings["stamina"].ToString("F3");
            StartReactionsTextBox.Text = ratings["start_reactions"].ToString("F3");
            TyreManagementTextBox.Text = ratings["tyre_management"].ToString("F3");
            VehicleReliabilityTextBox.Text = ratings["vehicle_reliability"].ToString("F3");
            WeatherTyreChangesTextBox.Text = ratings["weather_tyre_changes"].ToString("F3");
            WetSkillTextBox.Text = ratings["wet_skill"].ToString("F3");
        }

        private void YearOfBirthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateReputationComboBox();
        }

        private void UpdateReputationComboBox()
        {
            if (ReputationComboBox.Items == null) return;
            ReputationComboBox.ItemsSource = null;
            var reputationList = Enum.GetValues<DriverReputation>();
            // Get available reputations based on age
            IEnumerable<DriverReputation> availableReputations;
            if (int.TryParse(YearOfBirthTextBox.Text, out int yearOfBirth) && yearOfBirth > 0)
            {
                availableReputations = ReputationUpdater.AvailableReputationForAge(_seasonYear - yearOfBirth);
            }
            else
            {
                // If no valid age, show all reputations
                availableReputations = reputationList;
            }

            // Filter and add items
            var filteredReputations = reputationList.Where(r => availableReputations.Contains(r)).ToList();

            foreach (var item in filteredReputations)
            {
                ReputationComboBox.Items.Add(item);
            }

            if (ReputationComboBox.Items.Count > 0)
            {
                ReputationComboBox.SelectedIndex = 0;
            }
        }
    }
}