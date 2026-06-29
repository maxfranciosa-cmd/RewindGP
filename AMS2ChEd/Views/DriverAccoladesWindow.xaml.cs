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

            var baseAccolades = saveGame.AccoladesAtStart?.DriverAccolades?.GetValueOrDefault(driverId)
                ?? new Accolades();

            var allRaceResults = saveGame.GrandPrixResults.SelectMany(gp => gp.RaceResults ?? new List<SessionResult>());
            var allQualiResults = saveGame.GrandPrixResults.SelectMany(gp => gp.QualifyingResults ?? new List<SessionResult>());

            int wins = baseAccolades.Wins
                + allRaceResults.Count(r => r.DriverId == driverId && r.Position == 1);
            int podiums = baseAccolades.Podiums
                + allRaceResults.Count(r => r.DriverId == driverId && r.Position >= 1 && r.Position <= 3);
            int poles = baseAccolades.PolePositions
                + allQualiResults.Count(r => r.DriverId == driverId && r.Position == 1);

            var historicalChampYears = saveGame.HistoricalDriverStandings
                .Where(s => s.Standing.Any(e => e.DriverId == driverId && e.Position == 1))
                .Select(s => s.Year);

            var allChampionships = (baseAccolades.Championships ?? new List<int>())
                .Union(historicalChampYears)
                .OrderBy(y => y)
                .ToList();

            WinsText.Text = wins.ToString();
            PodiumsText.Text = podiums.ToString();
            PolesText.Text = poles.ToString();

            if (allChampionships.Count == 0)
            {
                NoChampionshipsText.Visibility = Visibility.Visible;
            }
            else
            {
                foreach (var year in allChampionships)
                {
                    ChampionshipsPanel.Children.Add(CreateChampionshipBadge(year.ToString()));
                }
            }
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
