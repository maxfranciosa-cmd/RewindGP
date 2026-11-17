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
        // the academy choices sit in the letter paper's own namescope, so they can't be named in XAML
        private System.Windows.Controls.ItemsControl AcademyList => (System.Windows.Controls.ItemsControl)Paper.ExtraContent!;

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

            // from the player's agent, not a team
            Paper.Header = Strings.AcademyLetterWindow_Header;
            Paper.Tagline = Strings.AcademyLetterWindow_Subtitle;
            Paper.SignatureName = Strings.AcademyLetterWindow_Signature;
        }

        // what the letter is about, on the paper and in the window title
        private void SetSubject(string subject)
        {
            Paper.Subject = subject;
            Title = subject;
        }

        private AcademyLetterWindow(string currentAcademyId, string activeAcademyId) : this(currentAcademyId)
        {
            _activeAcademyId = activeAcademyId;
        }

        public static AcademyLetterWindow ForChoice(string playerName, IReadOnlyList<Academy> academies, IEnumerable<ITeamEntry> teams, string currentAcademyId, int nextSeasonYear)
        {
            var teamNames = AcademyNames.TeamNames(teams);
            var active = academies?.FirstOrDefault(a => a.Id == currentAcademyId);
            var window = new AcademyLetterWindow(currentAcademyId, active?.Id);

            var paragraphs = new List<string> { string.Format(Strings.AcademyLetterWindow_Greeting_Format, playerName) };
            if (active != null)
            {
                var activeName = AcademyNames.AcademyName(active, teamNames);
                window.SetSubject(string.Format(Strings.LetterSubject_AcademyMember_Format, activeName, nextSeasonYear));
                paragraphs.Add(string.Format(Strings.AcademyLetterWindow_Intro_Member_Format, activeName));
            }
            else if (!string.IsNullOrEmpty(currentAcademyId))
            {
                window.SetSubject(Strings.LetterSubject_AcademyDormant);
                paragraphs.Add(Strings.AcademyLetterWindow_Intro_Dormant);
            }
            else
            {
                window.SetSubject(string.Format(Strings.LetterSubject_AcademyInvitations_Format, nextSeasonYear));
                paragraphs.Add(Strings.AcademyLetterWindow_Intro_NotMember);
            }
            paragraphs.Add(Strings.AcademyLetterWindow_HowItWorks);
            window.Paper.Body = string.Join("\n\n", paragraphs);

            foreach (var academy in academies ?? new List<Academy>())
            {
                window._options.Add(new AcademyOption
                {
                    Id = academy.Id,
                    Name = Capitalized(AcademyNames.AcademyName(academy, teamNames)),
                    Teams = AcademyNames.AcademyTeams(academy, teamNames),
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
            var window = ForChoice(playerName, academies, teams, currentAcademyId: null, nextSeasonYear: 0);
            window.SetSubject(Strings.LetterSubject_AcademyCareerStart);
            window.Paper.Body = string.Format(Strings.AcademyLetterWindow_Greeting_Format, playerName) + "\n\n" +
                                Strings.AcademyLetterWindow_Intro_CareerStart + "\n\n" +
                                Strings.AcademyLetterWindow_HowItWorks;
            window.DeclineButton.Content = Strings.AcademyLetterWindow_UsualWayButton;
            return window;
        }

        public static AcademyLetterWindow ForAgedOut(string playerName, string academyId, IReadOnlyList<Academy> academies, IEnumerable<ITeamEntry> teams)
        {
            var window = new AcademyLetterWindow(academyId);
            var academy = academies?.FirstOrDefault(a => a.Id == academyId);
            var name = academy != null ? AcademyNames.AcademyName(academy, teams) : Strings.AcademyLetterWindow_UnnamedAcademy;
            window.SetSubject(string.Format(Strings.LetterSubject_AcademyAgedOut_Format, name));
            window.Paper.Body = string.Format(Strings.AcademyLetterWindow_Greeting_Format, playerName) + "\n\n" +
                                string.Format(Strings.AcademyLetterWindow_AgedOut_Format, name);
            window.AcademyList.Visibility = Visibility.Collapsed;
            window.DeclineButton.Content = Strings.AcademyLetterWindow_ContinueButton;
            window.DeclineButton.Visibility = Visibility.Visible;
            window.ChosenAcademyId = null;
            return window;
        }

        private static string Capitalized(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);

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
