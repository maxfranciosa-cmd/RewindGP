using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Resources;
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
        // the entry at the bottom of the team list that shows the free agents instead of a team's seats
        public bool IsFreeAgents { get; set; }
    }

    public partial class TeamSelectionWindow : Window
    {
        private List<TeamDisplay> teams;
        private List<Driver> freeAgents;
        private Driver selectedDriver;
        private bool _allSelectable;
        private bool _showFreeAgents;
        private Dictionary<string, IDriverData> _driversCache;
        public Driver SelectedDriver => selectedDriver;
        public string SelectedTeamName { get; private set; }
        public string SelectedTeamId { get; private set; }
        public string SelectedTeamPrincipal { get; private set; }

        private IGameDataFactory _dataFactory;

        public TeamSelectionWindow(
            IGameDataFactory dataFactory,
            int seasonYear,
            bool allSelectable,
            Dictionary<string, IDriverData> driversCache,
            bool showFreeAgents = false)
        {
            InitializeComponent();
            _allSelectable = allSelectable;
            _showFreeAgents = showFreeAgents;
            _dataFactory = dataFactory;
            _driversCache = driversCache;
            LoadSeason(seasonYear);
        }

        internal static string GetRoleName(ContractRole role) => role switch
        {
            ContractRole.FIRST_DRIVER => Strings.TeamSelectionWindow_FirstDriverRole,
            ContractRole.SECOND_DRIVER => Strings.TeamSelectionWindow_SecondDriverRole,
            _ => Strings.TeamSelectionWindow_EqualDriverRole
        };

        private void LoadSeason(int seasonYear)
        {
            try
            {
                var teamsCache = _dataFactory.TeamsLoader.LoadTeams();
                var seasonData = _dataFactory.SeasonLoader.LoadBaseSeason(seasonYear);
                DriverHirer.AssignUndefinedRoles(seasonData, _driversCache.Values);

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
                        RoleName = GetRoleName(teamEntry.Driver1Contract.Role),
                        Name = driver1Data.Name,
                        Nationality = string.IsNullOrEmpty(driver1Data.Nationality) ? "N/A" : driver1Data.Nationality,
                        Number = teamEntry.Driver1Contract.DriverNumber,
                        PhotoUrl = (string.IsNullOrEmpty(driver1Picture) || driver1Picture.StartsWith("https")) ? driver1Picture : $"pack://siteoforigin:,,,/{driver1Picture}"
                    };

                    var driver2Id = teamEntry.Driver2Contract?.DriverId;
                    Driver driver2 = null;

                    // a team with no second car this season has an empty Driver2Contract.DriverId -
                    // unlike a missing driver1, this isn't a data error, just show the team with one driver.
                    if (!string.IsNullOrEmpty(driver2Id))
                    {
                        if (!_driversCache.ContainsKey(driver2Id))
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipping team {teamName}: Driver 2 '{driver2Id}' not found in drivers database");
                            continue;
                        }

                        var driver2Data = _driversCache[driver2Id];
                        var driver2Picture = driver2Data?.PictureUrl;

                        driver2 = new Driver
                        {
                            DriverId = driver2Data.DriverId,
                            RoleName = GetRoleName(teamEntry.Driver2Contract.Role),
                            Name = driver2Data.Name,
                            Nationality = string.IsNullOrEmpty(driver2Data.Nationality) ? "N/A" : driver2Data.Nationality,
                            Number = teamEntry.Driver2Contract.DriverNumber,
                            PhotoUrl = (string.IsNullOrEmpty(driver2Picture) || driver2Picture.StartsWith("https")) ? driver2Picture : $"pack://siteoforigin:,,,/{driver2Picture}",
                        };
                    }

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
                    if (!string.IsNullOrEmpty(driver2Id))
                    {
                        assignedDriverIds.Add(driver2Id);
                    }
                }

                var teamListEntries = new List<TeamDisplay>(teams);

                // Build the free agents entry
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
                                RoleName = Strings.TeamSelectionWindow_FreeAgentRole,
                                Name = d.Name,
                                Nationality = string.IsNullOrEmpty(d.Nationality) ? "N/A" : d.Nationality,
                                Number = 0,
                                PhotoUrl = (string.IsNullOrEmpty(pic) || pic.StartsWith("https")) ? pic : $"pack://siteoforigin:,,,/{pic}"
                            };
                        })
                        .OrderBy(d => d.Name)
                        .ToList();

                    if (freeAgents.Any())
                    {
                        teamListEntries.Add(new TeamDisplay
                        {
                            TeamName = Strings.TeamSelectionWindow_FreeAgentsHeader,
                            TeamColor = "#808080",
                            IsFreeAgents = true
                        });
                    }
                }

                ShowTeamList(teamListEntries);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(Strings.TeamSelectionWindow_LoadError_Message, ex.Message, ex.StackTrace),
                    Strings.TeamSelectionWindow_LoadError_Title, MessageBoxButton.OK, MessageBoxImage.Error);
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

            ShowTeamList(teams);
        }

        private void ShowTeamList(List<TeamDisplay> teamListEntries)
        {
            TeamsListBox.ItemsSource = teamListEntries;
            if (teamListEntries.Any())
                TeamsListBox.SelectedIndex = 0;
        }

        // picking a team on the left lists its seats (or the free agents) as radio buttons on the right
        private void TeamsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // the radio buttons are rebuilt, so nothing is selected any more
            selectedDriver = null;
            SelectedTeamId = null;
            SelectedTeamName = null;
            SelectedTeamPrincipal = null;
            StatusText.Text = " ";

            var team = TeamsListBox.SelectedItem as TeamDisplay;
            if (team == null)
            {
                SeatsHeaderText.Text = string.Empty;
                SeatsItemsControl.ItemsSource = null;
                return;
            }

            SeatsHeaderText.Text = team.TeamName;
            SeatsItemsControl.ItemsSource = team.IsFreeAgents
                ? freeAgents
                : new[] { team.Driver1, team.Driver2 }.Where(d => d != null).ToList();
        }

        private void SeatRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var driver = (sender as System.Windows.Controls.RadioButton)?.Tag as Driver;
            if (driver == null) return;

            selectedDriver = driver;

            // Find the team for this driver (null for free agents)
            var team = teams?.FirstOrDefault(t => t.Driver1 == driver || t.Driver2 == driver);
            StatusText.Text = team != null
                ? $"{driver.Name} ({team.TeamName}, {driver.RoleName})"
                : $"{driver.Name} ({driver.RoleName})";

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

        // wider windows get a wider team list, two seats per row and larger photos
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool isWide = ActualWidth >= 1100;

            TeamsColumn.Width = new GridLength(isWide ? 320 : 230);
            SeatsItemsControl.Tag = isWide ? 2 : 1;
            Resources["SeatPhotoWidth"] = isWide ? 150.0 : 78.0;
            Resources["SeatPhotoHeight"] = isWide ? 192.0 : 100.0;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedDriver == null)
            {
                System.Windows.MessageBox.Show(Strings.TeamSelectionWindow_NoDriverSelected_Message, Strings.TeamSelectionWindow_NoDriverSelected_Title,
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