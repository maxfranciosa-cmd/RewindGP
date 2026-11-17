using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Resources;
using System.Linq;

namespace AMS2ChEd.Views.Letters
{
    /// <summary>
    /// The end-of-season letter from the player's team: a renewal offer to accept or reject, or the
    /// end of the contract and why (was OffSeasonContractWindow). Closes with
    /// <see cref="LetterActionIds.Accept"/> only when the player takes the renewal.
    /// </summary>
    public static class OffSeasonContractLetters
    {
        public static Letter Compose(
            ISaveGame saveGame,
            IEnumerable<ITeamEntry> nextSeasonTeamEntries,
            DriverFirerOutcome dropOutcome,
            DriverReputation playerReputation)
        {
            var playerId = saveGame.PlayerData.DriverId;

            // the player's team next season, or this season's when it isn't competing next season
            var playerTeam = nextSeasonTeamEntries.FirstOrDefault(t =>
                                 t.Driver1Contract.DriverId == playerId ||
                                 t.Driver2Contract.DriverId == playerId)
                             ?? saveGame.CurrentSeason.Teams.FirstOrDefault(t =>
                                 t.Driver1Contract.DriverId == playerId ||
                                 t.Driver2Contract.DriverId == playerId);

            string teamName = playerTeam?.TeamName ?? Strings.OffSeasonContractWindow_DefaultTeamName;
            string teamPrincipal = playerTeam?.TeamPrincipal ?? Strings.OffSeasonContractWindow_DefaultTeamPrincipal;
            string playerName = saveGame.PlayerData.Name;
            int nextSeasonYear = saveGame.CurrentSeason.Year + 1;

            if (dropOutcome.IsDropped())
            {
                return Letter.FromTeam(
                    teamName,
                    teamPrincipal,
                    TerminationSubject(dropOutcome, nextSeasonYear),
                    TerminationLetter(playerName, teamName, dropOutcome, playerReputation),
                    Letter.ContinueAction());
            }

            return Letter.FromTeam(
                teamName,
                teamPrincipal,
                string.Format(Strings.LetterSubject_ContractRenewal_Format, nextSeasonYear),
                RenewalLetter(playerName, teamName, playerReputation),
                new LetterAction(LetterActionIds.Accept, Strings.Letter_AcceptButton, LetterActionKind.Accept),
                new LetterAction(LetterActionIds.Reject, Strings.Letter_RejectButton, LetterActionKind.Reject));
        }

        private static string TerminationSubject(DriverFirerOutcome dropOutcome, int nextSeasonYear) => dropOutcome switch
        {
            DriverFirerOutcome.DROPPED_TEAM_QUITTING => Strings.LetterSubject_TeamWithdrawing,
            DriverFirerOutcome.DROPPED_DISCIPLINARY => Strings.LetterSubject_ContractTerminated,
            _ => string.Format(Strings.LetterSubject_ContractEnded_Format, nextSeasonYear)
        };

        private static string RenewalLetter(string playerName, string teamName, DriverReputation playerReputation)
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

            return string.Format(Strings.OffSeasonContractWindow_RenewalLetter_Format, playerName, teamName, reputationMessage);
        }

        private static string TerminationLetter(string playerName, string teamName, DriverFirerOutcome dropOutcome, DriverReputation playerReputation)
        {
            string reasonMessage = dropOutcome switch
            {
                DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED =>
                    ContractExpiredMessage(playerReputation, teamName),

                DriverFirerOutcome.DROPPED_UNDERPERFORMING =>
                    string.Format(Strings.OffSeasonContractWindow_Underperforming_Format, teamName),

                DriverFirerOutcome.DROPPED_RETIRING =>
                    string.Format(Strings.OffSeasonContractWindow_Retiring_Format, teamName),

                DriverFirerOutcome.DROPPED_TEAM_QUITTING =>
                    string.Format(Strings.OffSeasonContractWindow_TeamQuitting_Format, teamName),

                DriverFirerOutcome.DROPPED_DISCIPLINARY =>
                    string.Format(Strings.OffSeasonContractWindow_Disciplinary_Format, teamName),

                _ => Strings.OffSeasonContractWindow_TerminationReason_Default
            };

            return string.Format(Strings.OffSeasonContractWindow_TerminationLetter_Format, playerName, teamName, reasonMessage);
        }

        private static string ContractExpiredMessage(DriverReputation reputation, string teamName)
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
    }
}
