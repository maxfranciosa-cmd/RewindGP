using System.Windows;

namespace AMS2ChEd
{
    public enum GenerateAbsenceWindowType
    {
        PayDriverAtGameStart,
        NoTeamForNextSeason,
        NoTeamBeforeTheRace
    }

    public partial class GenerateAbsenceWindow : Window
    {
        public bool CreateFictionalAbsence { get; private set; }

        // isNewGamePayDriver = true: Creating new game as Pay Driver Wild Card reputation
        // isNewGamePayDriver = false: Mid-career player without team for next season
        public GenerateAbsenceWindow(GenerateAbsenceWindowType type)
        {
            InitializeComponent();

            if (type == GenerateAbsenceWindowType.PayDriverAtGameStart) return;
            
            if (type == GenerateAbsenceWindowType.NoTeamForNextSeason)
            {
                // Player doesn't have team for next season (mid-career scenario)

                // Hide the "Choose Other Reputation" button (not applicable mid-career)
                BackButton.Visibility = Visibility.Collapsed;

                // Change title to be more generic
                TitleText.Text = "NO TEAM FOR NEXT SEASON";

                // Update content for mid-career scenario
                IntroText1.Text = "You don't have a team for the next season. You need to step in whenever one of the official drivers is absent.";
                IntroText2.Text = "You need to seize every single opportunity you get in order to prove your worth and secure a seat for the following season.";
                IntroText3.Text = "However, drivers might go a full season without any absences, so your opportunities may be limited or non-existent.";

                return;
            }
            
            if (type == GenerateAbsenceWindowType.NoTeamBeforeTheRace)
            {
                // Player doesn't have team for next race;
                // might get bored having only simulated races
                // so i'll keep pestering them.

                // Hide the "Choose Other Reputation" button (not applicable mid-career)
                BackButton.Visibility = Visibility.Collapsed;

                // Change title to be more generic
                TitleText.Text = "NO TEAM FOR NEXT RACE";

                // Update content for mid-career scenario
                IntroText1.Text = "You don't have a team for the next race. the following race will be simulated.";
                IntroText2.Text = "You need to seize every single opportunity you get in order to prove your worth and secure a seat for the following season.";
                IntroText3.Text = "However, drivers might go a full season without any absences, so your opportunities may be limited or non-existent.";

                return;
            }
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            CreateFictionalAbsence = true;
            this.DialogResult = true;
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            CreateFictionalAbsence = false;
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