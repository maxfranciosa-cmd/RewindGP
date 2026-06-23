using System.Windows;

namespace AMS2ChEd
{
    public partial class RaceInstructionsWindow : Window
    {
        public static RaceInstructionsWindow CreatePreQualiWindow(string playerName, string carName,string liveryName,int opponentsNumber,int suggestedDifficulty)
        {
            var window = new RaceInstructionsWindow(
                playerName, carName, liveryName, opponentsNumber, suggestedDifficulty);

            window.TitleText.Text = "PRE-QUALIFYING SESSION";
            window.IntroText4.Text =
                "4) Run A QUALIFYING SESSION ONLY — do not start the race. " +
                "Rewind GP will read your qualifying result automatically. " +
                "Only the top 2 drivers will advance to the race weekend.";
            window.OkButton.Content = "I'M READY TO PRE-QUALIFY!";

            return window;
        }
        public RaceInstructionsWindow(string playerName, string car_name, string livery_name, int opponentsNumber, int suggestedDifficulty)
        {
            InitializeComponent(); 
            IntroText1.Text = $"1) Ensure you SELECT THE RIGHT CAR ({car_name}) AND LIVERY. it will have your driver name on it! (in your case {livery_name})";
            IntroText2.Text = $"2) Select THE RIGHT NUMBER OF OPPONENTS (in your case {opponentsNumber})";
            IntroText3.Text = $"3) SUGGESTED DIFFICULTY: +{suggestedDifficulty} POINTS (compared to a difficulty where you fight for wins)";
            IntroText4.Text = "4) it doesn't matter the track or the duration of the race, but have at least A QUALIFYING SESSION and A RACE SESSION.";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}