using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AMS2ChEd.SeasonPackEditor
{
    /// <summary>
    /// Converts hex color strings (e.g., "#00D2BE") to SolidColorBrush
    /// Returns gray brush if string is null, empty, or invalid
    /// </summary>
    public class StringToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return DefaultBrush;
            }

            try
            {
                string colorString = value.ToString();

                // Ensure string starts with #
                if (!colorString.StartsWith("#"))
                {
                    colorString = "#" + colorString;
                }

                // Use ColorConverter to parse the hex string
                var color = (Color)ColorConverter.ConvertFromString(colorString);
                var brush = new SolidColorBrush(color);
                return brush;
            }
            catch (Exception ex)
            {
                // If conversion fails, return default gray
                return DefaultBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color.ToString();
            }

            return null;
        }
    }
}