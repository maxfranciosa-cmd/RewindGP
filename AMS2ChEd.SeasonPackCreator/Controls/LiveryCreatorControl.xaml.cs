using Ams2ChEd.Business.AMS2.Helpers;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Image = SixLabors.ImageSharp.Image;
using Point = SixLabors.ImageSharp.Point;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace AMS2ChEd.SeasonPackEditor.Controls
{
    public partial class LiveryCreatorControl : UserControl
    {
        private string _templatePath;
        private SponsorTemplate _sponsorTemplate;
        private string[] _sponsorImagePaths = new string[40];
        private bool[] _sponsorFlipHorizontal = new bool[40];
        private bool[] _sponsorFlipVertical = new bool[40];
        private Image<Rgba32> _currentLivery;
        // Zone editor state
        private bool _editMode = false;
        private System.Windows.Point _dragStart;
        private System.Windows.Shapes.Rectangle _dragRect;
        private List<ZonePlacement> _zonePlacements = new();
        private ZonePlacement _pendingDuplicate = null;
        private System.Windows.Shapes.Rectangle _duplicateGhost = null;
        private ZonePlacement _pendingMove = null;

        // Zone editor colors (WPF, mirrors existing ImageSharp colors)
        private static readonly System.Windows.Media.Color[] ZoneColors = new[]
        {
            System.Windows.Media.Color.FromArgb(120, 255, 0, 0),
            System.Windows.Media.Color.FromArgb(120, 0, 255, 0),
            System.Windows.Media.Color.FromArgb(120, 0, 0, 255),
            System.Windows.Media.Color.FromArgb(120, 255, 255, 0),
            System.Windows.Media.Color.FromArgb(120, 255, 0, 255),
            System.Windows.Media.Color.FromArgb(120, 0, 255, 255),
            System.Windows.Media.Color.FromArgb(120, 255, 128, 0),
            System.Windows.Media.Color.FromArgb(120, 128, 0, 255),
            System.Windows.Media.Color.FromArgb(120, 0, 255, 128),
            System.Windows.Media.Color.FromArgb(120, 255, 0, 128),
        };

        // Color mappings for template replacement
        private Dictionary<string, Rgba32> _colorMappings = new Dictionary<string, Rgba32>
        {
            { "Red", new Rgba32(255, 0, 0) },
            { "Green", new Rgba32(0, 255, 0) },
            { "Blue", new Rgba32(0, 0, 255) },
            { "Yellow", new Rgba32(255, 255, 0) },
            { "Magenta", new Rgba32(255, 0, 255) },
            { "Cyan", new Rgba32(0, 255, 255) },
        };

        public LiveryCreatorControl()
        {
            InitializeComponent();
        }

        private void LoadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PNG Images|*.png|All Files|*.*",
                Title = "Select Template PNG",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LiveryTemplates")
            };

            if (dialog.ShowDialog() == true)
            {
                _templatePath = dialog.FileName;
                txtTemplatePath.Text = Path.GetFileName(_templatePath);
                UpdateButtonStates();
            }
        }

        private void LoadPlacements_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files|*.json|All Files|*.*",
                Title = "Select Sponsor Placements JSON",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LiveryTemplates")
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    _sponsorTemplate = JsonSerializer.Deserialize<SponsorTemplate>(json, options);

                    txtPlacementsPath.Text = Path.GetFileName(dialog.FileName);

                    GenerateSponsorFields();
                    UpdateButtonStates();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading placements: {ex.Message}\n\n{ex.StackTrace}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GenerateSponsorFields()
        {
            // Save current sponsor selections before clearing
            var currentSelections = new Dictionary<int, string>();
            var currentFlipH = new Dictionary<int, bool>();
            var currentFlipV = new Dictionary<int, bool>();

            foreach (var child in sponsorPanel.Children)
            {
                if (child is StackPanel panel)
                {
                    var textBlock = panel.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => tb.Tag != null && tb.Tag is int);
                    if (textBlock != null && textBlock.Tag is int sponsorNum)
                    {
                        var imagePath = _sponsorImagePaths[sponsorNum - 1];
                        if (!string.IsNullOrEmpty(imagePath))
                        {
                            currentSelections[sponsorNum] = imagePath;
                            currentFlipH[sponsorNum] = _sponsorFlipHorizontal[sponsorNum - 1];
                            currentFlipV[sponsorNum] = _sponsorFlipVertical[sponsorNum - 1];
                        }
                    }
                }
            }

            sponsorPanel.Children.Clear();

            if (_sponsorTemplate?.Sponsors == null || !_sponsorTemplate.Sponsors.Any())
            {
                var noSponsorsText = new TextBlock
                {
                    Text = "No sponsors defined in placements file",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic
                };
                sponsorPanel.Children.Add(noSponsorsText);
                return;
            }

            var maxSponsor = _sponsorTemplate.Sponsors.Max(s => s.SponsorNumber);

            for (int i = 1; i <= maxSponsor; i++)
            {
                var sponsorNum = i;

                var sponsor = _sponsorTemplate.Sponsors.FirstOrDefault(s => s.SponsorNumber == sponsorNum);
                if (sponsor == null)
                    continue;

                var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

                // Header with sponsor number and placement count
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

                var label = new TextBlock
                {
                    Text = sponsor.Description ?? $"Sponsor {sponsorNum}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13
                };

                var countLabel = new TextBlock
                {
                    Text = $" ({sponsor.Placements.Count} placement{(sponsor.Placements.Count > 1 ? "s" : "")})",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(4, 0, 0, 0)
                };

                headerPanel.Children.Add(label);
                headerPanel.Children.Add(countLabel);

                var pathText = new TextBlock
                {
                    Text = "No image selected",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 5),
                    TextWrapping = TextWrapping.Wrap,
                    Tag = sponsorNum
                };

                // Restore previous selection if it exists
                if (currentSelections.TryGetValue(sponsorNum, out var previousPath))
                {
                    _sponsorImagePaths[sponsorNum - 1] = previousPath;
                    _sponsorFlipHorizontal[sponsorNum - 1] = currentFlipH.GetValueOrDefault(sponsorNum, false);
                    _sponsorFlipVertical[sponsorNum - 1] = currentFlipV.GetValueOrDefault(sponsorNum, false);
                    pathText.Text = Path.GetFileName(previousPath);
                    pathText.Foreground = Brushes.Black;
                }

                // Button panel with Select, Clear, and Preview
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

                var selectButton = new Button
                {
                    Content = "Select Image...",
                    Height = 28,
                    Width = 100,
                    Margin = new Thickness(0, 0, 5, 0),
                    Tag = sponsorNum
                };
                selectButton.Click += (s, e) => LoadSponsorImage_Click(sponsorNum, pathText);

                var clearButton = new Button
                {
                    Content = "Clear",
                    Height = 28,
                    Width = 60,
                    Margin = new Thickness(0, 0, 5, 0),
                    Tag = sponsorNum
                };
                clearButton.Click += (s, e) => ClearSponsorImage_Click(sponsorNum, pathText);

                var previewButton = new Button
                {
                    Content = "👁 Zones",
                    Height = 28,
                    Width = 70,
                    Tag = sponsorNum,
                    ToolTip = "Highlight this sponsor's zones on preview"
                };
                previewButton.Click += (s, e) => PreviewSponsorZones_Click(sponsorNum);

                buttonPanel.Children.Add(selectButton);
                buttonPanel.Children.Add(clearButton);
                buttonPanel.Children.Add(previewButton);

                // Flip controls
                var flipPanel = new StackPanel { Orientation = Orientation.Horizontal };

                var flipHCheckbox = new CheckBox
                {
                    Content = "Flip Horizontal (when rotated 180)",
                    Margin = new Thickness(0, 0, 10, 0),
                    Tag = sponsorNum,
                    IsChecked = _sponsorFlipHorizontal[sponsorNum - 1]
                };
                flipHCheckbox.Checked += (s, e) => { _sponsorFlipHorizontal[sponsorNum - 1] = true; };
                flipHCheckbox.Unchecked += (s, e) => { _sponsorFlipHorizontal[sponsorNum - 1] = false; };

                var flipVCheckbox = new CheckBox
                {
                    Content = "Flip Vertical (when rotated 180)",
                    Tag = sponsorNum,
                    IsChecked = _sponsorFlipVertical[sponsorNum - 1]
                };
                flipVCheckbox.Checked += (s, e) => { _sponsorFlipVertical[sponsorNum - 1] = true; };
                flipVCheckbox.Unchecked += (s, e) => { _sponsorFlipVertical[sponsorNum - 1] = false; };

                flipPanel.Children.Add(flipHCheckbox);
                flipPanel.Children.Add(flipVCheckbox);

                panel.Children.Add(headerPanel);
                panel.Children.Add(pathText);
                panel.Children.Add(buttonPanel);
                panel.Children.Add(flipPanel);

                sponsorPanel.Children.Add(panel);
            }
        }

        private void ClearSponsorImage_Click(int sponsorNumber, TextBlock pathTextBlock)
        {
            _sponsorImagePaths[sponsorNumber - 1] = null;
            pathTextBlock.Text = "No image selected";
            pathTextBlock.Foreground = Brushes.Gray;
        }

        private void LoadSponsorImage_Click(int sponsorNumber, TextBlock pathTextBlock)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*",
                Title = $"Select Image for Sponsor {sponsorNumber}"
            };

            if (dialog.ShowDialog() == true)
            {
                _sponsorImagePaths[sponsorNumber - 1] = dialog.FileName;
                pathTextBlock.Text = Path.GetFileName(dialog.FileName);
                pathTextBlock.Foreground = Brushes.Black;
                UpdateButtonStates();
            }
        }

        private void SelectColor_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ColorTextBox_Changed(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var colorName = textBox.Tag as string;
            var hexValue = textBox.Text.Trim();

            // Try to parse hex color
            if (!string.IsNullOrEmpty(colorName) && TryParseHexColor(hexValue, out var color))
            {
                // Update the color mapping
                _colorMappings[colorName] = new Rgba32(color.R, color.G, color.B, color.A);

                // Update the preview rectangle
                var previewRect = FindPreviewRectangle(colorName);
                if (previewRect != null)
                {
                    previewRect.Fill = new SolidColorBrush(color);
                }

                // Remove error styling
                textBox.Background = Brushes.White;
            }
            else
            {
                // Show error - light red background
                textBox.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 230, 230));
            }
        }

        private bool TryParseHexColor(string hex, out System.Windows.Media.Color color)
        {
            color = Colors.White;

            if (string.IsNullOrWhiteSpace(hex))
                return false;

            // Remove # if present
            hex = hex.TrimStart('#');

            // Must be 6 characters (RGB) or 8 characters (ARGB)
            if (hex.Length != 6 && hex.Length != 8)
                return false;

            try
            {
                byte a = 255;
                byte r, g, b;

                if (hex.Length == 8)
                {
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }

                color = System.Windows.Media.Color.FromArgb(a, r, g, b);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private System.Windows.Shapes.Rectangle FindPreviewRectangle(string colorName)
        {
            return colorName switch
            {
                "Red" => previewRed,
                "Green" => previewGreen,
                "Blue" => previewBlue,
                "Yellow" => previewYellow,
                "Magenta" => previewMagenta,
                "Cyan" => previewCyan,
                _ => null
            };
        }

        private void Zoom_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var scale = double.Parse(button.Tag.ToString());

            previewScale.ScaleX = scale;
            previewScale.ScaleY = scale;

            // Update button styles
            btnZoom25.FontWeight = scale == 0.25 ? FontWeights.Bold : FontWeights.Normal;
            btnZoom50.FontWeight = scale == 0.5 ? FontWeights.Bold : FontWeights.Normal;
            btnZoom100.FontWeight = scale == 1.0 ? FontWeights.Bold : FontWeights.Normal;
            btnZoomFit.FontWeight = FontWeights.Normal;
        }

        private void ZoomFit_Click(object sender, RoutedEventArgs e)
        {
            if (imgPreview.Source == null)
                return;

            var imageWidth = imgPreview.Source.Width;
            var imageHeight = imgPreview.Source.Height;
            var scrollWidth = previewScroll.ActualWidth;
            var scrollHeight = previewScroll.ActualHeight;

            var scaleX = scrollWidth / imageWidth;
            var scaleY = scrollHeight / imageHeight;
            var scale = Math.Min(scaleX, scaleY) * 0.95; // 95% to add padding

            previewScale.ScaleX = scale;
            previewScale.ScaleY = scale;

            // Update button styles
            btnZoom25.FontWeight = FontWeights.Normal;
            btnZoom50.FontWeight = FontWeights.Normal;
            btnZoom100.FontWeight = FontWeights.Normal;
            btnZoomFit.FontWeight = FontWeights.Bold;
        }

        private void GeneratePreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _currentLivery?.Dispose();

                _currentLivery = GenerateLivery(
                    _templatePath,
                    _sponsorTemplate,
                    _sponsorImagePaths);

                DisplayPreview(_currentLivery);

                btnExport.IsEnabled = true;
                txtNoPreview.Visibility = Visibility.Collapsed;
                previewScroll.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating livery: {ex.Message}\n\n{ex.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Image<Rgba32> GenerateLivery(
            string templatePath,
            SponsorTemplate sponsorTemplate,
            string[] sponsorImagePaths)
        {
            var livery = Image.Load<Rgba32>(templatePath);

            // Step 1: Replace template colors
            ReplaceTemplateColors(livery);

            if (sponsorTemplate?.Sponsors == null) return livery;

            // Step 2: Apply sponsors
            foreach (var sponsor in sponsorTemplate.Sponsors)
            {
                var sponsorNumber = sponsor.SponsorNumber;

                if (sponsorNumber < 1 || sponsorNumber > sponsorImagePaths.Length)
                    continue;

                var sponsorImagePath = sponsorImagePaths[sponsorNumber - 1];

                if (string.IsNullOrEmpty(sponsorImagePath) || !File.Exists(sponsorImagePath))
                    continue;

                using var sponsorImage = Image.Load<Rgba32>(sponsorImagePath);

                var flipH = _sponsorFlipHorizontal[sponsorNumber - 1];
                var flipV = _sponsorFlipVertical[sponsorNumber - 1];

                foreach (var placement in sponsor.Placements)
                {
                    ApplySponsorToPlacement(livery, sponsorImage, placement, flipH, flipV);
                }
            }

            return livery;
        }

        private void ReplaceTemplateColors(Image<Rgba32> livery)
        {
            // Define original template colors
            var templateColors = new Dictionary<string, Rgba32>
            {
                { "Red", new Rgba32(255, 0, 0) },
                { "Green", new Rgba32(0, 255, 0) },
                { "Blue", new Rgba32(0, 0, 255) },
                { "Yellow", new Rgba32(255, 255, 0) },
                { "Magenta", new Rgba32(255, 0, 255) },
                { "Cyan", new Rgba32(0, 255, 255) }
            };

            // Replace each color
            foreach (var kvp in templateColors)
            {
                var templateColor = kvp.Value;
                var replacementColor = _colorMappings[kvp.Key];

                // Only replace if color has changed
                if (templateColor.Equals(replacementColor))
                    continue;

                livery.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            if (row[x].R == templateColor.R &&
                                row[x].G == templateColor.G &&
                                row[x].B == templateColor.B)
                            {
                                row[x] = replacementColor;
                            }
                        }
                    }
                });
            }
        }

        private void ApplySponsorToPlacement(
            Image<Rgba32> livery,
            Image<Rgba32> sponsorImage,
            SponsorPlacement placement,
            bool flipHorizontal = false,
            bool flipVertical = false)
        {
            using var workingImage = ExtractPortion(sponsorImage, placement.Portion, placement.PortionPercentageSize);

            if (placement.Rotation > 0 && placement.Rotation <= 180)
            {
                if (flipHorizontal)
                {
                    workingImage.Mutate(ctx => ctx.Flip(FlipMode.Horizontal));
                }
                if (flipVertical)
                {
                    workingImage.Mutate(ctx => ctx.Flip(FlipMode.Vertical));
                }
            }

            // Step 2: Apply rotation
            if (placement.Rotation != 0)
            {
                workingImage.Mutate(ctx => ctx.Rotate(placement.Rotation));
            }

            // Step 3: Scale to fit
            var (scaledWidth, scaledHeight) = CalculateScaledDimensions(
                workingImage.Width,
                workingImage.Height,
                placement.Width,
                placement.Height);

            using var scaledImage = workingImage.Clone(ctx => ctx.Resize(scaledWidth, scaledHeight));

            // Step 4: Calculate position — anchor to edge for half portions, centre otherwise
            int xOffset;
            int yOffset;

            switch (placement.Portion)
            {
                case SponsorPortion.TOP_HALF:
                    xOffset = placement.Rotation switch
                    {
                        0 => placement.X + (placement.Width - scaledWidth) / 2,   // bottom centre → centre X, anchor bottom
                        90 => placement.X,                                          // left edge
                        270 => placement.X + placement.Width - scaledWidth,          // right edge
                        _ => placement.X + (placement.Width - scaledWidth) / 2    // 180° → centre X, anchor top
                    };
                    yOffset = placement.Rotation switch
                    {
                        0 => placement.Y + placement.Height - scaledHeight,        // anchor bottom
                        90 => placement.Y + (placement.Height - scaledHeight) / 2, // centre Y
                        270 => placement.Y + (placement.Height - scaledHeight) / 2, // centre Y
                        _ => placement.Y                                           // 180° → anchor top
                    };
                    break;

                case SponsorPortion.BOTTOM_HALF:
                    xOffset = placement.Rotation switch
                    {
                        0 => placement.X + (placement.Width - scaledWidth) / 2,   // centre X, anchor top
                        90 => placement.X + placement.Width - scaledWidth,          // right edge
                        270 => placement.X,                                          // left edge
                        _ => placement.X + (placement.Width - scaledWidth) / 2    // 180° → centre X, anchor bottom
                    };
                    yOffset = placement.Rotation switch
                    {
                        0 => placement.Y,                                          // anchor top
                        90 => placement.Y + (placement.Height - scaledHeight) / 2, // centre Y
                        270 => placement.Y + (placement.Height - scaledHeight) / 2, // centre Y
                        _ => placement.Y + placement.Height - scaledHeight         // 180° → anchor bottom
                    };
                    break;

                default:
                    // Full, LEFT_SIDE, RIGHT_SIDE — centre in zone
                    xOffset = placement.X + (placement.Width - scaledWidth) / 2;
                    yOffset = placement.Y + (placement.Height - scaledHeight) / 2;
                    break;
            }

            // Step 5: Composite
            livery.Mutate(ctx => ctx.DrawImage(
                scaledImage,
                new Point(xOffset, yOffset),
                1.0f));
        }

        private Image<Rgba32> ExtractPortion(Image<Rgba32> source, SponsorPortion? portion,int? portionPercentageSize)
        {
            var portionHeight = GetPercentageOfSizeOrHalf(portionPercentageSize, source.Height);
            var portionWidth = GetPercentageOfSizeOrHalf(portionPercentageSize, source.Width);

            return portion switch
            {
                SponsorPortion.TOP_HALF => source.Clone(ctx => ctx.Crop(
                    new SixLabors.ImageSharp.Rectangle(0, 0, source.Width, portionHeight))),

                SponsorPortion.BOTTOM_HALF => source.Clone(ctx => ctx.Crop(
                    new SixLabors.ImageSharp.Rectangle(0, source.Height - portionHeight, source.Width, portionHeight))),

                SponsorPortion.LEFT_SIDE => source.Clone(ctx => ctx.Crop(
                    new SixLabors.ImageSharp.Rectangle(0, 0, portionWidth, source.Height))),

                SponsorPortion.RIGHT_SIDE => source.Clone(ctx => ctx.Crop(
                    new SixLabors.ImageSharp.Rectangle(source.Width - portionWidth, 0, portionWidth, source.Height))),
                _ => source.Clone() // FULL or null
            };
        }

        private int GetPercentageOfSizeOrHalf(int? percentage, int total)
        {
            return percentage != null
                ? (int)Math.Round(total * (percentage.Value / 100.0))
                : total / 2;
        }


        private (int width, int height) CalculateScaledDimensions(
            int sourceWidth,
            int sourceHeight,
            int targetWidth,
            int targetHeight)
        {
            float sourceAspect = (float)sourceWidth / sourceHeight;
            float targetAspect = (float)targetWidth / targetHeight;

            if (sourceAspect > targetAspect)
            {
                // Fit to width
                return (targetWidth, (int)(targetWidth / sourceAspect));
            }
            else
            {
                // Fit to height
                return ((int)(targetHeight * sourceAspect), targetHeight);
            }
        }

        private void DisplayPreview(Image<Rgba32> image)
        {
            using var memoryStream = new MemoryStream();
            image.SaveAsPng(memoryStream);
            memoryStream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memoryStream;
            bitmap.EndInit();
            bitmap.Freeze();

            imgPreview.Source = bitmap;
        }

        private void ExportPng_Click(object sender, RoutedEventArgs e)
        {
            if (_currentLivery == null)
            {
                MessageBox.Show("Please generate a preview first.", "No Livery",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png|DDS Texture|*.dds",
                Title = "Export Livery",
                FileName = "livery",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool exportAsDds = dialog.FilterIndex == 2 ||
                                       Path.GetExtension(dialog.FileName).Equals(".dds", StringComparison.OrdinalIgnoreCase);

                    if (exportAsDds)
                    {
                        ExportAsDds(dialog.FileName);
                    }
                    else
                    {
                        _currentLivery.SaveAsPng(dialog.FileName);
                    }

                    MessageBox.Show("Livery exported successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting livery: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportAsDds(string filePath)
        {
            var encoder = new BcEncoder();
            encoder.OutputOptions.GenerateMipMaps = true;
            encoder.OutputOptions.Quality = CompressionQuality.BestQuality;
            encoder.OutputOptions.Format = CompressionFormat.Bc3;
            encoder.OutputOptions.FileFormat = OutputFileFormat.Dds;

            using var fs = File.OpenWrite(filePath);
            encoder.EncodeToStreamAsync(_currentLivery, fs).GetAwaiter().GetResult();
        }

        private void UpdateButtonStates()
        {
            bool hasTemplate = !string.IsNullOrEmpty(_templatePath);

            btnGenerate.IsEnabled = hasTemplate;
        }

        private void ShowZones_Changed(object sender, RoutedEventArgs e)
        {
            if (chkShowZones.IsChecked == true)
            {
                ShowAllSponsorZones();
            }
            else
            {
                // Restore original preview if exists
                if (_currentLivery != null)
                {
                    DisplayPreview(_currentLivery);
                }
            }
        }

        private void PreviewSponsorZones_Click(int sponsorNumber)
        {
            if (_currentLivery == null)
            {
                MessageBox.Show("Please generate a livery preview first.", "No Preview",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sponsor = _sponsorTemplate.Sponsors.FirstOrDefault(s => s.SponsorNumber == sponsorNumber);
            if (sponsor == null)
                return;

            // Clone current livery and draw zone overlays
            using var preview = _currentLivery.Clone();

            foreach (var placement in sponsor.Placements)
            {
                // Draw semi-transparent yellow rectangle
                preview.Mutate(ctx =>
                {
                    var rectColor = new Rgba32(255, 255, 0, 128); // Yellow, 50% opacity
                    var pen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(rectColor, 3);

                    ctx.Draw(pen, new RectangleF(
                        placement.X,
                        placement.Y,
                        placement.Width,
                        placement.Height));
                });
            }

            DisplayPreview(preview);
        }

        private void ShowAllSponsorZones()
        {
            if (_sponsorTemplate == null || _currentLivery == null)
                return;

            using var preview = _currentLivery.Clone();

            var colors = new[]
            {
                new Rgba32(255, 0, 0, 100),      // Red
                new Rgba32(0, 255, 0, 100),      // Green
                new Rgba32(0, 0, 255, 100),      // Blue
                new Rgba32(255, 255, 0, 100),    // Yellow
                new Rgba32(255, 0, 255, 100),    // Magenta
                new Rgba32(0, 255, 255, 100),    // Cyan
                new Rgba32(255, 128, 0, 100),    // Orange
                new Rgba32(128, 0, 255, 100),    // Purple
                new Rgba32(0, 255, 128, 100),    // Spring Green
                new Rgba32(255, 0, 128, 100)     // Rose
            };

            int colorIndex = 0;
            foreach (var sponsor in _sponsorTemplate.Sponsors)
            {
                var color = colors[colorIndex % colors.Length];
                colorIndex++;

                foreach (var placement in sponsor.Placements)
                {
                    preview.Mutate(ctx =>
                    {
                        var pen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(color, 2);
                        ctx.Draw(pen, new RectangleF(
                            placement.X,
                            placement.Y,
                            placement.Width,
                            placement.Height));
                    });
                }
            }

            DisplayPreview(preview);
        }

        private void EditZones_Click(object sender, RoutedEventArgs e)
        {
            if (imgPreview.Source == null)
            {
                MessageBox.Show("Please generate a preview first.", "No Preview",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _editMode = !_editMode;

            if (_editMode)
            {
                // Snap to 100% and lock zoom
                previewScale.ScaleX = 1.0;
                previewScale.ScaleY = 1.0;
                btnZoom25.IsEnabled = false;
                btnZoom50.IsEnabled = false;
                btnZoom100.IsEnabled = false;
                btnZoomFit.IsEnabled = false;

                // Always re-seed from loaded sponsor template
                if (_sponsorTemplate?.Sponsors != null)
                {
                    _zonePlacements.Clear();
                    foreach (var sponsor in _sponsorTemplate.Sponsors)
                    {
                        foreach (var placement in sponsor.Placements)
                        {
                            _zonePlacements.Add(new ZonePlacement
                            {
                                X = placement.X,
                                Y = placement.Y,
                                Width = placement.Width,
                                Height = placement.Height,
                                SponsorNumber = sponsor.SponsorNumber,
                                Description = sponsor.Description,
                                Rotation = placement.Rotation,
                                Portion = placement.Portion,
                                PortionPercentageSize = placement.PortionPercentageSize
                            });
                        }
                    }
                }

                // Activate canvas
                zoneEditorCanvas.IsHitTestVisible = true;

                if (imgPreview.Source != null)
                {
                    zoneEditorCanvas.Width = imgPreview.Source.Width;
                    zoneEditorCanvas.Height = imgPreview.Source.Height;
                }

                btnEditZones.Content = "✏ Exit Edit Mode";
                btnExportZones.IsEnabled = _zonePlacements.Any();

                RedrawZoneCanvas();
            }
            else
            {
                zoneEditorCanvas.IsHitTestVisible = false;
                zoneEditorCanvas.Children.Clear();
                btnEditZones.Content = "✏ Edit Zones";
                btnZoom25.IsEnabled = true;
                btnZoom50.IsEnabled = true;
                btnZoom100.IsEnabled = true;
                btnZoomFit.IsEnabled = true;
            }
        }

        private void ZoneCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_editMode) return;

            // Right-click: delete zone under cursor
            // Right-click: open context menu for zone under cursor
            if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(zoneEditorCanvas);
                var hit = _zonePlacements.LastOrDefault(z =>
                    pos.X >= z.X && pos.X <= z.X + z.Width &&
                    pos.Y >= z.Y && pos.Y <= z.Y + z.Height);

                if (hit != null)
                {
                    var menu = new ContextMenu();

                    var editItem = new MenuItem { Header = "✏ Edit" };
                    editItem.Click += (s, ev) =>
                    {
                        ShowZoneMetadataPopup(hit.X, hit.Y, hit.Width, hit.Height, hit);
                    };

                    var deleteItem = new MenuItem { Header = "🗑 Delete" };
                    deleteItem.Click += (s, ev) =>
                    {
                        _zonePlacements.Remove(hit);
                        RedrawZoneCanvas();
                        btnExportZones.IsEnabled = _zonePlacements.Any();
                    };

                    var dupItem = new MenuItem { Header = "⧉ Duplicate (click to place)" };
                    dupItem.Click += (s, ev) => BeginDuplicatePlacement(hit, flipRotation: false);

                    var dupFlipItem = new MenuItem { Header = "⧉ Duplicate and flip rotation" };
                    dupFlipItem.Click += (s, ev) => BeginDuplicatePlacement(hit, flipRotation: true);

                    var moveItem = new MenuItem { Header = "✥ Move (click to place)" };
                    moveItem.Click += (s, ev) => BeginMovePlacement(hit);

                    menu.Items.Add(editItem);
                    menu.Items.Add(deleteItem);
                    menu.Items.Add(new Separator());
                    menu.Items.Add(dupItem);
                    menu.Items.Add(dupFlipItem);
                    menu.Items.Add(new Separator());
                    menu.Items.Add(moveItem);

                    menu.IsOpen = true;
                }
                return;
            }

            // Left-click: start drawing
            _dragStart = e.GetPosition(zoneEditorCanvas);
            _dragRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 2,
                StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 255))
            };
            Canvas.SetLeft(_dragRect, _dragStart.X);
            Canvas.SetTop(_dragRect, _dragStart.Y);
            zoneEditorCanvas.Children.Add(_dragRect);
            zoneEditorCanvas.CaptureMouse();
        }

        private void ZoneCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(zoneEditorCanvas);

            // Move ghost if a duplicate or move is pending
            if ((_pendingDuplicate != null || _pendingMove != null) && _duplicateGhost != null)
            {
                double ghostW = _pendingDuplicate != null ? _pendingDuplicate.Width : _pendingMove.Width;
                double ghostH = _pendingDuplicate != null ? _pendingDuplicate.Height : _pendingMove.Height;
                Canvas.SetLeft(_duplicateGhost, pos.X - ghostW / 2.0);
                Canvas.SetTop(_duplicateGhost, pos.Y - ghostH / 2.0);
                return;
            }

            if (_dragRect == null) return;

            var x = Math.Min(pos.X, _dragStart.X);
            var y = Math.Min(pos.Y, _dragStart.Y);
            var w = Math.Abs(pos.X - _dragStart.X);
            var h = Math.Abs(pos.Y - _dragStart.Y);

            Canvas.SetLeft(_dragRect, x);
            Canvas.SetTop(_dragRect, y);
            _dragRect.Width = w;
            _dragRect.Height = h;
        }

        private void ZoneCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(zoneEditorCanvas);

            // Confirm move
            if (_pendingMove != null)
            {
                _pendingMove.X = (int)(pos.X - _pendingMove.Width / 2.0);
                _pendingMove.Y = (int)(pos.Y - _pendingMove.Height / 2.0);

                _pendingMove = null;
                if (_duplicateGhost != null)
                {
                    zoneEditorCanvas.Children.Remove(_duplicateGhost);
                    _duplicateGhost = null;
                }

                zoneEditorCanvas.Cursor = System.Windows.Input.Cursors.Cross;
                RedrawZoneCanvas();
                return;
            }

            // Place duplicate
            if (_pendingDuplicate != null)
            {
                _pendingDuplicate.X = (int)(pos.X - _pendingDuplicate.Width / 2.0);
                _pendingDuplicate.Y = (int)(pos.Y - _pendingDuplicate.Height / 2.0);
                _zonePlacements.Add(_pendingDuplicate);

                _pendingDuplicate = null;
                if (_duplicateGhost != null)
                {
                    zoneEditorCanvas.Children.Remove(_duplicateGhost);
                    _duplicateGhost = null;
                }

                zoneEditorCanvas.Cursor = System.Windows.Input.Cursors.Cross;
                RedrawZoneCanvas();
                btnExportZones.IsEnabled = true;
                return;
            }

            if (_dragRect == null) return;

            zoneEditorCanvas.ReleaseMouseCapture();

            var finalX = (int)Math.Min(pos.X, _dragStart.X);
            var finalY = (int)Math.Min(pos.Y, _dragStart.Y);
            var w = (int)Math.Abs(pos.X - _dragStart.X);
            var h = (int)Math.Abs(pos.Y - _dragStart.Y);

            zoneEditorCanvas.Children.Remove(_dragRect);
            _dragRect = null;

            if (w < 5 || h < 5) return;

            ShowZoneMetadataPopup(finalX, finalY, w, h);
        }

        private void ShowZoneMetadataPopup(int x, int y, int width, int height, ZonePlacement existingZone = null)
        {
            var isEditing = existingZone != null;

            var win = new Window
            {
                Title = isEditing ? "Edit Zone" : "Zone Properties",
                Width = 320,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = System.Windows.ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0  info
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1  x
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2  y
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3  width
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4  height
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5  sponsor
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 6  desc
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 7  rotation
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 8  auto-size
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 9  portion
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 10 portion %
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 11 spacer
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 12 buttons
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Info label
            var infoLabel = new TextBlock
            {
                Text = $"Size: {width} × {height} px",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetColumnSpan(infoLabel, 2);
            Grid.SetRow(infoLabel, 0);

            // X
            var xLabel = new TextBlock { Text = "X", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(xLabel, 1); Grid.SetColumn(xLabel, 0);
            var xBox = new TextBox { Text = (isEditing ? existingZone.X : x).ToString(), Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(xBox, 1); Grid.SetColumn(xBox, 1);

            // Y
            var yLabel = new TextBlock { Text = "Y", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(yLabel, 2); Grid.SetColumn(yLabel, 0);
            var yBox = new TextBox { Text = (isEditing ? existingZone.Y : y).ToString(), Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(yBox, 2); Grid.SetColumn(yBox, 1);

            // Width
            var wLabel = new TextBlock { Text = "Width", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(wLabel, 3); Grid.SetColumn(wLabel, 0);
            var wBox = new TextBox { Text = (isEditing ? existingZone.Width : width).ToString(), Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(wBox, 3); Grid.SetColumn(wBox, 1);

            // Height
            var hLabel = new TextBlock { Text = "Height", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(hLabel, 4); Grid.SetColumn(hLabel, 0);
            var hBox = new TextBox { Text = (isEditing ? existingZone.Height : height).ToString(), Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(hBox, 4); Grid.SetColumn(hBox, 1);

            // Sponsor number
            var sponsorLabel = new TextBlock { Text = "Sponsor #", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(sponsorLabel, 5); Grid.SetColumn(sponsorLabel, 0);
            var existingNumbers = _zonePlacements
                .Where(z => z != existingZone)
                .Select(z => z.SponsorNumber).Distinct().OrderBy(n => n).ToList();
            var nextNumber = existingNumbers.Any() ? existingNumbers.Max() + 1 : 1;
            var sponsorBox = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 8) };
            foreach (var n in existingNumbers) sponsorBox.Items.Add(n);
            sponsorBox.Text = isEditing ? existingZone.SponsorNumber.ToString() : nextNumber.ToString();
            Grid.SetRow(sponsorBox, 5); Grid.SetColumn(sponsorBox, 1);

            // Description
            var descLabel = new TextBlock { Text = "Description", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(descLabel, 6); Grid.SetColumn(descLabel, 0);
            var descBox = new TextBox
            {
                Text = isEditing ? existingZone.Description ?? "" : "",
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(descBox, 6); Grid.SetColumn(descBox, 1);

            sponsorBox.SelectionChanged += (s, e) =>
            {
                if (sponsorBox.SelectedItem is int num)
                {
                    var existing = _zonePlacements.FirstOrDefault(z => z.SponsorNumber == num && z != existingZone);
                    if (existing != null) descBox.Text = existing.Description ?? "";
                }
            };

            // Rotation
            var rotLabel = new TextBlock { Text = "Rotation (0–360)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(rotLabel, 7); Grid.SetColumn(rotLabel, 0);
            var rotBox = new TextBox
            {
                Text = isEditing ? existingZone.Rotation.ToString() : "0",
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(rotBox, 7); Grid.SetColumn(rotBox, 1);

            // Back-calculate original sticker dimensions from an already-expanded zone.
            // If the zone was previously auto-sized, W/H are the bounding box — invert to recover
            // the original sticker size before re-applying a new rotation.
            static (int origW, int origH) GetOriginalDimensions(int currentW, int currentH, int currentRotation)
            {
                double rad = currentRotation * Math.PI / 180.0;
                double cosA = Math.Abs(Math.Cos(rad));
                double sinA = Math.Abs(Math.Sin(rad));

                if (sinA < 1e-6) return (currentW, currentH);      // 0° / 180° — no expansion
                if (cosA < 1e-6) return (currentH, currentW);      // 90° / 270° — axes swapped

                double denom = cosA * cosA - sinA * sinA;          // = cos(2θ), zero at 45°/135°
                if (Math.Abs(denom) < 1e-6)
                {
                    double side = Math.Min(currentW, currentH);
                    return ((int)Math.Round(side), (int)Math.Round(side));
                }

                int origW = (int)Math.Round((currentW * cosA - currentH * sinA) / denom);
                int origH = (int)Math.Round((currentH * cosA - currentW * sinA) / denom);
                return (Math.Max(1, origW), Math.Max(1, origH));
            }

            // Capture the true sticker dimensions once at open time.
            // When editing: back-calculate from current zone size + current rotation.
            // When creating: the drawn dimensions are the originals.
            var (origW, origH) = isEditing
                ? GetOriginalDimensions(existingZone.Width, existingZone.Height, existingZone.Rotation)
                : (width, height);

            // Auto-size checkbox
            var autoSizeCheck = new CheckBox
            {
                Content = "Auto-fit zone size to preserve scale",
                Margin = new Thickness(0, 0, 0, 8),
                IsChecked = false
            };
            Grid.SetRow(autoSizeCheck, 8); Grid.SetColumnSpan(autoSizeCheck, 2);

            // RecomputeAutoSize reads wBox/hBox as the sticker base dimensions so that manual
            // overrides to those fields are respected before the rotation expansion is applied.
            void RecomputeAutoSize()
            {
                if (autoSizeCheck.IsChecked != true) return;
                if (!int.TryParse(rotBox.Text, out int deg)) return;

                double rad = deg * Math.PI / 180.0;
                double cosA = Math.Abs(Math.Cos(rad));
                double sinA = Math.Abs(Math.Sin(rad));

                int baseW = int.TryParse(wBox.Text, out int bw) ? bw : origW;
                int baseH = int.TryParse(hBox.Text, out int bh) ? bh : origH;

                // Back-calculate in case wBox/hBox already reflect a previous auto-size
                var (trueW, trueH) = GetOriginalDimensions(baseW, baseH, deg);

                int newW = (int)Math.Ceiling(trueW * cosA + trueH * sinA);
                int newH = (int)Math.Ceiling(trueW * sinA + trueH * cosA);

                wBox.Text = newW.ToString();
                hBox.Text = newH.ToString();

                // Re-centre around the original drawn centre point
                int cx = (isEditing ? existingZone.X : x) + width / 2;
                int cy = (isEditing ? existingZone.Y : y) + height / 2;
                xBox.Text = (cx - newW / 2).ToString();
                yBox.Text = (cy - newH / 2).ToString();

                infoLabel.Text = $"Size: {newW} × {newH} px  (original sticker {trueW} × {trueH})";
            }

            rotBox.TextChanged += (s, e) => RecomputeAutoSize();
            autoSizeCheck.Checked += (s, e) => RecomputeAutoSize();
            autoSizeCheck.Unchecked += (s, e) =>
            {
                wBox.Text = (isEditing ? existingZone.Width : width).ToString();
                hBox.Text = (isEditing ? existingZone.Height : height).ToString();
                xBox.Text = (isEditing ? existingZone.X : x).ToString();
                yBox.Text = (isEditing ? existingZone.Y : y).ToString();
                infoLabel.Text = $"Size: {width} × {height} px";
            };

            // Portion
            var portionLabel = new TextBlock { Text = "Portion", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(portionLabel, 9); Grid.SetColumn(portionLabel, 0);
            var portionBox = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
            portionBox.Items.Add("Full");
            portionBox.Items.Add("Top Half");
            portionBox.Items.Add("Bottom Half");
            portionBox.Items.Add("Left Side");
            portionBox.Items.Add("Right Side");
            portionBox.SelectedIndex = isEditing
                ? existingZone.Portion switch
                {
                    SponsorPortion.TOP_HALF => 1,
                    SponsorPortion.BOTTOM_HALF => 2,
                    SponsorPortion.LEFT_SIDE => 3,
                    SponsorPortion.RIGHT_SIDE => 4,
                    _ => 0
                }
                : 0;
            Grid.SetRow(portionBox, 9); Grid.SetColumn(portionBox, 1);

            // Portion percentage
            var portionPercLabel = new TextBlock { Text = "Portion %", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(portionPercLabel, 10); Grid.SetColumn(portionPercLabel, 0);
            var portionPercBox = new TextBox
            {
                Text = isEditing ? existingZone.PortionPercentageSize?.ToString() ?? "" : "",
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(portionPercBox, 10); Grid.SetColumn(portionPercBox, 1);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(buttonPanel, 12); Grid.SetColumnSpan(buttonPanel, 2);

            var cancelBtn = new Button { Content = "Cancel", Width = 75, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            cancelBtn.Click += (s, e) => win.Close();

            var confirmBtn = new Button { Content = isEditing ? "Save" : "Add Zone", Width = 75, Height = 28, IsDefault = true };
            confirmBtn.Click += (s, e) =>
            {
                if (!int.TryParse(xBox.Text, out int finalX) || finalX < 0)
                {
                    MessageBox.Show("Please enter a valid X position.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(yBox.Text, out int finalY) || finalY < 0)
                {
                    MessageBox.Show("Please enter a valid Y position.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(wBox.Text, out int finalW) || finalW < 1)
                {
                    MessageBox.Show("Please enter a valid Width.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(hBox.Text, out int finalH) || finalH < 1)
                {
                    MessageBox.Show("Please enter a valid Height.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(sponsorBox.Text, out int sponsorNum) || sponsorNum < 1)
                {
                    MessageBox.Show("Please enter a valid sponsor number.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(rotBox.Text, out int rotation) || rotation < 0 || rotation > 360)
                {
                    MessageBox.Show("Please enter a valid rotation between 0 and 360.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrEmpty(portionPercBox.Text) &&
                    (!int.TryParse(portionPercBox.Text, out int portionPercentage) || portionPercentage < 0 || portionPercentage > 100))
                {
                    MessageBox.Show("Please enter a valid percentage between 0 and 100.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SponsorPortion? portion = portionBox.SelectedIndex switch
                {
                    1 => SponsorPortion.TOP_HALF,
                    2 => SponsorPortion.BOTTOM_HALF,
                    3 => SponsorPortion.LEFT_SIDE,
                    4 => SponsorPortion.RIGHT_SIDE,
                    _ => null
                };

                if (isEditing)
                {
                    existingZone.X = finalX;
                    existingZone.Y = finalY;
                    existingZone.Width = finalW;
                    existingZone.Height = finalH;
                    existingZone.SponsorNumber = sponsorNum;
                    existingZone.Description = descBox.Text.Trim();
                    existingZone.Rotation = rotation;
                    existingZone.Portion = portion;
                    existingZone.PortionPercentageSize = string.IsNullOrEmpty(portionPercBox.Text) ? null : int.Parse(portionPercBox.Text);
                }
                else
                {
                    _zonePlacements.Add(new ZonePlacement
                    {
                        X = finalX,
                        Y = finalY,
                        Width = finalW,
                        Height = finalH,
                        SponsorNumber = sponsorNum,
                        Description = descBox.Text.Trim(),
                        Rotation = rotation,
                        Portion = portion,
                        PortionPercentageSize = string.IsNullOrEmpty(portionPercBox.Text) ? null : int.Parse(portionPercBox.Text)
                    });
                }

                RedrawZoneCanvas();
                btnExportZones.IsEnabled = true;
                win.Close();
            };

            buttonPanel.Children.Add(cancelBtn);
            buttonPanel.Children.Add(confirmBtn);

            grid.Children.Add(infoLabel);
            grid.Children.Add(xLabel); grid.Children.Add(xBox);
            grid.Children.Add(yLabel); grid.Children.Add(yBox);
            grid.Children.Add(wLabel); grid.Children.Add(wBox);
            grid.Children.Add(hLabel); grid.Children.Add(hBox);
            grid.Children.Add(sponsorLabel); grid.Children.Add(sponsorBox);
            grid.Children.Add(descLabel); grid.Children.Add(descBox);
            grid.Children.Add(rotLabel); grid.Children.Add(rotBox);
            grid.Children.Add(autoSizeCheck);
            grid.Children.Add(portionLabel); grid.Children.Add(portionBox);
            grid.Children.Add(portionPercLabel); grid.Children.Add(portionPercBox);
            grid.Children.Add(buttonPanel);

            win.Content = grid;
            win.ShowDialog();
        }

        private void RedrawZoneCanvas()
        {
            zoneEditorCanvas.Children.Clear();

            // Size the canvas to match the image
            if (imgPreview.Source != null)
            {
                zoneEditorCanvas.Width = imgPreview.Source.Width;
                zoneEditorCanvas.Height = imgPreview.Source.Height;
            }

            foreach (var zone in _zonePlacements.Where(z => z != _pendingMove))
            {
                var colorIndex = (zone.SponsorNumber - 1) % ZoneColors.Length;
                var color = ZoneColors[colorIndex];

                // Bounding box — drawn with reduced opacity fill so the inner indicator is visible
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = zone.Width,
                    Height = zone.Height,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, color.R, color.G, color.B)),
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, color.R, color.G, color.B)),
                    StrokeThickness = 1,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 6, 3 },
                    ToolTip = $"#{zone.SponsorNumber} {zone.Description}  [{zone.Width}×{zone.Height}]  rot:{zone.Rotation}°  {zone.Portion?.ToString() ?? "Full"}"
                };
                Canvas.SetLeft(rect, zone.X);
                Canvas.SetTop(rect, zone.Y);
                zoneEditorCanvas.Children.Add(rect);

                // Rotation indicator — largest rectangle inscribed in the bounding box at the given angle
                if (zone.Rotation != 0)
                {
                    var indicator = CreateInscribedRotationIndicator(zone, color);
                    if (indicator != null)
                        zoneEditorCanvas.Children.Add(indicator);
                }
                else
                {
                    // At 0° just draw a solid inner rectangle (same as bounding box, slightly inset)
                    var innerRect = new System.Windows.Shapes.Rectangle
                    {
                        Width = Math.Max(0, zone.Width - 4),
                        Height = Math.Max(0, zone.Height - 4),
                        Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, color.R, color.G, color.B)),
                        Stroke = new SolidColorBrush(color),
                        StrokeThickness = 1.5,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(innerRect, zone.X + 2);
                    Canvas.SetTop(innerRect, zone.Y + 2);
                    zoneEditorCanvas.Children.Add(innerRect);
                }

                // Label
                var label = new TextBlock
                {
                    Text = zone.Rotation != 0 ? $"#{zone.SponsorNumber} {zone.Rotation}°" : $"#{zone.SponsorNumber}",
                    Foreground = new SolidColorBrush(color),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, zone.X + 4);
                Canvas.SetTop(label, zone.Y + 4);
                zoneEditorCanvas.Children.Add(label);
            }
        }

        private void BeginDuplicatePlacement(ZonePlacement source, bool flipRotation)
        {
            // Cancel any in-progress pending operation first
            if (_duplicateGhost != null)
            {
                zoneEditorCanvas.Children.Remove(_duplicateGhost);
                _duplicateGhost = null;
            }
            _pendingDuplicate = null;
            _pendingMove = null;

            int flippedRotation = flipRotation
                ? (source.Rotation + 180) % 360
                : source.Rotation;

            _pendingDuplicate = new ZonePlacement
            {
                Width = source.Width,
                Height = source.Height,
                SponsorNumber = source.SponsorNumber,
                Description = source.Description,
                Rotation = flippedRotation,
                Portion = source.Portion,
                PortionPercentageSize = source.PortionPercentageSize
            };

            // Redraw first so the ghost is added last and won't be wiped
            RedrawZoneCanvas();

            _duplicateGhost = new System.Windows.Shapes.Rectangle
            {
                Width = source.Width,
                Height = source.Height,
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 2,
                StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 255)),
                IsHitTestVisible = false
            };
            zoneEditorCanvas.Children.Add(_duplicateGhost);

            zoneEditorCanvas.Cursor = System.Windows.Input.Cursors.Hand;
        }

        private void BeginMovePlacement(ZonePlacement zone)
        {
            // Cancel any in-progress pending operation first
            if (_duplicateGhost != null)
            {
                zoneEditorCanvas.Children.Remove(_duplicateGhost);
                _duplicateGhost = null;
            }
            _pendingDuplicate = null;
            _pendingMove = null;

            _pendingMove = zone;

            // Redraw first (excludes _pendingMove zone) then add ghost on top
            RedrawZoneCanvas();

            _duplicateGhost = new System.Windows.Shapes.Rectangle
            {
                Width = zone.Width,
                Height = zone.Height,
                Stroke = System.Windows.Media.Brushes.Yellow,
                StrokeThickness = 2,
                StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 0)),
                IsHitTestVisible = false
            };

            // Position ghost at the zone's current location immediately
            Canvas.SetLeft(_duplicateGhost, zone.X);
            Canvas.SetTop(_duplicateGhost, zone.Y);

            zoneEditorCanvas.Children.Add(_duplicateGhost);
            zoneEditorCanvas.Cursor = System.Windows.Input.Cursors.Hand;
        }

        /// <summary>
        /// Computes the largest rectangle that fits inside the zone's bounding box
        /// when rotated at zone.Rotation degrees, then returns it as a WPF Polygon
        /// centred in the bounding box.
        /// 
        /// The formula used is the standard "largest rotated rectangle inscribed in a
        /// rectangle" solution:  for a bounding box W×H and rotation angle θ,
        /// the inscribed rotated rectangle has:
        ///   w' = (W·cos θ - H·|sin θ|) / (cos²θ - sin²θ)   [when |sin 2θ| > 0]
        ///   h' = (H·cos θ - W·|sin θ|) / (cos²θ - sin²θ)
        /// clamped so both dimensions remain positive.
        /// </summary>
        private System.Windows.Shapes.Polygon CreateInscribedRotationIndicator(ZonePlacement zone, System.Windows.Media.Color color)
        {
            double angleDeg = zone.Rotation;
            double angleRad = angleDeg * Math.PI / 180.0;
            double cosA = Math.Abs(Math.Cos(angleRad));
            double sinA = Math.Abs(Math.Sin(angleRad));

            double W = zone.Width;
            double H = zone.Height;

            double iW, iH;

            // For 90° / 270° the formula degenerates; handle explicitly
            if (Math.Abs(sinA - 1.0) < 1e-6) // 90° or 270°
            {
                iW = H;
                iH = W;
            }
            else if (sinA < 1e-6) // 0° or 180° — shouldn't reach here but guard anyway
            {
                iW = W;
                iH = H;
            }
            else
            {
                // Largest axis-aligned rectangle that can contain a W×H box rotated by θ
                // Inverted: largest rotated-by-θ rectangle that fits inside a W×H box
                double denom = cosA * cosA - sinA * sinA;
                if (Math.Abs(denom) < 1e-6)
                {
                    // 45° / 135°: inscribed square with side = min(W,H) / √2
                    double side = Math.Min(W, H) / Math.Sqrt(2.0);
                    iW = side;
                    iH = side;
                }
                else
                {
                    iW = (W * cosA - H * sinA) / denom;
                    iH = (H * cosA - W * sinA) / denom;
                }
            }

            // Clamp — if the rotation means nothing fits, fall back to a minimal indicator
            iW = Math.Max(4, iW);
            iH = Math.Max(4, iH);

            // Centre of the bounding box
            double cx = zone.X + W / 2.0;
            double cy = zone.Y + H / 2.0;

            // Four corners of the inscribed rectangle (unrotated, centred at origin)
            double hw = iW / 2.0;
            double hh = iH / 2.0;
            var corners = new[]
            {
                new System.Windows.Point(-hw, -hh),
                new System.Windows.Point( hw, -hh),
                new System.Windows.Point( hw,  hh),
                new System.Windows.Point(-hw,  hh),
            };

            // Rotate each corner by the actual angle (not abs) so it visually tilts correctly
            double rotRad = zone.Rotation * Math.PI / 180.0;
            double cos = Math.Cos(rotRad);
            double sin = Math.Sin(rotRad);

            var points = new System.Windows.Media.PointCollection();
            foreach (var c in corners)
            {
                points.Add(new System.Windows.Point(
                    cx + c.X * cos - c.Y * sin,
                    cy + c.X * sin + c.Y * cos));
            }

            return new System.Windows.Shapes.Polygon
            {
                Points = points,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(70, color.R, color.G, color.B)),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                IsHitTestVisible = false,
                ToolTip = $"Sticker orientation at {zone.Rotation}°"
            };
        }

        private void ExportZonesJson_Click(object sender, RoutedEventArgs e)
        {
            if (!_zonePlacements.Any()) return;

            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files|*.json",
                Title = "Export Zone Placements",
                FileName = "placements.json"
            };

            if (dialog.ShowDialog() != true) return;

            // Build SponsorTemplate from zone placements
            var template = new SponsorTemplate { TemplateId = Path.GetFileNameWithoutExtension(_templatePath ?? "template") };

            foreach (var group in _zonePlacements.GroupBy(z => z.SponsorNumber).OrderBy(g => g.Key))
            {
                var sponsor = new Sponsor
                {
                    SponsorNumber = group.Key,
                    Description = group.First().Description,
                    Placements = group.Select(z => new SponsorPlacement
                    {
                        X = z.X,
                        Y = z.Y,
                        Width = z.Width,
                        Height = z.Height,
                        Rotation = z.Rotation,
                        Portion = z.Portion,
                        PortionPercentageSize = z.PortionPercentageSize
                    }).ToList()
                };
                template.Sponsors.Add(sponsor);
            }

            var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            var json = JsonSerializer.Serialize(template, options);
            File.WriteAllText(dialog.FileName, json);

            MessageBox.Show("Zones exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void Cleanup()
        {
            _currentLivery?.Dispose();
        }
    }

    #region Models

    public class SponsorPlacement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Rotation { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SponsorPortion? Portion { get; set; }
        public int? PortionPercentageSize { get; set; }
    }

    public enum SponsorPortion
    {
        TOP_HALF,
        BOTTOM_HALF,
        LEFT_SIDE,
        RIGHT_SIDE
    }

    public class Sponsor
    {
        public int SponsorNumber { get; set; }
        public string Description { get; set; }
        public List<SponsorPlacement> Placements { get; set; } = new();
    }

    public class SponsorTemplate
    {
        public string TemplateId { get; set; }
        public List<Sponsor> Sponsors { get; set; } = new();
    }

    public class ZonePlacement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int SponsorNumber { get; set; }
        public string Description { get; set; }
        public int Rotation { get; set; }
        public SponsorPortion? Portion { get; set; }
        public int? PortionPercentageSize { get; set; }
    }

    #endregion
}