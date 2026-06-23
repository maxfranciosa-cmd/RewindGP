using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Extensions;
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
                HeadlineText.Text = "SEASON COMPLETE";
                ArticleText.Text = "The season has concluded.";
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

            // Set the headline
            HeadlineText.Text = $"{championName.ToUpper()} CLAIMS {saveGame.CurrentSeason.Year} WORLD CHAMPIONSHIP";

            // Generate the article
            GenerateChampionshipArticle(saveGame, championName, championTeam, championReputation, champion.Points);
        }

        private void GenerateChampionshipArticle(
            ISaveGame saveGame,
            string championName,
            string championTeam,
            DriverReputation championReputation,
            double championPoints)
        {
            string article = "";

            // Opening paragraph
            article += $"The {saveGame.CurrentSeason.Year} championship has reached its thrilling conclusion, with {championName} " +
                      $"claiming the world title for {championTeam}. ";
            article += $"With a final tally of {championPoints} points, {championName} has secured motorsport's ultimate prize.\n\n";

            // Championship journey based on reputation
            article += GenerateChampionshipJourney(championName, championTeam, championReputation);
            article += "\n\n";

            // Final standings
            article += "FINAL CHAMPIONSHIP STANDINGS (TOP 5):\n\n";
            var topFive = saveGame.CurrentDriverStandings
                .OrderBy(s => s.Position)
                .Take(5);

            foreach (var standing in topFive)
            {
                string driverName = GetDriverName(saveGame, standing.DriverId);
                article += $"{standing.Position}. {driverName} - {standing.Points} points\n";
            }

            article += "\n";
            article += $"As the champagne dries and the celebrations continue, attention now turns to the future. " +
                      $"Contract negotiations begin in earnest as teams and drivers prepare for the next chapter in this " +
                      $"ever-evolving sport.";

            ArticleText.Text = article;
        }

        private string GenerateChampionshipJourney(
            string championName,
            string championTeam,
            DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD or DriverReputation.PAY_DRIVER_SEASON =>
                    $"In what can only be described as the ultimate underdog story, {championName} silenced every critic " +
                    $"who doubted their credentials. What began as questions about their funding has ended with their name " +
                    $"etched in motorsport history. This championship proves that determination and talent can overcome any narrative.",

                DriverReputation.YOUNG_TALENT or DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    $"The young sensation has announced their arrival on the world stage in emphatic fashion. {championName}'s " +
                    $"championship victory represents not just personal triumph, but signals a changing of the guard in motorsport. " +
                    $"The future arrived early, and it drives for {championTeam}.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    $"From prodigy to champion - {championName}'s coronation was inevitable, but no less spectacular. " +
                    $"This championship confirms what many have known for years: we are witnessing the emergence of a generational talent. " +
                    $"The question now is not whether they can win, but how many more titles will follow.",

                DriverReputation.PRIME_MIDFIELD or DriverReputation.PRIME_STRONG_MIDFIELD =>
                    $"Years of consistent performances have culminated in this ultimate reward. {championName} proved that " +
                    $"patience, dedication, and unwavering belief can overcome the odds. This championship validates a career " +
                    $"built on solid foundations and represents the perfect union of driver and machine.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    $"The monkey is finally off {championName}'s back. After years of 'what ifs' and near-misses, they have " +
                    $"delivered when it mattered most. This championship transforms them from talented contender to proven champion, " +
                    $"and their partnership with {championTeam} has been the key to unlocking their full potential.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    $"Another title to add to an already glittering career. {championName} continues to demonstrate why they " +
                    $"rank among the all-time greats. This championship, secured with {championTeam}, reinforces their status " +
                    $"as the standard-bearer of their generation. Excellence, it seems, never goes out of style.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    $"Written off by many as past their prime, {championName} has authored the comeback story of the decade. " +
                    $"This championship proves that class is permanent and that reports of their decline were greatly exaggerated. " +
                    $"In partnership with {championTeam}, they have reclaimed their place at the pinnacle of the sport.",

                DriverReputation.AGEING_MIDFIELD or DriverReputation.AGEING_STRONG_MIDFIELD =>
                    $"Experience triumphed over youth in a season that will be remembered for years to come. {championName} " +
                    $"demonstrated that racecraft and consistency can overcome raw speed. This late-career championship adds a " +
                    $"fairy-tale ending to a story of perseverance and dedication.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL =>
                    $"The veteran champion has proven they still have what it takes. {championName}'s latest title showcases " +
                    $"a driver at the peak of their powers, combining years of experience with the fire of youth. Age, it seems, " +
                    $"is indeed just a number when you have the skill set of a true champion.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    $"From the brink of retirement to world champion - {championName}'s journey this season has been nothing " +
                    $"short of miraculous. This championship represents redemption, resilience, and a refusal to accept limitations. " +
                    $"It stands as a testament to the human spirit and the enduring power of self-belief.",

                DriverReputation.JUST_ONE_LAST_DANCE =>
                    $"In what may be the greatest final act in motorsport history, {championName} has defied time itself. " +
                    $"Doubted, dismissed, and written off as past their sell-by date, they have silenced every critic with " +
                    $"a championship performance that will echo through the ages. This isn't just a title - it's the perfect " +
                    $"ending to a legendary career, a reminder that greatness knows no expiration date.",

                _ =>
                    $"Through a season of highs and lows, {championName} maintained their focus and delivered when it mattered. " +
                    $"This championship with {championTeam} is the culmination of a year-long battle and represents the perfect " +
                    $"harmony between driver ambition and team execution."
            };
        }

        private string GetDriverName(ISaveGame saveGame, string driverId)
        {
            if (driverId == saveGame.PlayerData.DriverId)
                return saveGame.PlayerData.Name;

            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);
            return driver?.Name ?? "Unknown Driver";
        }

        private string GetTeamName(ISaveGame saveGame, string teamId)
        {
            var team = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            return team?.TeamName ?? "Unknown Team";
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