using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Extensions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace AMS2ChEd.Views
{
    public partial class DriverAccoladesWindow : Window
    {
        public DriverAccoladesWindow(ISaveGame saveGame, string driverId, string driverName, string teamName, string pictureUrl)
        {
            InitializeComponent();

            DriverNameText.Text = driverName;
            TeamNameText.Text = teamName;
            DriverPhoto.LoadPhoto(pictureUrl, PhotoPlaceholder);

            var driverData = saveGame.Drivers?.FirstOrDefault(d => d.DriverId == driverId)
                ?? saveGame.RetiredDrivers?.FirstOrDefault(d => d.DriverId == driverId);
            if (driverData != null && driverData.YearOfBirth > 0)
            {
                AgeText.Text = $"Age {saveGame.CurrentSeason.Year - driverData.YearOfBirth}";
            }
            else
            {
                AgeText.Visibility = Visibility.Collapsed;
            }

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, driverId);

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

            var seasonRows = BuildSeasonRows(saveGame, driverId);
            SeasonsList.ItemsSource = seasonRows;
            SeasonsList.Visibility = seasonRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            NoSeasonsText.Visibility = seasonRows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private class SeasonAccoladeRow
        {
            public int Year { get; set; }
            public int Position { get; set; }
            public string TeamName { get; set; }
            public double Points { get; set; }
            public int Races { get; set; }
            public int Wins { get; set; }
            public int Podiums { get; set; }
            public int Poles { get; set; }
        }

        private List<SeasonAccoladeRow> BuildSeasonRows(ISaveGame saveGame, string driverId)
        {
            var rows = new List<SeasonAccoladeRow>();

            foreach (var season in saveGame.HistoricalDriverStandings.OrderByDescending(h => h.Year))
            {
                var entry = season.Standing.FirstOrDefault(e => e.DriverId == driverId);
                if (entry == null) continue;
                rows.Add(BuildSeasonRow(saveGame, season.Year, entry.Position, entry.TeamName, entry.Points, driverId));
            }

            return rows;
        }

        private SeasonAccoladeRow BuildSeasonRow(ISaveGame saveGame, int year, int position, string teamName, double points, string driverId)
        {
            var gp = saveGame.GrandPrixResults.Where(g => g.Year == year).ToList();
            var raceResults = gp.SelectMany(g => g.RaceResults ?? new List<SessionResult>());
            var qualiResults = gp.SelectMany(g => g.QualifyingResults ?? new List<SessionResult>());

            return new SeasonAccoladeRow
            {
                Year = year,
                Position = position,
                TeamName = teamName,
                Points = points,
                Races = raceResults.Count(r => r.DriverId == driverId && !r.DidNotPreQualify),
                Wins = raceResults.Count(r => r.DriverId == driverId && r.Position == 1),
                Podiums = raceResults.Count(r => r.DriverId == driverId && r.Position >= 1 && r.Position <= 3),
                Poles = qualiResults.Count(r => r.DriverId == driverId && r.Position == 1),
            };
        }

        private Border CreateChampionshipBadge(string year)
        {
            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc143c")),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(12, 6, 12, 6),
                Child = new TextBlock
                {
                    Text = year,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White)
                }
            };
        }
    }
}
