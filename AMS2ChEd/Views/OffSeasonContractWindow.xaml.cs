using Ams2ChEd.Business.AMS2.DependencyInjection;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
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
            Ams2StorageFactory storageFactory,
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

            string teamName = playerTeam?.TeamName ?? "Unknown Team";
            string teamPrincipal = playerTeam?.TeamPrincipal ?? "Team Management";

            TeamNameHeader.Text = teamName.ToUpper();
            SignatureName.Text = teamPrincipal;
            SignatureTitle.Text = $"Team Principal, {teamName}";

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
                    "While your financial contribution to the team has been valuable, we also recognize your development as a driver this season.",

                DriverReputation.YOUNG_TALENT =>
                    "You've shown promising flashes of talent throughout the season. We believe there's still much more to come from you.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN or DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    "Your performances this season have been exceptional. You've proven yourself as a championship-caliber driver.",

                DriverReputation.PRIME_MIDFIELD or DriverReputation.PRIME_STRONG_MIDFIELD =>
                    "Your consistency and reliability have been exactly what we needed. You've delivered solid results throughout the season.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN or DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    "Your championship-level performances speak for themselves. You remain a cornerstone of our team's ambitions.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    "You've shown glimpses of your former championship-winning form this season, and we believe the best is yet to come.",

                DriverReputation.AGEING_MIDFIELD or DriverReputation.AGEING_STRONG_MIDFIELD =>
                    "Your experience and consistency continue to be valuable assets to our team's development.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL or DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    "Your wealth of experience and leadership within the team remain invaluable to our success.",

                DriverReputation.JUST_ONE_LAST_DANCE =>
                    "Your legendary status and what you can still bring to the team, despite the years, makes us believe there's one more chapter to write together.",

                _ => "Your contributions to the team this season have been appreciated."
            };

            LetterContent.Text = $"Dear {playerName},\n\n" +
                              $"As the {teamName} season comes to a close, I wanted to personally reach out to discuss your future with our team.\n\n" +
                              $"{reputationMessage}\n\n" +
                              $"We would like to offer you a contract renewal for the upcoming season. We believe that the partnership " +
                              $"between you and {teamName} can continue to grow stronger, and we're excited about what we can achieve together.\n\n" +
                              $"This is your opportunity to continue representing {teamName} at the highest level of motorsport. " +
                              $"We hope you'll choose to remain with us and help drive our ambitions forward.\n\n" +
                              $"Please consider this offer carefully. We look forward to your decision.";
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

                _ => "After careful consideration, we have decided not to renew your contract for the upcoming season."
            };

            LetterContent.Text = $"Dear {playerName},\n\n" +
                              $"I am writing to inform you of an important decision regarding your future with {teamName}.\n\n" +
                              $"{reasonMessage}\n\n" +
                              $"This was not an easy decision, and we want to thank you for your service to {teamName}. " +
                              $"You will always be part of our team's history, and we wish you the very best in your future endeavors.\n\n" +
                              $"We hope our paths may cross again in the future.";
        }
        private string GenerateTeamQuittingMessage(string playerName, string teamName)
        {
            return $"It is with great regret that I must inform you that {teamName} has made the difficult decision to withdraw " +
                  $"from Formula One competition at the end of this season. This decision was made at the highest levels of our " +
                  $"organization due to circumstances beyond our control.\n\n" +
                  $"As a result, we will be unable to honor the continuation of your contract. Please know that this decision " +
                  $"reflects the team's situation and not your performance or value as a driver. We have been privileged to have " +
                  $"you represent {teamName}, and your contributions to our legacy in Formula One will not be forgotten.";
        }

        private string GenerateContractExpiredMessage(DriverReputation reputation, string teamName)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD or DriverReputation.PAY_DRIVER_SEASON =>
                    $"Your contract with {teamName} has reached its conclusion. As we plan for next season, we are " +
                    $"evaluating our financial structure and driver lineup options. We are in discussions with several " +
                    $"drivers and sponsors to determine the best combination for the team's competitiveness. Any continuation " +
                    $"would require fresh negotiations regarding both sporting and commercial terms. We will be in touch once " +
                    $"we've finalized our approach.",

                DriverReputation.YOUNG_TALENT =>
                    $"With your contract now concluded, we enter our evaluation period for next season. You've shown promise " +
                    $"during your time with us, but we need to carefully consider whether you've gained enough experience to " +
                    $"meet our evolving targets, or if additional development time elsewhere might be beneficial. We are " +
                    $"exploring various driver options as we assess what's best for both the team and your career progression. " +
                    $"Should we wish to continue together, new terms would need to be negotiated.",

                DriverReputation.PRIME_MIDFIELD or DriverReputation.AGEING_MIDFIELD =>
                    $"Your contract term has come to an end. As we review our direction for next season, we need to determine " +
                    $"whether maintaining stability with known quantities serves us better than pursuing potential upgrades " +
                    $"from the driver market. We will be speaking with several candidates to understand all available options. " +
                    $"You've been a solid contributor, and any renewal would require new contract discussions. We'll communicate " +
                    $"our decision once our evaluation is complete.",

                DriverReputation.AGEING_STRONG_MIDFIELD =>
                    $"As your contract reaches its expiration, we begin planning for the seasons ahead. We must weigh the value " +
                    $"of your experience and consistency against our longer-term timeline and the opportunities available in the " +
                    $"current driver market. We are exploring options with drivers at various career stages to determine the optimal " +
                    $"balance for our ambitions. Should we decide to offer you a new contract, fresh negotiations would be necessary. " +
                    $"You'll hear from us once we've completed our assessment.",

                DriverReputation.PRIME_STRONG_MIDFIELD =>
                    $"Your contract with {teamName} has concluded. As we plan our lineup for next season, we are evaluating " +
                    $"whether to maintain continuity with a driver of your caliber or explore opportunities with some of the " +
                    $"high-profile names currently available. These are competitive times in the driver market, and we need to " +
                    $"ensure we're maximizing our potential. Any continuation would require new contract terms. We will inform " +
                    $"you of our decision in the coming weeks.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN or DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    $"With your contract now expired, we face an interesting situation. Your exceptional performances have " +
                    $"naturally attracted attention from across the paddock, and we must now determine whether we can meet " +
                    $"the terms that a driver of your emerging status commands. We are assessing our budget, our competitive " +
                    $"position, and the realistic alternatives available to us. Should we wish to retain your services, " +
                    $"significant new negotiations would be required. We'll be in contact once we've made our decision.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN or DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    $"Your contract term has reached its conclusion. Given your championship-level performances, we must now " +
                    $"evaluate whether {teamName} can provide you with machinery worthy of your talent, and whether we can " +
                    $"meet the contractual expectations that come with retaining a driver of your caliber. We are reviewing " +
                    $"our competitive position and financial capacity while also exploring alternative options. Any renewal " +
                    $"would require substantial new negotiations. You'll hear from us shortly.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    $"As your contract expires, we enter a critical evaluation phase. We need to honestly assess whether your " +
                    $"performances represent a temporary dip or a longer-term trend, and whether we believe you can return to " +
                    $"your former championship-winning form. We are exploring the driver market to understand what options exist " +
                    $"at various experience and performance levels. Your track record speaks for itself, but any continuation " +
                    $"would require new terms that reflect current circumstances. We will inform you of our conclusions.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL or DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    $"Your current contract with {teamName} has concluded. As we plan ahead, we must carefully consider the " +
                    $"balance between your invaluable experience and championship pedigree versus the practical realities of " +
                    $"career longevity and the availability of younger talent. We are evaluating multiple drivers across " +
                    $"different age profiles and career stages. Your legacy is secure, but any new contract would require " +
                    $"extensive discussions about role, terms, and timeline. We'll be in touch with our decision.",

                DriverReputation.JUST_ONE_LAST_DANCE =>
                    $"With your contract now expired, we face a difficult decision. We must honestly evaluate how much longer " +
                    $"you can maintain competitive performance levels, and whether extending your career with us aligns with " +
                    $"the team's planning horizon. We have tremendous respect for what you've achieved, but we are also " +
                    $"speaking with younger drivers as we consider our long-term strategy. Should we decide to offer you " +
                    $"another season, fresh negotiations would be necessary. We will communicate our decision shortly.",

                _ =>
                    $"With your contract now expired, we enter our standard off-season evaluation period. We will be " +
                    $"exploring various options for our driver lineup and speaking with multiple candidates. Should we " +
                    $"wish to continue our partnership, new contract negotiations would be required. We will inform you " +
                    $"of our decision in due course."
            };
        }

        private string GenerateUnderperformingMessage(DriverReputation reputation, string teamName)
        {
            return $"Throughout this season, we have monitored performance levels carefully. Unfortunately, the results " +
                  $"have not met the standards we require at {teamName}. While we recognize your efforts, we must make " +
                  $"changes to our driver lineup to remain competitive. We believe this decision, though difficult, is " +
                  $"necessary for the team's future success.";
        }

        private string GenerateRetiringMessage(string playerName, string teamName)
        {
            return $"We've noticed your exceptional career is reaching its natural conclusion. After careful consideration " +
                  $"of your future in the sport and discussions within the team, we believe it may be time to consider stepping " +
                  $"back from active competition. Your legacy with {teamName} is secure, and we would be honored if you would " +
                  $"consider transitioning to an ambassadorial or advisory role with us. However, we understand if you choose " +
                  $"to pursue other opportunities.";
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
