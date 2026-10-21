using AMS2ChEd.Resources;
using System.Windows;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// Letter from the team principal to the player: telling a second driver that, with the first
    /// driver away for the next race, the decision on installing an improvement package is theirs,
    /// or any other news the team gives the player first-hand.
    /// </summary>
    public partial class TeamPrincipalLetterWindow : Window
    {
        public TeamPrincipalLetterWindow(string teamName, string teamPrincipal, string playerName, string firstDriverName, string grandPrixName, int remainingPackages)
            : this(teamName, teamPrincipal, Strings.TeamPrincipalLetterWindow_Subtitle,
                string.Format(Strings.TeamPrincipalLetterWindow_Letter_Format, playerName, firstDriverName, grandPrixName, remainingPackages))
        {
        }

        public TeamPrincipalLetterWindow(string teamName, string teamPrincipal, string subtitle, string letter)
        {
            InitializeComponent();

            TeamNameHeader.Text = teamName.ToUpper();
            SubtitleText.Text = subtitle;
            LetterContent.Text = letter;
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
