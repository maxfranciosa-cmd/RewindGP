using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
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

        private static readonly SolidColorBrush GainBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e7d32"));
        private static readonly SolidColorBrush LossBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#c41e3a"));

        public ImprovementPackagesWindow(ISaveGame saveGame, IReadOnlyList<ImprovementStat> stats)
        {
            InitializeComponent();

            SeasonYearText.Text = string.Format(Strings.ConstructorStandingsGridWindow_SeasonYear_Format, saveGame.CurrentSeason.Year);

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

            TeamsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            AddCell(CreateText(Strings.ConstructorStandingsGridWindow_TeamColumnHeader, isHeader: true), 0, 0, isHeader: true, columnSpan: 2);
            AddCell(CreateText(Strings.ImprovementPackagesWindow_PackagesColumnHeader, isHeader: true), 0, 2, isHeader: true);
            for (int i = 0; i < stats.Count; i++)
            {
                AddCell(CreateText(stats[i].DisplayName.ToUpper(), isHeader: true), 0, i + 3, isHeader: true);
            }

            var teams = saveGame.CurrentSeason.Teams
                .OrderBy(t => saveGame.CurrentConstructorStandings?.FirstOrDefault(s => s.TeamId == t.TeamId)?.Position ?? int.MaxValue)
                .ToList();

            for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                var team = teams[teamIndex];
                var packages = team.ImprovementPackages ?? new List<ImprovementPackage>();
                int row = teamIndex + 1;

                TeamsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

                var accent = new Border { Background = GetTeamColor(team) };
                Grid.SetRow(accent, row);
                Grid.SetColumn(accent, 0);
                TeamsGrid.Children.Add(accent);

                var teamName = CreateText(team.TeamName.ToUpper());
                teamName.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                teamName.Padding = new Thickness(10, 4, 0, 4);
                AddCell(teamName, row, 1);

                AddCell(CreateText($"{packages.Count}/{ImprovementPackageManager.PACKAGES_PER_SEASON}"), row, 2);

                for (int i = 0; i < stats.Count; i++)
                {
                    AddCell(CreateTrendText(packages, stats[i]), row, i + 3);
                }
            }
        }

        // one chevron per package that moved the value in the prevailing direction, "=" when as
        // many packages raised it as lowered it; green when that direction is a gain for the car
        private TextBlock CreateTrendText(List<ImprovementPackage> packages, ImprovementStat stat)
        {
            var values = packages
                .Where(p => p.Values != null && p.Values.ContainsKey(stat.Key))
                .Select(p => p.Values[stat.Key])
                .ToList();

            int raised = values.Count(v => v > 0);
            int lowered = values.Count(v => v < 0);

            if (raised == lowered)
                return CreateText("=");

            bool isRaised = raised > lowered;
            var text = CreateText(string.Concat(Enumerable.Repeat(isRaised ? UP_CHEVRON : DOWN_CHEVRON, isRaised ? raised : lowered)));
            text.Foreground = isRaised == stat.HigherIsBetter ? GainBrush : LossBrush;
            return text;
        }

        private static TextBlock CreateText(string text, bool isHeader = false)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Courier New"),
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                FontSize = isHeader ? 16 : 15,
                Foreground = Brushes.Black,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 4, 5, 4)
            };
        }

        private void AddCell(TextBlock content, int row, int column, bool isHeader = false, int columnSpan = 1)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 1, isHeader ? 2 : 1),
                Background = isHeader || row % 2 == 1 ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fafafa")),
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
