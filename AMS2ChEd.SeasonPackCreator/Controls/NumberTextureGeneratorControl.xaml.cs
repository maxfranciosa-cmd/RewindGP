using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AMS2ChEd.SeasonPackEditor.Controls
{
    public partial class NumberTextureGeneratorControl : UserControl
    {
        private const double EmSize = 100.0;
        private const double CellPaddingFraction = 0.1;

        private RenderTargetBitmap _currentBitmap;
        private bool _isInitialized;

        public NumberTextureGeneratorControl()
        {
            InitializeComponent();

            cmbFont.ItemsSource = Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .Distinct()
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cmbFont.SelectedItem = (string)cmbFont.Items.Cast<string>().FirstOrDefault(f => f == "Arial")
                ?? cmbFont.Items.Cast<string>().FirstOrDefault();

            _isInitialized = true;

            UpdateFontPreview();
            RegeneratePreview();
        }

        private void Field_Changed(object sender, RoutedEventArgs e)
        {
            // TextChanged/SelectionChanged also fire while InitializeComponent is still
            // parsing the XAML tree, before every named field has been assigned.
            if (!_isInitialized)
                return;

            UpdateColorSwatch(txtMainColor, previewMainColor);
            UpdateColorSwatch(txtOutlineColor, previewOutlineColor);
            UpdateFontPreview();
            RegeneratePreview();
        }

        private void UpdateFontPreview()
        {
            if (cmbFont.SelectedItem is string fontName)
            {
                txtFontPreview.FontFamily = new System.Windows.Media.FontFamily(fontName);
            }
        }

        private void UpdateColorSwatch(TextBox textBox, System.Windows.Shapes.Rectangle swatch)
        {
            if (TryParseHexColor(textBox.Text, out var color))
            {
                swatch.Fill = new SolidColorBrush(color);
                textBox.Background = System.Windows.Media.Brushes.White;
            }
            else
            {
                textBox.Background = new SolidColorBrush(Color.FromRgb(255, 230, 230));
            }
        }

        private void RegeneratePreview()
        {
            if (cmbFont.SelectedItem == null)
                return;

            string fontName = (string)cmbFont.SelectedItem;

            if (!TryParseHexColor(txtMainColor.Text, out var mainColor))
                return;

            if (!TryParseHexColor(txtOutlineColor.Text, out var outlineColor))
                return;

            if (!int.TryParse(txtDigitWidth.Text, out int digitWidth) || digitWidth <= 0)
                return;

            if (!int.TryParse(txtDigitHeight.Text, out int digitHeight) || digitHeight <= 0)
                return;

            if (!double.TryParse(txtOutlineThickness.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double outlineThickness) || outlineThickness < 0)
                return;

            try
            {
                _currentBitmap = RenderNumbersStrip(fontName, mainColor, outlineColor, outlineThickness, digitWidth, digitHeight);

                imgPreview.Source = _currentBitmap;
                txtNoPreview.Visibility = Visibility.Collapsed;
                previewScroll.Visibility = Visibility.Visible;
                btnExport.IsEnabled = true;
            }
            catch
            {
                // Leave last successfully rendered preview in place.
            }
        }

        private RenderTargetBitmap RenderNumbersStrip(
            string fontFamilyName,
            Color mainColor,
            Color outlineColor,
            double outlineThickness,
            int digitWidth,
            int digitHeight)
        {
            var typeface = new Typeface(
                new System.Windows.Media.FontFamily(fontFamilyName),
                FontStyles.Normal,
                FontWeights.Bold,
                FontStretches.Normal);

            var fillBrush = new SolidColorBrush(mainColor);
            fillBrush.Freeze();

            Pen outlinePen = null;
            if (outlineThickness > 0)
            {
                var outlineBrush = new SolidColorBrush(outlineColor);
                outlineBrush.Freeze();
                outlinePen = new Pen(outlineBrush, outlineThickness)
                {
                    LineJoin = PenLineJoin.Round
                };
                outlinePen.Freeze();
            }

            double padding = Math.Min(digitWidth, digitHeight) * CellPaddingFraction;
            double availableWidth = Math.Max(1, digitWidth - 2 * padding - outlineThickness);
            double availableHeight = Math.Max(1, digitHeight - 2 * padding - outlineThickness);

            // First pass: measure every digit at the same em size so we can derive a single
            // shared scale. Using a per-digit scale here would give each glyph a different
            // font size (e.g. "1" blown up to fill its cell like "8" does).
            var geometries = new Geometry[10];
            var boundsList = new Rect[10];
            double maxBoundsWidth = 0;
            double maxBoundsHeight = 0;

            for (int digit = 0; digit <= 9; digit++)
            {
                var formattedText = new FormattedText(
                    digit.ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    EmSize,
                    fillBrush,
                    1.0);

                var geometry = formattedText.BuildGeometry(new System.Windows.Point(0, 0));
                var bounds = geometry.Bounds;

                geometries[digit] = geometry;
                boundsList[digit] = bounds;

                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                    continue;

                maxBoundsWidth = Math.Max(maxBoundsWidth, bounds.Width);
                maxBoundsHeight = Math.Max(maxBoundsHeight, bounds.Height);
            }

            double scale = Math.Min(availableWidth / maxBoundsWidth, availableHeight / maxBoundsHeight);
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                scale = 1.0;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                for (int digit = 0; digit <= 9; digit++)
                {
                    var geometry = geometries[digit];
                    var bounds = boundsList[digit];

                    if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                        continue;

                    double cellCenterX = digit * digitWidth + digitWidth / 2.0;
                    double cellCenterY = digitHeight / 2.0;
                    double scaledCenterX = (bounds.Left + bounds.Width / 2.0) * scale;
                    double scaledCenterY = (bounds.Top + bounds.Height / 2.0) * scale;

                    var transform = new TransformGroup();
                    transform.Children.Add(new ScaleTransform(scale, scale));
                    transform.Children.Add(new TranslateTransform(cellCenterX - scaledCenterX, cellCenterY - scaledCenterY));
                    geometry.Transform = transform;

                    dc.DrawGeometry(fillBrush, outlinePen, geometry);
                }
            }

            int totalWidth = digitWidth * 10;
            var bitmap = new RenderTargetBitmap(totalWidth, digitHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private void Zoom_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var scale = double.Parse((string)button.Tag, CultureInfo.InvariantCulture);

            previewScale.ScaleX = scale;
            previewScale.ScaleY = scale;

            btnZoom100.FontWeight = scale == 1.0 ? FontWeights.Bold : FontWeights.Normal;
            btnZoom200.FontWeight = scale == 2.0 ? FontWeights.Bold : FontWeights.Normal;
            btnZoom400.FontWeight = scale == 4.0 ? FontWeights.Bold : FontWeights.Normal;
        }

        private void ExportPng_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBitmap == null)
            {
                MessageBox.Show("Please generate a preview first.", "No Texture",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Export Numbers Texture",
                FileName = "numbers.png"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(_currentBitmap));

                    using (var fs = File.Create(dialog.FileName))
                    {
                        encoder.Save(fs);
                    }

                    MessageBox.Show("Numbers texture exported successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting texture: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool TryParseHexColor(string hex, out Color color)
        {
            color = Colors.White;

            if (string.IsNullOrWhiteSpace(hex))
                return false;

            hex = hex.TrimStart('#');

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

                color = Color.FromArgb(a, r, g, b);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
