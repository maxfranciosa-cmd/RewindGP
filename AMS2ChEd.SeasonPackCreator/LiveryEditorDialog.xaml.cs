using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models.Concrete;
using Ams2ChEd.Business.AMS2.Models;
using BCnEncoder.ImageSharp;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class LiveryEditorDialog : Window
    {
        private Ams2TeamEntry _team;
        private IEnumerable<Race> _races;
        private Dictionary<string, string> _textureFiles;
        private Dictionary<string, string> _xmlFiles;
        private string _currentBaseLiveryDriver1Path;
        private string _currentBaseLiveryDriver2Path;
        private int _seasonYear;
        private double _previewScale = 1.0; // Scale factor for preview display
        private ExternalLiveriesConfig _externalLiveriesConfig;

        // Driver outfit texture selections
        private string _selectedTorso;
        private string _selectedArms;
        private string _selectedGloves;
        private string _selectedLegs;
        private string _selectedHarness;

        public LiveryEditorDialog(Ams2TeamEntry team, IEnumerable<Race> races, Dictionary<string, string> textureFiles, Dictionary<string, string> xmlFiles, int seasonYear, ExternalLiveriesConfig externalLiveriesConfig = null)
        {
            InitializeComponent();

            _team = team;
            _races = races;
            _textureFiles = textureFiles;
            _xmlFiles = xmlFiles;
            _seasonYear = seasonYear;
            _externalLiveriesConfig = externalLiveriesConfig ?? new ExternalLiveriesConfig();

            LoadTeamData();

            // Update preview after loading data so preview is visible on open
            UpdatePreview();
        }

        private void LoadTeamData()
        {
            Ams2CarTextBox.Text = _team.Ams2Car;
            BaseLiveryDriver1TextBox.Text = _team.BaseLiveryDriver1;
            BaseLiveryDriver2TextBox.Text = _team.BaseLiveryDriver2;
            HelmetSponsorsTextBox.Text = _team.HelmetSponsors;
            VisorSponsorsTextBox.Text = _team.VisorSponsors;
            LiveryPreviewTextBox.Text = _team.LiveryPreview;

            // Load XML content from dictionary
            if (_xmlFiles.ContainsKey(_team.TeamId))
            {
                XmlContentTextBox.Text = _xmlFiles[_team.TeamId];
            }
            else
            {
                XmlContentTextBox.Text = "";
            }

            // Resolve file paths for preview from texture files dictionary
            if (!string.IsNullOrEmpty(_team.BaseLiveryDriver1))
            {
                var key = $"car_liveries/{_team.TeamId}/{_team.BaseLiveryDriver1}";
                if (_textureFiles.ContainsKey(key))
                {
                    _currentBaseLiveryDriver1Path = _textureFiles[key];
                }
            }

            if (!string.IsNullOrEmpty(_team.BaseLiveryDriver2))
            {
                var key = $"car_liveries/{_team.TeamId}/{_team.BaseLiveryDriver2}";
                if (_textureFiles.ContainsKey(key))
                {
                    _currentBaseLiveryDriver2Path = _textureFiles[key];
                }
            }

            if (_team.NumbersPlacements == null)
            {
                _team.NumbersPlacements = new List<NumbersPlacement>();
            }
            NumberPlacementsDataGrid.ItemsSource = _team.NumbersPlacements;

            if (_team.LiveryOverrides == null)
            {
                _team.LiveryOverrides = new List<LiveryOverride>();
            }
            LiveryOverridesDataGrid.ItemsSource = _team.LiveryOverrides;

            // Update all source labels
            UpdateBaseLiveryDriver1SourceLabel();
            UpdateBaseLiveryDriver2SourceLabel();
            UpdateHelmetSponsorsSourceLabel();
            UpdateVisorSponsorsSourceLabel();
            UpdateLiveryPreviewSourceLabel();

            // Load driver outfit texture options
            LoadDriverOutfitTextures();

            // Parse existing XML to populate selections
            ParseExistingXmlForOutfit();

            // Restore external livery state from config
            LoadExternalLiveryState();

            // Populate preview source combobox (base + race overrides)
            RefreshPreviewSourceComboBox();
        }

        private void LoadExternalLiveryState()
        {
            var driver1Key = BaseLiveryDriver1TextBox.Text;
            if (!string.IsNullOrEmpty(driver1Key))
            {
                var entry = _externalLiveriesConfig.Entries.FirstOrDefault(e =>
                    string.Equals(e.DestinationPath, driver1Key, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    BaseLiveryDriver1ExternalCheckBox.IsChecked = true;
                    BaseLiveryDriver1ExternalPanel.Visibility = Visibility.Visible;
                    BaseLiveryDriver1SourcePathTextBox.Text = entry.SourcePath ?? "";
                }
            }

            var driver2Key = BaseLiveryDriver2TextBox.Text;
            if (!string.IsNullOrEmpty(driver2Key))
            {
                var entry = _externalLiveriesConfig.Entries.FirstOrDefault(e =>
                    string.Equals(e.DestinationPath, driver2Key, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    BaseLiveryDriver2ExternalCheckBox.IsChecked = true;
                    BaseLiveryDriver2ExternalPanel.Visibility = Visibility.Visible;
                    BaseLiveryDriver2SourcePathTextBox.Text = entry.SourcePath ?? "";
                }
            }

            var previewKey = LiveryPreviewTextBox.Text;
            if (!string.IsNullOrEmpty(previewKey))
            {
                var entry = _externalLiveriesConfig.Entries.FirstOrDefault(e =>
                    string.Equals(e.DestinationPath, previewKey, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    LiveryPreviewExternalCheckBox.IsChecked = true;
                    LiveryPreviewExternalPanel.Visibility = Visibility.Visible;
                    LiveryPreviewSourcePathTextBox.Text = entry.SourcePath ?? "";
                }
            }

            UpdateExternalBadge();
        }

        private void UpdateExternalBadge()
        {
            if (ExternalLiveryBadge == null) return;

            string key;
            if (PreviewSourceComboBox?.SelectedIndex == 0)
                key = BaseLiveryDriver1TextBox?.Text;
            else if (PreviewSourceComboBox?.SelectedIndex == 1)
                key = BaseLiveryDriver2TextBox?.Text;
            else if (PreviewSourceComboBox?.SelectedItem is ComboBoxItem { Tag: string overridePath })
                key = overridePath;
            else
                key = null;

            bool isExternal = !string.IsNullOrEmpty(key) &&
                _externalLiveriesConfig.Entries.Any(e =>
                    string.Equals(e.DestinationPath, key, StringComparison.OrdinalIgnoreCase));

            ExternalLiveryBadge.Visibility = isExternal ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshPreviewSourceComboBox()
        {
            int savedIndex = PreviewSourceComboBox.SelectedIndex;
            PreviewSourceComboBox.SelectionChanged -= PreviewSource_Changed;
            PreviewSourceComboBox.Items.Clear();

            PreviewSourceComboBox.Items.Add(new ComboBoxItem { Content = "Base Livery Driver 1" });
            PreviewSourceComboBox.Items.Add(new ComboBoxItem { Content = "Base Livery Driver 2" });

            if (_team.LiveryOverrides != null)
            {
                foreach (var ovr in _team.LiveryOverrides)
                {
                    var raceName = _races?.FirstOrDefault(r => r.RaceId == ovr.RaceId)?.RaceName
                                   ?? $"Race {ovr.RaceId}";

                    if (!string.IsNullOrEmpty(ovr.Driver1Livery))
                        PreviewSourceComboBox.Items.Add(new ComboBoxItem
                            { Content = $"{raceName} Livery Driver 1", Tag = ovr.Driver1Livery });

                    if (!string.IsNullOrEmpty(ovr.Driver2Livery))
                        PreviewSourceComboBox.Items.Add(new ComboBoxItem
                            { Content = $"{raceName} Livery Driver 2", Tag = ovr.Driver2Livery });
                }
            }

            PreviewSourceComboBox.SelectionChanged += PreviewSource_Changed;
            PreviewSourceComboBox.SelectedIndex = Math.Max(0,
                Math.Min(savedIndex, PreviewSourceComboBox.Items.Count - 1));
        }

        private void UpdateBaseLiveryDriver1SourceLabel()
        {
            if (!string.IsNullOrWhiteSpace(BaseLiveryDriver1TextBox.Text))
            {
                var relativePath = BaseLiveryDriver1TextBox.Text;
                if (_textureFiles.ContainsKey(relativePath))
                {
                    BaseLiveryDriver1SourceLabel.Text = $"Source: {_textureFiles[relativePath]}";
                }
                else
                {
                    BaseLiveryDriver1SourceLabel.Text = "";
                }
            }
            else
            {
                BaseLiveryDriver1SourceLabel.Text = "";
            }
        }

        private void UpdateBaseLiveryDriver2SourceLabel()
        {
            if (!string.IsNullOrWhiteSpace(BaseLiveryDriver2TextBox.Text))
            {
                var relativePath = BaseLiveryDriver2TextBox.Text;
                if (_textureFiles.ContainsKey(relativePath))
                {
                    BaseLiveryDriver2SourceLabel.Text = $"Source: {_textureFiles[relativePath]}";
                }
                else
                {
                    BaseLiveryDriver2SourceLabel.Text = "";
                }
            }
            else
            {
                BaseLiveryDriver2SourceLabel.Text = "";
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

        #region File Browsing

        private void BrowseBaseLiveryDriver1_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Base Livery for Driver 1", "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                (path, filename) =>
                {
                    _team.BaseLiveryDriver1 = $"car_liveries/{_team.TeamId}/default_1.png";
                    BaseLiveryDriver1TextBox.Text = $"car_liveries/{_team.TeamId}/default_1.png";
                    var relativePath = $"car_liveries/{_team.TeamId}/default_1.png";
                    _textureFiles[relativePath] = path;
                    _currentBaseLiveryDriver1Path = path;

                    // Update source label
                    UpdateBaseLiveryDriver1SourceLabel();

                    // Update preview if Driver 1 is selected
                    if (PreviewSourceComboBox.SelectedIndex == 0)
                    {
                        UpdatePreview();
                    }
                });
        }

        private void BrowseBaseLiveryDriver2_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Base Livery for Driver 2", "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                (path, filename) =>
                {
                    _team.BaseLiveryDriver2 = $"car_liveries/{_team.TeamId}/default_2.png";
                    BaseLiveryDriver2TextBox.Text = $"car_liveries/{_team.TeamId}/default_2.png";
                    var relativePath = $"car_liveries/{_team.TeamId}/default_2.png";
                    _textureFiles[relativePath] = path;
                    _currentBaseLiveryDriver2Path = path;

                    // Update source label
                    UpdateBaseLiveryDriver2SourceLabel();

                    // Update preview if Driver 2 is selected
                    if (PreviewSourceComboBox.SelectedIndex == 1)
                    {
                        UpdatePreview();
                    }
                });
        }

        private void BrowseHelmetSponsors_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Helmet Sponsors Overlay", "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                (path, filename) =>
                {
                    _team.HelmetSponsors = $"helmet_sponsors/{_team.TeamId}/default.png";
                    HelmetSponsorsTextBox.Text = $"helmet_sponsors/{_team.TeamId}/default.png";
                    var relativePath = $"helmet_sponsors/{_team.TeamId}/default.png";
                    _textureFiles[relativePath] = path;

                    // Update source label
                    UpdateHelmetSponsorsSourceLabel();
                });
        }

        private void BrowseVisorSponsors_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Visor Sponsors Overlay", "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                (path, filename) =>
                {
                    _team.VisorSponsors = $"helmet_sponsors/{_team.TeamId}/default_visor.png";
                    VisorSponsorsTextBox.Text = $"helmet_sponsors/{_team.TeamId}/default_visor.png";
                    var relativePath = $"helmet_sponsors/{_team.TeamId}/default_visor.png";
                    _textureFiles[relativePath] = path;

                    // Update source label
                    UpdateVisorSponsorsSourceLabel();
                });
        }

        private void BrowseLiveryPreview_Click(object sender, RoutedEventArgs e)
        {
            BrowseForFile("Select Livery Preview Image (for AMS2 menus)", "Image files (*.dds;*.png)|*.dds;*.png",
                (path, filename) =>
                {
                    _team.LiveryPreview = $"previews/{_team.TeamId}/default.dds";
                    LiveryPreviewTextBox.Text = $"previews/{_team.TeamId}/default.dds";
                    var relativePath = $"previews/{_team.TeamId}/default.dds";
                    _textureFiles[relativePath] = path;

                    // Update source label
                    UpdateLiveryPreviewSourceLabel();
                });
        }

        #region External Livery State

        private void BaseLiveryDriver1External_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = BaseLiveryDriver1ExternalCheckBox.IsChecked == true;
            BaseLiveryDriver1ExternalPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            var key = BaseLiveryDriver1TextBox.Text;
            if (!string.IsNullOrEmpty(key))
            {
                _externalLiveriesConfig.Entries.RemoveAll(entry =>
                    string.Equals(entry.DestinationPath, key, StringComparison.OrdinalIgnoreCase));

                if (isChecked)
                {
                    _externalLiveriesConfig.Entries.Add(new ExternalLiveriesEntry
                    {
                        SourcePath = BaseLiveryDriver1SourcePathTextBox.Text,
                        DestinationPath = key
                    });
                }
            }

            UpdateExternalBadge();
        }

        private void BaseLiveryDriver2External_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = BaseLiveryDriver2ExternalCheckBox.IsChecked == true;
            BaseLiveryDriver2ExternalPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            var key = BaseLiveryDriver2TextBox.Text;
            if (!string.IsNullOrEmpty(key))
            {
                _externalLiveriesConfig.Entries.RemoveAll(entry =>
                    string.Equals(entry.DestinationPath, key, StringComparison.OrdinalIgnoreCase));

                if (isChecked)
                {
                    _externalLiveriesConfig.Entries.Add(new ExternalLiveriesEntry
                    {
                        SourcePath = BaseLiveryDriver2SourcePathTextBox.Text,
                        DestinationPath = key
                    });
                }
            }

            UpdateExternalBadge();
        }

        private void BaseLiveryDriver1SourcePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var key = BaseLiveryDriver1TextBox.Text;
            if (string.IsNullOrEmpty(key)) return;
            var entry = _externalLiveriesConfig.Entries.FirstOrDefault(en =>
                string.Equals(en.DestinationPath, key, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
                entry.SourcePath = BaseLiveryDriver1SourcePathTextBox.Text;
        }

        private void BaseLiveryDriver2SourcePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var key = BaseLiveryDriver2TextBox.Text;
            if (string.IsNullOrEmpty(key)) return;
            var entry = _externalLiveriesConfig.Entries.FirstOrDefault(en =>
                string.Equals(en.DestinationPath, key, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
                entry.SourcePath = BaseLiveryDriver2SourcePathTextBox.Text;
        }

        private void LiveryPreviewExternal_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = LiveryPreviewExternalCheckBox.IsChecked == true;
            LiveryPreviewExternalPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            var key = LiveryPreviewTextBox.Text;
            if (!string.IsNullOrEmpty(key))
            {
                _externalLiveriesConfig.Entries.RemoveAll(entry =>
                    string.Equals(entry.DestinationPath, key, StringComparison.OrdinalIgnoreCase));

                if (isChecked)
                {
                    _externalLiveriesConfig.Entries.Add(new ExternalLiveriesEntry
                    {
                        SourcePath = LiveryPreviewSourcePathTextBox.Text,
                        DestinationPath = key
                    });
                }
            }
        }

        private void LiveryPreviewSourcePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var key = LiveryPreviewTextBox.Text;
            if (string.IsNullOrEmpty(key)) return;
            var entry = _externalLiveriesConfig.Entries.FirstOrDefault(en =>
                string.Equals(en.DestinationPath, key, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
                entry.SourcePath = LiveryPreviewSourcePathTextBox.Text;
        }

        #endregion

        private void BrowseForFile(string title, string filter, Action<string, string> onSelected)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = title
            };

            if (dialog.ShowDialog() == true)
            {
                onSelected(dialog.FileName, System.IO.Path.GetFileName(dialog.FileName));
            }
        }

        #endregion

        #region Number Placements

        private void NumberPlacementsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void AddNumberPlacement_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NumberPlacementDialog(_team.TeamId, _textureFiles);
            if (dialog.ShowDialog() == true)
            {
                var placements = _team.NumbersPlacements.ToList();
                placements.Add(dialog.NumberPlacement);
                _team.NumbersPlacements = placements;
                NumberPlacementsDataGrid.ItemsSource = null;
                NumberPlacementsDataGrid.ItemsSource = _team.NumbersPlacements;
                UpdatePreview();
            }
        }

        private void EditNumberPlacement_Click(object sender, RoutedEventArgs e)
        {
            if (NumberPlacementsDataGrid.SelectedItem is NumbersPlacement selected)
            {
                var dialog = new NumberPlacementDialog(_team.TeamId, _textureFiles, selected);
                if (dialog.ShowDialog() == true)
                {
                    NumberPlacementsDataGrid.Items.Refresh();
                    UpdatePreview();
                }
            }
        }

        private void RemoveNumberPlacement_Click(object sender, RoutedEventArgs e)
        {
            if (NumberPlacementsDataGrid.SelectedItem is NumbersPlacement selected)
            {
                var placements = _team.NumbersPlacements.ToList();
                placements.Remove(selected);
                _team.NumbersPlacements = placements;
                NumberPlacementsDataGrid.ItemsSource = null;
                NumberPlacementsDataGrid.ItemsSource = _team.NumbersPlacements;
                UpdatePreview();
            }
        }

        #endregion

        #region Livery Overrides

        private void AddLiveryOverride_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LiveryOverrideDialog(_races, _textureFiles, _team.TeamId, _externalLiveriesConfig);
            if (dialog.ShowDialog() == true)
            {
                var overrides = _team.LiveryOverrides.ToList();
                overrides.AddRange(dialog.LiveryOverrides);
                _team.LiveryOverrides = overrides;
                LiveryOverridesDataGrid.ItemsSource = null;
                LiveryOverridesDataGrid.ItemsSource = _team.LiveryOverrides;
                RefreshPreviewSourceComboBox();
            }
        }

        private void EditLiveryOverride_Click(object sender, RoutedEventArgs e)
        {
            if (LiveryOverridesDataGrid.SelectedItem is LiveryOverride selected)
            {
                var dialog = new LiveryOverrideDialog(_races, _textureFiles, _team.TeamId, _externalLiveriesConfig, selected);
                if (dialog.ShowDialog() == true)
                {
                    var overrides = _team.LiveryOverrides.ToList();
                    overrides.AddRange(dialog.LiveryOverrides.Where(lo => lo != selected));
                    _team.LiveryOverrides = overrides;
                    LiveryOverridesDataGrid.ItemsSource = null;
                    LiveryOverridesDataGrid.ItemsSource = _team.LiveryOverrides;
                    RefreshPreviewSourceComboBox();
                }
            }
        }

        private void RemoveLiveryOverride_Click(object sender, RoutedEventArgs e)
        {
            if (LiveryOverridesDataGrid.SelectedItem is LiveryOverride selected)
            {
                var overrides = _team.LiveryOverrides.ToList();
                overrides.Remove(selected);
                _team.LiveryOverrides = overrides;
                LiveryOverridesDataGrid.ItemsSource = null;
                LiveryOverridesDataGrid.ItemsSource = _team.LiveryOverrides;
                RefreshPreviewSourceComboBox();
            }
        }

        #endregion

        #region Preview Rendering

        private void PreviewSource_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
            UpdateExternalBadge();
        }

        private void TestNumber_Changed(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            // Check if controls are initialized
            if (PreviewSourceComboBox == null || LiveryPreviewImage == null || LiveryPreviewCanvas == null)
            {
                return;
            }

            if (_team != null && !string.IsNullOrEmpty(_team.BaseLiveryDriver1)) _textureFiles.TryGetValue(_team?.BaseLiveryDriver1, out _currentBaseLiveryDriver1Path);
            if (_team != null && !string.IsNullOrEmpty(_team.BaseLiveryDriver2)) _textureFiles.TryGetValue(_team?.BaseLiveryDriver2, out _currentBaseLiveryDriver2Path);

            // Determine which livery to preview
            string previewPath;
            if (PreviewSourceComboBox.SelectedIndex == 0)
                previewPath = _currentBaseLiveryDriver1Path;
            else if (PreviewSourceComboBox.SelectedIndex == 1)
                previewPath = _currentBaseLiveryDriver2Path;
            else if (PreviewSourceComboBox.SelectedItem is ComboBoxItem { Tag: string relativePath })
                _textureFiles.TryGetValue(relativePath, out previewPath);
            else
                previewPath = null;

            if (string.IsNullOrEmpty(previewPath) || !System.IO.File.Exists(previewPath))
            {
                LiveryPreviewImage.Source = null;
                return;
            }

            try
            {
                // Load the base livery image
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(previewPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                // Calculate scale to fit in preview area while maintaining aspect ratio
                // Use larger maximum size for better preview quality
                const double maxPreviewWidth = 2048.0;
                const double maxPreviewHeight = 2048.0;

                double originalWidth = bitmap.PixelWidth;
                double originalHeight = bitmap.PixelHeight;

                double scaleX = maxPreviewWidth / originalWidth;
                double scaleY = maxPreviewHeight / originalHeight;
                _previewScale = Math.Min(scaleX, scaleY);

                // Allow scaling up to 2x for small images
                if (_previewScale > 2.0)
                    _previewScale = 2.0;

                double scaledWidth = originalWidth * _previewScale;
                double scaledHeight = originalHeight * _previewScale;

                LiveryPreviewImage.Source = bitmap;
                LiveryPreviewImage.Width = scaledWidth;
                LiveryPreviewImage.Height = scaledHeight;

                // Set canvas size to scaled image size
                LiveryPreviewCanvas.Width = scaledWidth;
                LiveryPreviewCanvas.Height = scaledHeight;

                // Clear existing number overlays
                LiveryPreviewCanvas.Children.Clear();
                LiveryPreviewCanvas.Children.Add(LiveryPreviewImage);

                // Explicitly position the image at (0,0) in the canvas
                Canvas.SetLeft(LiveryPreviewImage, 0);
                Canvas.SetTop(LiveryPreviewImage, 0);

                // Render number placements
                if (_team.NumbersPlacements != null && int.TryParse(TestNumberTextBox.Text, out int testNumber))
                {
                    foreach (var placement in _team.NumbersPlacements)
                    {
                        RenderNumberPlacement(placement, testNumber.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rendering preview: {ex.Message}", "Preview Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RenderNumberPlacement(NumbersPlacement placement, string number)
        {
            // Create a visual representation of where the number would be placed

            var textureHeight = GetTextureHeight(placement.NumbersTexture);

            // Apply scaling to dimensions
            var scaledWidth = placement.NumberPlateWidth * _previewScale;
            var scaledHeight = textureHeight * _previewScale;

            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = scaledWidth,
                Height = scaledHeight,
                Fill = new SolidColorBrush(Colors.Yellow),
                Opacity = 0.3,
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeThickness = 2
            };

            // Calculate position so the correct corner is at StartingPoint
            // Apply scale factor to positions
            double rectLeft, rectTop;
            var degrees = (int)placement.NumberRotation;

            switch (degrees)
            {
                case 0:
                    // StartingPoint is top-left corner
                    rectLeft = placement.StartingPoint.X * _previewScale;
                    rectTop = placement.StartingPoint.Y * _previewScale;
                    break;
                case 90:
                    // StartingPoint is top-right corner
                    rectLeft = (placement.StartingPoint.X * _previewScale) - scaledHeight;
                    rectTop = placement.StartingPoint.Y * _previewScale;
                    rect.Height = scaledWidth;
                    rect.Width = scaledHeight;
                    break;
                case 180:
                    // StartingPoint is bottom-right corner
                    rectLeft = (placement.StartingPoint.X * _previewScale) - scaledWidth;
                    rectTop = (placement.StartingPoint.Y * _previewScale) - scaledHeight;
                    break;
                case 270:
                    // StartingPoint is bottom-left corner
                    rectLeft = placement.StartingPoint.X * _previewScale;
                    rectTop = (placement.StartingPoint.Y * _previewScale) - scaledWidth;
                    rect.Height = scaledWidth;
                    rect.Width = scaledHeight;
                    break;
                default:
                    // Default to top-left
                    rectLeft = placement.StartingPoint.X * _previewScale;
                    rectTop = placement.StartingPoint.Y * _previewScale;
                    break;
            }

            Canvas.SetLeft(rect, rectLeft);
            Canvas.SetTop(rect, rectTop);

            LiveryPreviewCanvas.Children.Add(rect);
        }


        private int GetTextureHeight(string textureRelativePath)
        {
            // Default height if we can't determine from texture
            int defaultHeight = 40;

            if (string.IsNullOrWhiteSpace(textureRelativePath))
                return defaultHeight;

            // Check if this texture file is in our tracking dictionary
            if (_textureFiles.ContainsKey(textureRelativePath))
            {
                var filePath = _textureFiles[textureRelativePath];
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp")
                {
                    // Use System.Drawing for common image formats
                    using (var img = System.Drawing.Image.FromFile(filePath))
                    {
                        return img.Height;
                    }
                }
                else if (extension == ".dds")
                {
                    // Use BCnEncoder for DDS files
                    using (var fs = File.OpenRead(filePath))
                    {
                        var decoder = new BCnEncoder.Decoder.BcDecoder();
                        using (var image = decoder.DecodeToImageRgba32(fs))
                        {
                            return image.Height;
                        }
                    }
                }
            }

            return defaultHeight;
        }

        private void LiveryPreviewCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Check if a number placement is selected
            if (NumberPlacementsDataGrid.SelectedItem is NumbersPlacement selectedPlacement)
            {
                // Get the click position relative to the Image element (not the Canvas)
                // This ensures we're getting the correct coordinates even if there's any offset
                var clickPosition = e.GetPosition(LiveryPreviewImage);

                // Convert scaled coordinates back to original image coordinates
                int originalX = (int)(clickPosition.X / _previewScale);
                int originalY = (int)(clickPosition.Y / _previewScale);

                // Update the selected placement's coordinates with original image coordinates
                selectedPlacement.StartingPoint = new System.Drawing.Point(originalX, originalY);

                // Refresh the data grid to show updated values
                NumberPlacementsDataGrid.Items.Refresh();

                // Update the preview to show new position
                UpdatePreview();
            }
        }

        #endregion

        #region Import from Livery XML

        private void ImportFromLiveryXml_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ask user to select livery XML file
                var dialog = new OpenFileDialog
                {
                    Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                    Title = "Select AMS2 Livery XML File"
                };

                if (dialog.ShowDialog() != true)
                    return;

                string xmlFilePath = dialog.FileName;
                string xmlDirectory = Path.GetDirectoryName(xmlFilePath);

                // Load and parse XML
                var xmlDoc = XDocument.Load(xmlFilePath);

                // Find all LIVERY_OVERRIDE nodes
                var liveryOverrides = xmlDoc.Descendants("LIVERY_OVERRIDE")
                    .Where(node => node.Attribute("NAME") != null)
                    .ToList();

                if (liveryOverrides.Count == 0)
                {
                    MessageBox.Show("No LIVERY_OVERRIDE nodes found in the selected XML file.",
                        "No Liveries Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show selection dialog
                var liveryNames = liveryOverrides.Select(node => node.Attribute("NAME").Value).ToList();
                var selectionDialog = new LiverySelectionDialog(liveryNames);

                if (selectionDialog.ShowDialog() != true)
                    return;

                int selectedIndex = selectionDialog.SelectedIndex;
                var selectedLiveryNode = liveryOverrides[selectedIndex];
                string selectedName = selectedLiveryNode.Attribute("NAME").Value;
                string liveryValue = selectedLiveryNode.Attribute("LIVERY")?.Value;

                // Build XML content with selected nodes
                BuildXmlContentAndSetHelmetAndVisorSponsor(xmlDoc, selectedName, liveryValue, xmlDirectory);

                // Set base livery paths
                SetBaseLiveryPaths(selectedLiveryNode, xmlDirectory);

                // Set livery preview
                SetLiveryPreview(selectedLiveryNode, xmlDirectory);

                MessageBox.Show($"Successfully imported livery '{selectedName}' from XML file.",
                    "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing from livery XML: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildXmlContentAndSetHelmetAndVisorSponsor(XDocument xmlDoc, string selectedName, string liveryValue, string xmlDirectory)
        {
            var xmlContent = new XDocument(new XDeclaration("1.0", "UTF-8", null));
            var root = new XElement("LIVERY_OVERRIDES");

            // 1. Add the selected LIVERY_OVERRIDE node
            var liveryOverrideNode = xmlDoc.Descendants("LIVERY_OVERRIDE")
                .FirstOrDefault(node => node.Attribute("NAME")?.Value == selectedName);

            if (liveryOverrideNode != null)
            {
                root.Add(new XElement(liveryOverrideNode));
            }

            // 2. Add HELMET_OVERRIDE node with matching LIVERY attribute
            if (!string.IsNullOrEmpty(liveryValue))
            {

                var helmetOverrideNode = xmlDoc.Descendants("HELMET_OVERRIDE")
                    .FirstOrDefault(node => node.Attribute("LIVERY")?.Value == liveryValue);

                if (helmetOverrideNode != null)
                {
                    root.Add(new XElement(helmetOverrideNode));

                    var helmetBodyDiff = helmetOverrideNode.Descendants("TEXTURE")
                        .FirstOrDefault(t => t.Attribute("NAME")?.Value == "BODY_DIFF");
                    var helmetPathAttr = helmetBodyDiff.Attribute("PATH");
                    if (helmetPathAttr != null)
                    {
                        string relativePath = helmetPathAttr.Value;
                        string fullPath = Path.Combine(xmlDirectory, relativePath);
                        string fileName = Path.GetFileName(fullPath);

                        // Set both driver liveries to the same file
                        _team.HelmetSponsors = $"helmet_sponsors/{_team.TeamId}/default.png";
                        HelmetSponsorsTextBox.Text = $"helmet_sponsors/{_team.TeamId}/default.png";

                        // Track the file for export
                        var exportPath = $"helmet_sponsors/{_team.TeamId}/default.png";
                        _textureFiles[exportPath] = fullPath;
                    }

                    var visorDiff = helmetOverrideNode.Descendants("TEXTURE")
                        .FirstOrDefault(t => t.Attribute("NAME")?.Value == "VISOR_DIFF");
                    var visorPathAttr = visorDiff?.Attribute("PATH");
                    if (visorPathAttr != null)
                    {
                        string relativePath = visorPathAttr.Value;
                        string fullPath = Path.Combine(xmlDirectory, relativePath);
                        string fileName = Path.GetFileName(fullPath);

                        // Set both driver liveries to the same file
                        _team.HelmetSponsors = $"helmet_sponsors/{_team.TeamId}/default_visor.png";
                        VisorSponsorsTextBox.Text = $"helmet_sponsors/{_team.TeamId}/default_visor.png";

                        // Track the file for export
                        var exportPath = $"helmet_sponsors/{_team.TeamId}/default_visor.png";
                        _textureFiles[exportPath] = fullPath;
                    }
                }
            }

            // 3. Add OUTFIT_OVERRIDE node with matching LIVERY attribute
            if (!string.IsNullOrEmpty(liveryValue))
            {
                var outfitOverrideNode = xmlDoc.Descendants("OUTFIT_OVERRIDE")
                    .FirstOrDefault(node => node.Attribute("LIVERY")?.Value == liveryValue);

                if (outfitOverrideNode != null)
                {
                    root.Add(new XElement(outfitOverrideNode));
                }
            }

            xmlContent.Add(root);

            XmlContentTextBox.Text = root.ToString();
        }

        private void SetBaseLiveryPaths(XElement liveryOverrideNode, string xmlDirectory)
        {
            // Find TEXTURE node with NAME="BODY"
            var bodyTexture = liveryOverrideNode.Descendants("TEXTURE")
                .FirstOrDefault(t => t.Attribute("NAME")?.Value == "BODY");

            if (bodyTexture != null)
            {
                var pathAttr = bodyTexture.Attribute("PATH");
                if (pathAttr != null)
                {
                    string relativePath = pathAttr.Value;
                    string fullPath = Path.Combine(xmlDirectory, relativePath);
                    string fileName = Path.GetFileName(fullPath);

                    // Set both driver liveries to the same file
                    _team.BaseLiveryDriver1 = $"car_liveries/{_team.TeamId}/default.png";
                    _team.BaseLiveryDriver2 = $"car_liveries/{_team.TeamId}/default.png";
                    BaseLiveryDriver1TextBox.Text = $"car_liveries/{_team.TeamId}/default.png";
                    BaseLiveryDriver2TextBox.Text = $"car_liveries/{_team.TeamId}/default.png";

                    // Track the file for export
                    var exportPath = $"car_liveries/{_team.TeamId}/default.png";
                    _textureFiles[exportPath] = fullPath;

                    // Store paths for preview
                    _currentBaseLiveryDriver1Path = fullPath;
                    _currentBaseLiveryDriver2Path = fullPath;
                }
            }
        }

        private void SetLiveryPreview(XElement liveryOverrideNode, string xmlDirectory)
        {
            // Find PREVIEWIMAGE node
            var previewImageNode = liveryOverrideNode.Descendants("PREVIEWIMAGE").FirstOrDefault();

            if (previewImageNode != null)
            {
                var pathAttr = previewImageNode.Attribute("PATH");
                if (pathAttr != null)
                {
                    string relativePath = pathAttr.Value;
                    string fullPath = Path.Combine(xmlDirectory, relativePath);
                    string fileName = Path.GetFileName(fullPath);

                    _team.LiveryPreview = $"previews/{_team.TeamId}/default.dds";
                    LiveryPreviewTextBox.Text = $"previews/{_team.TeamId}/default.dds";

                    // Track the file for export
                    var exportPath = $"previews/{_team.TeamId}/default.dds";
                    _textureFiles[exportPath] = fullPath;
                }
            }
        }

        #endregion

        #region Driver Outfit Textures

        private void LoadDriverOutfitTextures()
        {
            LoadTextureOptions("Torso", TorsoOptionsPanel);
            LoadTextureOptions("Arms", ArmsOptionsPanel);
            LoadTextureOptions("Gloves", GlovesOptionsPanel);
            LoadTextureOptions("Legs", LegsOptionsPanel);
            LoadTextureOptions("Harness", HarnessOptionsPanel);
        }

        private void LoadTextureOptions(string bodyPart, WrapPanel panel)
        {
            panel.Children.Clear();

            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleBodies", bodyPart);

            if (!Directory.Exists(folderPath))
            {
                var noFilesLabel = new TextBlock
                {
                    Text = $"No {bodyPart.ToLower()} textures found in {folderPath}",
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(5)
                };
                panel.Children.Add(noFilesLabel);
                return;
            }

            var ddsFiles = Directory.GetFiles(folderPath, "*.dds");

            if (ddsFiles.Length == 0)
            {
                var noFilesLabel = new TextBlock
                {
                    Text = $"No .dds files found in {bodyPart}",
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(5)
                };
                panel.Children.Add(noFilesLabel);
                return;
            }

            // Create a group name for radio buttons
            string groupName = $"{bodyPart}Group";

            foreach (var filePath in ddsFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                var radioButton = new RadioButton
                {
                    Content = fileName,
                    GroupName = groupName,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 2, 15, 2),
                    Tag = Path.GetFileName(filePath)
                };

                radioButton.Checked += (s, e) => OutfitTexture_Changed(bodyPart, (string)radioButton.Tag);

                panel.Children.Add(radioButton);
            }
        }

        private void OutfitTexture_Changed(string bodyPart, string fileName)
        {
            // Update the selected texture for this body part
            switch (bodyPart)
            {
                case "Torso":
                    _selectedTorso = fileName;
                    break;
                case "Arms":
                    _selectedArms = fileName;
                    break;
                case "Gloves":
                    _selectedGloves = fileName;
                    break;
                case "Legs":
                    _selectedLegs = fileName;
                    break;
                case "Harness":
                    _selectedHarness = fileName;
                    break;
            }

            // Regenerate XML content
            GenerateXmlContent();
        }

        private void GenerateXmlContent()
        {
            // Only generate if we have at least one selection
            if (string.IsNullOrEmpty(_selectedTorso) && string.IsNullOrEmpty(_selectedArms) &&
                string.IsNullOrEmpty(_selectedGloves) && string.IsNullOrEmpty(_selectedLegs) &&
                string.IsNullOrEmpty(_selectedHarness))
            {
                return;
            }

            // Build the new OUTFIT_OVERRIDE element
            var outfitOverride = new XElement("OUTFIT_OVERRIDE",
                new XAttribute("LIVERY", "51"),
                new XAttribute("BASEOUTFIT", "DEFAULT"));

            outfitOverride.Add(new XElement("TEXTURE",
                new XAttribute("NAME", "BODY_DIFF"),
                new XAttribute("PATH", _selectedTorso != null ? $"Driver\\Torso\\{_selectedTorso}" : "")));

            outfitOverride.Add(new XElement("TEXTURE",
                new XAttribute("NAME", "ARMS_DIFF"),
                new XAttribute("PATH", _selectedArms != null ? $"Driver\\Arms\\{_selectedArms}" : "")));

            outfitOverride.Add(new XElement("TEXTURE",
                new XAttribute("NAME", "GLOVES_DIFF"),
                new XAttribute("PATH", _selectedGloves != null ? $"Driver\\Gloves\\{_selectedGloves}" : "")));

            outfitOverride.Add(new XElement("TEXTURE",
                new XAttribute("NAME", "LEGS_DIFF"),
                new XAttribute("PATH", _selectedLegs != null ? $"Driver\\Legs\\{_selectedLegs}" : "")));

            outfitOverride.Add(new XElement("TEXTURE",
                new XAttribute("NAME", "SEATBELTS_DIFF"),
                new XAttribute("PATH", _selectedHarness != null ? $"Driver\\Harness\\{_selectedHarness}" : "")));

            if (string.IsNullOrWhiteSpace(XmlContentTextBox.Text))
            {
                // Textbox is empty — generate the full XML from scratch
                var root = new XElement("USER_OVERRIDES");

                var liveryOverride = new XElement("LIVERY_OVERRIDE",
                    new XAttribute("LIVERY", ""),
                    new XAttribute("NAME", ""),
                    new XAttribute("BASELIVERY", "Default"));

                liveryOverride.Add(new XElement("PREVIEWIMAGE", new XAttribute("PATH", "")));
                liveryOverride.Add(new XElement("TEXTURE",
                    new XAttribute("NAME", "BODY"),
                    new XAttribute("PATH", "")));

                root.Add(liveryOverride);

                var helmetOverride = new XElement("HELMET_OVERRIDE",
                    new XAttribute("LIVERY", "51"),
                    new XAttribute("BASEHELMET", "DEFAULT"));

                helmetOverride.Add(new XElement("TEXTURE",
                    new XAttribute("NAME", "BODY_DIFF"),
                    new XAttribute("PATH", "")));
                helmetOverride.Add(new XElement("TEXTURE",
                    new XAttribute("NAME", "VISOR_DIFF"),
                    new XAttribute("PATH", "")));

                root.Add(helmetOverride);
                root.Add(outfitOverride);

                XmlContentTextBox.Text = root.ToString();
            }
            else
            {
                // Textbox already has XML — replace only the OUTFIT_OVERRIDE element
                try
                {
                    var xmlDoc = XDocument.Parse(XmlContentTextBox.Text);
                    var existing = xmlDoc.Descendants("OUTFIT_OVERRIDE").FirstOrDefault();

                    if (existing != null)
                    {
                        existing.ReplaceWith(outfitOverride);
                    }
                    else
                    {
                        // No OUTFIT_OVERRIDE present yet — append it to the root
                        xmlDoc.Root?.Add(outfitOverride);
                    }

                    XmlContentTextBox.Text = xmlDoc.ToString();
                }
                catch
                {
                    // If the existing XML is malformed, fall back to overwriting entirely
                    XmlContentTextBox.Text = new XElement("USER_OVERRIDES", outfitOverride).ToString();
                }
            }
        }

        private void ParseExistingXmlForOutfit()
        {
            if (string.IsNullOrWhiteSpace(XmlContentTextBox.Text))
            {
                return;
            }

            try
            {
                var xmlDoc = XDocument.Parse(XmlContentTextBox.Text);

                // Find OUTFIT_OVERRIDE section
                var outfitOverride = xmlDoc.Descendants("OUTFIT_OVERRIDE").FirstOrDefault();
                if (outfitOverride == null)
                {
                    return;
                }

                // Parse each texture
                ParseAndSelectTexture(outfitOverride, "BODY_DIFF", "Torso", TorsoOptionsPanel);
                ParseAndSelectTexture(outfitOverride, "ARMS_DIFF", "Arms", ArmsOptionsPanel);
                ParseAndSelectTexture(outfitOverride, "GLOVES_DIFF", "Gloves", GlovesOptionsPanel);
                ParseAndSelectTexture(outfitOverride, "LEGS_DIFF", "Legs", LegsOptionsPanel);
                ParseAndSelectTexture(outfitOverride, "SEATBELTS_DIFF", "Harness", HarnessOptionsPanel);
            }
            catch
            {
                // If XML parsing fails, just continue without pre-selecting
            }
        }

        private void ParseAndSelectTexture(XElement outfitOverride, string textureName, string bodyPart, WrapPanel panel)
        {
            var texture = outfitOverride.Descendants("TEXTURE")
                .FirstOrDefault(t => t.Attribute("NAME")?.Value == textureName);

            if (texture == null)
            {
                return;
            }

            var path = texture.Attribute("PATH")?.Value;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Extract filename from path (e.g., "Driver\Torso\filename.dds" -> "filename.dds")
            string fileName = Path.GetFileName(path);

            // Find and check the corresponding radio button
            foreach (var child in panel.Children)
            {
                if (child is RadioButton rb && (string)rb.Tag == fileName)
                {
                    rb.IsChecked = true;
                    break;
                }
            }
        }

        #endregion

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Validate Ams2Car is populated
            if (string.IsNullOrWhiteSpace(Ams2CarTextBox.Text))
            {
                MessageBox.Show("AMS2 Car field must be populated before saving.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _team.Ams2Car = Ams2CarTextBox.Text;
            _team.BaseLiveryDriver1 = BaseLiveryDriver1TextBox.Text;
            _team.BaseLiveryDriver2 = BaseLiveryDriver2TextBox.Text;
            _team.HelmetSponsors = HelmetSponsorsTextBox.Text;
            _team.VisorSponsors = VisorSponsorsTextBox.Text;
            _team.LiveryPreview = LiveryPreviewTextBox.Text;

            // Save XML content to dictionary
            _xmlFiles[_team.TeamId] = XmlContentTextBox.Text;

            // Add/replace selected outfit textures in _textureFiles
            AddOutfitTextureToFiles("Torso", _selectedTorso);
            AddOutfitTextureToFiles("Arms", _selectedArms);
            AddOutfitTextureToFiles("Gloves", _selectedGloves);
            AddOutfitTextureToFiles("Legs", _selectedLegs);
            AddOutfitTextureToFiles("Harness", _selectedHarness);

            DialogResult = true;
            Close();
        }

        private void AddOutfitTextureToFiles(string bodyPart, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            string ams2Car = Ams2CarTextBox.Text;
            string key = $"static_assets/Vehicles/Textures/CustomLiveries/Overrides/{ams2Car}/Driver/{bodyPart}/{fileName}";
            string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleBodies", bodyPart, fileName);

            if (File.Exists(sourcePath))
            {
                _textureFiles[key] = sourcePath;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}