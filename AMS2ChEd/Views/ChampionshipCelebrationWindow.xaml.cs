using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Extensions;
using AMS2ChEd.Localization;
using AMS2ChEd.Resources;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AMS2ChEd.Views
{
    public partial class ChampionshipCelebrationWindow : Window
    {
        public ChampionshipCelebrationWindow(ISaveGame saveGame)
        {
            InitializeComponent();

            // Set the date
            DateText.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");

            // Get the champion
            var champion = saveGame.CurrentDriverStandings
                .OrderBy(s => s.Position)
                .FirstOrDefault();

            if (champion == null)
            {
                HeadlineText.Text = Strings.ChampionshipCelebrationWindow_SeasonComplete_Headline;
                ArticleText.Text = Strings.ChampionshipCelebrationWindow_SeasonComplete_Article;
                return;
            }

            string championName = GetDriverName(saveGame, champion.DriverId);
            string championTeam = GetTeamName(saveGame, champion.TeamId);
            var championReputation = GetDriverReputation(saveGame, champion.DriverId);

            var championDriverData = saveGame.Drivers.FirstOrDefault(d => d.DriverId == champion.DriverId);
            var championPhoto = championDriverData?.PictureUrl;

            // Load driver portrait if provided
            if (!string.IsNullOrEmpty(championPhoto))
            {
                DriverPortraitImage.LoadPhoto(championPhoto);
            }

            var championDriverAccolades = AccoladesCalculator.GetDriverAccolades(saveGame, champion.DriverId, saveGame.CurrentSeason.Year);
            var championTeamAccolades = AccoladesCalculator.GetTeamAccolades(saveGame, champion.TeamId, saveGame.CurrentSeason.Year);
            bool isMaidenTitle = championDriverAccolades.HasBaseline && championDriverAccolades.Championships == 1;

            // Set the headline
            HeadlineText.Text = BuildHeadline(championName, saveGame.CurrentSeason.Year, isMaidenTitle);

            // Generate the article
            GenerateChampionshipArticle(saveGame, championName, championTeam, championReputation, champion.Points,
                championDriverAccolades, championTeamAccolades, isMaidenTitle);
        }

        private string BuildHeadline(string championName, int year, bool isMaidenTitle)
        {
            string upperName = championName.ToUpper();

            if (!isMaidenTitle)
                return string.Format(Strings.ChampionshipCelebrationWindow_Headline_Standard_Format, upperName, year);

            var random = new Random();
            var maidenTitleHeadlines = new[]
            {
                Strings.ChampionshipCelebrationWindow_Headline_Maiden1_Format,
                Strings.ChampionshipCelebrationWindow_Headline_Maiden2_Format
            };
            return string.Format(maidenTitleHeadlines[random.Next(maidenTitleHeadlines.Length)], upperName);
        }

        private void GenerateChampionshipArticle(
            ISaveGame saveGame,
            string championName,
            string championTeam,
            DriverReputation championReputation,
            double championPoints,
            AccoladeSummary championDriverAccolades,
            AccoladeSummary championTeamAccolades,
            bool isMaidenTitle)
        {
            string article = "";

            // Opening paragraph
            article += string.Format(Strings.ChampionshipCelebrationWindow_Opening_Format,
                saveGame.CurrentSeason.Year, championName, championTeam, championPoints);
            article += "\n\n";

            // Championship journey based on reputation
            article += GenerateChampionshipJourney(championName, championTeam, championReputation);
            article += "\n\n";

            // Career milestone (title tally for the driver and constructor)
            article += GenerateChampionshipMilestoneParagraph(championName, championTeam, championDriverAccolades, championTeamAccolades, isMaidenTitle);
            article += "\n\n";

            // Final standings
            article += Strings.ChampionshipCelebrationWindow_StandingsHeader;
            article += "\n\n";
            var topFive = saveGame.CurrentDriverStandings
                .OrderBy(s => s.Position)
                .Take(5);

            foreach (var standing in topFive)
            {
                string driverName = GetDriverName(saveGame, standing.DriverId);
                string pointsWord = standing.Points != 1
                    ? Strings.ChampionshipCelebrationWindow_StandingsPointsWord_Plural
                    : Strings.ChampionshipCelebrationWindow_StandingsPointsWord_Singular;
                article += string.Format(Strings.ChampionshipCelebrationWindow_StandingsLine_Format,
                    standing.Position, driverName, standing.Points, pointsWord);
                article += "\n";
            }

            article += "\n";
            article += Strings.ChampionshipCelebrationWindow_Closing;

            ArticleText.Text = article;
        }

        // Argument order standardized as (championName, championTeam) regardless of whether a
        // given case's English prose actually uses the team name, so every language's resx
        // template can freely reorder or drop placeholders.
        private string GenerateChampionshipJourney(
            string championName,
            string championTeam,
            DriverReputation reputation)
        {
            string template = reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD or DriverReputation.PAY_DRIVER_SEASON =>
                    Strings.ChampionshipCelebrationWindow_Journey_PayDriver_Format,

                DriverReputation.YOUNG_TALENT or DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    Strings.ChampionshipCelebrationWindow_Journey_YoungTalent_Format,

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    Strings.ChampionshipCelebrationWindow_Journey_YoungChampionshipLevel_Format,

                DriverReputation.PRIME_MIDFIELD or DriverReputation.PRIME_STRONG_MIDFIELD =>
                    Strings.ChampionshipCelebrationWindow_Journey_PrimeMidfield_Format,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    Strings.ChampionshipCelebrationWindow_Journey_PrimeChampionshipUnproven_Format,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    Strings.ChampionshipCelebrationWindow_Journey_PrimeChampionship_Format,

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.ChampionshipCelebrationWindow_Journey_PrimeChampionshipWashed_Format,

                DriverReputation.AGEING_MIDFIELD or DriverReputation.AGEING_STRONG_MIDFIELD =>
                    Strings.ChampionshipCelebrationWindow_Journey_AgeingMidfield_Format,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL =>
                    Strings.ChampionshipCelebrationWindow_Journey_AgeingChampionship_Format,

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    Strings.ChampionshipCelebrationWindow_Journey_AgeingChampionshipWashed_Format,

                DriverReputation.JUST_ONE_LAST_DANCE =>
                    Strings.ChampionshipCelebrationWindow_Journey_LastDance_Format,

                _ =>
                    Strings.ChampionshipCelebrationWindow_Journey_Default_Format
            };

            return string.Format(template, championName, championTeam);
        }

        private string GenerateChampionshipMilestoneParagraph(
            string championName,
            string championTeam,
            AccoladeSummary driverAccolades,
            AccoladeSummary teamAccolades,
            bool isMaidenTitle)
        {
            var random = new Random();

            if (isMaidenTitle)
            {
                var maidenTitleVariants = new[]
                {
                    Strings.ChampionshipCelebrationWindow_Milestone_Maiden1_Format,
                    Strings.ChampionshipCelebrationWindow_Milestone_Maiden2_Format,
                    Strings.ChampionshipCelebrationWindow_Milestone_Maiden3_Format
                };
                return string.Format(maidenTitleVariants[random.Next(maidenTitleVariants.Length)], championName);
            }

            string driverTitlePhrase = driverAccolades.HasBaseline
                ? string.Format(Strings.ChampionshipCelebrationWindow_Milestone_DriverTitlePhrase_Baseline_Format, OrdinalFormatter.Format(driverAccolades.Championships))
                : string.Format(Strings.ChampionshipCelebrationWindow_Milestone_DriverTitlePhrase_SinceYear_Format, OrdinalFormatter.Format(driverAccolades.Championships), driverAccolades.StartYear);

            string teamTitlePhrase = teamAccolades.HasBaseline
                ? string.Format(Strings.ChampionshipCelebrationWindow_Milestone_TeamTitlePhrase_Baseline_Format, OrdinalFormatter.Format(teamAccolades.Championships))
                : string.Format(Strings.ChampionshipCelebrationWindow_Milestone_TeamTitlePhrase_SinceYear_Format, OrdinalFormatter.Format(teamAccolades.Championships), teamAccolades.StartYear);

            string driverPriorYears = driverAccolades.ChampionshipYears.Count > 1
                ? string.Format(Strings.ChampionshipCelebrationWindow_Milestone_PriorYears_Format,
                    string.Join(", ", driverAccolades.ChampionshipYears.Where(y => y != driverAccolades.ChampionshipYears.Max())))
                : "";

            string championshipWord = driverAccolades.Championships != 1
                ? Strings.ChampionshipCelebrationWindow_Milestone_ChampionshipWord_Plural
                : Strings.ChampionshipCelebrationWindow_Milestone_ChampionshipWord_Singular;

            var milestoneVariants = new[]
            {
                string.Format(Strings.ChampionshipCelebrationWindow_Milestone_Variant1_Format,
                    championName, driverTitlePhrase, driverPriorYears, championTeam, teamTitlePhrase),
                string.Format(Strings.ChampionshipCelebrationWindow_Milestone_Variant2_Format,
                    championName, driverAccolades.Championships, championshipWord, championTeam, teamTitlePhrase)
            };

            return milestoneVariants[random.Next(milestoneVariants.Length)];
        }

        private string GetDriverName(ISaveGame saveGame, string driverId)
        {
            if (driverId == saveGame.PlayerData.DriverId)
                return saveGame.PlayerData.Name;

            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);
            return driver?.Name ?? Strings.ChampionshipCelebrationWindow_UnknownDriver;
        }

        private string GetTeamName(ISaveGame saveGame, string teamId)
        {
            var team = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            return team?.TeamName ?? Strings.ChampionshipCelebrationWindow_UnknownTeam;
        }

        private DriverReputation GetDriverReputation(ISaveGame saveGame, string driverId)
        {
            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);
            if (driver != null)
            {
                return driver.Reputation;
            }

            return DriverReputation.PRIME_MIDFIELD;
        }

        private void ProgressButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}