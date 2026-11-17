using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Resources;
using System.Linq;

namespace AMS2ChEd.Views.Letters
{
    /// <summary>
    /// A team's answer to the player's application for a seat (was ContractLetterWindow): at career
    /// start, where the hire is decided here, and in the off-season, where it was already decided
    /// by the resolved new season.
    /// </summary>
    public static class ContractLetters
    {
        /// <summary>
        /// Career start: too young or too old to be considered, or the team's decision on the seat
        /// (judged on the easier reserve policy for a reserve seat). Closes with
        /// <see cref="LetterActionIds.Next"/> when the player was hired, with
        /// <see cref="LetterActionIds.ChooseAnotherTeam"/> when he wasn't.
        /// </summary>
        public static Letter CareerStartApplication(
            GameLogicFactory gameLogicFactory,
            string teamName,
            string teamId,
            string teamPrincipal,
            string playerName,
            int playerAge,
            string playerDriverId,
            DriverReputation playerReputation,
            string replacedDriverName,
            string replacedDriverId,
            DriverReputation replacedDriverReputation,
            string roleName,
            ISeason season,
            bool isReserve = false)
        {
            var hired = Letter.NextAction();
            var chooseAnotherTeam = new LetterAction(LetterActionIds.ChooseAnotherTeam, Strings.Letter_ChooseAnotherTeamButton, LetterActionKind.Neutral);
            int seasonYear = season?.Year ?? 0;

            if (playerAge < 18)
            {
                string template = playerAge <= 6
                    // very young children - pure dream stage
                    ? Strings.ContractLetterWindow_YoungDriver_VeryYoung_Format
                    : playerAge <= 14
                        // karting age - developmental stage
                        ? Strings.ContractLetterWindow_YoungDriver_Karting_Format
                        // junior formula age - serious development stage
                        : Strings.ContractLetterWindow_YoungDriver_Junior_Format;

                return Letter.FromTeam(teamName, teamPrincipal, Strings.LetterSubject_AgeRequirement,
                    string.Format(template, playerName, teamName, playerAge), chooseAnotherTeam);
            }

            if (playerAge > 42 && playerReputation != DriverReputation.JUST_ONE_LAST_DANCE)
            {
                return Letter.FromTeam(teamName, teamPrincipal, Strings.LetterSubject_AgeRequirement,
                    string.Format(Strings.ContractLetterWindow_OlderDriver_Rejection_Format, playerName, teamName, playerAge), chooseAnotherTeam);
            }

            var contestedTeam = season?.Teams?.FirstOrDefault(t => t.TeamId == teamId);
            var contestedTeamReputation = contestedTeam?.Reputation ?? TeamReputation.MIDFIELD;
            var result = isReserve
                ? gameLogicFactory.ContractNegotiationEngine.EvaluateReserveContract(playerReputation, replacedDriverReputation, contestedTeamReputation)
                : gameLogicFactory.ContractNegotiationEngine.EvaluateContract(playerDriverId, playerReputation, replacedDriverId, replacedDriverReputation);

            // the kind of driver the team wants in the seat, for the qualification-fit detail (over-,
            // under- or "good but not perfect" fit) when the team/role being contested is known
            DriverRole? contestedRole = contestedTeam != null
                ? GetContestedRole(contestedTeam, replacedDriverId, replacedDriverReputation)
                : null;

            if (result.IsPlayerHired)
            {
                string reputationReason = GetReputationReason(playerReputation);
                if (contestedRole != null)
                    reputationReason = AppendSuccessQualificationDetail(reputationReason, playerReputation, contestedRole.Value, contestedTeam!.Reputation, isReserve);

                string competitionMention = string.Format(Strings.ContractLetterWindow_CompetitionMention_Format, replacedDriverName);

                return Letter.FromTeam(teamName, teamPrincipal,
                    string.Format(Strings.LetterSubject_ContractOffer_Format, roleName, seasonYear),
                    string.Format(Strings.ContractLetterWindow_SuccessLetter_Format, playerName, teamName, roleName, competitionMention, reputationReason),
                    hired);
            }

            string rejectionReason = GetRejectionReason(playerReputation);
            if (contestedRole != null)
                rejectionReason = AppendQualificationDetail(rejectionReason, playerReputation, contestedRole.Value, contestedTeam!.Reputation, isReserve);

            string preferredDriverReason = GetPreferredDriverReason(replacedDriverReputation, replacedDriverName);

            return Letter.FromTeam(teamName, teamPrincipal,
                string.Format(Strings.LetterSubject_ApplicationUnsuccessful_Format, roleName, seasonYear),
                string.Format(Strings.ContractLetterWindow_RejectionLetter_Format, playerName, teamName, roleName, replacedDriverName, rejectionReason, preferredDriverReason),
                chooseAnotherTeam);
        }

        /// <summary>
        /// Off-season team-application result. The hired/not outcome is already decided by the
        /// resolved new season (OffSeasonOrchestrator), so there are no age checks and no
        /// re-evaluation: a read-only outcome letter with a single NEXT.
        /// </summary>
        public static Letter OffSeasonApplicationResult(
            string teamName,
            string teamPrincipal,
            string playerName,
            DriverReputation playerReputation,
            bool isPlayerHired,
            string? otherDriverName,
            bool otherDriverWasAtTeamBefore,
            string roleName,
            TeamReputation teamReputation,
            DriverRole role,
            int nextSeasonYear,
            bool isReserve = false,
            string? roleChangeNote = null,
            bool otherDriverSignedElsewhere = false)
        {
            if (isPlayerHired)
            {
                string reputationReason = AppendSuccessQualificationDetail(GetReputationReason(playerReputation), playerReputation, role, teamReputation, isReserve);
                string beatDriverClause = string.IsNullOrEmpty(otherDriverName)
                    ? Strings.ContractLetterWindow_OffSeasonSuccess_NoRival
                    : string.Format(
                        otherDriverSignedElsewhere
                            ? Strings.ContractLetterWindow_OffSeasonSuccess_PickSignedElsewhere_Format
                            : otherDriverWasAtTeamBefore
                                ? Strings.ContractLetterWindow_OffSeasonSuccess_BeatIncumbent_Format
                                : Strings.ContractLetterWindow_OffSeasonSuccess_BeatNewcomer_Format,
                        otherDriverName);

                if (!string.IsNullOrEmpty(roleChangeNote))
                    beatDriverClause = $"{beatDriverClause} {roleChangeNote}";

                return Letter.FromTeam(teamName, teamPrincipal,
                    string.Format(Strings.LetterSubject_ContractOffer_Format, roleName, nextSeasonYear),
                    string.Format(Strings.ContractLetterWindow_OffSeasonSuccessLetter_Format, playerName, teamName, roleName, beatDriverClause, reputationReason),
                    Letter.NextAction());
            }

            string rejectionReason = AppendQualificationDetail(GetRejectionReason(playerReputation), playerReputation, role, teamReputation, isReserve);
            string otherDriverClause = string.Format(
                otherDriverWasAtTeamBefore
                    ? Strings.ContractLetterWindow_OffSeasonRejection_KeptIncumbent_Format
                    : Strings.ContractLetterWindow_OffSeasonRejection_SignedNewcomer_Format,
                otherDriverName);

            return Letter.FromTeam(teamName, teamPrincipal,
                string.Format(Strings.LetterSubject_ApplicationUnsuccessful_Format, roleName, nextSeasonYear),
                string.Format(Strings.ContractLetterWindow_OffSeasonRejectionLetter_Format, playerName, teamName, roleName, otherDriverName, rejectionReason, otherDriverClause),
                Letter.NextAction());
        }

        // the kind of driver the team wants in the seat being contested: the seat's contract role
        // when explicit, otherwise whether the driver being replaced was leading the team
        private static DriverRole GetContestedRole(ITeamEntry contestedTeam, string replacedDriverId, DriverReputation replacedDriverReputation)
        {
            var seat = contestedTeam.Driver1Contract?.DriverId == replacedDriverId
                ? contestedTeam.Driver1Contract
                : contestedTeam.Driver2Contract;

            return seat?.Role switch
            {
                ContractRole.FIRST_DRIVER => DriverRole.FIRST_DRIVER,
                ContractRole.SECOND_DRIVER => DriverRole.SECOND_DRIVER,
                _ => DriverHirer.IsFirstDriverMaterial(replacedDriverReputation, contestedTeam.Reputation)
                        ? DriverRole.FIRST_DRIVER
                        : DriverRole.SECOND_DRIVER
            };
        }

        /// <summary>
        /// Tacks an extra sentence onto a rejection reason describing how the player's reputation
        /// measures up against this specific seat, using the same over/good/perfect/under-qualified
        /// scale <see cref="DriverHirer"/> uses to decide who a team actually hires. PerfectFit gets
        /// no extra sentence - the standard rejection/preferred-driver reasons already cover a "lost
        /// to an equally-strong rival" outcome.
        /// </summary>
        private static string AppendQualificationDetail(string rejectionReason, DriverReputation playerReputation, DriverRole role, TeamReputation teamReputation, bool isReserve)
        {
            var fit = new DriverHirer().DoesDriverFitSeat(playerReputation, role, teamReputation, isReserve);
            string? detail = fit switch
            {
                DriverHirer.DriverPolicyFit.OverQualified => Strings.ContractLetterWindow_QualificationDetail_OverQualified,
                DriverHirer.DriverPolicyFit.GoodFit => Strings.ContractLetterWindow_QualificationDetail_GoodFit,
                DriverHirer.DriverPolicyFit.UnderQualified => Strings.ContractLetterWindow_QualificationDetail_UnderQualified,
                _ => null
            };

            return string.IsNullOrEmpty(detail) ? rejectionReason : $"{rejectionReason} {detail}";
        }

        /// <summary>
        /// Success-side counterpart of <see cref="AppendQualificationDetail"/>: PerfectFit gets no
        /// extra sentence - the standard reputation reason already covers a straightforward hire.
        /// </summary>
        private static string AppendSuccessQualificationDetail(string reputationReason, DriverReputation playerReputation, DriverRole role, TeamReputation teamReputation, bool isReserve)
        {
            var fit = new DriverHirer().DoesDriverFitSeat(playerReputation, role, teamReputation, isReserve);
            string? detail = fit switch
            {
                DriverHirer.DriverPolicyFit.OverQualified => Strings.ContractLetterWindow_SuccessQualificationDetail_OverQualified,
                DriverHirer.DriverPolicyFit.GoodFit => Strings.ContractLetterWindow_SuccessQualificationDetail_GoodFit,
                DriverHirer.DriverPolicyFit.UnderQualified => Strings.ContractLetterWindow_SuccessQualificationDetail_UnderQualified,
                _ => null
            };

            return string.IsNullOrEmpty(detail) ? reputationReason : $"{reputationReason} {detail}";
        }

        /// <summary>
        /// Shared RNG for picking among the alternative phrasings in <see cref="GetReputationReason"/>
        /// and <see cref="GetRejectionReason"/>, so re-generating a letter for the same reputation
        /// doesn't always produce word-for-word the same text.
        /// </summary>
        private static readonly Random _phrasingRandom = new Random();

        private static string GetReputationReason(DriverReputation reputation)
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

        private static string GetRejectionReason(DriverReputation reputation)
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

        private static string GetPreferredDriverReason(DriverReputation replacedReputation, string replacedDriverName)
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
    }
}
