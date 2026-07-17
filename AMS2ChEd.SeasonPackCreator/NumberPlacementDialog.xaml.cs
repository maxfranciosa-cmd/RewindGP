using AMS2ChEd.Business.AMS2.Models;
using BCnEncoder.ImageSharp;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using Point = System.Drawing.Point;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class NumberPlacementDialog : Window
    {
        public NumbersPlacement NumberPlacement { get; private set; }
        private string _teamId;
        private Dictionary<string, string> _textureFiles;

        public NumberPlacementDialog(string teamId, Dictionary<string, string> textureFiles, NumbersPlacement placement = null)
        {
            InitializeComponent();

            _teamId = teamId;
            _textureFiles = textureFiles;

            RotationComboBox.ItemsSource = Enum.GetValues(typeof(NumberRotation)).Cast<NumberRotation>();

            if (placement == null)
            {
                NumberPlacement = new NumbersPlacement
                {
                    NumberRotation = NumberRotation.Deg0,
                    StartingPoint = new Point(0, 0)
                };

                // Set default values in UI
                RotationComboBox.SelectedIndex = 0; // Select first item (Deg0)
            }
            else
            {
                NumberPlacement = placement;
                LoadPlacementData();
            }
        }

        private void LoadPlacementData()
        {
            NumbersTextureTextBox.Text = NumberPlacement.NumbersTexture;
            NumberPlateWidthTextBox.Text = NumberPlacement.NumberPlateWidth.ToString();
            StartXTextBox.Text = NumberPlacement.StartingPoint.X.ToString();
            StartYTextBox.Text = NumberPlacement.StartingPoint.Y.ToString();
            RotationComboBox.SelectedItem = NumberPlacement.NumberRotation;
            FillColorTextBox.Text = NumberPlacement.FillColor ?? "";
        }

        private void BrowseNumbersTexture_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.dds;*.png)|*.dds;*.png|All files (*.*)|*.*",
                Title = "Select Numbers Texture"
            };

            if (dialog.ShowDialog() == true)
            {

                var filename = System.IO.Path.GetFileName(dialog.FileName);
                var fileNamePng = Path.ChangeExtension(filename, "png");
                NumbersTextureTextBox.Text = $"car_liveries/{_teamId}/{fileNamePng}";

                // Track the file for export
                var relativePath = $"car_liveries/{_teamId}/{fileNamePng}";
                _textureFiles[relativePath] = dialog.FileName;

                try
                {
                    var ( imageWidth, imageHeight ) = GetImageDimensions(dialog.FileName);
                    if (imageWidth > 0)
                    {
                        int plateWidth = imageWidth / 5;
                        NumberPlateWidthTextBox.Text = plateWidth.ToString();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not read image dimensions: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private (int width, int height) GetImageDimensions(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp")
            {
                // Use System.Drawing for common image formats
                using (var img = Image.FromFile(filePath))
                {
                    return (img.Width, img.Height);
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
                        return (image.Width, image.Height);
                    }
                }
            }

            return (0,0);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
            {
                return;
            }

            NumberPlacement.NumbersTexture = NumbersTextureTextBox.Text;
            NumberPlacement.NumberPlateWidth = int.Parse(NumberPlateWidthTextBox.Text);
            NumberPlacement.StartingPoint = new Point(
                int.Parse(StartXTextBox.Text),
                int.Parse(StartYTextBox.Text)
            );
            NumberPlacement.NumberRotation = (NumberRotation)RotationComboBox.SelectedItem;
            NumberPlacement.FillColor = string.IsNullOrWhiteSpace(FillColorTextBox.Text) ? null : FillColorTextBox.Text;

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
            if (string.IsNullOrWhiteSpace(NumbersTextureTextBox.Text))
            {
                MessageBox.Show("Numbers Texture is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(NumberPlateWidthTextBox.Text, out _))
            {
                MessageBox.Show("Number Plate Width must be a valid integer.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(StartXTextBox.Text, out _))
            {
                MessageBox.Show("Start X must be a valid integer.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(StartYTextBox.Text, out _))
            {
                MessageBox.Show("Start Y must be a valid integer.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (RotationComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a rotation.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(FillColorTextBox.Text) && !IsValidFillColor(FillColorTextBox.Text))
            {
                MessageBox.Show("Fill Color must be a valid HTML color (e.g. #FF0000, #80FF0000 for translucent, or Red).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        // ColorTranslator.FromHtml only understands the 3/6-digit hex forms, so 8-digit
        // "#AARRGGBB" (as produced by the livery preview's eyedropper) needs its own check.
        private static bool IsValidFillColor(string text)
        {
            string trimmed = text.Trim();
            string hexPart = trimmed.StartsWith("#") ? trimmed.Substring(1) : trimmed;

            if (hexPart.Length == 8)
            {
                try
                {
                    Convert.ToByte(hexPart.Substring(0, 2), 16);
                    Convert.ToByte(hexPart.Substring(2, 2), 16);
                    Convert.ToByte(hexPart.Substring(4, 2), 16);
                    Convert.ToByte(hexPart.Substring(6, 2), 16);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                System.Drawing.ColorTranslator.FromHtml(trimmed);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}