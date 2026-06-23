using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.Windows.Shell;
using System.IO;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class LiveryOverrideDialog : Window
    {
        public List<LiveryOverride> LiveryOverrides { get; private set; }

        private readonly IEnumerable<Race> _races;
        private readonly Dictionary<string, string> _textureFiles;
        private readonly string _teamId;
        private readonly bool _isEditMode;
        private readonly LiveryOverride _originalOverride;

        public LiveryOverrideDialog(IEnumerable<Race> races, Dictionary<string, string> textureFiles, string teamId, LiveryOverride liveryOverride = null)
        {
            InitializeComponent();

            _races = races;
            _textureFiles = textureFiles;
            _teamId = teamId;
            _isEditMode = liveryOverride != null;
            _originalOverride = liveryOverride;

            LoadRaceCheckBoxes();

            if (liveryOverride != null)
            {
                LoadOverrideData(liveryOverride);
            }
        }

        private void LoadRaceCheckBoxes()
        {
            foreach (var race in _races)
            {
                var checkBox = new CheckBox
                {
                    Content = $"{race.RaceId} - {race.RaceName}",
                    Tag = race.RaceId,
                    Style = (Style)FindResource("ModernCheckBox")
                };

                // If editing, check the race that matches
                if (_isEditMode && _originalOverride.RaceId == race.RaceId)
                {
                    checkBox.IsChecked = true;
                }

                RacesCheckBoxPanel.Children.Add(checkBox);
            }
        }

        private void LoadOverrideData(LiveryOverride liveryOverride)
        {
            Driver1LiveryTextBox.Text = liveryOverride.Driver1Livery;
            Driver2LiveryTextBox.Text = liveryOverride.Driver2Livery;
            HelmetSponsorsTextBox.Text = liveryOverride.HelmetSponsors;
            VisorSponsorsTextBox.Text = liveryOverride.VisorSponsors;
            LiveryPreviewTextBox.Text = liveryOverride.LiveryPreview;

            // Update all source labels
            UpdateDriver1LiverySourceLabel();
            UpdateDriver2LiverySourceLabel();
            UpdateHelmetSponsorsSourceLabel();
            UpdateVisorSponsorsSourceLabel();
            UpdateLiveryPreviewSourceLabel();

        }

        #region Browse Methods

        private void BrowseDriver1Livery_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Driver 1 Livery Override", "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                (path, filename) =>
                {
                    var fileNamePng = Path.ChangeExtension(filename, "png");
                    Driver1LiveryTextBox.Text = $"car_liveries/{_teamId}/{fileNamePng}";
                    var relativePath = $"car_liveries/{_teamId}/{fileNamePng}";
                    _textureFiles[relativePath] = path;
                    UpdateDriver1LiverySourceLabel();
                });
        }

        private void BrowseDriver2Livery_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Driver 2 Livery Override", "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                (path, filename) =>
                {
                    var fileNamePng = Path.ChangeExtension(filename, "png");
                    Driver2LiveryTextBox.Text = $"car_liveries/{_teamId}/{fileNamePng}";
                    var relativePath = $"car_liveries/{_teamId}/{fileNamePng}";
                    _textureFiles[relativePath] = path;
                    UpdateDriver2LiverySourceLabel();
                });
        }

        private void BrowseHelmetSponsors_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Helmet Sponsors Override", "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                (path, filename) =>
                {
                    var fileNamePng = Path.ChangeExtension(filename, "png");
                    HelmetSponsorsTextBox.Text = $"helmet_sponsors/{_teamId}/{fileNamePng}";
                    var relativePath = $"helmet_sponsors/{_teamId}/{fileNamePng}";
                    _textureFiles[relativePath] = path;
                    UpdateHelmetSponsorsSourceLabel();
                });
        }

        private void BrowseVisorSponsors_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Visor Sponsors Override", "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                (path, filename) =>
                {
                    var fileNamePng = Path.ChangeExtension(filename, "png");
                    VisorSponsorsTextBox.Text = $"helmet_sponsors/{_teamId}/{fileNamePng}";
                    var relativePath = $"helmet_sponsors/{_teamId}/{fileNamePng}";
                    _textureFiles[relativePath] = path;
                    UpdateVisorSponsorsSourceLabel();
                });
        }

        private void BrowseLiveryPreview_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Livery Preview Override (for AMS2 menus)", "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                (path, filename) =>
                {
                    var fileNameDDs = Path.ChangeExtension(filename, "dds");
                    LiveryPreviewTextBox.Text = $"previews/{_teamId}/{fileNameDDs}";
                    var relativePath = $"previews/{_teamId}/{fileNameDDs}";
                    _textureFiles[relativePath] = path;
                    UpdateLiveryPreviewSourceLabel();
                });
        }



        private void BrowseForFile(string title, string filter, System.Action<string, string> onSelected)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter
            };

            if (dialog.ShowDialog() == true)
            {
                var filename = System.IO.Path.GetFileName(dialog.FileName);
                onSelected(dialog.FileName, filename);
            }
        }

        #endregion

        #region Source Label Updates

        private void UpdateDriver1LiverySourceLabel()
        {
            if (!string.IsNullOrWhiteSpace(Driver1LiveryTextBox.Text))
            {
                var relativePath = Driver1LiveryTextBox.Text;
                if (_textureFiles.ContainsKey(relativePath))
                {
                    Driver1LiverySourceLabel.Text = $"Source: {_textureFiles[relativePath]}";
                }
                else
                {
                    Driver1LiverySourceLabel.Text = "";
                }
            }
            else
            {
                Driver1LiverySourceLabel.Text = "";
            }
        }

        private void UpdateDriver2LiverySourceLabel()
        {
            if (!string.IsNullOrWhiteSpace(Driver2LiveryTextBox.Text))
            {
                var relativePath = Driver2LiveryTextBox.Text;
                if (_textureFiles.ContainsKey(relativePath))
                {
                    Driver2LiverySourceLabel.Text = $"Source: {_textureFiles[relativePath]}";
                }
                else
                {
                    Driver2LiverySourceLabel.Text = "";
                }
            }
            else
            {
                Driver2LiverySourceLabel.Text = "";
            }
        }

        private void UpdateHelmetSponsorsSourceLabel()
        {
            if (!string.IsNullOrWhiteSpace(HelmetSponsorsTextBox.Text))
            {
                // HelmetSponsors uses full path like "helmet_sponsors/{teamId}/{filename}"
                var relativePath = HelmetSponsorsTextBox.Text;
                if (_textureFiles.ContainsKey(relativePath))
                {
                    HelmetSponsorsSourceLabel.Text = $"Source: {_textureFiles[relativePath]}";
                }
                else
                {
                    HelmetSponsorsSourceLabel.Text = "";
                }
            }
            else
            {
                HelmetSponsorsSourceLabel.Text = "";
            }
        }

        private void UpdateVisorSponsorsSourceLabel()
        {
            if (!string.IsNullOrWhiteSpace(VisorSponsorsTextBox.Text))
            {
                // VisorSponsors uses full path like "helmet_sponsors/{teamId}/{filename}"
                var relativePath = VisorSponsorsTextBox.Text;
                if (_textureFiles.ContainsKey(relativePath))
                {
                    VisorSponsorsSourceLabel.Text = $"Source: {_textureFiles[relativePath]}";
                }
                else
                {
                    VisorSponsorsSourceLabel.Text = "";
                }
            }
            else
            {
                VisorSponsorsSourceLabel.Text = "";
            }
        }

        private void UpdateLiveryPreviewSourceLabel()
        {
            if (!string.IsNullOrWhiteSpace(LiveryPreviewTextBox.Text))
            {
                // LiveryPreview uses just filename, stored under "previews/{teamId}/{filename}"
                var relativePath = LiveryPreviewTextBox.Text;
                if (_textureFiles.ContainsKey(relativePath))
                {
                    LiveryPreviewSourceLabel.Text = $"Source: {_textureFiles[relativePath]}";
                }
                else
                {
                    LiveryPreviewSourceLabel.Text = "";
                }
            }
            else
            {
                LiveryPreviewSourceLabel.Text = "";
            }
        }



        #endregion

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            // Get all selected races
            var selectedRaceIds = RacesCheckBoxPanel.Children
                .OfType<CheckBox>()
                .Where(cb => cb.IsChecked == true)
                .Select(cb => (int)cb.Tag)
                .ToList();

            // Create one LiveryOverride per selected race
            LiveryOverrides = selectedRaceIds.Select(raceId => { 
                var liveryoverride = _originalOverride != null && _originalOverride.RaceId == raceId ? _originalOverride : new LiveryOverride();
                liveryoverride.RaceId = raceId;
                liveryoverride.Driver1Livery = string.IsNullOrWhiteSpace(Driver1LiveryTextBox.Text) ? null : Driver1LiveryTextBox.Text;
                liveryoverride.Driver2Livery = string.IsNullOrWhiteSpace(Driver2LiveryTextBox.Text) ? null : Driver2LiveryTextBox.Text;
                liveryoverride.HelmetSponsors = string.IsNullOrWhiteSpace(HelmetSponsorsTextBox.Text) ? null : HelmetSponsorsTextBox.Text;
                liveryoverride.VisorSponsors = string.IsNullOrWhiteSpace(VisorSponsorsTextBox.Text) ? null : VisorSponsorsTextBox.Text;
                liveryoverride.LiveryPreview = string.IsNullOrWhiteSpace(LiveryPreviewTextBox.Text) ? null : LiveryPreviewTextBox.Text;
                return liveryoverride;
            }).ToList();

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
            // Check if at least one race is selected
            var hasSelectedRace = RacesCheckBoxPanel.Children
                .OfType<CheckBox>()
                .Any(cb => cb.IsChecked == true);

            if (!hasSelectedRace)
            {
                MessageBox.Show("Please select at least one race.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Check if at least one livery field is filled
            var hasAnyData = !string.IsNullOrWhiteSpace(Driver1LiveryTextBox.Text) ||
                           !string.IsNullOrWhiteSpace(Driver2LiveryTextBox.Text) ||
                           !string.IsNullOrWhiteSpace(HelmetSponsorsTextBox.Text) ||
                           !string.IsNullOrWhiteSpace(VisorSponsorsTextBox.Text) ||
                           !string.IsNullOrWhiteSpace(LiveryPreviewTextBox.Text);


            if (!hasAnyData)
            {
                MessageBox.Show("Please specify at least one livery override.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}