using Ams2ChEd.Business.AMS2.DependencyInjection;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AMS2ChEd
{
    public class Driver
    {
        public string RoleName { get; set; }
        public string Name { get; set; }
        public string Nationality { get; set; }
        public int Number { get; set; }
        public string DriverId { get; set; }
        public string PhotoUrl { get; set; }
    }

    public class TeamDisplay
    {
        public string TeamId { get; set; }

        public string TeamColor { get; set; }
        public string TeamName { get; set; }
        public string TeamPrincipal { get; set; }
        public Driver Driver1 { get; set; }
        public Driver Driver2 { get; set; }
        public TeamReputation Reputation { get; set; }
    }

    public partial class TeamSelectionWindow : Window
    {
        private List<TeamDisplay> teams;
        private List<Driver> freeAgents;
        private Driver selectedDriver;
        private Border selectedBorder;
        private bool _allSelectable;
        private bool _showFreeAgents;
        private Dictionary<string, IDriverData> _driversCache;
        public Driver SelectedDriver => selectedDriver;
        public string SelectedTeamName { get; private set; }
        public string SelectedTeamId { get; private set; }
        public string SelectedTeamPrincipal { get; private set; }

        private Ams2StorageFactory _ams2StorageFactory;

        public TeamSelectionWindow(
            Ams2StorageFactory ams2StorageFactory,
            int seasonYear,
            bool allSelectable,
            Dictionary<string, IDriverData> driversCache,
            bool showFreeAgents = false)
        {
            InitializeComponent();
            _allSelectable = allSelectable;
            _showFreeAgents = showFreeAgents;
            _ams2StorageFactory = ams2StorageFactory;
            _driversCache = driversCache;
            LoadSeason(seasonYear);
        }

        private void LoadSeason(int seasonYear)
        {
            try
            {
                var teamsCache = _ams2StorageFactory.TeamsLoader.LoadTeams();
                var seasonData = _ams2StorageFactory.SeasonLoader.LoadSeason(seasonYear);

                teams = new List<TeamDisplay>();
                var assignedDriverIds = new HashSet<string>();

                foreach (var teamEntry in seasonData.Teams.OrderByDescending(t => t.Reputation))
                {
                    string teamName = teamEntry.TeamName;

                    var driver1Id = teamEntry.Driver1Contract.DriverId;
                    if (!_driversCache.ContainsKey(driver1Id))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping team {teamName}: Driver 1 '{driver1Id}' not found in drivers database");
                        continue;
                    }

                    var driver1Data = _driversCache[driver1Id];
                    var driver1Picture = driver1Data?.PictureUrl;

                    var driver1 = new Driver
                    {
                        DriverId = driver1Data.DriverId,
                        RoleName = "1st driver",
                        Name = driver1Data.Name,
                        Nationality = string.IsNullOrEmpty(driver1Data.Nationality) ? "N/A" : driver1Data.Nationality,
                        Number = teamEntry.Driver1Contract.DriverNumber,
                        PhotoUrl = (string.IsNullOrEmpty(driver1Picture) || driver1Picture.StartsWith("https")) ? driver1Picture : $"pack://siteoforigin:,,,/{driver1Picture}"
                    };

                    var driver2Id = teamEntry.Driver2Contract.DriverId;

                    if (!_driversCache.ContainsKey(driver2Id))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping team {teamName}: Driver 2 '{driver2Id}' not found in drivers database");
                        continue;
                    }

                    var driver2Data = _driversCache[driver2Id];
                    var driver2Picture = driver2Data?.PictureUrl;

                    var driver2 = new Driver
                    {
                        DriverId = driver2Data.DriverId,
                        RoleName = "2nd driver",
                        Name = driver2Data.Name,
                        Nationality = string.IsNullOrEmpty(driver2Data.Nationality) ? "N/A" : driver2Data.Nationality,
                        Number = teamEntry.Driver2Contract.DriverNumber,
                        PhotoUrl = (string.IsNullOrEmpty(driver2Picture) || driver2Picture.StartsWith("https")) ? driver2Picture : $"pack://siteoforigin:,,,/{driver2Picture}",
                    };

                    teams.Add(new TeamDisplay
                    {
                        TeamId = teamEntry.TeamId,
                        TeamName = teamName,
                        TeamPrincipal = teamEntry.TeamPrincipal,
                        Reputation = teamEntry.Reputation,
                        Driver1 = driver1,
                        Driver2 = driver2,
                        TeamColor = teamEntry.Color
                    });

                    assignedDriverIds.Add(driver1Id);
                    assignedDriverIds.Add(driver2Id);
                }

                TeamsItemsControl.ItemsSource = teams;

                // Build and display free agents section
                if (_showFreeAgents)
                {
                    freeAgents = _driversCache
                        .Where(kvp => !assignedDriverIds.Contains(kvp.Key))
                        .Select(kvp =>
                        {
                            var d = kvp.Value;
                            var pic = d?.PictureUrl;
                            return new Driver
                            {
                                DriverId = d.DriverId,
                                RoleName = "Free Agent",
                                Name = d.Name,
                                Nationality = string.IsNullOrEmpty(d.Nationality) ? "N/A" : d.Nationality,
                                Number = 0,
                                PhotoUrl = (string.IsNullOrEmpty(pic) || pic.StartsWith("https")) ? pic : $"pack://siteoforigin:,,,/{pic}"
                            };
                        })
                        .OrderBy(d => d.Name)
                        .ToList();

                    FreeAgentsSection.Visibility = freeAgents.Any() ? Visibility.Visible : Visibility.Collapsed;
                    FreeAgentsItemsControl.ItemsSource = freeAgents;
                }
                else
                {
                    FreeAgentsSection.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading season data: {ex.Message}\n\n{ex.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadMockData();
            }
        }

        private void LoadMockData()
        {
            teams = new List<TeamDisplay>
            {
                new TeamDisplay
                {
                    TeamName = "Red Bull Racing",
                    Driver1 = new Driver { RoleName = "1st driver", DriverId = "verstappen_max", Name = "Max Verstappen", Nationality = "NED", Number = 1 },
                    Driver2 = new Driver { RoleName = "2nd driver", DriverId = "perez_sergio", Name = "Sergio Perez", Nationality = "MEX", Number = 11}
                }
            };

            TeamsItemsControl.ItemsSource = teams;
            FreeAgentsSection.Visibility = Visibility.Collapsed;
        }

        private void DriverCard_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            var driver = border.Tag as Driver;
            if (driver == null) return;

            // Remove previous selection highlight
            if (selectedBorder != null)
            {
                selectedBorder.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#444444"));
                selectedBorder.BorderThickness = new Thickness(2);
            }

            // Apply new selection
            selectedDriver = driver;
            selectedBorder = border;
            selectedBorder.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#c41e3a"));
            selectedBorder.BorderThickness = new Thickness(3);

            // Find the team for this driver (null for free agents)
            var team = teams?.FirstOrDefault(t => t.Driver1 == driver || t.Driver2 == driver);
            if (team != null)
            {
                SelectedTeamId = team.TeamId;
                SelectedTeamName = team.TeamName;
                SelectedTeamPrincipal = team.TeamPrincipal;
            }
            else
            {
                // Free agent — no team association
                SelectedTeamId = null;
                SelectedTeamName = null;
                SelectedTeamPrincipal = null;
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedDriver == null)
            {
                System.Windows.MessageBox.Show("Please select a driver to replace.", "No Driver Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}