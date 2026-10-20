using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Resources;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// Overview of the improvement packages every team has installed this season, and which
    /// way they have pushed each performance value.
    /// </summary>
    public partial class ImprovementPackagesWindow : Window
    {
        private const string UP_CHEVRON = "▲";
        private const string DOWN_CHEVRON = "▼";


        public ImprovementPackagesWindow(ISaveGame saveGame, IReadOnlyList<ImprovementStat> stats)
        {
            InitializeComponent();

            SeasonYearText.Text = string.Format(Strings.ConstructorStandingsGridWindow_SeasonYear_Format, saveGame.CurrentSeason.Year);
            TitleUnlitText.Text = UnlitSegments(TitleText.Text);
            SeasonYearUnlitText.Text = UnlitSegments(SeasonYearText.Text);

            PopulateTeams(saveGame, stats);
        }

        private void PopulateTeams(ISaveGame saveGame, IReadOnlyList<ImprovementStat> stats)
        {
            // team colour accent, team name, number of packages, then one column per performance value
            TeamsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            TeamsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            TeamsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            foreach (var stat in stats)
            {
                TeamsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            }

            TeamsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            AddCell(CreateText(Strings.ConstructorStandingsGridWindow_TeamColumnHeader, "HeaderCellText"), 0, 0, isHeader: true, columnSpan: 2);
            AddCell(CreateText(Strings.ImprovementPackagesWindow_PackagesColumnHeader, "HeaderCellText"), 0, 2, isHeader: true);
            for (int i = 0; i < stats.Count; i++)
            {
                AddCell(CreateText(stats[i].DisplayName.ToUpper(), "HeaderCellText"), 0, i + 3, isHeader: true);
            }

            var teams = saveGame.CurrentSeason.Teams
                .OrderBy(t => saveGame.CurrentConstructorStandings?.FirstOrDefault(s => s.TeamId == t.TeamId)?.Position ?? int.MaxValue)
                .ToList();

            for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                var team = teams[teamIndex];
                var packages = team.ImprovementPackages ?? new List<ImprovementPackage>();
                int row = teamIndex + 1;

                TeamsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });

                var accent = new Border { Background = GetTeamColor(team) };
                Grid.SetRow(accent, row);
                Grid.SetColumn(accent, 0);
                TeamsGrid.Children.Add(accent);

                AddCell(CreateSegmentText(team.ShortTeamName().ToUpper(), "TeamNameText"), row, 1);

                AddCell(CreateSegmentText($"{packages.Count}/{ImprovementPackageManager.PACKAGES_PER_SEASON}"), row, 2);

                for (int i = 0; i < stats.Count; i++)
                {
                    AddCell(CreateTrendText(packages, stats[i]), row, i + 3);
                }
            }
        }

        // one chevron per package that moved the value in the prevailing direction, "=" when as
        // many packages raised it as lowered it; green when that direction is a gain for the car.
        // A style with trend lamps lights one lamp per such package instead, out of a row of as
        // many lamps as a team may install in a season.
        private UIElement CreateTrendText(List<ImprovementPackage> packages, ImprovementStat stat)
        {
            var values = packages
                .Where(p => p.Values != null && p.Values.ContainsKey(stat.Key))
                .Select(p => p.Values[stat.Key])
                .ToList();

            int raised = values.Count(v => v > 0);
            int lowered = values.Count(v => v < 0);

            if (raised == lowered)
                return CreateSegmentText("=");

            bool isRaised = raised > lowered;
            bool isGain = isRaised == stat.HigherIsBetter;
            int count = isRaised ? raised : lowered;
            string chevron = isRaised ? UP_CHEVRON : DOWN_CHEVRON;

            if (TryFindResource("ImprovementPackages.TrendLampUnlit") is Style unlitLampStyle)
            {
                var litLampStyle = (Style)FindResource(isGain ? "ImprovementPackages.TrendLampGain" : "ImprovementPackages.TrendLampLoss");
                var lamps = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                for (int i = 0; i < Math.Max(count, ImprovementPackageManager.PACKAGES_PER_SEASON); i++)
                {
                    lamps.Children.Add(new TextBlock { Text = chevron, Style = i < count ? litLampStyle : unlitLampStyle });
                }
                return lamps;
            }

            var text = CreateText(string.Concat(Enumerable.Repeat(chevron, count)));
            text.Foreground = (System.Windows.Media.Brush)FindResource(isGain ? "ImprovementPackages.GainBrush" : "ImprovementPackages.LossBrush");
            return text;
        }

        // the look of every cell and of its text comes from the active visual style
        private TextBlock CreateText(string text, string styleName = "CellText")
        {
            return new TextBlock
            {
                Text = text,
                Style = (Style)FindResource("ImprovementPackages." + styleName)
            };
        }

        // a display field: when the active style has an "Unlit" version of the text style, the
        // text is drawn over a copy of it with every segment unlit, as on a segment display
        private UIElement CreateSegmentText(string text, string styleName = "CellText")
        {
            var lit = CreateText(text, styleName);
            if (TryFindResource("ImprovementPackages." + styleName + "Unlit") is not Style unlitStyle)
                return lit;

            var field = new Grid();
            field.Children.Add(new TextBlock { Text = UnlitSegments(text), Style = unlitStyle });
            field.Children.Add(lit);
            return field;
        }

        // every character a segment display has turns into "~", which lights all of its segments in
        // the DSEG fonts; the rest (spaces, dots, accented letters) stay as they are, so the copy
        // keeps the width of the text it sits under
        private static string UnlitSegments(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return new string(text.Select(c => char.IsAsciiLetterOrDigit(c) || "-/&=+*'".Contains(c) ? '~' : c).ToArray());
        }

        private void AddCell(UIElement content, int row, int column, bool isHeader = false, int columnSpan = 1)
        {
            string styleName = isHeader ? "HeaderCell" : row % 2 == 1 ? "Cell" : "AlternateCell";
            var border = new Border
            {
                Style = (Style)FindResource("ImprovementPackages." + styleName),
                Child = content
            };

            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
            Grid.SetColumnSpan(border, columnSpan);
            TeamsGrid.Children.Add(border);
        }

        private static SolidColorBrush GetTeamColor(ITeamEntry team)
        {
            if (!string.IsNullOrEmpty(team.Color))
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(team.Color));
                }
                catch
                {
                    // If color conversion fails, use default
                }
            }

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
        }
    }
}
