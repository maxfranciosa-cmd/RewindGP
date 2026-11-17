using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Resources;

namespace AMS2ChEd.Views.Letters
{
    /// <summary>
    /// The one off-season letter of a player who was a team's reserve (ReserveLetter, was
    /// ReserveLetterWindow): the promotion to an opening race seat (first refusal), or why there's
    /// none, then whether the team keeps them as reserve next season. Declining the seat turns the
    /// page to the reserve part of the same letter; declining everything leaves them a free agent.
    /// </summary>
    public static class ReserveLetters
    {
        public const string AcceptSeatId = "accept_seat";
        public const string StayOnId = "stay_on";
        public const string LeaveId = "leave";

        public static Letter Compose(string playerName, ITeamEntry? teamEntry, ReserveLetter letter, int nextSeasonYear)
        {
            var context = new Context(playerName, teamEntry, letter, nextSeasonYear);
            return letter.Promotion?.IsOffered == true
                ? context.Promotion()
                : context.ReserveOutlook(context.NotPromotedReason(), afterDecliningSeat: false);
        }

        /// <summary>What the player decided, from the button the letter closed with.</summary>
        public static ReserveLetterDecision ToDecision(string? actionId) => actionId switch
        {
            AcceptSeatId => ReserveLetterDecision.AcceptedPromotion,
            StayOnId => ReserveLetterDecision.StaysReserve,
            _ => ReserveLetterDecision.FreeAgent
        };

        private sealed class Context
        {
            private readonly string _playerName;
            private readonly string _teamName;
            private readonly string _teamPrincipal;
            private readonly ReserveLetter _letter;
            private readonly int _nextSeasonYear;

            public Context(string playerName, ITeamEntry? teamEntry, ReserveLetter letter, int nextSeasonYear)
            {
                _playerName = playerName;
                _letter = letter;
                _nextSeasonYear = nextSeasonYear;
                _teamName = teamEntry?.TeamName ?? Strings.OffSeasonContractWindow_DefaultTeamName;
                _teamPrincipal = teamEntry?.TeamPrincipal ?? Strings.OffSeasonContractWindow_DefaultTeamPrincipal;
            }

            private static string RoleName(DriverRole role) => role == DriverRole.FIRST_DRIVER
                ? Strings.TeamApplicationWindow_FirstDriverRole
                : Strings.TeamApplicationWindow_SecondDriverRole;

            // a seat opened but the reserve isn't a PerfectFit for it: why, from their fit
            public string? NotPromotedReason()
            {
                var promotion = _letter.Promotion;
                if (promotion == null)
                    return null;

                var format = promotion.Fit switch
                {
                    DriverHirer.DriverPolicyFit.OverQualified => Strings.ReserveLetterWindow_NotPromoted_OverQualified_Format,
                    DriverHirer.DriverPolicyFit.GoodFit => Strings.ReserveLetterWindow_NotPromoted_GoodFit_Format,
                    _ => Strings.ReserveLetterWindow_NotPromoted_UnderQualified_Format
                };

                return string.Format(format, _teamName, RoleName(promotion.Role)) + " " + Strings.ReserveLetterWindow_NotPromoted_CanApply;
            }

            public Letter Promotion()
            {
                return Letter.FromTeam(
                    _teamName,
                    _teamPrincipal,
                    string.Format(Strings.LetterSubject_ReservePromotion_Format, _nextSeasonYear),
                    string.Format(Strings.ReserveLetterWindow_Promotion_Format, _playerName, _teamName, RoleName(_letter.Promotion!.Role)),
                    new LetterAction(AcceptSeatId, Strings.ReserveLetterWindow_AcceptSeatButton, LetterActionKind.Accept),
                    // the same letter goes on: staying on as reserve, if the team runs one
                    new LetterAction(LetterActionIds.Reject, Strings.ReserveLetterWindow_DeclineButton, LetterActionKind.Reject,
                        next: () => ReserveOutlook(notPromotedReason: null, afterDecliningSeat: true)));
            }

            public Letter ReserveOutlook(string? notPromotedReason, bool afterDecliningSeat)
            {
                var paragraphs = new List<string> { string.Format(Strings.ReserveLetterWindow_Greeting_Format, _playerName) };

                if (afterDecliningSeat)
                    paragraphs.Add(Strings.ReserveLetterWindow_SeatDeclined);
                else if (notPromotedReason != null)
                    paragraphs.Add(notPromotedReason);

                if (_letter.TeamWantsReserve)
                {
                    paragraphs.Add(Strings.ReserveLetterWindow_StayOn);
                    return Letter.FromTeam(
                        _teamName,
                        _teamPrincipal,
                        string.Format(Strings.LetterSubject_ReserveStayOn_Format, _nextSeasonYear),
                        string.Join("\n\n", paragraphs),
                        new LetterAction(StayOnId, Strings.ReserveLetterWindow_StayButton, LetterActionKind.Accept),
                        new LetterAction(LeaveId, Strings.ReserveLetterWindow_LeaveButton, LetterActionKind.Reject));
                }

                // nothing to choose: the team runs no reserve next season
                paragraphs.Add(Strings.ReserveLetterWindow_NoReserveNextSeason);
                return Letter.FromTeam(
                    _teamName,
                    _teamPrincipal,
                    Strings.LetterSubject_ReserveFarewell,
                    string.Join("\n\n", paragraphs),
                    Letter.ContinueAction());
            }
        }
    }
}
