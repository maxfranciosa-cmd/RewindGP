using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Resources;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace AMS2ChEd
{
    public partial class ContractLetterWindow : Window
    {
        private bool isHired;
        private DriverReputation playerReputation;
        private DriverReputation replacedDriverReputation;
        private ISeason currentSeason;
        private string playerName;
        private string playerDriverId;
        private string playerNationality;
        private int playerAge;
        private string replacedDriverId;
        private string teamId;
        private IEnumerable<int> favouriteNumbers;
        private GameLogicFactory _gameLogicFactory;

        public ContractLetterWindow(
            GameLogicFactory gameLogicFactory,
            string teamName,
            string teamId,
            string teamPrincipal,
            string playerName,
            string playerNationality,
            int playerAge,
            string playerDriverId,
            IEnumerable<int> favouriteNumbers,
            DriverReputation playerReputation,
            string replacedDriverName,
            string replacedDriverId,
            DriverReputation replacedDriverReputation,
            string roleName,
            ISeason season)
        {
            InitializeComponent();
            this._gameLogicFactory = gameLogicFactory;
            this.playerReputation = playerReputation;
            this.replacedDriverReputation = replacedDriverReputation;
            this.currentSeason = season;
            this.playerName = playerName;
            this.playerDriverId = playerDriverId;
            this.playerNationality = playerNationality;
            this.favouriteNumbers = favouriteNumbers;
            this.playerAge = playerAge;
            this.teamId = teamId;
            this.replacedDriverId = replacedDriverId;
            
            if (playerAge < 18)
            {
                GenerateYoungDriverRejectionLetter(teamName, teamPrincipal, playerName, playerAge);
                NextButton.Visibility = Visibility.Collapsed;
                ChooseAnotherTeamButton.Visibility = Visibility.Visible;
                return;
            }

            if (playerAge > 42 && playerReputation != DriverReputation.JUST_ONE_LAST_DANCE)
            {
                GenerateOlderDriverRejectionLetter(teamName,teamPrincipal, playerName, playerAge);
                NextButton.Visibility = Visibility.Collapsed;
                ChooseAnotherTeamButton.Visibility = Visibility.Visible;
                return;
            }

            // Check if player is hired
            var result = _gameLogicFactory.ContractNegotiationEngine.EvaluateContract(playerDriverId,
                                                                                      playerReputation,
                                                                                      replacedDriverId,
                                                                                      replacedDriverReputation);
            isHired = result.IsPlayerHired;

            if (isHired)
            {
                GenerateSuccessLetter(teamName, teamPrincipal, playerName, replacedDriverName, playerReputation, roleName);
                NextButton.Visibility = Visibility.Visible;
                ChooseAnotherTeamButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                GenerateRejectionLetter(teamName, teamPrincipal, playerName, replacedDriverName, roleName);
                NextButton.Visibility = Visibility.Collapsed;
                ChooseAnotherTeamButton.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Off-season team-application result letter. Unlike the main constructor, the hired/not
        /// outcome is already decided by the resolved new season (OffSeasonOrchestrator), so this
        /// path skips the young/older-driver auto-rejection branches and the
        /// ContractNegotiationEngine re-evaluation, and always shows a single "Next" button - it's
        /// a read-only outcome letter, not a live choice.
        /// </summary>
        public ContractLetterWindow(
            string teamName,
            string teamPrincipal,
            string playerName,
            DriverReputation playerReputation,
            bool isPlayerHired,
            string otherDriverName,
            DriverReputation otherDriverReputation,
            bool otherDriverWasAtTeamBefore,
            string roleName)
        {
            InitializeComponent();
            this.playerReputation = playerReputation;
            this.playerName = playerName;
            this.isHired = isPlayerHired;

            NextButton.Visibility = Visibility.Visible;
            ChooseAnotherTeamButton.Visibility = Visibility.Collapsed;

            if (isPlayerHired)
            {
                GenerateOffSeasonSuccessLetter(teamName, teamPrincipal, playerName, otherDriverName, otherDriverWasAtTeamBefore, playerReputation, roleName);
            }
            else
            {
                GenerateOffSeasonRejectionLetter(teamName, teamPrincipal, playerName, otherDriverName, otherDriverWasAtTeamBefore, roleName);
            }
        }

        private void GenerateOffSeasonSuccessLetter(string teamName, string teamPrincipal, string playerName, string otherDriverName, bool otherDriverWasAtTeamBefore, DriverReputation playerReputation, string roleName)
        {
            TeamNameHeader.Text = teamName.ToUpper();

            string reputationReason = GetReputationReason(playerReputation);
            string beatDriverClause = string.IsNullOrEmpty(otherDriverName)
                ? Strings.ContractLetterWindow_OffSeasonSuccess_NoRival
                : string.Format(
                    otherDriverWasAtTeamBefore
                        ? Strings.ContractLetterWindow_OffSeasonSuccess_BeatIncumbent_Format
                        : Strings.ContractLetterWindow_OffSeasonSuccess_BeatNewcomer_Format,
                    otherDriverName);

            LetterContent.Text = string.Format(Strings.ContractLetterWindow_OffSeasonSuccessLetter_Format,
                playerName, teamName, roleName, beatDriverClause, reputationReason);

            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        private void GenerateOffSeasonRejectionLetter(string teamName, string teamPrincipal, string playerName, string otherDriverName, bool otherDriverWasAtTeamBefore, string roleName)
        {
            TeamNameHeader.Text = teamName.ToUpper();

            string rejectionReason = GetRejectionReason(playerReputation);
            string otherDriverClause = string.Format(
                otherDriverWasAtTeamBefore
                    ? Strings.ContractLetterWindow_OffSeasonRejection_KeptIncumbent_Format
                    : Strings.ContractLetterWindow_OffSeasonRejection_SignedNewcomer_Format,
                otherDriverName);

            LetterContent.Text = string.Format(Strings.ContractLetterWindow_OffSeasonRejectionLetter_Format,
                playerName, teamName, roleName, otherDriverName, rejectionReason, otherDriverClause);

            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        private void GenerateYoungDriverRejectionLetter(string teamName, string teamPrincipal, string playerName, int driverAge)
        {
            // Set team header
            TeamNameHeader.Text = teamName.ToUpper();

            string template;

            if (driverAge <= 6)
            {
                // Very young children - pure dream stage
                template = Strings.ContractLetterWindow_YoungDriver_VeryYoung_Format;
            }
            else if (driverAge <= 14)
            {
                // Karting age - developmental stage
                template = Strings.ContractLetterWindow_YoungDriver_Karting_Format;
            }
            else // 14-17
            {
                // Junior formula age - serious development stage
                template = Strings.ContractLetterWindow_YoungDriver_Junior_Format;
            }

            LetterContent.Text = string.Format(template, playerName, teamName, driverAge);

            // Set signature with age-appropriate tone
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        private void GenerateOlderDriverRejectionLetter(string teamName, string teamPrincipal, string playerName, int driverAge)
        {
            // Set team header
            TeamNameHeader.Text = teamName.ToUpper();

            // Generate personalized message for older driver
            LetterContent.Text = string.Format(Strings.ContractLetterWindow_OlderDriver_Rejection_Format, playerName, teamName, driverAge);

            // Set signature
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        private void GenerateSuccessLetter(string teamName, string teamPrincipal, string playerName, string replacedDriverName, DriverReputation playerReputation, string roleName)
        {
            // Set team header
            TeamNameHeader.Text = teamName.ToUpper();

            // Generate personalized message based on reputation
            string reputationReason = GetReputationReason(playerReputation);
            string competitionMention = GetCompetitionMention(replacedDriverName);

            LetterContent.Text = string.Format(Strings.ContractLetterWindow_SuccessLetter_Format,
                playerName, teamName, roleName, competitionMention, reputationReason);

            // Set signature
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        private string GetReputationReason(DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD =>
                    Strings.ContractLetterWindow_ReputationReason_PayDriverWildCard,

                DriverReputation.PAY_DRIVER_SEASON =>
                    Strings.ContractLetterWindow_ReputationReason_PayDriverSeason,

                DriverReputation.YOUNG_TALENT =>
                    Strings.ContractLetterWindow_ReputationReason_YoungTalent,

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    Strings.ContractLetterWindow_ReputationReason_YoungChampionshipUnproven,

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    Strings.ContractLetterWindow_ReputationReason_YoungChampionship,

                DriverReputation.PRIME_MIDFIELD =>
                    Strings.ContractLetterWindow_ReputationReason_PrimeMidfield,

                DriverReputation.PRIME_STRONG_MIDFIELD =>
                    Strings.ContractLetterWindow_ReputationReason_PrimeStrongMidfield,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionshipUnproven,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionship,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionshipWashed,

                DriverReputation.AGEING_MIDFIELD =>
                    Strings.ContractLetterWindow_ReputationReason_AgeingMidfield,

                DriverReputation.AGEING_STRONG_MIDFIELD =>
                    Strings.ContractLetterWindow_ReputationReason_AgeingStrongMidfield,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL =>
                    Strings.ContractLetterWindow_ReputationReason_AgeingChampionship,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.ContractLetterWindow_ReputationReason_AgeingChampionshipWashed,

                _ => Strings.ContractLetterWindow_ReputationReason_Default
            };
        }

        private string GetCompetitionMention(string replacedDriverName)
        {
            return string.Format(Strings.ContractLetterWindow_CompetitionMention_Format, replacedDriverName);
        }

        private void GenerateRejectionLetter(string teamName, string teamPrincipal, string playerName, string replacedDriverName, string roleName)
        {
            // Set team header
            TeamNameHeader.Text = teamName.ToUpper();

            // Get rejection reason based on reputation
            string rejectionReason = GetRejectionReason(playerReputation);
            string preferredDriverReason = GetPreferredDriverReason(replacedDriverReputation, replacedDriverName);

            LetterContent.Text = string.Format(Strings.ContractLetterWindow_RejectionLetter_Format,
                playerName, teamName, roleName, replacedDriverName, rejectionReason, preferredDriverReason);

            // Set signature
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        private string GetRejectionReason(DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD =>
                    Strings.ContractLetterWindow_RejectionReason_PayDriverWildCard,

                DriverReputation.PAY_DRIVER_SEASON =>
                    Strings.ContractLetterWindow_RejectionReason_PayDriverSeason,

                DriverReputation.YOUNG_TALENT =>
                    Strings.ContractLetterWindow_RejectionReason_YoungTalent,

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    Strings.ContractLetterWindow_RejectionReason_YoungChampionshipUnproven,

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    Strings.ContractLetterWindow_RejectionReason_YoungChampionship,

                DriverReputation.PRIME_MIDFIELD =>
                    Strings.ContractLetterWindow_RejectionReason_PrimeMidfield,

                DriverReputation.PRIME_STRONG_MIDFIELD =>
                    Strings.ContractLetterWindow_RejectionReason_PrimeStrongMidfield,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionshipUnproven,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionship,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionshipWashed,

                DriverReputation.AGEING_MIDFIELD =>
                    Strings.ContractLetterWindow_RejectionReason_AgeingMidfield,

                DriverReputation.AGEING_STRONG_MIDFIELD =>
                    Strings.ContractLetterWindow_RejectionReason_AgeingStrongMidfield,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL =>
                    Strings.ContractLetterWindow_RejectionReason_AgeingChampionship,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.ContractLetterWindow_RejectionReason_AgeingChampionshipWashed,

                _ => Strings.ContractLetterWindow_RejectionReason_Default
            };
        }

        private string GetPreferredDriverReason(DriverReputation replacedReputation, string replacedDriverName)
        {
            string template = replacedReputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD =>
                    Strings.ContractLetterWindow_PreferredDriverReason_PayDriverWildCard_Format,

                DriverReputation.PAY_DRIVER_SEASON =>
                    Strings.ContractLetterWindow_PreferredDriverReason_PayDriverSeason_Format,

                DriverReputation.YOUNG_TALENT =>
                    Strings.ContractLetterWindow_PreferredDriverReason_YoungTalent_Format,

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    Strings.ContractLetterWindow_PreferredDriverReason_YoungChampionshipUnproven_Format,

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    Strings.ContractLetterWindow_PreferredDriverReason_YoungChampionship_Format,

                DriverReputation.PRIME_MIDFIELD =>
                    Strings.ContractLetterWindow_PreferredDriverReason_PrimeMidfield_Format,

                DriverReputation.PRIME_STRONG_MIDFIELD =>
                    Strings.ContractLetterWindow_PreferredDriverReason_PrimeStrongMidfield_Format,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    Strings.ContractLetterWindow_PreferredDriverReason_PrimeChampionshipUnproven_Format,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    Strings.ContractLetterWindow_PreferredDriverReason_PrimeChampionship_Format,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.ContractLetterWindow_PreferredDriverReason_PrimeChampionshipWashed_Format,

                DriverReputation.AGEING_MIDFIELD =>
                    Strings.ContractLetterWindow_PreferredDriverReason_AgeingMidfield_Format,

                DriverReputation.AGEING_STRONG_MIDFIELD =>
                    Strings.ContractLetterWindow_PreferredDriverReason_AgeingStrongMidfield_Format,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL =>
                    Strings.ContractLetterWindow_PreferredDriverReason_AgeingChampionship_Format,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.ContractLetterWindow_PreferredDriverReason_AgeingChampionshipWashed_Format,

                _ => Strings.ContractLetterWindow_PreferredDriverReason_Default_Format
            };

            return string.Format(template, replacedDriverName);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void ChooseAnotherTeamButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}