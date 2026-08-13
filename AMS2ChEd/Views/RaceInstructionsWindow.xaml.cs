using AMS2ChEd.Resources;
using System.Windows;

namespace AMS2ChEd
{
    public partial class RaceInstructionsWindow : Window
    {
        public static RaceInstructionsWindow CreatePreQualiWindow(string playerName, string carName,string liveryName,int opponentsNumber,int suggestedDifficulty, bool usesPerformanceScalars)
        {
            var window = new RaceInstructionsWindow(
                playerName, carName, liveryName, opponentsNumber, suggestedDifficulty, usesPerformanceScalars);

            window.TitleText.Text = Strings.RaceInstructionsWindow_PreQuali_Title;
            window.IntroText4.Text = Strings.RaceInstructionsWindow_PreQuali_Intro4;
            window.OkButton.Content = Strings.RaceInstructionsWindow_PreQuali_OkButton;

            return window;
        }
        public RaceInstructionsWindow(string playerName, string car_name, string livery_name, int opponentsNumber, int suggestedDifficulty, bool usesPerformanceScalars)
        {
            InitializeComponent();
            IntroText1.Text = string.Format(Strings.RaceInstructionsWindow_Intro1_Format, car_name, livery_name);
            IntroText2.Text = string.Format(Strings.RaceInstructionsWindow_Intro2_Format, opponentsNumber);
            IntroText3.Text = usesPerformanceScalars
                ? Strings.RaceInstructionsWindow_Intro3_UsesScalars
                : string.Format(Strings.RaceInstructionsWindow_Intro3_Format, suggestedDifficulty);
            IntroText4.Text = Strings.RaceInstructionsWindow_Intro4;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}