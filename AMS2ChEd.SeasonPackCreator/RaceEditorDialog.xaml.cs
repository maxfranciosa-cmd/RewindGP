using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class RaceEditorDialog : Window
    {
        public Race Race { get; private set; }
        private readonly Dictionary<string, string> _textureFiles;

        public RaceEditorDialog(Dictionary<string, string> textureFiles, Race race = null)
        {
            InitializeComponent();

            _textureFiles = textureFiles;

            if (race == null)
            {
                Race = new Race();
            }
            else
            {
                Race = race;
                LoadRaceData();
            }
        }

        private void LoadRaceData()
        {
            RaceIdTextBox.Text = Race.RaceId.ToString();
            RaceNameTextBox.Text = Race.RaceName;
            RaceShortNameTextBox.Text = Race.RaceShortName;
            RaceDateTextBox.Text = Race.RaceDate;
            CircuitTextBox.Text = Race.Circuit;
            CoverPictureTextBox.Text = Race.CoverPictureUrl;

            // Update source label
            UpdateCoverPictureSourceLabel();
        }

        private void UpdateCoverPictureSourceLabel()
        {
            if (!string.IsNullOrWhiteSpace(CoverPictureTextBox.Text))
            {
                var relativePath = $"race_covers/{CoverPictureTextBox.Text}";
                if (_textureFiles.ContainsKey(relativePath))
                {
                    CoverPictureSourceLabel.Text = $"Source: {_textureFiles[relativePath]}";
                }
                else
                {
                    CoverPictureSourceLabel.Text = "";
                }
            }
            else
            {
                CoverPictureSourceLabel.Text = "";
            }
        }

        private void BrowseCoverPicture_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
                Title = "Select Cover Picture"
            };

            if (dialog.ShowDialog() == true)
            {
                var filename = System.IO.Path.GetFileName(dialog.FileName);
                CoverPictureTextBox.Text = $"race_covers/{filename}";

                // Track the file for export
                var relativePath = $"race_covers/{filename}";
                _textureFiles[relativePath] = dialog.FileName;

                // Update source label
                UpdateCoverPictureSourceLabel();
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
            {
                return;
            }

            Race.RaceId = int.Parse(RaceIdTextBox.Text);
            Race.RaceName = RaceNameTextBox.Text;
            Race.RaceShortName = RaceShortNameTextBox.Text;
            Race.RaceDate = RaceDateTextBox.Text;
            Race.Circuit = CircuitTextBox.Text;
            Race.CoverPictureUrl = CoverPictureTextBox.Text;

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
            if (!int.TryParse(RaceIdTextBox.Text, out _))
            {
                MessageBox.Show("Race ID must be a valid number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(RaceNameTextBox.Text))
            {
                MessageBox.Show("Race Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(CircuitTextBox.Text))
            {
                MessageBox.Show("Circuit is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}