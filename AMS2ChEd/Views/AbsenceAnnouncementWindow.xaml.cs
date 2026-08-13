using AMS2ChEd.Business.Models;
using AMS2ChEd.Resources;
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
            HeadlineText.Text = string.Format(Strings.AbsenceAnnouncementWindow_Headline_Main_Format,
                driverOutName.ToUpper(), teamName.ToUpper(), gpName.ToUpper());

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
                ? string.Format(Strings.AbsenceAnnouncementWindow_Headline_Refused_Consecutive_Format, driverOutName.ToUpper(), driverInName.ToUpper())
                : string.Format(Strings.AbsenceAnnouncementWindow_Headline_Refused_OneOff_Format, driverInName.ToUpper(), teamName.ToUpper());
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
                ? string.Format(Strings.AbsenceAnnouncementWindow_Headline_TeamRefused_Consecutive_Format, driverOutName.ToUpper())
                : string.Format(Strings.AbsenceAnnouncementWindow_Headline_TeamRefused_OneOff_Format, playerTeamName.ToUpper());
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
            window.HeadlineText.Text = string.Format(Strings.AbsenceAnnouncementWindow_Headline_Accepted_Format, playerName.ToUpper(), teamName.ToUpper());
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
                article = string.Format(Strings.AbsenceAnnouncementWindow_Article_Consecutive_Opening_Format, driverOutName, teamName, gpName) + "\n\n";

                article += string.Format(Strings.AbsenceAnnouncementWindow_Article_Consecutive_Discussion_Format, teamName) + "\n\n";

                article += !string.IsNullOrEmpty(driverInName)
                    ? string.Format(Strings.AbsenceAnnouncementWindow_Article_Consecutive_WithReplacement_Format, driverInName, teamName) + "\n\n"
                    : string.Format(Strings.AbsenceAnnouncementWindow_Article_Consecutive_NoReplacement_Format, teamName) + "\n\n";

                article += Strings.AbsenceAnnouncementWindow_Article_Consecutive_Ongoing + "\n\n";

                article += string.Format(Strings.AbsenceAnnouncementWindow_Article_Consecutive_Closing_Format, teamName);
            }
            else
            {
                article = string.Format(Strings.AbsenceAnnouncementWindow_Article_OneOff_Opening_Format, gpName, driverOutName, teamName) + "\n\n";

                article += Strings.AbsenceAnnouncementWindow_Article_OneOff_Announcement + "\n\n";

                article += !string.IsNullOrEmpty(driverInName)
                    ? string.Format(Strings.AbsenceAnnouncementWindow_Article_OneOff_WithReplacement_Format, driverInName) + "\n\n"
                    : Strings.AbsenceAnnouncementWindow_Article_OneOff_NoReplacement + "\n\n";

                article += string.Format(Strings.AbsenceAnnouncementWindow_Article_OneOff_Ramifications_Format, gpName, teamName) + "\n\n";

                article += Strings.AbsenceAnnouncementWindow_Article_OneOff_Closing;
            }

            ArticleText.Text = article;
        }

        // Argument order standardized as (driverOutName, teamName, gpName, driverInName) across
        // both branches, regardless of which placeholders a given language's template actually uses.
        private void GenerateRefusedArticle(string driverOutName, string teamName, string gpName, string driverInName, string playerTeamName, bool isConsecutiveAbsence)
        {
            string template = isConsecutiveAbsence
                ? Strings.AbsenceAnnouncementWindow_Refused_Consecutive_Format
                : Strings.AbsenceAnnouncementWindow_Refused_OneOff_Format;

            ArticleText.Text = string.Format(template, driverOutName, teamName, gpName, driverInName);
        }

        // Argument order standardized as (driverOutName, teamName, gpName, driverInName, playerName, playerTeamName).
        private void GenerateTeamRefusedArticle(string driverOutName, string teamName, string gpName, string driverInName, string playerName, string playerTeamName, bool isConsecutiveAbsence)
        {
            string template = isConsecutiveAbsence
                ? Strings.AbsenceAnnouncementWindow_TeamRefused_Consecutive_Format
                : Strings.AbsenceAnnouncementWindow_TeamRefused_OneOff_Format;

            ArticleText.Text = string.Format(template, driverOutName, teamName, gpName, driverInName, playerName, playerTeamName);
        }

        private void GenerateAcceptedArticle(string driverOutName, string teamName, string gpName, string playerName, string playerTeamName, bool isConsecutiveAbsence)
        {
            string article;

            if (isConsecutiveAbsence)
            {
                article = string.Format(Strings.AbsenceAnnouncementWindow_Accepted_Consecutive_Opening_Format, playerName, teamName) + "\n\n";

                article += string.Format(Strings.AbsenceAnnouncementWindow_Accepted_Consecutive_Intro_Format, playerName, driverOutName, teamName) + "\n\n";

                article += string.Format(Strings.AbsenceAnnouncementWindow_Accepted_Consecutive_Familiarity_Format, playerName, teamName) + "\n\n";

                if (!string.IsNullOrEmpty(playerTeamName))
                {
                    article += string.Format(Strings.AbsenceAnnouncementWindow_Accepted_Consecutive_TeamQuote_Format, playerTeamName) + "\n\n";
                }

                article += string.Format(Strings.AbsenceAnnouncementWindow_Accepted_Consecutive_Closing_Format, playerName);
            }
            else
            {
                article = string.Format(Strings.AbsenceAnnouncementWindow_Accepted_OneOff_Opening_Format, playerName, teamName, gpName) + "\n\n";

                article += string.Format(Strings.AbsenceAnnouncementWindow_Accepted_OneOff_Announcement_Format, playerName, teamName, driverOutName) + "\n\n";

                article += string.Format(Strings.AbsenceAnnouncementWindow_Accepted_OneOff_Shockwaves_Format, playerName, teamName) + "\n\n";

                if (!string.IsNullOrEmpty(playerTeamName))
                {
                    article += string.Format(Strings.AbsenceAnnouncementWindow_Accepted_OneOff_TeamQuote_Format, playerTeamName, playerName) + "\n\n";
                }

                article += string.Format(Strings.AbsenceAnnouncementWindow_Accepted_OneOff_Anticipation_Format, playerName, gpName) + "\n\n";

                article += string.Format(Strings.AbsenceAnnouncementWindow_Accepted_OneOff_Closing_Format, playerName);
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