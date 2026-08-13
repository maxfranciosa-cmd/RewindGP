using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;

namespace AMS2ChEd
{
    public partial class DriverStandingsGridWindow : Window
    {
        public DriverStandingsGridWindow(ISaveGame saveGame)
        {
            InitializeComponent();

            // Set season year in header
            if (saveGame?.CurrentSeason != null)
            {
                SeasonYearText.Text = string.Format(Strings.DriverStandingsGridWindow_SeasonYear_Format, saveGame.CurrentSeason.Year);
            }

            PopulateGrid(saveGame);
        }

        private void PopulateGrid(ISaveGame saveGame)
        {
            if (saveGame?.CurrentSeason?.Races == null || !saveGame.CurrentSeason.Races.Any())
            {
                return;
            }

            // Get all races from the season (not just completed ones)
            var allRaces = saveGame.CurrentSeason.Races.OrderBy(r => r.RaceId).ToList();

            // Get completed races with results - FILTER BY CURRENT SEASON YEAR
            var completedRaces = saveGame.GrandPrixResults?
                .Where(gp => gp.Year == saveGame.CurrentSeason.Year)
                .OrderBy(gp => gp.Year)
                .ToList() ?? new List<GrandPrixResult>();

            // Collect all drivers from the current season
            var allDrivers = new HashSet<string>();

            // Add drivers from team contracts
            if (saveGame.CurrentSeason.Teams != null)
            {
                foreach (var team in saveGame.CurrentSeason.Teams)
                {
                    if (!string.IsNullOrEmpty(team.Driver1Contract?.DriverId))
                        allDrivers.Add(team.Driver1Contract.DriverId);
                    if (!string.IsNullOrEmpty(team.Driver2Contract?.DriverId))
                        allDrivers.Add(team.Driver2Contract.DriverId);
                }
            }

            // Also add drivers from completed race results (in case of substitutions)
            foreach (var race in completedRaces)
            {
                if (race.RaceResults != null)
                {
                    foreach (var result in race.RaceResults)
                    {
                        allDrivers.Add(result.DriverId);
                    }
                }
            }

            // Sort drivers by current championship position, then by best race result
            var sortedDrivers = allDrivers
                .Select(driverId => new
                {
                    DriverId = driverId,
                    ChampionshipPosition = saveGame.CurrentDriverStandings?
                        .FirstOrDefault(s => s.DriverId == driverId)?.Position ?? int.MaxValue,
                    BestPosition = completedRaces
                        .SelectMany(r => r.RaceResults ?? new List<SessionResult>())
                        .Where(sr => sr.DriverId == driverId)
                        .Select(sr => (int?)sr.Position)
                        .Min() ?? int.MaxValue
                })
                .OrderBy(d => d.ChampionshipPosition)
                .ThenBy(d => d.BestPosition)
                .Select(d => d.DriverId)
                .ToList();

            // Create column definitions
            ResultsGrid.ColumnDefinitions.Clear();
            ResultsGrid.RowDefinitions.Clear();

            // First column for driver names (wider)
            ResultsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });

            // Columns for each race (all races in season)
            foreach (var race in allRaces)
            {
                ResultsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            }

            // Header row
            ResultsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            // Row for each driver
            foreach (var driver in sortedDrivers)
            {
                ResultsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            }

            // Add header cells
            AddHeaderCell(Strings.DriverStandingsGridWindow_DriverColumnHeader, 0, 0);

            for (int i = 0; i < allRaces.Count; i++)
            {
                var raceAbbr = GetRaceAbbreviation(allRaces[i]);
                AddHeaderCell(raceAbbr, 0, i + 1);
            }

            // Add driver rows
            for (int driverIndex = 0; driverIndex < sortedDrivers.Count; driverIndex++)
            {
                var driverId = sortedDrivers[driverIndex];
                var driverName = GetDriverDisplayName(saveGame, driverId);
                int row = driverIndex + 1;

                // Driver name cell
                AddDriverNameCell(driverName, row, 0);

                var discardedResults = ChampionshipScoringCalculator.GetDiscardedResults(driverId, saveGame.CurrentSeason, completedRaces);

                // Result cells for each race
                for (int raceIndex = 0; raceIndex < allRaces.Count; raceIndex++)
                {
                    var race = allRaces[raceIndex];

                    // Find if this race has been completed - try multiple matching strategies
                    GrandPrixResult completedRace = null;

                    // Strategy 1: Match by RaceId if GrandPrixResult has it stored somewhere
                    // For now, match by name (try exact match first, then contains)
                    completedRace = completedRaces.FirstOrDefault(cr =>
                        cr.GrandPrixName != null && race.RaceName != null &&
                        cr.GrandPrixName.Equals(race.RaceName, StringComparison.OrdinalIgnoreCase));

                    // Strategy 2: Try partial match if exact match fails
                    if (completedRace == null && race.RaceName != null)
                    {
                        completedRace = completedRaces.FirstOrDefault(cr =>
                            cr.GrandPrixName != null &&
                            (cr.GrandPrixName.Contains(race.RaceName, StringComparison.OrdinalIgnoreCase) ||
                             race.RaceName.Contains(cr.GrandPrixName, StringComparison.OrdinalIgnoreCase)));
                    }

                    var result = completedRace?.RaceResults?.FirstOrDefault(r => r.DriverId == driverId);

                    string cellText = "";
                    if (result != null)
                    {
                        if(result.DidNotPreQualify)
                        {
                            cellText = "DNQ";
                        }
                        else if (result.DNF)
                        {
                            cellText = "DNF";
                        }
                        else
                        {
                            cellText = result.Position.ToString();
                        }
                    }

                    bool isDiscarded = completedRace != null && discardedResults.Contains(completedRace);

                    AddResultCell(cellText, row, raceIndex + 1, isDiscarded);
                }
            }
        }

        private void AddHeaderCell(string text, int row, int col)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 1, 2),
                Background = Brushes.White
            };

            var textBlock = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Courier New"),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                FontSize = 16,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5)
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            ResultsGrid.Children.Add(border);
        }

        private void AddDriverNameCell(string name, int row, int col)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = row % 2 == 0 ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fafafa"))
            };

            var textBlock = new TextBlock
            {
                Text = name.ToUpper(),
                FontFamily = new FontFamily("Courier New"),
                FontSize = 15,
                Foreground = Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 0, 4)
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            ResultsGrid.Children.Add(border);
        }

        private void AddResultCell(string text, int row, int col, bool isDiscarded = false)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = row % 2 == 0 ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fafafa"))
            };

            bool showAsDiscarded = isDiscarded && !string.IsNullOrEmpty(text);

            var textBlock = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Courier New"),
                FontSize = 15,
                Foreground = showAsDiscarded ? Brushes.Gray : (string.IsNullOrEmpty(text) ? Brushes.LightGray : Brushes.Black),
                FontStyle = showAsDiscarded ? FontStyles.Italic : FontStyles.Normal,
                TextDecorations = showAsDiscarded ? TextDecorations.Strikethrough : null,
                FontWeight = text == "DNF" || text == "DNQ" ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 4, 5, 4)
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            ResultsGrid.Children.Add(border);
        }

        private string GetRaceAbbreviation(Race race)
        {
            if (!string.IsNullOrEmpty(race.RaceShortName))
                return race.RaceShortName;

            var grandPrixName = race.RaceName;

            // Common F1 race abbreviations
            var abbreviations = new Dictionary<string, string>
            {
                { "Australian", "AUS" },
                { "Brazilian", "BRA" },
                { "Canadian", "CAN" },
                { "Italian", "ITA" },
                { "British", "GBR" },
                { "German", "GER" },
                { "French", "FRA" },
                { "Spanish", "ESP" },
                { "Monaco", "MON" },
                { "Belgian", "BEL" },
                { "Hungarian", "HUN" },
                { "Austrian", "AUT" },
                { "Japanese", "JPN" },
                { "United States", "USA" },
                { "Mexican", "MEX" },
                { "Argentine", "ARG" },
                { "San Marino", "SMR" },
                { "Pacific", "PAC" },
                { "European", "EUR" },
                { "Luxembourg", "LUX" },
                { "Portuguese", "POR" },
                { "South African", "RSA" }
            };

            foreach (var kvp in abbreviations)
            {
                if (grandPrixName.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            // Fallback: take first 3 letters and uppercase
            return grandPrixName.Length >= 3 ? grandPrixName.Substring(0, 3).ToUpper() : grandPrixName.ToUpper();
        }

        private string GetDriverDisplayName(ISaveGame saveGame, string driverId)
        {
            var driver = saveGame?.Drivers?.FirstOrDefault(d => d.DriverId == driverId);
            if (driver != null)
            {
                return $"{driver.Name}";
            }
            return driverId;
        }
    }
}