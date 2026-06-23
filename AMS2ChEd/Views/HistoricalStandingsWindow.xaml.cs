using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AMS2ChEd.Views
{
    public class YearSelectorItem
    {
        public int Year { get; set; }
        public bool IsSelected { get; set; }
    }

    public class HistoricalDriverStandingDisplay
    {
        public int Position { get; set; }
        public string DriverName { get; set; }
        public string TeamName { get; set; }
        public double Points { get; set; }
        public bool IsEven { get; set; }
    }

    public class HistoricalConstructorStandingDisplay
    {
        public int Position { get; set; }
        public string TeamName { get; set; }
        public double Points { get; set; }
        public bool IsEven { get; set; }
    }

    public partial class HistoricalStandingsWindow : Window
    {
        private readonly ISaveGame _saveGame;
        private int _selectedYear;

        public HistoricalStandingsWindow(ISaveGame saveGame)
        {
            InitializeComponent();
            _saveGame = saveGame;
            
            LoadYearSelector();
        }

        private void LoadYearSelector()
        {
            // Get all available years from historical standings
            var driverYears = _saveGame.HistoricalDriverStandings?.Select(h => h.Year) ?? Enumerable.Empty<int>();
            var constructorYears = _saveGame.HistoricalConstructorStandings?.Select(h => h.Year) ?? Enumerable.Empty<int>();
            
            var allYears = driverYears.Union(constructorYears).OrderByDescending(y => y).ToList();

            if (!allYears.Any())
            {
                // No historical data available
                var noDataText = new TextBlock
                {
                    Text = "No historical standings data available",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    Margin = new Thickness(5)
                };
                YearSelector.Items.Add(noDataText);
                return;
            }

            // Select the most recent year by default
            _selectedYear = allYears.First();

            var yearItems = allYears.Select(year => new YearSelectorItem
            {
                Year = year,
                IsSelected = year == _selectedYear
            }).ToList();

            YearSelector.ItemsSource = yearItems;

            // Load standings for the selected year
            LoadStandingsForYear(_selectedYear);
        }

        private void YearButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is int year)
            {
                _selectedYear = year;

                // Update year selector to show selected state
                if (YearSelector.ItemsSource is IEnumerable<YearSelectorItem> items)
                {
                    var updatedItems = items.Select(item => new YearSelectorItem
                    {
                        Year = item.Year,
                        IsSelected = item.Year == year
                    }).ToList();

                    YearSelector.ItemsSource = null;
                    YearSelector.ItemsSource = updatedItems;
                }

                // Load standings for selected year
                LoadStandingsForYear(year);
            }
        }

        private void LoadStandingsForYear(int year)
        {
            LoadHistoricalDriverStandings(year);
            LoadHistoricalConstructorStandings(year);
        }

        private void LoadHistoricalDriverStandings(int year)
        {
            var historicalStanding = _saveGame.HistoricalDriverStandings?
                .FirstOrDefault(h => h.Year == year);

            if (historicalStanding?.Standing == null || !historicalStanding.Standing.Any())
            {
                HistoricalDriverStandingsItems.ItemsSource = null;
                return;
            }

            var displayItems = historicalStanding.Standing
                .OrderBy(s => s.Position)
                .Select((standing, index) => new HistoricalDriverStandingDisplay
                {
                    Position = standing.Position,
                    DriverName = standing.DriverName,
                    TeamName = standing.TeamName,
                    Points = standing.Points,
                    IsEven = index % 2 == 1
                })
                .ToList();

            HistoricalDriverStandingsItems.ItemsSource = displayItems;
        }

        private void LoadHistoricalConstructorStandings(int year)
        {
            var historicalStanding = _saveGame.HistoricalConstructorStandings?
                .FirstOrDefault(h => h.Year == year);

            if (historicalStanding?.Standing == null || !historicalStanding.Standing.Any())
            {
                HistoricalConstructorStandingsItems.ItemsSource = null;
                return;
            }

            var displayItems = historicalStanding.Standing
                .OrderBy(s => s.Position)
                .Select((standing, index) =>
                {
                    return new HistoricalConstructorStandingDisplay
                    {
                        Position = standing.Position,
                        TeamName = standing.TeamName,
                        Points = standing.Points,
                        IsEven = index % 2 == 1
                    };
                })
                .ToList();

            HistoricalConstructorStandingsItems.ItemsSource = displayItems;
        }
    }
}
