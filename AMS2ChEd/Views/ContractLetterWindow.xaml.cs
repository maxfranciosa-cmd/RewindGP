using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Resources;
using System.Linq;
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
            string roleName,
            TeamReputation teamReputation,
            DriverRole role)
        {
            InitializeComponent();
            this.playerReputation = playerReputation;
            this.playerName = playerName;
            this.isHired = isPlayerHired;

            NextButton.Visibility = Visibility.Visible;
            ChooseAnotherTeamButton.Visibility = Visibility.Collapsed;

            if (isPlayerHired)
            {
                GenerateOffSeasonSuccessLetter(teamName, teamPrincipal, playerName, otherDriverName, otherDriverWasAtTeamBefore, playerReputation, roleName, teamReputation, role);
            }
            else
            {
                GenerateOffSeasonRejectionLetter(teamName, teamPrincipal, playerName, otherDriverName, otherDriverWasAtTeamBefore, roleName, teamReputation, role);
            }
        }

        private void GenerateOffSeasonSuccessLetter(string teamName, string teamPrincipal, string playerName, string otherDriverName, bool otherDriverWasAtTeamBefore, DriverReputation playerReputation, string roleName, TeamReputation teamReputation, DriverRole role)
        {
            TeamNameHeader.Text = teamName.ToUpper();

            string reputationReason = AppendSuccessQualificationDetail(GetReputationReason(playerReputation), playerReputation, role, teamReputation);
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

        private void GenerateOffSeasonRejectionLetter(string teamName, string teamPrincipal, string playerName, string otherDriverName, bool otherDriverWasAtTeamBefore, string roleName, TeamReputation teamReputation, DriverRole role)
        {
            TeamNameHeader.Text = teamName.ToUpper();

            string rejectionReason = AppendQualificationDetail(GetRejectionReason(playerReputation), playerReputation, role, teamReputation);
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

            // Generate personalized message based on reputation, plus a qualification-fit detail
            // (over-, under- or "good but not perfect" fit for this specific seat) when we can
            // resolve the team/role being contested - see AppendSuccessQualificationDetail.
            string reputationReason = GetReputationReason(playerReputation);
            var contestedTeam = currentSeason?.Teams?.FirstOrDefault(t => t.TeamId == teamId);
            if (contestedTeam != null)
            {
                var contestedRole = contestedTeam.Driver1Contract?.DriverId == replacedDriverId
                    ? DriverRole.FIRST_DRIVER
                    : DriverRole.SECOND_DRIVER;
                reputationReason = AppendSuccessQualificationDetail(reputationReason, playerReputation, contestedRole, contestedTeam.Reputation);
            }

            string competitionMention = GetCompetitionMention(replacedDriverName);

            LetterContent.Text = string.Format(Strings.ContractLetterWindow_SuccessLetter_Format,
                playerName, teamName, roleName, competitionMention, reputationReason);

            // Set signature
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        /// <summary>
        /// Shared RNG for picking among the alternative phrasings in <see cref="GetReputationReason"/>
        /// and <see cref="GetRejectionReason"/>, so re-generating a letter for the same reputation
        /// doesn't always produce word-for-word the same text.
        /// </summary>
        private static readonly Random _phrasingRandom = new Random();

        private string GetReputationReason(DriverReputation reputation)
        {
            string[] alternatives = reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_PayDriverWildCard,
                    Strings.ContractLetterWindow_ReputationReason_PayDriverWildCard_2,
                    Strings.ContractLetterWindow_ReputationReason_PayDriverWildCard_3
                },

                DriverReputation.PAY_DRIVER_SEASON => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_PayDriverSeason,
                    Strings.ContractLetterWindow_ReputationReason_PayDriverSeason_2,
                    Strings.ContractLetterWindow_ReputationReason_PayDriverSeason_3
                },

                DriverReputation.YOUNG_TALENT => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_YoungTalent,
                    Strings.ContractLetterWindow_ReputationReason_YoungTalent_2,
                    Strings.ContractLetterWindow_ReputationReason_YoungTalent_3
                },

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_YoungChampionshipUnproven,
                    Strings.ContractLetterWindow_ReputationReason_YoungChampionshipUnproven_2,
                    Strings.ContractLetterWindow_ReputationReason_YoungChampionshipUnproven_3
                },

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_YoungChampionship,
                    Strings.ContractLetterWindow_ReputationReason_YoungChampionship_2,
                    Strings.ContractLetterWindow_ReputationReason_YoungChampionship_3
                },

                DriverReputation.PRIME_MIDFIELD => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_PrimeMidfield,
                    Strings.ContractLetterWindow_ReputationReason_PrimeMidfield_2,
                    Strings.ContractLetterWindow_ReputationReason_PrimeMidfield_3
                },

                DriverReputation.PRIME_STRONG_MIDFIELD => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_PrimeStrongMidfield,
                    Strings.ContractLetterWindow_ReputationReason_PrimeStrongMidfield_2,
                    Strings.ContractLetterWindow_ReputationReason_PrimeStrongMidfield_3
                },

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionshipUnproven,
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionshipUnproven_2,
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionshipUnproven_3
                },

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionship,
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionship_2,
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionship_3
                },

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionshipWashed,
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionshipWashed_2,
                    Strings.ContractLetterWindow_ReputationReason_PrimeChampionshipWashed_3
                },

                DriverReputation.AGEING_MIDFIELD => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_AgeingMidfield,
                    Strings.ContractLetterWindow_ReputationReason_AgeingMidfield_2,
                    Strings.ContractLetterWindow_ReputationReason_AgeingMidfield_3
                },

                DriverReputation.AGEING_STRONG_MIDFIELD => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_AgeingStrongMidfield,
                    Strings.ContractLetterWindow_ReputationReason_AgeingStrongMidfield_2,
                    Strings.ContractLetterWindow_ReputationReason_AgeingStrongMidfield_3
                },

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_AgeingChampionship,
                    Strings.ContractLetterWindow_ReputationReason_AgeingChampionship_2,
                    Strings.ContractLetterWindow_ReputationReason_AgeingChampionship_3
                },

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_AgeingChampionshipWashed,
                    Strings.ContractLetterWindow_ReputationReason_AgeingChampionshipWashed_2,
                    Strings.ContractLetterWindow_ReputationReason_AgeingChampionshipWashed_3
                },

                _ => new[]
                {
                    Strings.ContractLetterWindow_ReputationReason_Default,
                    Strings.ContractLetterWindow_ReputationReason_Default_2,
                    Strings.ContractLetterWindow_ReputationReason_Default_3
                }
            };

            return alternatives[_phrasingRandom.Next(alternatives.Length)];
        }

        private string GetCompetitionMention(string replacedDriverName)
        {
            return string.Format(Strings.ContractLetterWindow_CompetitionMention_Format, replacedDriverName);
        }

        private void GenerateRejectionLetter(string teamName, string teamPrincipal, string playerName, string replacedDriverName, string roleName)
        {
            // Set team header
            TeamNameHeader.Text = teamName.ToUpper();

            // Get rejection reason based on reputation, plus a qualification-fit detail (over-,
            // under- or "good but not perfect" fit for this specific seat) when we can resolve the
            // team/role that were being contested - see AppendQualificationDetail.
            string rejectionReason = GetRejectionReason(playerReputation);
            var contestedTeam = currentSeason?.Teams?.FirstOrDefault(t => t.TeamId == teamId);
            if (contestedTeam != null)
            {
                var contestedRole = contestedTeam.Driver1Contract?.DriverId == replacedDriverId
                    ? DriverRole.FIRST_DRIVER
                    : DriverRole.SECOND_DRIVER;
                rejectionReason = AppendQualificationDetail(rejectionReason, playerReputation, contestedRole, contestedTeam.Reputation);
            }

            string preferredDriverReason = GetPreferredDriverReason(replacedDriverReputation, replacedDriverName);

            LetterContent.Text = string.Format(Strings.ContractLetterWindow_RejectionLetter_Format,
                playerName, teamName, roleName, replacedDriverName, rejectionReason, preferredDriverReason);

            // Set signature
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = string.Format(Strings.ContractLetterWindow_SignatureTitle_Format, teamName);
        }

        /// <summary>
        /// Tacks an extra sentence onto a rejection reason describing how the player's reputation
        /// measures up against this specific seat, using the same over/good/perfect/under-qualified
        /// scale <see cref="DriverHirer"/> uses to decide who a team actually hires. PerfectFit gets
        /// no extra sentence - the standard rejection/preferred-driver reasons already cover a "lost
        /// to an equally-strong rival" outcome.
        /// </summary>
        private static string AppendQualificationDetail(string rejectionReason, DriverReputation playerReputation, DriverRole role, TeamReputation teamReputation)
        {
            var fit = new DriverHirer().DoesDriverFitTeamPolicy(playerReputation, role, teamReputation);
            string detail = fit switch
            {
                DriverHirer.DriverPolicyFit.OverQualified => Strings.ContractLetterWindow_QualificationDetail_OverQualified,
                DriverHirer.DriverPolicyFit.GoodFit => Strings.ContractLetterWindow_QualificationDetail_GoodFit,
                DriverHirer.DriverPolicyFit.UnderQualified => Strings.ContractLetterWindow_QualificationDetail_UnderQualified,
                _ => null
            };

            return string.IsNullOrEmpty(detail) ? rejectionReason : $"{rejectionReason} {detail}";
        }

        /// <summary>
        /// Tacks an extra sentence onto a reputation reason describing how the player's reputation
        /// measures up against this specific seat, using the same over/good/perfect/under-qualified
        /// scale <see cref="DriverHirer"/> uses to decide who a team actually hires. PerfectFit gets
        /// no extra sentence - the standard reputation reason already covers a straightforward hire.
        /// Success-side counterpart of <see cref="AppendQualificationDetail"/>.
        /// </summary>
        private static string AppendSuccessQualificationDetail(string reputationReason, DriverReputation playerReputation, DriverRole role, TeamReputation teamReputation)
        {
            var fit = new DriverHirer().DoesDriverFitTeamPolicy(playerReputation, role, teamReputation);
            string detail = fit switch
            {
                DriverHirer.DriverPolicyFit.OverQualified => Strings.ContractLetterWindow_SuccessQualificationDetail_OverQualified,
                DriverHirer.DriverPolicyFit.GoodFit => Strings.ContractLetterWindow_SuccessQualificationDetail_GoodFit,
                DriverHirer.DriverPolicyFit.UnderQualified => Strings.ContractLetterWindow_SuccessQualificationDetail_UnderQualified,
                _ => null
            };

            return string.IsNullOrEmpty(detail) ? reputationReason : $"{reputationReason} {detail}";
        }

        private string GetRejectionReason(DriverReputation reputation)
        {
            string[] alternatives = reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_PayDriverWildCard,
                    Strings.ContractLetterWindow_RejectionReason_PayDriverWildCard_2,
                    Strings.ContractLetterWindow_RejectionReason_PayDriverWildCard_3
                },

                DriverReputation.PAY_DRIVER_SEASON => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_PayDriverSeason,
                    Strings.ContractLetterWindow_RejectionReason_PayDriverSeason_2,
                    Strings.ContractLetterWindow_RejectionReason_PayDriverSeason_3
                },

                DriverReputation.YOUNG_TALENT => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_YoungTalent,
                    Strings.ContractLetterWindow_RejectionReason_YoungTalent_2,
                    Strings.ContractLetterWindow_RejectionReason_YoungTalent_3
                },

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_YoungChampionshipUnproven,
                    Strings.ContractLetterWindow_RejectionReason_YoungChampionshipUnproven_2,
                    Strings.ContractLetterWindow_RejectionReason_YoungChampionshipUnproven_3
                },

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_YoungChampionship,
                    Strings.ContractLetterWindow_RejectionReason_YoungChampionship_2,
                    Strings.ContractLetterWindow_RejectionReason_YoungChampionship_3
                },

                DriverReputation.PRIME_MIDFIELD => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_PrimeMidfield,
                    Strings.ContractLetterWindow_RejectionReason_PrimeMidfield_2,
                    Strings.ContractLetterWindow_RejectionReason_PrimeMidfield_3
                },

                DriverReputation.PRIME_STRONG_MIDFIELD => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_PrimeStrongMidfield,
                    Strings.ContractLetterWindow_RejectionReason_PrimeStrongMidfield_2,
                    Strings.ContractLetterWindow_RejectionReason_PrimeStrongMidfield_3
                },

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionshipUnproven,
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionshipUnproven_2,
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionshipUnproven_3
                },

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionship,
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionship_2,
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionship_3
                },

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionshipWashed,
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionshipWashed_2,
                    Strings.ContractLetterWindow_RejectionReason_PrimeChampionshipWashed_3
                },

                DriverReputation.AGEING_MIDFIELD => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_AgeingMidfield,
                    Strings.ContractLetterWindow_RejectionReason_AgeingMidfield_2,
                    Strings.ContractLetterWindow_RejectionReason_AgeingMidfield_3
                },

                DriverReputation.AGEING_STRONG_MIDFIELD => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_AgeingStrongMidfield,
                    Strings.ContractLetterWindow_RejectionReason_AgeingStrongMidfield_2,
                    Strings.ContractLetterWindow_RejectionReason_AgeingStrongMidfield_3
                },

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_AgeingChampionship,
                    Strings.ContractLetterWindow_RejectionReason_AgeingChampionship_2,
                    Strings.ContractLetterWindow_RejectionReason_AgeingChampionship_3
                },

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_AgeingChampionshipWashed,
                    Strings.ContractLetterWindow_RejectionReason_AgeingChampionshipWashed_2,
                    Strings.ContractLetterWindow_RejectionReason_AgeingChampionshipWashed_3
                },

                _ => new[]
                {
                    Strings.ContractLetterWindow_RejectionReason_Default,
                    Strings.ContractLetterWindow_RejectionReason_Default_2,
                    Strings.ContractLetterWindow_RejectionReason_Default_3
                }
            };

            return alternatives[_phrasingRandom.Next(alternatives.Length)];
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