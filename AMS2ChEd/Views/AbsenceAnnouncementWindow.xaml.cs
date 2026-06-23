using AMS2ChEd.Business.Models;
using System;
using System.Windows;

namespace AMS2ChEd.Views
{
    public partial class AbsenceAnnouncementWindow : Window
    {
        public bool? PlayerWantsToApply { get; private set; }
        private bool _isConsecutiveAbsence;

        // Constructor for asking player if they want to apply
        public AbsenceAnnouncementWindow(
            string driverOutName,
            string teamName,
            string gpName,
            string driverInName,
            DateTime raceDate,
            bool askPlayerToApply,
            bool isConsecutiveAbsence)
        {
            InitializeComponent();

            // Set the date
            DateText.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");

            // Set the headline
            HeadlineText.Text = $"{driverOutName.ToUpper()} WON'T RACE FOR {teamName.ToUpper()} IN {gpName.ToUpper()}";

            // Generate the article body
            GenerateArticle(driverOutName, teamName, gpName, driverInName, isConsecutiveAbsence);

            // Show player application option if needed
            if (askPlayerToApply)
            {
                PlayerApplicationPanel.Visibility = Visibility.Visible;
                CloseButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                PlayerApplicationPanel.Visibility = Visibility.Collapsed;
                CloseButton.Visibility = Visibility.Visible;
            }
        }

        // Constructor for when player is refused (team prefers different driver)
        public static AbsenceAnnouncementWindow CreateRefusedWindow(
            string driverOutName,
            string teamName,
            string gpName,
            string driverInName,
            string playerTeamName,
            bool isConsecutiveAbsence)
        {
            var window = new AbsenceAnnouncementWindow();
            window.InitializeComponent();

            window.DateText.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            window.HeadlineText.Text = isConsecutiveAbsence
                ? $"{driverOutName.ToUpper()} OUT AGAIN - {driverInName.ToUpper()} STEPS IN"
                : $"{driverInName.ToUpper()} CONFIRMED FOR {teamName.ToUpper()}";
            window.GenerateRefusedArticle(driverOutName, teamName, gpName, driverInName, playerTeamName, isConsecutiveAbsence);

            window.PlayerApplicationPanel.Visibility = Visibility.Collapsed;
            window.CloseButton.Visibility = Visibility.Visible;

            return window;
        }

        // Constructor for when player's team won't let them go
        public static AbsenceAnnouncementWindow CreateTeamRefusedWindow(
            string driverOutName,
            string teamName,
            string gpName,
            string driverInName,
            string playerName,
            string playerTeamName,
            bool isConsecutiveAbsence)
        {
            var window = new AbsenceAnnouncementWindow();
            window.InitializeComponent();

            window.DateText.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            window.HeadlineText.Text = isConsecutiveAbsence
                ? $"{driverOutName.ToUpper()} MISSES ANOTHER RACE"
                : $"{playerTeamName.ToUpper()} BLOCKS DRIVER MOVE";
            window.GenerateTeamRefusedArticle(driverOutName, teamName, gpName, driverInName, playerName, playerTeamName, isConsecutiveAbsence);

            window.PlayerApplicationPanel.Visibility = Visibility.Collapsed;
            window.CloseButton.Visibility = Visibility.Visible;

            return window;
        }

        // Constructor for when player is accepted
        public static AbsenceAnnouncementWindow CreateAcceptedWindow(
            string driverOutName,
            string teamName,
            string gpName,
            string playerName,
            string playerTeamName,
            bool isConsecutiveAbsence)
        {
            var window = new AbsenceAnnouncementWindow();
            window.InitializeComponent();

            window.DateText.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            window.HeadlineText.Text = $"{playerName.ToUpper()} TO RACE FOR {teamName.ToUpper()}!";
            window.GenerateAcceptedArticle(driverOutName, teamName, gpName, playerName, playerTeamName, isConsecutiveAbsence);

            window.PlayerApplicationPanel.Visibility = Visibility.Collapsed;
            window.CloseButton.Visibility = Visibility.Visible;

            return window;
        }

        // Private parameterless constructor for factory methods
        private AbsenceAnnouncementWindow()
        {
        }

        private void GenerateArticle(string driverOutName, string teamName, string gpName, string driverInName, bool isConsecutiveAbsence)
        {
            string article;

            if (isConsecutiveAbsence)
            {
                article = $"Once again, {driverOutName} will not be on the grid for {teamName} this weekend at the {gpName}.\n\n";

                article += $"The ongoing absence continues to generate discussion within the paddock. ";
                article += $"While {teamName} has maintained discretion regarding the specifics, the team has focused their public comments on managing the situation professionally and maintaining competitive performance.\n\n";

                if (!string.IsNullOrEmpty(driverInName))
                {
                    article += $"For this weekend's race, sources indicate {driverInName} will handle driving duties. ";
                    article += $"{teamName} appears to have established their approach to covering the vacant seat as the situation continues.\n\n";
                }
                else
                {
                    article += $"As with previous races in this sequence, {teamName} is working to secure a capable replacement for the weekend. ";
                    article += $"An official announcement regarding the driver lineup is expected shortly.\n\n";
                }

                article += $"The extended nature of the situation has become a recurring storyline throughout the season. Speculation within the paddock ranges from short-term circumstances to potential longer-term arrangements, though no official clarity has been provided on the duration or underlying factors.\n\n";

                article += $"For now, {teamName} continues to adapt race by race, while the motorsport community watches to see how the situation develops.";
            }
            else
            {
                article = $"In a dramatic turn of events ahead of the {gpName}, {driverOutName} will not be racing for {teamName} this weekend.\n\n";

                article += $"The team announced the change earlier today. While specific details remain undisclosed, ";
                article += $"sources within the paddock indicate that circumstances have necessitated a driver lineup adjustment for this race.\n\n";

                if (!string.IsNullOrEmpty(driverInName))
                {
                    article += $"Paddock insiders suggest {driverInName} will step into the vacant seat for this weekend's race. ";
                    article += $"The team is expected to make an official announcement shortly.\n\n";
                }
                else
                {
                    article += $"The identity of the replacement driver has not yet been confirmed by the team. ";
                    article += $"Multiple sources indicate several candidates are under consideration for the opportunity.\n\n";
                }

                article += $"The last-minute change adds an intriguing dimension to the {gpName} weekend and could have ramifications for both {teamName} and the championship standings.\n\n";

                article += $"All eyes will be on the grid as teams and drivers adjust to the reshuffled lineup.";
            }

            ArticleText.Text = article;
        }

        private void GenerateRefusedArticle(string driverOutName, string teamName, string gpName, string driverInName, string playerTeamName, bool isConsecutiveAbsence)
        {
            string article;

            if (isConsecutiveAbsence)
            {
                article = $"BREAKING: With {driverOutName} still out, {teamName} confirms {driverInName} for {gpName}.\n\n";

                article += $"As {driverOutName}'s absence from the {teamName} lineup continues, the team has finalized {driverInName} as their choice to fill the seat for this weekend's race.\n\n";

                article += $"The decision to proceed with {driverInName} follows what has become a familiar pattern for {teamName} as they manage their ongoing driver situation. ";
                article += $"\"We've developed our processes for handling these circumstances,\" a {teamName} source revealed. \"We're focused on maintaining our competitive standards.\"\n\n";

                article += $"Despite continued interest from other drivers seeking an opportunity with the team, including fresh approaches from rival teams, {teamName} has settled on their preferred solution with {driverInName}. ";
                article += $"The prolonged absence of {driverOutName} has created an unusual situation where replacement drivers have had extended opportunities to demonstrate their capabilities.\n\n";

                article += $"Questions persist about the nature and duration of {driverOutName}'s absence, though {teamName} has maintained their position of limited public comment. ";
                article += $"As the situation extends across multiple races, paddock observers note the team's consistent approach to managing what has become an ongoing challenge.";
            }
            else
            {
                article = $"BREAKING: {driverOutName} will not compete for {teamName} at the {gpName}. {driverInName} confirmed as replacement.\n\n";

                article += $"In a swift response to {driverOutName}'s unavailability, {teamName} has secured the services of {driverInName} for this weekend's race.\n\n";

                article += $"Our sources indicate that multiple drivers expressed interest in the opportunity, including approaches from rival teams. ";
                article += $"However, after careful evaluation, {teamName} opted to move forward with {driverInName}.\n\n";

                article += $"A {teamName} spokesperson commented: \"We received interest from several talented drivers across the grid. ";
                article += $"After reviewing all options, we're confident {driverInName} is the right choice for this particular race.\"\n\n";

                article += $"The decision reflects {teamName}'s specific requirements for the weekend and their assessment of the available candidates. ";
                article += $"{driverInName} will now have the opportunity to prove themselves in machinery they may not be familiar with.\n\n";

                article += $"With this decision finalized, all teams can now turn their attention to preparing for what promises to be an exciting {gpName}.";
            }

            ArticleText.Text = article;
        }

        private void GenerateTeamRefusedArticle(string driverOutName, string teamName, string gpName, string driverInName, string playerName, string playerTeamName, bool isConsecutiveAbsence)
        {
            string article;

            if (isConsecutiveAbsence)
            {
                article = $"BREAKING: {driverInName} confirmed by {teamName}\n\n";

                article += $"With {driverOutName} out for {teamName}, the team has confirmed {driverInName} will handle driving duties this weekend at the {gpName}.\n\n";

                article += $"As has occurred with previous races in this sequence, sources indicate {playerName} made inquiries about the opportunity. ";
                article += $"However, {playerTeamName} has maintained their established position throughout this ongoing situation.\n\n";

                article += $"\"Our stance has been consistent,\" stated a {playerTeamName} representative. \"We have commitments to our championship campaign that require our full driver lineup. ";
                article += $"While we respect that these circumstances create opportunities, our team objectives remain the priority.\"\n\n";

                article += $"The recurring pattern has generated ongoing paddock discussion about driver availability and team strategies. ";
                article += $"As {driverOutName}'s absence extends across multiple races, the dynamics continue to evolve, though {playerTeamName} has shown no indication of reconsidering their position.\n\n";

                article += $"{teamName} moves forward with {driverInName}, continuing to navigate what has become an extended period of driver lineup adjustments. ";
                article += $"The situation remains fluid, with no clear timeline established for {driverOutName}'s return to the cockpit.";
            }
            else
            {
                article = $"BREAKING: {driverInName} to replace {driverOutName} at {teamName} for {gpName}\n\n";

                article += $"The driver lineup changes for the {gpName} have been finalized, with {driverInName} confirmed to race for {teamName} this weekend.\n\n";

                article += $"Paddock sources reveal that the situation generated significant interest from multiple drivers, including {playerName}. ";
                article += $"However, {playerTeamName} made it clear they would not release their driver for the opportunity.\n\n";

                article += $"A {playerTeamName} spokesperson stated: \"We have contractual commitments and championship objectives that require our full lineup. ";
                article += $"While we understand the appeal of such opportunities, our focus remains on our own team's performance and goals.\"\n\n";

                article += $"{teamName} subsequently moved forward with {driverInName}, who will bring their talents to the team for this race. ";
                article += $"\"We respect the position of other teams regarding their drivers,\" a {teamName} representative noted. ";
                article += $"\"We're pleased to have {driverInName} join us for this weekend.\"\n\n";

                article += $"The decision underscores the complex dynamics of driver movements in modern motorsport, where team priorities and contractual obligations often take precedence.";
            }

            ArticleText.Text = article;
        }

        private void GenerateAcceptedArticle(string driverOutName, string teamName, string gpName, string playerName, string playerTeamName, bool isConsecutiveAbsence)
        {
            string article;

            if (isConsecutiveAbsence)
            {
                article = $"{playerName} will race with {teamName}!\n\n";

                article += $"In what has become an evolving situation, {playerName} will fill {driverOutName}'s vacant seat at {teamName}.\n\n";

                article += $"This will allow {playerName} to develop genuine familiarity with {teamName}'s machinery, procedures, and personnel.\n\n";

                if(!string.IsNullOrEmpty(playerTeamName))
                {
                    article += $"{playerTeamName} continues to support the arrangement: \"We've maintained our flexible approach as this situation has developed. ";
                    article += $"Our substitute has performed admirably and we're managing our own lineup effectively.\"\n\n";
                }

                article += $"The motorsport community is buzzing with anticipation to see how {playerName} performs in this environment. ";
                article += $"For now, {playerName} focuses on maximizing this unusual opportunity to demonstrate their capabilities.";
            }
            else
            {
                article = $"SHOCK MOVE: {playerName} to race for {teamName} at {gpName}!\n\n";

                article += $"In one of the most surprising developments of the season, {playerName} will step into {teamName}'s car this weekend, ";
                article += $"replacing {driverOutName} who will not compete in the upcoming race.\n\n";

                article += $"The announcement sent shockwaves through the paddock as {playerName} secures a dramatic opportunity to race for one of the grid's teams. ";
                article += $"\"{playerName} brings exceptional talent and we're confident in this decision,\" said a {teamName} team principal. ";
                article += $"\"This is a fantastic opportunity for everyone involved.\"\n\n";

                if (!string.IsNullOrEmpty(playerTeamName))
                {
                    article += $"{playerTeamName} released a statement confirming the temporary arrangement: \"We've agreed to release {playerName} for this race. ";
                    article += $"It's an exciting opportunity and we wish them well. We've secured a capable substitute to fill our seat for the weekend.\"\n\n";
                }

                article += $"The motorsport community is buzzing with anticipation to see how {playerName} performs in their new environment. ";
                article += $"This unexpected twist adds a fascinating storyline to the {gpName} weekend.\n\n";

                article += $"All eyes will be on {playerName} as they prepare for what could be a career-defining race.";
            }

            ArticleText.Text = article;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            PlayerWantsToApply = true;
            this.DialogResult = true;
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            PlayerWantsToApply = false;
            this.DialogResult = true;
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}