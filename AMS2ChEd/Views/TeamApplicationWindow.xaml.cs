using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Resources;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using static AMS2ChEd.Business.Services.OffSeasonMovements;

namespace AMS2ChEd.Views
{
    public class TeamApplicationItem : INotifyPropertyChanged
    {
        public string TeamId { get; set; }
        public string TeamName { get; set; }

        public string TeamColor { get; set; }
        public TeamReputation TeamReputation { get; set; }
        public DriverRole Role { get; set; }
        public string RoleDescription { get; set; }
        public bool IsDisabled { get; set; }
        public string StatusMessage { get; set; }
        public TeamHiringBallot Ballot { get; set; }

        // Classified-ad copy, composed once at load time
        public string Kicker { get; set; }
        public string Headline { get; set; }
        public string BodyText { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        // True once the player has reached the application cap and this team wasn't one of the picks
        private bool _isAtCapacity;
        public bool IsAtCapacity
        {
            get => _isAtCapacity;
            set
            {
                if (_isAtCapacity != value)
                {
                    _isAtCapacity = value;
                    OnPropertyChanged(nameof(IsAtCapacity));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class TeamApplicationWindow : Window
    {
        private List<TeamApplicationItem> _teamItems;
        private string _playerDriverId;
        private DriverReputation _playerReputation;
        private const int MAX_APPLICATIONS = 4;

        public List<TeamHiringBallot> UpdatedBallots { get; private set; }

        private static string GetDropReasonText(DriverFirerOutcome outcome, string driverName)
        {
            if (string.IsNullOrEmpty(driverName))
                return null;

            return outcome switch
            {
                DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED => string.Format(Strings.TeamApplicationWindow_DropReason_ContractExpired, driverName),
                DriverFirerOutcome.DROPPED_UNDERPERFORMING => string.Format(Strings.TeamApplicationWindow_DropReason_Underperforming, driverName),
                DriverFirerOutcome.DROPPED_RETIRING => string.Format(Strings.TeamApplicationWindow_DropReason_Retiring, driverName),
                DriverFirerOutcome.DROPPED_TEAM_QUITTING => string.Format(Strings.TeamApplicationWindow_DropReason_TeamQuitting, driverName),
                DriverFirerOutcome.DROPPED_PLAYER_REJECTING => string.Format(Strings.TeamApplicationWindow_DropReason_PlayerRejecting, driverName),
                DriverFirerOutcome.NOT_DROPPED => null,
                _ => null
            };
        }

        private static string GetReputationLabel(TeamReputation reputation) => reputation switch
        {
            TeamReputation.TOP_TEAM => Strings.TeamApplicationWindow_Reputation_TopTeam,
            TeamReputation.MIDFIELD_HIGH => Strings.TeamApplicationWindow_Reputation_MidfieldHigh,
            TeamReputation.MIDFIELD => Strings.TeamApplicationWindow_Reputation_Midfield,
            TeamReputation.MINNOW => Strings.TeamApplicationWindow_Reputation_Minnow,
            TeamReputation.SUPER_MINNOW => Strings.TeamApplicationWindow_Reputation_SuperMinnow,
            _ => Strings.TeamApplicationWindow_Reputation_Midfield
        };

        public TeamApplicationWindow(
            ISaveGame saveGame,
            IEnumerable<TeamHiringBallot> ballots,
            List<DropTeamResult> droppedDrivers,
            DriverReputation playerReputation,
            IEnumerable<ITeamEntry> nextSeasonTeamEntries)
        {
            InitializeComponent();

            _playerDriverId = saveGame.PlayerData.DriverId;
            _playerReputation = playerReputation;
            var nextYearTeamEntries = nextSeasonTeamEntries.ToDictionary(t => t.TeamId);
            var driversDictionary = saveGame.Drivers.ToDictionary(d => d.DriverId);
            var retiredDriversDictionary = saveGame.RetiredDrivers.ToDictionary(d => d.DriverId);
            var allDriversDictionary = driversDictionary.Union(retiredDriversDictionary).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Create a lookup dictionary for dropped drivers by team
            var dropLookup = new Dictionary<string, DropTeamResult>();
            if (droppedDrivers != null)
            {
                foreach (var drop in droppedDrivers)
                {
                    dropLookup[drop.TeamId] = drop;
                }
            }

            // Process team application ballots
            _teamItems = new List<TeamApplicationItem>();

            foreach (var ballot in ballots)
            {
                var teamData = nextYearTeamEntries.ContainsKey(ballot.OriginalTeamHiring.TeamId)
                    ? nextYearTeamEntries[ballot.OriginalTeamHiring.TeamId]
                    : null;

                var teamEntry = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == ballot.OriginalTeamHiring.TeamId);
                string teamName = teamData?.TeamName ?? teamEntry?.TeamName ?? Strings.TeamApplicationWindow_UnknownTeam;
                string teamColor = teamData?.Color ?? teamEntry?.Color ?? "000000";

                bool teamAlreadyInterested = ballot.OriginalTeamHiring?.DriverId == _playerDriverId;
                string roleDescription = ballot.OriginalTeamHiring.Role == DriverRole.FIRST_DRIVER
                    ? Strings.TeamApplicationWindow_FirstDriverRole
                    : Strings.TeamApplicationWindow_SecondDriverRole;

                // Check if there's a dropped driver for this team/role combination
                string dropReasonText = null;

                if (dropLookup.TryGetValue(ballot.OriginalTeamHiring.TeamId, out var teamDropInfo) && teamEntry != null)
                {
                    // Determine which drop outcome to use based on the role
                    DriverFirerOutcome dropOutcome = ballot.OriginalTeamHiring.Role == DriverRole.FIRST_DRIVER
                        ? teamDropInfo.DropDriver1
                        : teamDropInfo.DropDriver2;

                    // Get the driver name from the team entry in the save game
                    string droppedDriverName = null;
                    if (ballot.OriginalTeamHiring.Role == DriverRole.FIRST_DRIVER)
                    {

                        droppedDriverName = allDriversDictionary[teamEntry.Driver1Contract.DriverId].Name;
                    }
                    else
                    {
                        droppedDriverName = allDriversDictionary[teamEntry.Driver2Contract.DriverId].Name;
                    }

                    dropReasonText = GetDropReasonText(dropOutcome, droppedDriverName);
                }

                string reputationLabel = GetReputationLabel(teamEntry?.Reputation ?? TeamReputation.MINNOW);
                string kicker = string.Format(Strings.TeamApplicationWindow_WantedKicker, reputationLabel).ToUpperInvariant();
                string headline = string.Format(Strings.TeamApplicationWindow_AdHeadline, teamName, roleDescription).ToUpperInvariant();
                string bodyText = !string.IsNullOrEmpty(dropReasonText)
                    ? dropReasonText
                    : string.Format(Strings.TeamApplicationWindow_NoDropReasonBlurb, roleDescription);

                _teamItems.Add(new TeamApplicationItem
                {
                    TeamId = ballot.OriginalTeamHiring.TeamId,
                    TeamName = teamName,
                    TeamColor = teamColor,
                    Role = ballot.OriginalTeamHiring.Role,
                    RoleDescription = roleDescription,
                    IsDisabled = false,
                    StatusMessage = teamAlreadyInterested
                        ? Strings.TeamApplicationWindow_AlreadyInterestedStatus
                        : Strings.TeamApplicationWindow_ClickToApplyStatus,
                    Ballot = ballot,
                    IsSelected = false,
                    TeamReputation = teamEntry?.Reputation ?? TeamReputation.MINNOW,
                    Kicker = kicker,
                    Headline = headline,
                    BodyText = bodyText
                });
            }

            TeamList.ItemsSource = _teamItems
                                    .OrderByDescending(t => t.TeamReputation)
                                    .ThenByDescending(t => t.TeamName)
                                    .ThenBy(t => t.Role);
            InstructionText.Text = string.Format(Strings.TeamApplicationWindow_InstructionText, MAX_APPLICATIONS);
            UpdateSelectionCount();
        }

        private void TeamCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is TeamApplicationItem item)
            {
                // Cards at capacity are visually locked (greyed out, hit-testing disabled by the
                // trigger in the DataTemplate) - this is a defensive fallback, not the normal path.
                if (item.IsDisabled || item.IsAtCapacity)
                    return;

                item.IsSelected = !item.IsSelected;
                UpdateSelectionCount();
            }
        }

        private void UpdateSelectionCount()
        {
            int count = _teamItems.Count(t => t.IsSelected);

            foreach (var item in _teamItems)
            {
                item.IsAtCapacity = !item.IsSelected && !item.IsDisabled && count >= MAX_APPLICATIONS;
            }

            SelectionCountText.Text = count == 1
                ? Strings.TeamApplicationWindow_SelectionCount_Singular
                : string.Format(Strings.TeamApplicationWindow_SelectionCount_Plural, count, MAX_APPLICATIONS);
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            // Create updated ballots with player applications
            UpdatedBallots = new List<TeamHiringBallot>();

            var playerDriver = new TeamHiringBallotCandidate
            {
                DriverId = _playerDriverId,
                DriverReputation = _playerReputation
            };

            foreach (var item in _teamItems)
            {
                var updatedCandidates = item.Ballot.Candidates.ToList();

                // If player selected this team add player to candidates
                if (item.IsSelected)
                {
                    // Only add if not already in candidates (or it's already been considered by the team)
                    if (!updatedCandidates.Any(c => c.DriverId == _playerDriverId) && item.Ballot.OriginalTeamHiring.DriverId != _playerDriverId)
                    {
                        updatedCandidates.Add(playerDriver);
                    }
                }
                else
                {
                    // if it WAS considered by the team, but the player is not interested, set the original team hiring to null
                    if (item.Ballot.OriginalTeamHiring.DriverId == _playerDriverId)
                    {
                        item.Ballot.OriginalTeamHiring.DriverId = null;
                    }
                }

                UpdatedBallots.Add(new TeamHiringBallot
                {
                    OriginalTeamHiring = item.Ballot.OriginalTeamHiring,
                    Candidates = updatedCandidates
                });
            }

            this.DialogResult = true;
            this.Close();
        }
    }
}
