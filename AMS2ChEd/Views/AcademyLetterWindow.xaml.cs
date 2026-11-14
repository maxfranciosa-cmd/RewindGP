using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Resources;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// The off-season academy step of a young player without a race seat next season (join,
    /// switch, stay or leave one of next season's academies), and the short farewell to a player
    /// no longer young enough for his. Only PlayerData.AcademyId changes: a contract he holds never does.
    /// </summary>
    public partial class AcademyLetterWindow : Window
    {
        public class AcademyOption : INotifyPropertyChanged
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Teams { get; set; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private readonly string _currentAcademyId;
        // the academy he's an active member of next season (null when none, or dormant)
        private readonly string _activeAcademyId;
        private readonly List<AcademyOption> _options = new();

        /// <summary>His academy_id once the letter is closed: unchanged unless he joins, switches or leaves.</summary>
        public string ChosenAcademyId { get; private set; }

        private AcademyLetterWindow(string currentAcademyId)
        {
            InitializeComponent();
            _currentAcademyId = currentAcademyId;
            ChosenAcademyId = currentAcademyId;
        }

        private AcademyLetterWindow(string currentAcademyId, string activeAcademyId) : this(currentAcademyId)
        {
            _activeAcademyId = activeAcademyId;
        }

        public static AcademyLetterWindow ForChoice(string playerName, IReadOnlyList<Academy> academies, IEnumerable<ITeamEntry> teams, string currentAcademyId)
        {
            var teamNames = TeamNames(teams);
            var active = academies?.FirstOrDefault(a => a.Id == currentAcademyId);
            var window = new AcademyLetterWindow(currentAcademyId, active?.Id);

            var paragraphs = new List<string> { string.Format(Strings.AcademyLetterWindow_Greeting_Format, playerName) };
            if (active != null)
                paragraphs.Add(string.Format(Strings.AcademyLetterWindow_Intro_Member_Format, AcademyName(active, teamNames)));
            else if (!string.IsNullOrEmpty(currentAcademyId))
                paragraphs.Add(Strings.AcademyLetterWindow_Intro_Dormant);
            else
                paragraphs.Add(Strings.AcademyLetterWindow_Intro_NotMember);
            paragraphs.Add(Strings.AcademyLetterWindow_HowItWorks);
            window.LetterContent.Text = string.Join("\n\n", paragraphs);

            foreach (var academy in academies ?? new List<Academy>())
            {
                window._options.Add(new AcademyOption
                {
                    Id = academy.Id,
                    Name = Capitalized(AcademyName(academy, teamNames)),
                    Teams = AcademyTeams(academy, teamNames),
                    IsSelected = academy.Id == active?.Id
                });
            }
            window.AcademyList.ItemsSource = window._options;

            window.ConfirmButton.Visibility = Visibility.Visible;
            window.DeclineButton.Visibility = Visibility.Visible;
            window.DeclineButton.Content = active != null ? Strings.AcademyLetterWindow_StayButton : Strings.AcademyLetterWindow_NoThanksButton;
            window.LeaveButton.Visibility = active != null ? Visibility.Visible : Visibility.Collapsed;
            window.UpdateConfirmButton();
            return window;
        }

        /// <summary>
        /// At career creation: start in one of the season's academies, without a team, or the usual
        /// way (ChosenAcademyId null). Closing the window cancels the creation (DialogResult not true).
        /// </summary>
        public static AcademyLetterWindow ForCareerStart(string playerName, IReadOnlyList<Academy> academies, IEnumerable<ITeamEntry> teams)
        {
            var window = ForChoice(playerName, academies, teams, currentAcademyId: null);
            window.LetterContent.Text = string.Format(Strings.AcademyLetterWindow_Greeting_Format, playerName) + "\n\n" +
                                        Strings.AcademyLetterWindow_Intro_CareerStart + "\n\n" +
                                        Strings.AcademyLetterWindow_HowItWorks;
            window.DeclineButton.Content = Strings.AcademyLetterWindow_UsualWayButton;
            return window;
        }

        public static AcademyLetterWindow ForAgedOut(string playerName, string academyId, IReadOnlyList<Academy> academies, IEnumerable<ITeamEntry> teams)
        {
            var window = new AcademyLetterWindow(academyId);
            var academy = academies?.FirstOrDefault(a => a.Id == academyId);
            var name = academy != null ? AcademyName(academy, TeamNames(teams)) : Strings.AcademyLetterWindow_UnnamedAcademy;
            window.LetterContent.Text = string.Format(Strings.AcademyLetterWindow_Greeting_Format, playerName) + "\n\n" +
                                        string.Format(Strings.AcademyLetterWindow_AgedOut_Format, name);
            window.AcademyList.Visibility = Visibility.Collapsed;
            window.DeclineButton.Content = Strings.AcademyLetterWindow_ContinueButton;
            window.DeclineButton.Visibility = Visibility.Visible;
            window.ChosenAcademyId = null;
            return window;
        }

        private static string Capitalized(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);

        private static Dictionary<string, string> TeamNames(IEnumerable<ITeamEntry> teams)
        {
            var names = new Dictionary<string, string>();
            foreach (var team in teams ?? Enumerable.Empty<ITeamEntry>())
                names.TryAdd(team.TeamId, team.TeamName);
            return names;
        }

        private static string Names(IEnumerable<string> teamIds, Dictionary<string, string> teamNames) =>
            string.Join(", ", (teamIds ?? Enumerable.Empty<string>()).Select(id => teamNames.GetValueOrDefault(id) ?? id));

        // named after its parent teams ("the Ferrari driver academy")
        public static string AcademyName(Academy academy, IEnumerable<ITeamEntry> teams) => AcademyName(academy, TeamNames(teams));

        private static string AcademyName(Academy academy, Dictionary<string, string> teamNames)
        {
            var parents = Names(academy.ParentTeams, teamNames);
            return string.Format(Strings.AcademyLetterWindow_AcademyName_Format, string.IsNullOrEmpty(parents) ? academy.Id : parents);
        }

        // "Parent team: Ferrari · Sister team: Sauber"
        public static string AcademyTeams(Academy academy, IEnumerable<ITeamEntry> teams) => AcademyTeams(academy, TeamNames(teams));

        private static string AcademyTeams(Academy academy, Dictionary<string, string> teamNames)
        {
            var sisters = Names(academy.SisterTeams, teamNames);
            return string.Format(Strings.AcademyLetterWindow_AcademyTeams_Format,
                Names(academy.ParentTeams, teamNames),
                string.IsNullOrEmpty(sisters) ? Strings.AcademyLetterWindow_NoSisterTeam : sisters);
        }

        private AcademyOption Selected => _options.FirstOrDefault(o => o.IsSelected);

        private void UpdateConfirmButton()
        {
            var selected = Selected;
            ConfirmButton.IsEnabled = selected != null && selected.Id != _activeAcademyId;
            ConfirmButton.Content = _activeAcademyId != null ? Strings.AcademyLetterWindow_SwitchButton : Strings.AcademyLetterWindow_JoinButton;
        }

        private void Academy_Checked(object sender, RoutedEventArgs e) => UpdateConfirmButton();

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected;
            if (selected == null)
                return;

            ChosenAcademyId = selected.Id;
            DialogResult = true;
            Close();
        }

        private void LeaveButton_Click(object sender, RoutedEventArgs e)
        {
            ChosenAcademyId = null;
            DialogResult = true;
            Close();
        }

        // stay, no thanks, or continue: nothing changes (an aged-out player has already left)
        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
