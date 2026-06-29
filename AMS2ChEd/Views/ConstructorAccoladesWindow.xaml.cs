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

            var baseAccolades = saveGame.AccoladesAtStart?.TeamsAccolades?.GetValueOrDefault(teamId)
                ?? new Accolades();

            var allRaceResults = saveGame.GrandPrixResults.SelectMany(gp => gp.RaceResults ?? new List<SessionResult>());
            var allQualiResults = saveGame.GrandPrixResults.SelectMany(gp => gp.QualifyingResults ?? new List<SessionResult>());

            int wins = baseAccolades.Wins
                + allRaceResults.Count(r => r.TeamId == teamId && r.Position == 1);
            int podiums = baseAccolades.Podiums
                + allRaceResults.Count(r => r.TeamId == teamId && r.Position >= 1 && r.Position <= 3);
            int poles = baseAccolades.PolePositions
                + allQualiResults.Count(r => r.TeamId == teamId && r.Position == 1);

            var historicalChampYears = saveGame.HistoricalConstructorStandings
                .Where(s => s.Standing.Any(e => e.TeamId == teamId && e.Position == 1))
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
