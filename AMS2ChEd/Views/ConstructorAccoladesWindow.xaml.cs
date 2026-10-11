using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace AMS2ChEd.Views
{
    public partial class ConstructorAccoladesWindow : Window
    {
        public ConstructorAccoladesWindow(ISaveGame saveGame, string teamId, string teamName, string teamColor)
        {
            InitializeComponent();

            TeamNameText.Text = teamName;

            var fallbackColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
            if (!string.IsNullOrEmpty(teamColor))
            {
                try
                {
                    TeamColorStrip.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(teamColor));
                }
                catch
                {
                    TeamColorStrip.Background = fallbackColor;
                }
            }
            else
            {
                TeamColorStrip.Background = fallbackColor;
            }

            var accolades = AccoladesCalculator.GetTeamAccolades(saveGame, teamId);

            WinsText.Text = accolades.Wins.ToString();
            PodiumsText.Text = accolades.Podiums.ToString();
            PolesText.Text = accolades.PolePositions.ToString();

            if (accolades.ChampionshipYears.Count == 0)
            {
                NoChampionshipsText.Visibility = Visibility.Visible;
            }
            else
            {
                foreach (var year in accolades.ChampionshipYears)
                {
                    ChampionshipsPanel.Children.Add(CreateChampionshipBadge(year.ToString()));
                }
            }

            var seasonRows = BuildSeasonRows(saveGame, teamId);
            SeasonsList.ItemsSource = seasonRows;
            SeasonsList.Visibility = seasonRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            NoSeasonsText.Visibility = seasonRows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private class SeasonAccoladeRow
        {
            public int Year { get; set; }
            public int Position { get; set; }
            public double Points { get; set; }
            public int Wins { get; set; }
            public int Podiums { get; set; }
            public int Poles { get; set; }
        }

        private List<SeasonAccoladeRow> BuildSeasonRows(ISaveGame saveGame, string teamId)
        {
            var rows = new List<SeasonAccoladeRow>();

            foreach (var season in saveGame.HistoricalConstructorStandings.OrderByDescending(h => h.Year))
            {
                var entry = season.Standing.FirstOrDefault(e => e.TeamId == teamId);
                if (entry == null) continue;
                rows.Add(BuildSeasonRow(saveGame, season.Year, entry.Position, entry.Points, teamId));
            }

            return rows;
        }

        private SeasonAccoladeRow BuildSeasonRow(ISaveGame saveGame, int year, int position, double points, string teamId)
        {
            var gp = saveGame.GrandPrixResults.Where(g => g.Year == year).ToList();
            var raceResults = gp.SelectMany(g => g.RaceResults ?? new List<SessionResult>());
            var qualiResults = gp.SelectMany(g => g.QualifyingResults ?? new List<SessionResult>());

            return new SeasonAccoladeRow
            {
                Year = year,
                Position = position,
                Points = points,
                Wins = raceResults.Count(r => r.TeamId == teamId && r.Position == 1),
                Podiums = raceResults.Count(r => r.TeamId == teamId && r.Position >= 1 && r.Position <= 3),
                Poles = qualiResults.Count(r => r.TeamId == teamId && r.Position == 1),
            };
        }

        // a championship year, drawn by the active visual style
        private Border CreateChampionshipBadge(string year)
        {
            return new Border
            {
                Style = (Style)FindResource("Accolades.ChampionshipBadge"),
                Child = new TextBlock
                {
                    Text = year,
                    Style = (Style)FindResource("Accolades.ChampionshipBadgeText")
                }
            };
        }

        // a wide window puts the previous seasons next to the totals instead of below them
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool isWide = ActualWidth >= 800;

            Grid.SetColumnSpan(ProfileBlock, isWide ? 1 : 2);
            Grid.SetRowSpan(ProfileBlock, isWide ? 2 : 1);

            Grid.SetRow(SeasonsBlock, isWide ? 0 : 1);
            Grid.SetRowSpan(SeasonsBlock, isWide ? 2 : 1);
            Grid.SetColumn(SeasonsBlock, isWide ? 1 : 0);
            Grid.SetColumnSpan(SeasonsBlock, isWide ? 1 : 2);
            SeasonsBlock.Margin = isWide ? new Thickness(30, 0, 0, 0) : new Thickness(0, 16, 0, 0);
        }
    }
}
