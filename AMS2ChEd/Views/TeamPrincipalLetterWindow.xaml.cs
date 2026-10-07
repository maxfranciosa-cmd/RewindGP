using AMS2ChEd.Resources;
using System.Windows;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// Letter from the team principal telling a second driver that, with the first driver away
    /// for the next race, the decision on installing an improvement package is theirs.
    /// </summary>
    public partial class TeamPrincipalLetterWindow : Window
    {
        public TeamPrincipalLetterWindow(string teamName, string teamPrincipal, string playerName, string firstDriverName, string grandPrixName, int remainingPackages)
        {
            InitializeComponent();

            TeamNameHeader.Text = teamName.ToUpper();
            LetterContent.Text = string.Format(Strings.TeamPrincipalLetterWindow_Letter_Format, playerName, firstDriverName, grandPrixName, remainingPackages);
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
