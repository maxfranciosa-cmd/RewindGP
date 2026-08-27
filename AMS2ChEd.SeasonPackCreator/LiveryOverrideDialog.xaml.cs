using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models.Concrete;
using Ams2ChEd.Business.AMS2.Models;
using System.Windows.Shell;
using System.IO;
using System.Xml.Linq;

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
        private readonly ExternalLiveriesConfig _externalLiveriesConfig;
        private readonly IEnumerable<NumbersPlacement> _baseNumbersPlacements;
        private List<NumbersPlacement> _numbersPlacements;

        public LiveryOverrideDialog(IEnumerable<Race> races, Dictionary<string, string> textureFiles, string teamId, ExternalLiveriesConfig externalLiveriesConfig = null, LiveryOverride liveryOverride = null, IEnumerable<NumbersPlacement> baseNumbersPlacements = null)
        {
            InitializeComponent();

            _races = races;
            _textureFiles = textureFiles;
            _teamId = teamId;
            _externalLiveriesConfig = externalLiveriesConfig ?? new ExternalLiveriesConfig();
            _isEditMode = liveryOverride != null;
            _originalOverride = liveryOverride;
            _baseNumbersPlacements = baseNumbersPlacements ?? Enumerable.Empty<NumbersPlacement>();

            LoadRaceCheckBoxes();

            _numbersPlacements = liveryOverride?.NumbersPlacements?.ToList() ?? new List<NumbersPlacement>();
            NumberPlacementsDataGrid.ItemsSource = _numbersPlacements;

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

            // Restore external livery state
            LoadExternalOverrideState();
        }

        private void LoadExternalOverrideState()
        {
            var d1Key = Driver1LiveryTextBox.Text;
            if (!string.IsNullOrEmpty(d1Key))
            {
                var entry = _externalLiveriesConfig.Entries.FirstOrDefault(e =>
                    string.Equals(e.DestinationPath, d1Key, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    Driver1ExternalCheckBox.IsChecked = true;
                    Driver1ExternalPanel.Visibility = Visibility.Visible;
                    Driver1SourcePathTextBox.Text = entry.SourcePath ?? "";
                }
            }

            var d2Key = Driver2LiveryTextBox.Text;
            if (!string.IsNullOrEmpty(d2Key))
            {
                var entry = _externalLiveriesConfig.Entries.FirstOrDefault(e =>
                    string.Equals(e.DestinationPath, d2Key, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    Driver2ExternalCheckBox.IsChecked = true;
                    Driver2ExternalPanel.Visibility = Visibility.Visible;
                    Driver2SourcePathTextBox.Text = entry.SourcePath ?? "";
                }
            }

            var previewKey = LiveryPreviewTextBox.Text;
            if (!string.IsNullOrEmpty(previewKey))
            {
                var entry = _externalLiveriesConfig.Entries.FirstOrDefault(e =>
                    string.Equals(e.DestinationPath, previewKey, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    PreviewExternalCheckBox.IsChecked = true;
                    PreviewExternalPanel.Visibility = Visibility.Visible;
                    PreviewSourcePathTextBox.Text = entry.SourcePath ?? "";
                }
            }
        }

        #region External Livery State

        private void Driver1External_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = Driver1ExternalCheckBox.IsChecked == true;
            Driver1ExternalPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            var key = Driver1LiveryTextBox.Text;
            if (!string.IsNullOrEmpty(key))
            {
                _externalLiveriesConfig.Entries.RemoveAll(entry =>
                    string.Equals(entry.DestinationPath, key, StringComparison.OrdinalIgnoreCase));

                if (isChecked)
                {
                    _externalLiveriesConfig.Entries.Add(new ExternalLiveriesEntry
                    {
                        SourcePath = Driver1SourcePathTextBox.Text,
                        DestinationPath = key
                    });
                }
            }
        }

        private void Driver2External_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = Driver2ExternalCheckBox.IsChecked == true;
            Driver2ExternalPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            var key = Driver2LiveryTextBox.Text;
            if (!string.IsNullOrEmpty(key))
            {
                _externalLiveriesConfig.Entries.RemoveAll(entry =>
                    string.Equals(entry.DestinationPath, key, StringComparison.OrdinalIgnoreCase));

                if (isChecked)
                {
                    _externalLiveriesConfig.Entries.Add(new ExternalLiveriesEntry
                    {
                        SourcePath = Driver2SourcePathTextBox.Text,
                        DestinationPath = key
                    });
                }
            }
        }

        private void PreviewExternal_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = PreviewExternalCheckBox.IsChecked == true;
            PreviewExternalPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            var key = LiveryPreviewTextBox.Text;
            if (!string.IsNullOrEmpty(key))
            {
                _externalLiveriesConfig.Entries.RemoveAll(entry =>
                    string.Equals(entry.DestinationPath, key, StringComparison.OrdinalIgnoreCase));

                if (isChecked)
                {
                    _externalLiveriesConfig.Entries.Add(new ExternalLiveriesEntry
                    {
                        SourcePath = PreviewSourcePathTextBox.Text,
                        DestinationPath = key
                    });
                }
            }
        }

        private void Driver1SourcePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var key = Driver1LiveryTextBox.Text;
            if (string.IsNullOrEmpty(key)) return;
            var entry = _externalLiveriesConfig.Entries.FirstOrDefault(en =>
                string.Equals(en.DestinationPath, key, StringComparison.OrdinalIgnoreCase));
            if (entry != null) entry.SourcePath = Driver1SourcePathTextBox.Text;
        }

        private void Driver2SourcePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var key = Driver2LiveryTextBox.Text;
            if (string.IsNullOrEmpty(key)) return;
            var entry = _externalLiveriesConfig.Entries.FirstOrDefault(en =>
                string.Equals(en.DestinationPath, key, StringComparison.OrdinalIgnoreCase));
            if (entry != null) entry.SourcePath = Driver2SourcePathTextBox.Text;
        }

        private void PreviewSourcePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var key = LiveryPreviewTextBox.Text;
            if (string.IsNullOrEmpty(key)) return;
            var entry = _externalLiveriesConfig.Entries.FirstOrDefault(en =>
                string.Equals(en.DestinationPath, key, StringComparison.OrdinalIgnoreCase));
            if (entry != null) entry.SourcePath = PreviewSourcePathTextBox.Text;
        }

        #endregion

        #region Import External Livery from XML

        private void ImportExternalLiveryFromXml_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                    Title = "Select AMS2 Custom AI Driver XML File"
                };

                if (dialog.ShowDialog() != true)
                    return;

                string xmlFilePath = dialog.FileName;
                string xmlDirectory = Path.GetDirectoryName(xmlFilePath);
                var xmlDoc = XDocument.Load(xmlFilePath);

                var candidates = ExternalLiveryXmlImportHelper.FindLiveryOverrideCandidates(xmlDoc);
                if (candidates.Count == 0)
                {
                    MessageBox.Show("No LIVERY_OVERRIDE nodes with NAME and LIVERY attributes found in the selected XML file.",
                        "No Liveries Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectionDialog = new LiverySelectionDialog(candidates.Select(c => c.Name).ToList());
                if (selectionDialog.ShowDialog() != true)
                    return;

                var selected = candidates[selectionDialog.SelectedIndex];

                var bodyPath = ExternalLiveryXmlImportHelper.GetTexturePath(selected.LiveryOverrideNode, "BODY");
                if (!string.IsNullOrEmpty(bodyPath))
                {
                    string fullPath = ExternalLiveryXmlImportHelper.ResolveXmlRelativePath(xmlDirectory, bodyPath);
                    string fileName = Path.GetFileName(bodyPath);
                    string destKey = $"external_liveries/car_liveries/{_teamId}/{fileName}";

                    Driver1LiveryTextBox.Text = destKey;
                    Driver2LiveryTextBox.Text = destKey;
                    _textureFiles[destKey] = fullPath;

                    string sourcePath = ExternalLiveryXmlImportHelper.ComputeExternalSourcePath(xmlDirectory, fullPath);
                    _externalLiveriesConfig.Entries.RemoveAll(en =>
                        string.Equals(en.DestinationPath, destKey, StringComparison.OrdinalIgnoreCase));
                    _externalLiveriesConfig.Entries.Add(new ExternalLiveriesEntry
                    {
                        SourcePath = sourcePath,
                        DestinationPath = destKey
                    });
                }

                var previewPath = ExternalLiveryXmlImportHelper.GetPreviewImagePath(selected.LiveryOverrideNode);
                if (!string.IsNullOrEmpty(previewPath))
                {
                    string fullPath = ExternalLiveryXmlImportHelper.ResolveXmlRelativePath(xmlDirectory, previewPath);
                    string fileName = Path.GetFileName(previewPath);
                    string destKey = $"external_liveries/previews/{_teamId}/{fileName}";

                    LiveryPreviewTextBox.Text = destKey;
                    _textureFiles[destKey] = fullPath;

                    string sourcePath = ExternalLiveryXmlImportHelper.ComputeExternalSourcePath(xmlDirectory, fullPath);
                    _externalLiveriesConfig.Entries.RemoveAll(en =>
                        string.Equals(en.DestinationPath, destKey, StringComparison.OrdinalIgnoreCase));
                    _externalLiveriesConfig.Entries.Add(new ExternalLiveriesEntry
                    {
                        SourcePath = sourcePath,
                        DestinationPath = destKey
                    });
                }

                UpdateDriver1LiverySourceLabel();
                UpdateDriver2LiverySourceLabel();
                UpdateLiveryPreviewSourceLabel();
                LoadExternalOverrideState();

                MessageBox.Show($"Successfully imported external livery '{selected.Name}' from XML file.",
                    "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing external livery from XML: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Number Placements

        private void AddNumberPlacement_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NumberPlacementDialog(_teamId, _textureFiles);
            if (dialog.ShowDialog() == true)
            {
                _numbersPlacements.Add(dialog.NumberPlacement);
                RefreshNumberPlacementsGrid();
            }
        }

        private void EditNumberPlacement_Click(object sender, RoutedEventArgs e)
        {
            if (NumberPlacementsDataGrid.SelectedItem is NumbersPlacement selected)
            {
                var dialog = new NumberPlacementDialog(_teamId, _textureFiles, selected);
                if (dialog.ShowDialog() == true)
                {
                    RefreshNumberPlacementsGrid();
                }
            }
        }

        private void RemoveNumberPlacement_Click(object sender, RoutedEventArgs e)
        {
            if (NumberPlacementsDataGrid.SelectedItem is NumbersPlacement selected)
            {
                _numbersPlacements.Remove(selected);
                RefreshNumberPlacementsGrid();
            }
        }

        private void CloneFromBaseLivery_Click(object sender, RoutedEventArgs e)
        {
            if (!_baseNumbersPlacements.Any())
            {
                MessageBox.Show("The base livery has no number placements to clone.",
                    "Nothing to Clone", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_numbersPlacements.Count > 0)
            {
                var confirm = MessageBox.Show(
                    "This will replace the current number placements for this override with a copy of the base livery's placements. Continue?",
                    "Clone from Base Livery", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            _numbersPlacements = _baseNumbersPlacements.Select(p => new NumbersPlacement
            {
                NumbersTexture = p.NumbersTexture,
                NumbersTextureDriver2 = p.NumbersTextureDriver2,
                NumberPlateWidth = p.NumberPlateWidth,
                StartingPoint = p.StartingPoint,
                NumberRotation = p.NumberRotation,
                FillColor = p.FillColor
            }).ToList();

            RefreshNumberPlacementsGrid();
        }

        private void RefreshNumberPlacementsGrid()
        {
            NumberPlacementsDataGrid.ItemsSource = null;
            NumberPlacementsDataGrid.ItemsSource = _numbersPlacements;
        }

        #endregion

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
                liveryoverride.NumbersPlacements = _numbersPlacements.Count > 0 ? _numbersPlacements.ToList() : null;
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
                           !string.IsNullOrWhiteSpace(LiveryPreviewTextBox.Text) ||
                           _numbersPlacements.Count > 0;


            if (!hasAnyData)
            {
                MessageBox.Show("Please specify at least one livery override.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}