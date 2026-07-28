using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Extensions;
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
                    $"{upperName} HANGS UP THE HELMET AFTER TITLE-WINNING CAREER",
                    $"CHAMPION {upperName} CALLS TIME ON RACING CAREER",
                    $"{upperName} RETIRES AS A CHAMPION OF THE SPORT"
                };
                return championVariants[random.Next(championVariants.Length)];
            }

            if (reputation == DriverReputation.JUST_ONE_LAST_DANCE)
            {
                var expectedVariants = new[]
                {
                    $"{upperName} BOWS OUT AFTER FAREWELL SEASON",
                    $"THE FINAL CHEQUERED FLAG FALLS FOR {upperName}",
                    $"{upperName}'S LAST DANCE COMES TO AN END"
                };
                return expectedVariants[random.Next(expectedVariants.Length)];
            }

            var standardVariants = new[]
            {
                $"{upperName} ANNOUNCES RETIREMENT FROM MOTORSPORT",
                $"{upperName} CALLS TIME ON RACING CAREER",
                $"THE CHEQUERED FLAG FALLS ON {upperName}'S CAREER"
            };
            return standardVariants[random.Next(standardVariants.Length)];
        }

        private string GenerateOpeningParagraph(string name, string teamName, int year, int age)
        {
            var random = new Random();
            var variants = new[]
            {
                $"{name} has announced their retirement from motorsport, bringing the curtain down on a career that reaches its conclusion at the end of the {year} season. " +
                $"The {age}-year-old departs {teamName} for the final time, closing a chapter that leaves a lasting mark on the sport.",

                $"After careful consideration, {name} has confirmed they will not return to the grid next season. " +
                $"The announcement ends the {age}-year-old's time with {teamName}, drawing a career to a close following the {year} campaign.",

                $"It's official: {name} is stepping away from competitive racing. The {age}-year-old's final race for {teamName} came at the end of the {year} season, " +
                $"and with it, an accomplished career reaches its natural conclusion.",

                $"The paddock bids farewell to {name}, who has confirmed their retirement following the conclusion of the {year} season. " +
                $"{teamName} will now need to find a replacement for the {age}-year-old, whose career passes into the history books."
            };
            return variants[random.Next(variants.Length)];
        }

        private string GenerateReputationNarrative(string name, string teamName, DriverReputation reputation)
        {
            var random = new Random();

            switch (reputation)
            {
                case DriverReputation.PAY_DRIVER_WILD_CARD:
                case DriverReputation.PAY_DRIVER_SEASON:
                    var payDriverVariants = new[]
                    {
                        $"{name}'s time in the sport was never about outright pace, and that was never really the point. What they brought to {teamName} - and to every garage they passed through - went beyond the stopwatch.",
                        $"Few expected {name} to top many timesheets, but their commitment to the craft earned quiet respect throughout the paddock during their time with {teamName}.",
                        $"{name} made the absolute most of every opportunity afforded to them, and {teamName} - among others - are grateful for it."
                    };
                    return payDriverVariants[random.Next(payDriverVariants.Length)];

                case DriverReputation.YOUNG_TALENT:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    var youngVariants = new[]
                    {
                        $"It's a retirement that catches the paddock off guard - {name} was widely tipped for a long and successful future, and few saw this call coming from {teamName}'s garage.",
                        $"There's a sense of unfinished business to {name}'s departure. The talent that made them one of the grid's brightest prospects never got the chance to fully bloom.",
                        $"{name} leaves with the paddock still wondering what might have been. Potential of this magnitude rarely walks away from the sport this early."
                    };
                    return youngVariants[random.Next(youngVariants.Length)];

                case DriverReputation.PRIME_MIDFIELD:
                case DriverReputation.PRIME_STRONG_MIDFIELD:
                    var primeMidfieldVariants = new[]
                    {
                        $"{name} was never the fastest driver on the grid, but few could match their consistency. That reliability made them a valuable asset to every team they drove for, {teamName} included.",
                        $"A model of dependability throughout their career, {name} built a reputation as a driver teams could count on race after race - a trait that will be missed at {teamName}.",
                        $"{name}'s career was defined by steady, unspectacular excellence - the kind that rarely makes headlines but always earns respect within the paddock."
                    };
                    return primeMidfieldVariants[random.Next(primeMidfieldVariants.Length)];

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                    var primeChampVariants = new[]
                    {
                        $"Perhaps the most striking part of this announcement is the timing: {name} walks away from {teamName} while still firmly among the fastest drivers on the grid.",
                        $"{name} leaves the sport at the peak of their powers, still capable of fighting at the front - a rare case of a driver choosing to go out on their own terms.",
                        $"There will be no gentle fade into the midfield for {name}. They bow out from {teamName} as one of the standard-bearers of their generation, undiminished."
                    };
                    return primeChampVariants[random.Next(primeChampVariants.Length)];

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                    var washedVariants = new[]
                    {
                        $"The {name} of recent seasons was a shadow of the driver who once dominated the sport, but nobody who saw them at their peak will ever forget it.",
                        $"It's a quiet ending to what was once a glittering career. {name}'s best days were behind them by the time they left {teamName}, but the glory years remain untouchable.",
                        $"Time catches up with everyone eventually, and {name} is no exception. Still, the heights they once reached ensure their name will endure long after this final season with {teamName}."
                    };
                    return washedVariants[random.Next(washedVariants.Length)];

                case DriverReputation.AGEING_MIDFIELD:
                case DriverReputation.AGEING_STRONG_MIDFIELD:
                    var ageingMidfieldVariants = new[]
                    {
                        $"{name} leaves the sport as a respected veteran, the kind of driver every team wants in the garage even when the results don't always show it.",
                        $"There's no fanfare to {name}'s exit, just the quiet satisfaction of a long career built on graft and professionalism at {teamName} and beyond.",
                        $"{name} never chased the spotlight, and their retirement is much the same - a low-key end to a career built on hard work rather than headlines."
                    };
                    return ageingMidfieldVariants[random.Next(ageingMidfieldVariants.Length)];

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                    var ageingChampVariants = new[]
                    {
                        $"Few drivers get to leave the sport still competing at the very top, but {name} has managed exactly that, bowing out from {teamName} as one of the grid's elder statesmen.",
                        $"{name} departs with their reputation fully intact - a driver who remained relevant deep into the twilight of their career.",
                        $"There's a rare symmetry to {name}'s career: still winning, still respected, still feared on their way out the door."
                    };
                    return ageingChampVariants[random.Next(ageingChampVariants.Length)];

                case DriverReputation.JUST_ONE_LAST_DANCE:
                    var lastDanceVariants = new[]
                    {
                        $"This retirement comes as no surprise - {name}'s farewell season with {teamName} was always going to be their last, and the paddock made sure to savor every moment of it.",
                        $"Exactly as billed, {name}'s one last dance has reached its final bow. It was a fitting send-off for a career that gave the sport so much.",
                        $"The farewell tour is complete. {name} leaves {teamName} - and the sport - on their own terms, precisely as planned."
                    };
                    return lastDanceVariants[random.Next(lastDanceVariants.Length)];

                default:
                    var defaultVariants = new[]
                    {
                        $"{name}'s career with {teamName} and beyond will be remembered fondly by fans and colleagues alike.",
                        $"It's the end of an era for {name}, whose contribution to the sport goes beyond any single result.",
                        $"{name} leaves the grid having given everything to the sport across a long and varied career."
                    };
                    return defaultVariants[random.Next(defaultVariants.Length)];
            }
        }

        private string GenerateAccoladesParagraph(string name, AccoladeSummary accolades)
        {
            var random = new Random();

            if (accolades.Championships == 0 && accolades.Wins == 0)
            {
                var noWinVariants = new[]
                {
                    $"Though a World Championship - or even a race win - never came {name}'s way, their contribution to the grid over the years will not be forgotten.",
                    $"Victory may have proven elusive across {name}'s career, but their presence on the grid earned respect from teammates and rivals alike.",
                    $"{name} leaves the sport without a race win to their name, but their commitment to the craft over many seasons speaks for itself."
                };
                return noWinVariants[random.Next(noWinVariants.Length)];
            }

            string careerSpanPhrase = accolades.HasBaseline
                ? "career"
                : $"career since {accolades.StartYear} (as recorded)";

            string championshipPhrase = accolades.Championships > 0
                ? $"{accolades.Championships} World Championship{(accolades.Championships > 1 ? "s" : "")} ({string.Join(", ", accolades.ChampionshipYears)}), "
                : "";

            var tallyVariants = new[]
            {
                $"{name} retires with {championshipPhrase}{accolades.Wins} race win{(accolades.Wins != 1 ? "s" : "")}, {accolades.Podiums} podium{(accolades.Podiums != 1 ? "s" : "")}, and {accolades.PolePositions} pole position{(accolades.PolePositions != 1 ? "s" : "")} across their {careerSpanPhrase}.",
                $"The numbers tell their own story: {championshipPhrase}{accolades.Wins} win{(accolades.Wins != 1 ? "s" : "")}, {accolades.Podiums} podium finish{(accolades.Podiums != 1 ? "es" : "")}, and {accolades.PolePositions} pole{(accolades.PolePositions != 1 ? "s" : "")} across a {careerSpanPhrase} that spanned the grid.",
                $"Looking back on a {careerSpanPhrase} that yielded {championshipPhrase}{accolades.Wins} win{(accolades.Wins != 1 ? "s" : "")} and {accolades.Podiums} podium{(accolades.Podiums != 1 ? "s" : "")}, {name} leaves with a record few can match."
            };

            return tallyVariants[random.Next(tallyVariants.Length)];
        }

        private string GenerateCareerRecapParagraph(ISaveGame saveGame, string driverId, string lastTeamId, int finalYear, AccoladeSummary accolades)
        {
            var (seasons, teamNames) = GetCareerRecap(saveGame, driverId, lastTeamId, finalYear);
            if (teamNames.Count == 0) return "";

            var random = new Random();
            string teamListPhrase = FormatTeamList(teamNames);
            string seasonsPhrase = $"{seasons} season{(seasons != 1 ? "s" : "")}";

            // AccoladesAtStart can carry a pre-save baseline for this driver, meaning their real
            // career may stretch back further than this save's own recorded history - so the team
            // list here only covers "the last N seasons", not necessarily their whole career.
            if (accolades.HasBaseline)
            {
                var variants = new[]
                {
                    $"In the last {seasonsPhrase} of a longer career, they raced for {teamListPhrase}.",
                    $"Their final {seasonsPhrase} on record saw them race for {teamListPhrase}.",
                    $"Over their last {seasonsPhrase}, they turned out for {teamListPhrase}."
                };
                return variants[random.Next(variants.Length)];
            }
            else
            {
                var variants = new[]
                {
                    $"Across {seasonsPhrase}, they raced for {teamListPhrase}.",
                    $"Their {seasonsPhrase} on the grid were spent racing for {teamListPhrase}.",
                    $"Over {seasonsPhrase}, they represented {teamListPhrase}."
                };
                return variants[random.Next(variants.Length)];
            }
        }

        private string FormatTeamList(List<string> teamNames)
        {
            if (teamNames.Count == 1) return teamNames[0];
            if (teamNames.Count == 2) return $"{teamNames[0]} and {teamNames[1]}";
            return $"{string.Join(", ", teamNames.Take(teamNames.Count - 1))}, and {teamNames[^1]}";
        }

        private string GenerateClosingParagraph(string name, string teamName)
        {
            var random = new Random();
            var variants = new[]
            {
                $"{teamName} will now turn its attention to finding a replacement, but {name}'s legacy on the grid is secure. The paddock wishes them well in whatever comes next.",
                $"As {teamName} begins the search for a new driver, tributes continue to pour in for {name} from across the motorsport world.",
                $"The grid will feel a little different without {name} next season, but their story is one that will be told for years to come."
            };
            return variants[random.Next(variants.Length)];
        }

        private string GetTeamName(ISaveGame saveGame, string teamId)
        {
            if (string.IsNullOrEmpty(teamId)) return "their team";

            var team = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            return team?.TeamName ?? "their team";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
