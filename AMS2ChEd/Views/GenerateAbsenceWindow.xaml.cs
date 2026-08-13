using AMS2ChEd.Resources;
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
                TitleText.Text = Strings.GenerateAbsenceWindow_NoTeamSeasonTitle;

                // Update content for mid-career scenario
                IntroText1.Text = Strings.GenerateAbsenceWindow_NoTeamSeason_Intro1;
                IntroText2.Text = Strings.GenerateAbsenceWindow_SeekOpportunity_Intro2;
                IntroText3.Text = Strings.GenerateAbsenceWindow_Intro3;

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
                TitleText.Text = Strings.GenerateAbsenceWindow_NoTeamRaceTitle;

                // Update content for mid-career scenario
                IntroText1.Text = Strings.GenerateAbsenceWindow_NoTeamRace_Intro1;
                IntroText2.Text = Strings.GenerateAbsenceWindow_SeekOpportunity_Intro2;
                IntroText3.Text = Strings.GenerateAbsenceWindow_Intro3;

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