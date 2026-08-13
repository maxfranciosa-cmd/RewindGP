using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Extensions;
using AMS2ChEd.Resources;
using System;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.Views
{
    public partial class RetirementNewsWindow : Window
    {
        public RetirementNewsWindow(ISaveGame saveGame, IDriverData retiredDriver, string lastTeamId)
        {
            InitializeComponent();

            DateText.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");

            if (!string.IsNullOrEmpty(retiredDriver.PictureUrl))
            {
                DriverPortraitImage.LoadPhoto(retiredDriver.PictureUrl);
            }

            string teamName = GetTeamName(saveGame, lastTeamId);
            int finalYear = saveGame.CurrentSeason.Year;
            int age = finalYear - retiredDriver.YearOfBirth;

            // The season that just ended hasn't been folded into HistoricalDriverStandings yet
            // (that happens later, in EndOfSeasonManager.StartNewSeason) - so a title clinched in
            // this final season needs to be passed in explicitly, the same way
            // ChampionshipCelebrationWindow does for the reigning champion.
            int? justClinchedChampionshipYear = saveGame.CurrentDriverStandings
                .FirstOrDefault(s => s.DriverId == retiredDriver.DriverId)?.Position == 1
                ? finalYear
                : (int?)null;

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, retiredDriver.DriverId, justClinchedChampionshipYear);

            HeadlineText.Text = BuildHeadline(retiredDriver.Name, retiredDriver.Reputation, accolades);

            GenerateArticle(saveGame, retiredDriver, lastTeamId, teamName, finalYear, age, accolades);
        }

        private void GenerateArticle(
            ISaveGame saveGame,
            IDriverData driver,
            string lastTeamId,
            string teamName,
            int finalYear,
            int age,
            AccoladeSummary accolades)
        {
            string name = driver.Name;

            string article = "";

            article += GenerateOpeningParagraph(name, teamName, finalYear, age);
            article += "\n\n";

            article += GenerateReputationNarrative(name, teamName, driver.Reputation);
            article += "\n\n";

            article += GenerateAccoladesParagraph(name, accolades);
            article += "\n\n";

            string careerRecap = GenerateCareerRecapParagraph(saveGame, driver.DriverId, lastTeamId, finalYear, accolades);
            if (!string.IsNullOrEmpty(careerRecap))
            {
                article += careerRecap;
                article += "\n\n";
            }

            article += GenerateClosingParagraph(name, teamName);

            ArticleText.Text = article;
        }

        /// <summary>
        /// Chronological list of distinct team stints (collapsing consecutive years at the same
        /// team) this driver had in the save, drawn from HistoricalDriverStandings plus the season
        /// that just ended (not yet archived into history at this point in the off-season flow).
        /// Team names use the most recent name on record for each team_id, since a team's display
        /// name can change across years (e.g. sponsor changes) while the id stays stable.
        /// </summary>
        private (int seasons, List<string> teamNames) GetCareerRecap(ISaveGame saveGame, string driverId, string finalSeasonTeamId, int finalYear)
        {
            var mostRecentTeamName = new Dictionary<string, (int year, string name)>();

            void ConsiderName(string teamId, string teamName, int year)
            {
                if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(teamName)) return;
                if (!mostRecentTeamName.TryGetValue(teamId, out var existing) || year >= existing.year)
                {
                    mostRecentTeamName[teamId] = (year, teamName);
                }
            }

            var yearlyTeamIds = new List<(int Year, string TeamId)>();

            foreach (var h in saveGame.HistoricalDriverStandings.OrderBy(h => h.Year))
            {
                foreach (var entry in h.Standing)
                {
                    ConsiderName(entry.TeamId, entry.TeamName, h.Year);
                    if (entry.DriverId == driverId)
                    {
                        yearlyTeamIds.Add((h.Year, entry.TeamId));
                    }
                }
            }

            // the season that just ended is the freshest source for team names, and isn't in
            // HistoricalDriverStandings yet
            foreach (var team in saveGame.CurrentSeason.Teams)
            {
                ConsiderName(team.TeamId, team.TeamName, finalYear);
            }

            if (!string.IsNullOrEmpty(finalSeasonTeamId))
            {
                yearlyTeamIds.Add((finalYear, finalSeasonTeamId));
            }

            var teamStints = new List<string>();
            string lastTeamId = null;
            foreach (var (_, teamId) in yearlyTeamIds.OrderBy(t => t.Year))
            {
                if (string.IsNullOrEmpty(teamId) || teamId == lastTeamId) continue;
                teamStints.Add(mostRecentTeamName.TryGetValue(teamId, out var info) ? info.name : teamId);
                lastTeamId = teamId;
            }

            int seasons = yearlyTeamIds.Select(t => t.Year).Distinct().Count();

            return (seasons, teamStints);
        }

        private string BuildHeadline(string name, DriverReputation reputation, AccoladeSummary accolades)
        {
            var random = new Random();
            string upperName = name.ToUpper();

            if (accolades.Championships > 0)
            {
                var championVariants = new[]
                {
                    Strings.RetirementNewsWindow_Headline_Champion1,
                    Strings.RetirementNewsWindow_Headline_Champion2,
                    Strings.RetirementNewsWindow_Headline_Champion3
                };
                return string.Format(championVariants[random.Next(championVariants.Length)], upperName);
            }

            if (reputation == DriverReputation.JUST_ONE_LAST_DANCE)
            {
                var expectedVariants = new[]
                {
                    Strings.RetirementNewsWindow_Headline_LastDance1,
                    Strings.RetirementNewsWindow_Headline_LastDance2,
                    Strings.RetirementNewsWindow_Headline_LastDance3
                };
                return string.Format(expectedVariants[random.Next(expectedVariants.Length)], upperName);
            }

            var standardVariants = new[]
            {
                Strings.RetirementNewsWindow_Headline_Standard1,
                Strings.RetirementNewsWindow_Headline_Standard2,
                Strings.RetirementNewsWindow_Headline_Standard3
            };
            return string.Format(standardVariants[random.Next(standardVariants.Length)], upperName);
        }

        // Argument order standardized as (name, teamName, year, age) regardless of English word
        // order, so each language's resx template can place {0}-{3} wherever its own phrasing needs.
        private string GenerateOpeningParagraph(string name, string teamName, int year, int age)
        {
            var random = new Random();
            var variants = new[]
            {
                Strings.RetirementNewsWindow_Opening1,
                Strings.RetirementNewsWindow_Opening2,
                Strings.RetirementNewsWindow_Opening3,
                Strings.RetirementNewsWindow_Opening4
            };
            return string.Format(variants[random.Next(variants.Length)], name, teamName, year, age);
        }

        private string GenerateReputationNarrative(string name, string teamName, DriverReputation reputation)
        {
            var random = new Random();
            string[] variants;

            switch (reputation)
            {
                case DriverReputation.PAY_DRIVER_WILD_CARD:
                case DriverReputation.PAY_DRIVER_SEASON:
                    variants = new[] { Strings.RetirementNewsWindow_PayDriver1, Strings.RetirementNewsWindow_PayDriver2, Strings.RetirementNewsWindow_PayDriver3 };
                    break;

                case DriverReputation.YOUNG_TALENT:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    variants = new[] { Strings.RetirementNewsWindow_Young1, Strings.RetirementNewsWindow_Young2, Strings.RetirementNewsWindow_Young3 };
                    break;

                case DriverReputation.PRIME_MIDFIELD:
                case DriverReputation.PRIME_STRONG_MIDFIELD:
                    variants = new[] { Strings.RetirementNewsWindow_PrimeMidfield1, Strings.RetirementNewsWindow_PrimeMidfield2, Strings.RetirementNewsWindow_PrimeMidfield3 };
                    break;

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                    variants = new[] { Strings.RetirementNewsWindow_PrimeChamp1, Strings.RetirementNewsWindow_PrimeChamp2, Strings.RetirementNewsWindow_PrimeChamp3 };
                    break;

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                    variants = new[] { Strings.RetirementNewsWindow_Washed1, Strings.RetirementNewsWindow_Washed2, Strings.RetirementNewsWindow_Washed3 };
                    break;

                case DriverReputation.AGEING_MIDFIELD:
                case DriverReputation.AGEING_STRONG_MIDFIELD:
                    variants = new[] { Strings.RetirementNewsWindow_AgeingMidfield1, Strings.RetirementNewsWindow_AgeingMidfield2, Strings.RetirementNewsWindow_AgeingMidfield3 };
                    break;

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                    variants = new[] { Strings.RetirementNewsWindow_AgeingChamp1, Strings.RetirementNewsWindow_AgeingChamp2, Strings.RetirementNewsWindow_AgeingChamp3 };
                    break;

                case DriverReputation.JUST_ONE_LAST_DANCE:
                    variants = new[] { Strings.RetirementNewsWindow_LastDanceNarrative1, Strings.RetirementNewsWindow_LastDanceNarrative2, Strings.RetirementNewsWindow_LastDanceNarrative3 };
                    break;

                default:
                    variants = new[] { Strings.RetirementNewsWindow_DefaultNarrative1, Strings.RetirementNewsWindow_DefaultNarrative2, Strings.RetirementNewsWindow_DefaultNarrative3 };
                    break;
            }

            return string.Format(variants[random.Next(variants.Length)], name, teamName);
        }

        private string GenerateAccoladesParagraph(string name, AccoladeSummary accolades)
        {
            var random = new Random();

            if (accolades.Championships == 0 && accolades.Wins == 0)
            {
                var noWinVariants = new[]
                {
                    Strings.RetirementNewsWindow_NoWin1,
                    Strings.RetirementNewsWindow_NoWin2,
                    Strings.RetirementNewsWindow_NoWin3
                };
                return string.Format(noWinVariants[random.Next(noWinVariants.Length)], name);
            }

            string careerSpanPhrase = accolades.HasBaseline
                ? Strings.RetirementNewsWindow_CareerSpan_Baseline
                : string.Format(Strings.RetirementNewsWindow_CareerSpan_SinceYear_Format, accolades.StartYear);

            string championshipPhrase = accolades.Championships > 0
                ? string.Format(
                    accolades.Championships > 1 ? Strings.RetirementNewsWindow_ChampionshipPhrase_Plural : Strings.RetirementNewsWindow_ChampionshipPhrase_Singular,
                    accolades.Championships, string.Join(", ", accolades.ChampionshipYears))
                : "";

            string winWord = accolades.Wins != 1 ? Strings.RetirementNewsWindow_WinWord_Plural : Strings.RetirementNewsWindow_WinWord_Singular;
            string podiumWord = accolades.Podiums != 1 ? Strings.RetirementNewsWindow_PodiumWord_Plural : Strings.RetirementNewsWindow_PodiumWord_Singular;
            string poleWord = accolades.PolePositions != 1 ? Strings.RetirementNewsWindow_PoleWord_Plural : Strings.RetirementNewsWindow_PoleWord_Singular;

            var tallyVariants = new[]
            {
                Strings.RetirementNewsWindow_Tally1,
                Strings.RetirementNewsWindow_Tally2,
                Strings.RetirementNewsWindow_Tally3
            };

            return string.Format(tallyVariants[random.Next(tallyVariants.Length)],
                name, championshipPhrase, accolades.Wins, winWord, accolades.Podiums, podiumWord, accolades.PolePositions, poleWord, careerSpanPhrase);
        }

        private string GenerateCareerRecapParagraph(ISaveGame saveGame, string driverId, string lastTeamId, int finalYear, AccoladeSummary accolades)
        {
            var (seasons, teamNames) = GetCareerRecap(saveGame, driverId, lastTeamId, finalYear);
            if (teamNames.Count == 0) return "";

            var random = new Random();
            string teamListPhrase = FormatTeamList(teamNames);
            string seasonsPhrase = seasons == 1
                ? Strings.RetirementNewsWindow_SeasonsPhrase_Singular
                : string.Format(Strings.RetirementNewsWindow_SeasonsPhrase_Plural_Format, seasons);

            // AccoladesAtStart can carry a pre-save baseline for this driver, meaning their real
            // career may stretch back further than this save's own recorded history - so the team
            // list here only covers "the last N seasons", not necessarily their whole career.
            string[] variants = accolades.HasBaseline
                ? new[] { Strings.RetirementNewsWindow_CareerRecapBaseline1, Strings.RetirementNewsWindow_CareerRecapBaseline2, Strings.RetirementNewsWindow_CareerRecapBaseline3 }
                : new[] { Strings.RetirementNewsWindow_CareerRecapFull1, Strings.RetirementNewsWindow_CareerRecapFull2, Strings.RetirementNewsWindow_CareerRecapFull3 };

            return string.Format(variants[random.Next(variants.Length)], seasonsPhrase, teamListPhrase);
        }

        private string FormatTeamList(List<string> teamNames)
        {
            if (teamNames.Count == 1) return teamNames[0];
            if (teamNames.Count == 2) return string.Format(Strings.RetirementNewsWindow_TeamList_TwoFormat, teamNames[0], teamNames[1]);
            return string.Format(Strings.RetirementNewsWindow_TeamList_ManyFormat, string.Join(", ", teamNames.Take(teamNames.Count - 1)), teamNames[^1]);
        }

        // Argument order standardized as (teamName, name).
        private string GenerateClosingParagraph(string name, string teamName)
        {
            var random = new Random();
            var variants = new[]
            {
                Strings.RetirementNewsWindow_Closing1,
                Strings.RetirementNewsWindow_Closing2,
                Strings.RetirementNewsWindow_Closing3
            };
            return string.Format(variants[random.Next(variants.Length)], teamName, name);
        }

        private string GetTeamName(ISaveGame saveGame, string teamId)
        {
            if (string.IsNullOrEmpty(teamId)) return Strings.RetirementNewsWindow_DefaultTeamName;

            var team = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            return team?.TeamName ?? Strings.RetirementNewsWindow_DefaultTeamName;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
