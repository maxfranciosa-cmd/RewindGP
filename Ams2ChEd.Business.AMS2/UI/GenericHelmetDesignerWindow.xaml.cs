using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Ams2ChEd.Business.AMS2.UI
{
    /// <summary>
    /// Developer tool: pick a generic helmet design, recolour it and copy the generic_helmet
    /// json for a driver. Previews the Modern helmet's texture, not the 3D render.
    /// </summary>
    public partial class GenericHelmetDesignerWindow : Window
    {
        private readonly string _folder = StoragePaths.GenericHelmetDesignsFolder;
        private bool _loading;

        public GenericHelmetDesignerWindow()
        {
            InitializeComponent();
            if (Directory.Exists(_folder))
            {
                foreach (var file in Directory.GetFiles(_folder, "*.png").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.StartsWith("90s_") || name.StartsWith("80s_") || name.StartsWith("70s_"))
                        continue;
                    DesignComboBox.Items.Add(name);
                }
            }
            if (DesignComboBox.Items.Count > 0)
                DesignComboBox.SelectedIndex = 0;
        }

        private void DesignComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DesignComboBox.SelectedItem is not string design)
                return;

            var palette = GenericHelmetComposer.Palette(_folder, new GenericHelmet { Design = design });
            _loading = true;
            var rows = new List<ColourRow>();
            foreach (var key in palette)
            {
                var row = new ColourRow { Key = key.Key, Hex = key.Value };
                row.PropertyChanged += (_, _) => { if (!_loading) Refresh(); };
                rows.Add(row);
            }
            ColoursList.ItemsSource = rows;
            _loading = false;
            Refresh();
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).Tag is not ColourRow row)
                return;
            var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            if (row.TryGetColour(out var current))
                dialog.Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                row.Hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(JsonTextBox.Text))
                System.Windows.Clipboard.SetText(JsonTextBox.Text);
        }

        private void Refresh()
        {
            if (DesignComboBox.SelectedItem is not string design || ColoursList.ItemsSource is not List<ColourRow> rows)
                return;

            var palette = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (row.TryGetColour(out _))
                    palette[row.Key] = row.Hex.Trim().ToUpperInvariant();
            }
            var helmet = new GenericHelmet { Design = design, Palette = palette };

            using var image = GenericHelmetComposer.Build(helmet, HelmetEra.Modern, _folder);
            if (image != null)
            {
                using var stream = new MemoryStream();
                image.Save(stream, new PngEncoder());
                stream.Position = 0;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                PreviewImage.Source = bitmap;
            }
            else
            {
                PreviewImage.Source = null;
            }

            JsonTextBox.Text = BuildJson(design, palette);
        }

        private static string BuildJson(string design, Dictionary<string, string> palette)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\"generic_helmet\": {");
            sb.AppendLine($"  \"design\": \"{design}\",");
            sb.AppendLine("  \"palette\": {");
            var entries = palette.Select(p => $"    \"{p.Key.ToLowerInvariant()}\": \"{p.Value}\"").ToList();
            sb.AppendLine(string.Join("," + Environment.NewLine, entries));
            sb.AppendLine("  }");
            sb.Append('}');
            return sb.ToString();
        }

        private class ColourRow : INotifyPropertyChanged
        {
            private string _hex;

            public string Key { get; set; }

            public string Hex
            {
                get => _hex;
                set
                {
                    if (_hex == value) return;
                    _hex = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hex)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Brush)));
                }
            }

            public System.Windows.Media.Brush Brush => TryGetColour(out var colour) ? new SolidColorBrush(colour) : System.Windows.Media.Brushes.Transparent;

            public bool TryGetColour(out System.Windows.Media.Color colour)
            {
                colour = default;
                try
                {
                    if (string.IsNullOrWhiteSpace(_hex) || !_hex.Trim().StartsWith('#') || _hex.Trim().Length != 7)
                        return false;
                    colour = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_hex.Trim());
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
