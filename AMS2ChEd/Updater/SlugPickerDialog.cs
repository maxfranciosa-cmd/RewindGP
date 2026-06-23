using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AMS2ChEd.Business.Updater.Models;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;

namespace AMS2ChEd.Dialogs
{
    public class SlugPickerDialog : Window
    {
        public SlugPickerDialog(int year, List<SeasonManifestEntry> options, string downloadUrlFormat)
        {
            var optionsWithoutDefault = options.Where(o => !o.IsDefault).ToList();
            var defaultOption = options.FirstOrDefault(o => o.IsDefault);
            var hasDefault = defaultOption != null;
            
            Title = $"Choose {year} Season Pack";
            Width = 460;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1a1a1a"));

            var panel = new StackPanel { Margin = new Thickness(24) };

            var title = new TextBlock
            {
                Text = $"PACKS AVAILABLE FOR {year}",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            panel.Children.Add(title);

            var istructionText = ((hasDefault) ? $"You have currently installed {defaultOption.DisplayName}, but there are more season packs available for {year}.\n\n" : "") +
                                 "Download the pack you want using the links below, then click OK to locate the file.";

            var instructions = new TextBlock
            {
                Text = istructionText,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#cccccc")),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
            panel.Children.Add(instructions);

            // One hyperlink per option
            foreach (var option in optionsWithoutDefault)
            {
                var tb = new TextBlock { Margin = new Thickness(0, 0, 0, 8) };
                var link = new Hyperlink(new Run(
                    $"{option.DisplayName}" +
                    (option.FileSizeMb > 0 ? $" ({option.FileSizeMb} MB)" : "") +
                    (!string.IsNullOrEmpty(option.Credits) ? $" — by {option.Credits}" : "")));

                link.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#c41e3a"));

                var url = option.PageUrl;
                link.Click += (_, _) =>
                    Process.Start(new ProcessStartInfo(string.Format(downloadUrlFormat, url)) { UseShellExecute = true });

                tb.Inlines.Add(link);
                panel.Children.Add(tb);
            }

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var cancelBtn = new Button
            {
                Content = hasDefault ? "NO, CONTINUE WITH WHAT I'VE GOT" : "CANCEL",
                Width = hasDefault ? 250 : 100,
                Height = 36,
                Margin = new Thickness(0, 0, 12, 0)
            };
            cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };

            var okBtn = new Button
            {
                Content = "I'VE DOWNLOADED IT",
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0)
            };
            okBtn.Click += (_, _) => { DialogResult = true; Close(); };

            buttonPanel.Children.Add(cancelBtn);
            buttonPanel.Children.Add(okBtn);
            panel.Children.Add(buttonPanel);

            Content = panel;
        }
    }
}