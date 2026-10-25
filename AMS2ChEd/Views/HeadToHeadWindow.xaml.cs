using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Extensions;
using AMS2ChEd.Resources;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Image = System.Windows.Controls.Image;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// Teammates compared over the current season, team by team (HeadToHeadCalculator). Earlier
    /// seasons aren't offered: the save doesn't keep who was under contract back then.
    /// </summary>
    public partial class HeadToHeadWindow : Window
    {
        private class TeamItem
        {
            public string TeamId { get; set; }
            public string TeamName { get; set; }
            public Brush TeamColor { get; set; }
            public string Note { get; set; }
            public bool HasSubstitutes { get; set; }
        }

        private readonly ISaveGame _saveGame;
        private readonly Dictionary<string, IDriverData> _drivers;

        public HeadToHeadWindow(ISaveGame saveGame)
        {
            InitializeComponent();

            _saveGame = saveGame;
            _drivers = (saveGame.Drivers ?? Enumerable.Empty<IDriverData>())
                .Concat(saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>())
                .GroupBy(d => d.DriverId)
                .ToDictionary(g => g.Key, g => g.First());

            TeamsHeaderText.Text = string.Format(Strings.HeadToHeadWindow_TeamsHeader, saveGame.CurrentSeason.Year);
            SheetCaptionText.Text = string.Format(Strings.HeadToHeadWindow_SheetCaption, saveGame.CurrentSeason.Year);

            // teams in championship order
            var standings = (saveGame.CurrentConstructorStandings ?? Enumerable.Empty<ConstructorStandingEntry>())
                .ToDictionary(s => s.TeamId, s => s.Position);
            var items = saveGame.CurrentSeason.Teams
                .OrderBy(t => standings.GetValueOrDefault(t.TeamId, int.MaxValue))
                .Select(t => CreateTeamItem(t))
                .ToList();
            TeamList.ItemsSource = items;
            TeamList.SelectedItem = items.FirstOrDefault(i => i.TeamId == saveGame.PlayerData?.TeamId) ?? items.FirstOrDefault();
        }

        private TeamItem CreateTeamItem(ITeamEntry team)
        {
            var headToHead = HeadToHeadCalculator.GetTeamHeadToHead(_saveGame, team.TeamId);
            var subbed = headToHead.RacesWithSubstitute;

            return new TeamItem
            {
                TeamId = team.TeamId,
                TeamName = team.TeamName,
                TeamColor = ToBrush(team.Color),
                HasSubstitutes = subbed > 0,
                Note = subbed == 1 ? Strings.HeadToHeadWindow_SubbedOne
                    : subbed > 1 ? string.Format(Strings.HeadToHeadWindow_SubbedMany, subbed)
                    : string.Join(" · ", headToHead.Drivers.Where(d => !d.IsSubstitute).Select(d => Surname(d.DriverId)))
            };
        }

        private void TeamList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TeamList.SelectedItem is TeamItem item)
            {
                ShowTeam(item);
            }
        }

        private void ShowTeam(TeamItem item)
        {
            var headToHead = HeadToHeadCalculator.GetTeamHeadToHead(_saveGame, item.TeamId);
            var contracted = headToHead.Drivers.Where(d => !d.IsSubstitute).ToList();

            TeamTitleText.Text = item.TeamName;
            RacesTogetherText.Text = headToHead.IsOneCarTeam ? "" : string.Format(Strings.HeadToHeadWindow_RacesTogether, headToHead.RacesTogether, headToHead.RacesRun);

            ComparisonPanel.Visibility = headToHead.IsOneCarTeam ? Visibility.Collapsed : Visibility.Visible;
            OneCarTeamText.Visibility = headToHead.IsOneCarTeam ? Visibility.Visible : Visibility.Collapsed;
            if (!headToHead.IsOneCarTeam)
            {
                ShowDriver(contracted[0], Driver1Photo, Driver1PhotoPlaceholder, Driver1NameText, Driver1DetailText);
                ShowDriver(contracted[1], Driver2Photo, Driver2PhotoPlaceholder, Driver2NameText, Driver2DetailText);

                QualifyingScoreText.Text = $"{contracted[0].QualifyingWins} – {contracted[1].QualifyingWins}";
                RaceScoreText.Text = $"{contracted[0].RaceWins} – {contracted[1].RaceWins}";

                Driver1AverageGridText.Text = FormatAverage(contracted[0].AverageGrid);
                Driver2AverageGridText.Text = FormatAverage(contracted[1].AverageGrid);
                Driver1AverageFinishText.Text = FormatAverage(contracted[0].AverageFinish);
                Driver2AverageFinishText.Text = FormatAverage(contracted[1].AverageFinish);
                Driver1BestFinishText.Text = contracted[0].BestFinish?.ToString() ?? "–";
                Driver2BestFinishText.Text = contracted[1].BestFinish?.ToString() ?? "–";
                Driver1RetirementsText.Text = contracted[0].Retirements.ToString();
                Driver2RetirementsText.Text = contracted[1].Retirements.ToString();
            }

            BuildRaceTable(headToHead);
            var anyRun = headToHead.RacesRun > 0;
            NoRacesText.Visibility = anyRun ? Visibility.Collapsed : Visibility.Visible;
            LegendText.Visibility = anyRun && !headToHead.IsOneCarTeam ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowDriver(HeadToHeadDriver driver, Image photo, UIElement placeholder, TextBlock name, TextBlock detail)
        {
            var data = _drivers.GetValueOrDefault(driver.DriverId);
            name.Text = Surname(driver.DriverId);
            photo.Source = null;
            placeholder.Visibility = Visibility.Visible;
            photo.LoadPhoto(data?.Portrait(), placeholder);

            var details = new List<string>();
            if (data?.YearOfBirth > 0)
                details.Add(string.Format(Strings.DriverAccoladesWindow_AgeText_Format, _saveGame.CurrentSeason.Year - data.YearOfBirth));
            if (!string.IsNullOrEmpty(data?.Nationality))
                details.Add(data.Nationality);
            detail.Text = string.Join(" · ", details);
        }

        // one column per race of the calendar, one row per driver: the contracted pair, then any substitute
        private void BuildRaceTable(TeamHeadToHead headToHead)
        {
            RaceTable.Children.Clear();
            RaceTable.ColumnDefinitions.Clear();
            RaceTable.RowDefinitions.Clear();

            RaceTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            foreach (var _ in headToHead.Races)
            {
                RaceTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            RaceTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int r = 0; r < headToHead.Races.Count; r++)
            {
                var race = headToHead.Races[r];
                var style = race.HasSubstitute ? "HeadToHead.RaceHeaderAlert" : race.IsRun ? "HeadToHead.RaceHeader" : "HeadToHead.RaceHeaderUpcoming";
                Place(new TextBlock { Text = ShortName(race), ToolTip = race.Name, Style = Style(style) }, 0, r + 1);
            }

            for (int d = 0; d < headToHead.Drivers.Count; d++)
            {
                var driver = headToHead.Drivers[d];
                RaceTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var nameCell = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                nameCell.Children.Add(new TextBlock { Text = Surname(driver.DriverId), Style = Style(driver.IsSubstitute ? "HeadToHead.RowNameSubstitute" : "HeadToHead.RowName") });
                if (driver.IsSubstitute)
                    nameCell.Children.Add(new TextBlock { Text = Strings.HeadToHeadWindow_SubstituteLabel, Style = Style("HeadToHead.SubstituteCaption") });
                Place(nameCell, d + 1, 0);

                for (int r = 0; r < driver.Cells.Count; r++)
                {
                    var cell = CreateCell(driver, driver.Cells[r], headToHead.IsOneCarTeam);
                    if (cell != null)
                        Place(cell, d + 1, r + 1);
                }
            }
        }

        private UIElement CreateCell(HeadToHeadDriver driver, HeadToHeadCell cell, bool oneCarTeam)
        {
            if (cell.Absent)
            {
                return new Border
                {
                    Style = Style("HeadToHead.AbsentStamp"),
                    Child = new TextBlock { Text = Strings.HeadToHeadWindow_Absent, Style = Style("HeadToHead.AbsentText") }
                };
            }

            if (!cell.Drove)
                return null;

            var text = cell.NotPrequalified
                ? Strings.HeadToHeadWindow_NotPrequalified
                : $"{cell.Grid?.ToString() ?? "–"}→{(cell.Retired ? Strings.HeadToHeadWindow_Retired : cell.Finish.ToString())}";

            // what's left out of the score is shown faded: substitutes, and races that didn't count
            var counts = !driver.IsSubstitute && (cell.Counted || oneCarTeam);
            return new Border
            {
                Style = Style(cell.AheadOfTeammate ? "HeadToHead.CellAhead" : "HeadToHead.Cell"),
                Child = new TextBlock { Text = text, Style = Style(counts ? "HeadToHead.CellText" : "HeadToHead.CellTextMuted") }
            };
        }

        private void Place(UIElement element, int row, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            RaceTable.Children.Add(element);
        }

        private Style Style(string key) => (Style)FindResource(key);

        private static string ShortName(HeadToHeadRace race) =>
            !string.IsNullOrEmpty(race.ShortName) ? race.ShortName
            : race.Name?.Length > 3 ? race.Name.Substring(0, 3).ToUpperInvariant()
            : race.Name;

        // "Christian Fittipaldi" -> "Fittipaldi", "Andrea de Cesaris" -> "de Cesaris"
        private string Surname(string driverId)
        {
            return _drivers.GetValueOrDefault(driverId)?.Surname() ?? driverId;
        }

        private static string FormatAverage(double? value) => value?.ToString("0.0", CultureInfo.CurrentCulture) ?? "–";

        internal static Brush ToBrush(string color)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(color))
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.StartsWith("#") ? color : "#" + color));
            }
            catch (FormatException)
            {
            }
            return Brushes.Gray;
        }
    }
}
