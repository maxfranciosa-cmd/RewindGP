using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AMS2ChEd.SeasonPackEditor.HelmetEditor.Models;

namespace AMS2ChEd.SeasonPackEditor.HelmetEditor.Controls
{
    // ════════════════════════════════════════════════════════════════════════
    // RgbaSliderPanel
    // ════════════════════════════════════════════════════════════════════════

    public class RgbaSliderPanel : UserControl
    {
        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register(nameof(Color), typeof(Color), typeof(RgbaSliderPanel),
                new FrameworkPropertyMetadata(Colors.White,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnColorChanged));

        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        private readonly Slider _r = MakeSlider();
        private readonly Slider _g = MakeSlider();
        private readonly Slider _b = MakeSlider();
        private readonly Slider _a = MakeSlider();
        private bool _sync;

        public RgbaSliderPanel()
        {
            var rows = new StackPanel();
            rows.Children.Add(MakeRow("R", _r, Color.FromRgb(200, 80,  80)));
            rows.Children.Add(MakeRow("G", _g, Color.FromRgb(80,  180, 80)));
            rows.Children.Add(MakeRow("B", _b, Color.FromRgb(80,  120, 220)));
            rows.Children.Add(MakeRow("A", _a, Color.FromRgb(130, 130, 130)));
            Content = rows;
            Background = Brushes.Transparent;

            _r.ValueChanged += OnSlider;
            _g.ValueChanged += OnSlider;
            _b.ValueChanged += OnSlider;
            _a.ValueChanged += OnSlider;

            SyncFrom(Colors.White);
        }

        private static Slider MakeSlider() => new()
        {
            Minimum = 0, Maximum = 255,
            SmallChange = 1, LargeChange = 16,
            VerticalAlignment = VerticalAlignment.Center, Height = 18
        };

        private static UIElement MakeRow(string label, Slider sl, Color accent)
        {
            var lbl = new TextBlock
            {
                Text = label, Width = 12, FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accent),
                VerticalAlignment = VerticalAlignment.Center
            };
            var val = new TextBlock
            {
                Width = 26, FontSize = 9, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            val.SetBinding(TextBlock.TextProperty,
                new Binding("Value") { Source = sl, StringFormat = "{0:0}" });

            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2), Height = 22 };
            DockPanel.SetDock(lbl, Dock.Left);
            DockPanel.SetDock(val, Dock.Right);
            row.Children.Add(lbl);
            row.Children.Add(val);
            row.Children.Add(sl);
            return row;
        }

        private void OnSlider(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_sync) return;
            Color = Color.FromArgb((byte)_a.Value, (byte)_r.Value,
                                   (byte)_g.Value, (byte)_b.Value);
        }

        private static void OnColorChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
            => ((RgbaSliderPanel)d).SyncFrom((Color)e.NewValue);

        private void SyncFrom(Color c)
        {
            _sync = true;
            _r.Value = c.R; _g.Value = c.G; _b.Value = c.B; _a.Value = c.A;
            _sync = false;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Converters
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>bool → Visibility (true = Visible)</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) =>
            v is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
            v is Visibility.Visible;
    }

    /// <summary>bool → Visibility (true = Collapsed)</summary>
    public class InverseBoolVisConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) =>
            v is true ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
            v is not Visibility.Visible;
    }

    /// <summary>int count → Visibility: Visible when 0, Collapsed otherwise.</summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            if (v == null) return Visibility.Visible;
            if (v is int i) return i == 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }

    /// <summary>KeyColor enum → WPF Color, for the key-color dot in the sticker slot list.</summary>
    public class KeyColorToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            if (v is KeyColor kc) return KeyColorValues.ToWpfColor(kc);
            return Colors.Gray;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }

    /// <summary>Full file path → filename only.</summary>
    public class FileNameConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) =>
            v is string s ? Path.GetFileName(s) : string.Empty;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }

    /// <summary>ShowPreview bool → button label text.</summary>
    public class PreviewButtonLabelConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) =>
            v is true ? "👁  Hide Preview" : "👁  Show Preview";
        public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }

    /// <summary>Returns true when two HelmetEra values are equal.</summary>
    public class EraEqualityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type t, object p, CultureInfo c) =>
            values.Length == 2 &&
            values[0] is HelmetEra a &&
            values[1] is HelmetEra b &&
            a == b;
        public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }
}
