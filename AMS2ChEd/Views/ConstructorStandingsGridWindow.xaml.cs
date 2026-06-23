using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;

namespace AMS2ChEd
{
    public partial class ConstructorStandingsGridWindow : Window
    {
        public ConstructorStandingsGridWindow(ISaveGame saveGame)
        {
            InitializeComponent();

            // Set season year in header
            if (saveGame?.CurrentSeason != null)
            {
                SeasonYearText.Text = $"{saveGame.CurrentSeason.Year} Season";
            }

            PopulateTeams(saveGame);
        }

        private void PopulateTeams(ISaveGame saveGame)
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

            // Collect team-driver combinations: teamId -> list of drivers (in order: official drivers first, then substitutes)
            var teamDriverLists = new Dictionary<string, List<string>>();

            // First, add official drivers from current season teams
            if (saveGame.CurrentSeason.Teams != null)
            {
                foreach (var teamEntry in saveGame.CurrentSeason.Teams)
                {
                    if (!teamDriverLists.ContainsKey(teamEntry.TeamId))
                    {
                        teamDriverLists[teamEntry.TeamId] = new List<string>();
                    }

                    // Add official drivers in order
                    if (!string.IsNullOrEmpty(teamEntry.Driver1Contract?.DriverId))
                        teamDriverLists[teamEntry.TeamId].Add(teamEntry.Driver1Contract.DriverId);
                    if (!string.IsNullOrEmpty(teamEntry.Driver2Contract?.DriverId))
                        teamDriverLists[teamEntry.TeamId].Add(teamEntry.Driver2Contract.DriverId);
                }
            }

            // Now add any additional drivers from race results (substitutes/changes)
            // Track which driver-team combinations we've seen
            foreach (var race in completedRaces)
            {
                if (race.RaceResults != null)
                {
                    foreach (var result in race.RaceResults)
                    {
                        if (!teamDriverLists.ContainsKey(result.TeamId))
                        {
                            teamDriverLists[result.TeamId] = new List<string>();
                        }

                        // Add driver if not already in the list for this team
                        if (!teamDriverLists[result.TeamId].Contains(result.DriverId))
                        {
                            teamDriverLists[result.TeamId].Add(result.DriverId);
                        }
                    }
                }
            }

            // Sort teams by their championship position, then by best race result
            var sortedTeams = teamDriverLists.Keys
                .Select(teamId => new
                {
                    TeamId = teamId,
                    ChampionshipPosition = saveGame.CurrentConstructorStandings?
                        .FirstOrDefault(s => s.TeamId == teamId)?.Position ?? int.MaxValue,
                    BestPosition = completedRaces
                        .SelectMany(r => r.RaceResults ?? new List<SessionResult>())
                        .Where(sr => sr.TeamId == teamId)
                        .Select(sr => (int?)sr.Position)
                        .Min() ?? int.MaxValue
                })
                .OrderBy(t => t.ChampionshipPosition)
                .ThenBy(t => t.BestPosition)
                .Select(t => t.TeamId)
                .ToList();

            // Add race header grid first
            var headerGrid = CreateRaceHeaderGrid(allRaces);
            TeamsStackPanel.Children.Add(headerGrid);

            // Add separator after header
            var headerSeparator = new Border
            {
                Height = 2,
                Background = Brushes.Black,
                Margin = new Thickness(0, 5, 0, 10)
            };
            TeamsStackPanel.Children.Add(headerSeparator);

            // Create a grid for each team
            foreach (var teamId in sortedTeams)
            {
                var driversForTeam = teamDriverLists[teamId];
                var teamGrid = CreateTeamGrid(saveGame, teamId, driversForTeam, allRaces, completedRaces);
                TeamsStackPanel.Children.Add(teamGrid);

                // Add separator line between teams
                var separator = new Border
                {
                    Height = 1,
                    Background = Brushes.Black,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                TeamsStackPanel.Children.Add(separator);
            }
        }

        private Grid CreateRaceHeaderGrid(List<Race> allRaces)
        {
            var grid = new Grid();

            // Create column definitions matching the team grids
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            foreach (var race in allRaces)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            }

            // Single row for headers
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

            // Add "TEAM" header in first column
            AddHeaderCell(grid, "TEAM", 0, 0);

            // Add race abbreviation headers
            for (int i = 0; i < allRaces.Count; i++)
            {
                var raceAbbr = GetRaceAbbreviation(allRaces[i]);
                AddHeaderCell(grid, raceAbbr, 0, i + 1);
            }

            return grid;
        }

        private void AddHeaderCell(Grid grid, string text, int row, int col)
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
                FontSize = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5)
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }

        private Grid CreateTeamGrid(ISaveGame saveGame, string teamId, List<string> drivers, List<Race> allRaces, List<GrandPrixResult> completedRaces)
        {
            var grid = new Grid();

            // Create column definitions
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            foreach (var race in allRaces)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            }

            // Create row definitions: team header + one row per driver
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Team name row
            foreach (var driver in drivers)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            }

            // Team header row
            var teamName = GetTeamDisplayName(saveGame, teamId);
            AddTeamHeaderCell(grid, teamName, 0, 0, allRaces.Count + 1);

            // Driver rows
            for (int driverIndex = 0; driverIndex < drivers.Count; driverIndex++)
            {
                var driverId = drivers[driverIndex];
                var driverName = GetDriverDisplayName(saveGame, driverId);
                int row = driverIndex + 1;

                // Driver name cell
                AddDriverNameCell(grid, driverName, row, 0);

                // Result cells for each race
                for (int raceIndex = 0; raceIndex < allRaces.Count; raceIndex++)
                {
                    var race = allRaces[raceIndex];

                    // Find if this race has been completed - try multiple matching strategies
                    GrandPrixResult completedRace = null;

                    // Strategy 1: Exact match (case-insensitive)
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

                    var result = completedRace?.RaceResults?.FirstOrDefault(r => r.DriverId == driverId && r.TeamId == teamId);

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

                    AddResultCell(grid, cellText, row, raceIndex + 1);
                }
            }

            return grid;
        }

        private void AddTeamHeaderCell(Grid grid, string teamName, int row, int col, int colSpan)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = Brushes.Black
            };

            var textBlock = new TextBlock
            {
                Text = teamName.ToUpper(),
                FontFamily = new FontFamily("Arial Black"),
                FontWeight = FontWeights.Black,
                FontSize = 11,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 0, 4)
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            Grid.SetColumnSpan(border, colSpan);
            grid.Children.Add(border);
        }

        private void AddDriverNameCell(Grid grid, string name, int row, int col)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = row % 2 == 1 ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fafafa"))
            };

            var textBlock = new TextBlock
            {
                Text = name.ToUpper(),
                FontFamily = new FontFamily("Courier New"),
                FontSize = 11,
                Foreground = Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(20, 4, 0, 4) // Indent driver names under team
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }

        private void AddResultCell(Grid grid, string text, int row, int col)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = row % 2 == 1 ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fafafa"))
            };

            var textBlock = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Courier New"),
                FontSize = 11,
                Foreground = string.IsNullOrEmpty(text) ? Brushes.LightGray : Brushes.Black,
                FontWeight = text == "DNF" || text == "DNQ" ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 4, 5, 4)
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }

        private string GetTeamDisplayName(ISaveGame saveGame, string teamId)
        {
            var team = saveGame?.CurrentSeason.Teams?.FirstOrDefault(t => t.TeamId == teamId);
            if (team != null)
            {
                return team.TeamName;
            }
            return teamId;
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
    }
}