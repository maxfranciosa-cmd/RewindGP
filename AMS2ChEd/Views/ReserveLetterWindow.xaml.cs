using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Resources;
using System.Windows;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// The one off-season letter of a player who was a team's reserve (ReserveLetter): the
    /// promotion to an opening race seat (first refusal), or why there's none, then whether the team
    /// keeps them as reserve next season. Declining everything leaves them a free agent.
    /// </summary>
    public partial class ReserveLetterWindow : Window
    {
        private enum Stage
        {
            // accept or decline the race seat
            Promotion,
            // stay on as reserve, or go to the market
            StayOn,
            // nothing to choose: the team runs no reserve next season
            Farewell
        }

        private readonly string _playerName;
        private readonly string _teamName;
        private readonly ReserveLetter _letter;
        private Stage _stage;

        public ReserveLetterDecision Decision { get; private set; } = ReserveLetterDecision.FreeAgent;

        public ReserveLetterWindow(string playerName, ITeamEntry teamEntry, ReserveLetter letter)
        {
            InitializeComponent();
            _playerName = playerName;
            _letter = letter;
            _teamName = teamEntry?.TeamName ?? Strings.OffSeasonContractWindow_DefaultTeamName;

            TeamNameHeader.Text = _teamName.ToUpper();
            SignatureName.Text = teamEntry?.TeamPrincipal ?? Strings.OffSeasonContractWindow_DefaultTeamPrincipal;
            SignatureTitle.Text = string.Format(Strings.OffSeasonContractWindow_SignatureTitle_Format, _teamName);

            if (letter.Promotion?.IsOffered == true)
                ShowPromotion();
            else
                ShowReserveOutlook(notPromotedReason: NotPromotedReason());
        }

        private string RoleName(DriverRole role) => role == DriverRole.FIRST_DRIVER
            ? Strings.TeamApplicationWindow_FirstDriverRole
            : Strings.TeamApplicationWindow_SecondDriverRole;

        // a seat opened but the reserve isn't a PerfectFit for it: why, from their fit
        private string NotPromotedReason()
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

        private void ShowPromotion()
        {
            _stage = Stage.Promotion;
            LetterContent.Text = string.Format(Strings.ReserveLetterWindow_Promotion_Format, _playerName, _teamName, RoleName(_letter.Promotion.Role));
            ShowChoice(Strings.ReserveLetterWindow_AcceptSeatButton, Strings.ReserveLetterWindow_DeclineButton);
        }

        private void ShowReserveOutlook(string notPromotedReason, bool afterDecliningSeat = false)
        {
            var paragraphs = new List<string> { string.Format(Strings.ReserveLetterWindow_Greeting_Format, _playerName) };

            if (afterDecliningSeat)
                paragraphs.Add(Strings.ReserveLetterWindow_SeatDeclined);
            else if (notPromotedReason != null)
                paragraphs.Add(notPromotedReason);

            if (_letter.TeamWantsReserve)
            {
                _stage = Stage.StayOn;
                paragraphs.Add(Strings.ReserveLetterWindow_StayOn);
                ShowChoice(Strings.ReserveLetterWindow_StayButton, Strings.ReserveLetterWindow_LeaveButton);
            }
            else
            {
                _stage = Stage.Farewell;
                paragraphs.Add(Strings.ReserveLetterWindow_NoReserveNextSeason);
                AcceptButton.Visibility = Visibility.Collapsed;
                DeclineButton.Visibility = Visibility.Collapsed;
                ContinueButton.Visibility = Visibility.Visible;
            }

            LetterContent.Text = string.Join("\n\n", paragraphs);
        }

        private void ShowChoice(string acceptText, string declineText)
        {
            AcceptButton.Content = acceptText;
            DeclineButton.Content = declineText;
            AcceptButton.Visibility = Visibility.Visible;
            DeclineButton.Visibility = Visibility.Visible;
            ContinueButton.Visibility = Visibility.Collapsed;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            Decision = _stage == Stage.Promotion ? ReserveLetterDecision.AcceptedPromotion : ReserveLetterDecision.StaysReserve;
            DialogResult = true;
            Close();
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            if (_stage == Stage.Promotion)
            {
                // the same letter goes on: staying on as reserve, if the team runs one
                ShowReserveOutlook(notPromotedReason: null, afterDecliningSeat: true);
                return;
            }

            Decision = ReserveLetterDecision.FreeAgent;
            DialogResult = true;
            Close();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            Decision = ReserveLetterDecision.FreeAgent;
            DialogResult = true;
            Close();
        }
    }
}
