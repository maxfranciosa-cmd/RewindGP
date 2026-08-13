using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Resources;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.Views
{
    public partial class OffSeasonContractWindow : Window
    {
        public bool PlayerAcceptedContract { get; private set; }
        private bool isPlayerDropped;

        public OffSeasonContractWindow(
            ISaveGame saveGame,
            IEnumerable<ITeamEntry> nextSeasonTeamEntries,
            DriverFirerOutcome dropOutcome,
            DriverReputation playerReputation)
        {
            InitializeComponent();

            isPlayerDropped = dropOutcome.IsDropped();

            // Get player's team info
            var playerTeam = nextSeasonTeamEntries.FirstOrDefault(t =>
                t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            // get player's team info from current season if it can't find the team for next season
            // (this means the team is not going to compete next season)
            playerTeam = playerTeam ?? saveGame.CurrentSeason.Teams.FirstOrDefault(t =>
                    t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                    t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            string teamName = playerTeam?.TeamName ?? Strings.OffSeasonContractWindow_DefaultTeamName;
            string teamPrincipal = playerTeam?.TeamPrincipal ?? Strings.OffSeasonContractWindow_DefaultTeamPrincipal;

            TeamNameHeader.Text = teamName.ToUpper();
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.OffSeasonContractWindow_SignatureTitle_Format, teamName);

            if (isPlayerDropped)
            {
                GenerateTerminationLetter(saveGame.PlayerData.Name, teamName, dropOutcome, playerReputation);
                ContinueButton.Visibility = Visibility.Visible;
            }
            else
            {
                GenerateRenewalLetter(saveGame.PlayerData.Name, teamName, playerReputation);
                AcceptButton.Visibility = Visibility.Visible;
                RejectButton.Visibility = Visibility.Visible;
            }
        }

        private void GenerateRenewalLetter(string playerName, string teamName, DriverReputation playerReputation)
        {
            string reputationMessage = playerReputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD or DriverReputation.PAY_DRIVER_SEASON =>
                    Strings.OffSeasonContractWindow_Renewal_PayDriver,

                DriverReputation.YOUNG_TALENT =>
                    Strings.OffSeasonContractWindow_Renewal_YoungTalent,

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN or DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    Strings.OffSeasonContractWindow_Renewal_YoungChampionshipLevel,

                DriverReputation.PRIME_MIDFIELD or DriverReputation.PRIME_STRONG_MIDFIELD =>
                    Strings.OffSeasonContractWindow_Renewal_PrimeMidfield,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN or DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    Strings.OffSeasonContractWindow_Renewal_PrimeChampionship,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.OffSeasonContractWindow_Renewal_PrimeChampionshipWashed,

                DriverReputation.AGEING_MIDFIELD or DriverReputation.AGEING_STRONG_MIDFIELD =>
                    Strings.OffSeasonContractWindow_Renewal_AgeingMidfield,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL or DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.OffSeasonContractWindow_Renewal_AgeingChampionship,

                DriverReputation.JUST_ONE_LAST_DANCE =>
                    Strings.OffSeasonContractWindow_Renewal_LastDance,

                _ => Strings.OffSeasonContractWindow_Renewal_Default
            };

            LetterContent.Text = string.Format(Strings.OffSeasonContractWindow_RenewalLetter_Format, playerName, teamName, reputationMessage);
        }

        private void GenerateTerminationLetter(
            string playerName,
            string teamName,
            DriverFirerOutcome dropOutcome,
            DriverReputation playerReputation)
        {
            string reasonMessage = dropOutcome switch
            {
                DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED =>
                    GenerateContractExpiredMessage(playerReputation, teamName),

                DriverFirerOutcome.DROPPED_UNDERPERFORMING =>
                    GenerateUnderperformingMessage(playerReputation, teamName),

                DriverFirerOutcome.DROPPED_RETIRING =>
                    GenerateRetiringMessage(playerName, teamName),

                DriverFirerOutcome.DROPPED_TEAM_QUITTING =>
                    GenerateTeamQuittingMessage(playerName, teamName),

                _ => Strings.OffSeasonContractWindow_TerminationReason_Default
            };

            LetterContent.Text = string.Format(Strings.OffSeasonContractWindow_TerminationLetter_Format, playerName, teamName, reasonMessage);
        }
        private string GenerateTeamQuittingMessage(string playerName, string teamName)
        {
            return string.Format(Strings.OffSeasonContractWindow_TeamQuitting_Format, teamName);
        }

        private string GenerateContractExpiredMessage(DriverReputation reputation, string teamName)
        {
            string template = reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD or DriverReputation.PAY_DRIVER_SEASON =>
                    Strings.OffSeasonContractWindow_ContractExpired_PayDriver_Format,

                DriverReputation.YOUNG_TALENT =>
                    Strings.OffSeasonContractWindow_ContractExpired_YoungTalent_Format,

                DriverReputation.PRIME_MIDFIELD or DriverReputation.AGEING_MIDFIELD =>
                    Strings.OffSeasonContractWindow_ContractExpired_MidfieldGeneric_Format,

                DriverReputation.AGEING_STRONG_MIDFIELD =>
                    Strings.OffSeasonContractWindow_ContractExpired_AgeingStrongMidfield_Format,

                DriverReputation.PRIME_STRONG_MIDFIELD =>
                    Strings.OffSeasonContractWindow_ContractExpired_PrimeStrongMidfield_Format,

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN or DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    Strings.OffSeasonContractWindow_ContractExpired_YoungChampionship_Format,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN or DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    Strings.OffSeasonContractWindow_ContractExpired_PrimeChampionship_Format,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.OffSeasonContractWindow_ContractExpired_PrimeChampionshipWashed_Format,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL or DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.OffSeasonContractWindow_ContractExpired_AgeingChampionship_Format,

                DriverReputation.JUST_ONE_LAST_DANCE =>
                    Strings.OffSeasonContractWindow_ContractExpired_LastDance_Format,

                _ => Strings.OffSeasonContractWindow_ContractExpired_Default_Format
            };

            return string.Format(template, teamName);
        }

        private string GenerateUnderperformingMessage(DriverReputation reputation, string teamName)
        {
            return string.Format(Strings.OffSeasonContractWindow_Underperforming_Format, teamName);
        }

        private string GenerateRetiringMessage(string playerName, string teamName)
        {
            return string.Format(Strings.OffSeasonContractWindow_Retiring_Format, teamName);
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            PlayerAcceptedContract = true;
            this.DialogResult = true;
            this.Close();
        }

        private void RejectButton_Click(object sender, RoutedEventArgs e)
        {
            PlayerAcceptedContract = false;
            this.DialogResult = true;
            this.Close();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            PlayerAcceptedContract = false;
            this.DialogResult = true;
            this.Close();
        }
    }
}
