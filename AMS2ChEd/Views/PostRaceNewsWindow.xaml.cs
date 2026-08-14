using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Extensions;
using AMS2ChEd.Localization;
using AMS2ChEd.Resources;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AMS2ChEd.Views
{
    public partial class PostRaceNewsWindow : Window
    {
        public PostRaceNewsWindow(
            ISaveGame saveGame,
            GrandPrixResult raceResult,
            int previousWinnerStandingPosition,
            List<string> previousTopThreeDriverIds,
            DateTime grandPrixDate,
            string winnerPortraitPath)
        {
            InitializeComponent();

            // Set the date
            var raceJustFinished = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex - 1);
            DateText.Text = (DateTime.ParseExact(raceJustFinished.RaceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToString("dddd, MMMM dd, yyyy");

            // Load winner portrait if provided
            if (!string.IsNullOrEmpty(winnerPortraitPath))
            {
                DriverPortraitImage.LoadPhoto(winnerPortraitPath);
            }

            // Generate the article
            GenerateArticle(saveGame, raceResult, previousWinnerStandingPosition, previousTopThreeDriverIds);
        }

        private void GenerateArticle(
            ISaveGame saveGame,
            GrandPrixResult raceResult,
            int previousWinnerStandingPosition,
            List<string> previousTopThreeDriverIds)
        {
            // Get podium finishers
            var winner = raceResult.RaceResults.FirstOrDefault(d => d.Position == 1);
            var second = raceResult.RaceResults.FirstOrDefault(d => d.Position == 2);
            var third = raceResult.RaceResults.FirstOrDefault(d => d.Position == 3);

            if (winner == null)
            {
                ArticleText.Text = Strings.PostRaceNewsWindow_NoResults;
                return;
            }

            // Check if this is the first race of the season
            bool isFirstRace = (saveGame.NextGpIndex - 1) == 0;
            bool isLastRace = (saveGame.NextGpIndex) == saveGame.CurrentSeason.Races.Count();

            // Get driver names and teams
            string winnerName = GetDriverName(saveGame, winner.DriverId);
            string winnerTeam = GetTeamName(saveGame, winner.TeamId);
            DriverReputation winnerReputation = GetDriverReputation(saveGame, winner.DriverId);

            var winnerDriverAccolades = AccoladesCalculator.GetDriverAccolades(saveGame, winner.DriverId);
            var winnerTeamAccolades = AccoladesCalculator.GetTeamAccolades(saveGame, winner.TeamId);
            bool isMaidenWin = winnerDriverAccolades.HasBaseline && winnerDriverAccolades.Wins == 1;

            bool isGrandSlam = winner.FastestLap == true &&
                (raceResult.QualifyingResults ?? new List<SessionResult>()).Any(q => q.DriverId == winner.DriverId && q.Position == 1);

            // Set the headline
            HeadlineText.Text = BuildHeadline(winnerName, raceResult, isMaidenWin, isGrandSlam);

            string secondName = second != null ? GetDriverName(saveGame, second.DriverId) : Strings.PostRaceNewsWindow_UnknownName;
            string secondTeam = second != null ? GetTeamName(saveGame, second.TeamId) : Strings.PostRaceNewsWindow_UnknownName;

            string thirdName = third != null ? GetDriverName(saveGame, third.DriverId) : Strings.PostRaceNewsWindow_UnknownName;
            string thirdTeam = third != null ? GetTeamName(saveGame, third.TeamId) : Strings.PostRaceNewsWindow_UnknownName;

            // Get winner's new standing position
            var winnerStanding = saveGame.CurrentDriverStandings
                .FirstOrDefault(s => s.DriverId == winner.DriverId);
            int winnerNewPosition = winnerStanding?.Position ?? 1;

            // Build the article
            string article = "";
            var random = new Random();

            // Opening paragraph with podium. Argument order standardized across every variant as
            // (year, gpName, winnerName, winnerTeam, secondName, secondTeam, thirdName, thirdTeam)
            // regardless of English word order, so each language's resx template can freely reorder.
            object[] openingArgs = { raceResult.Year, raceResult.GrandPrixName, winnerName, winnerTeam, secondName, secondTeam, thirdName, thirdTeam };

            if (isFirstRace)
            {
                var firstRaceOpenings = new[]
                {
                    Strings.PostRaceNewsWindow_Opening_FirstRace1_Format,
                    Strings.PostRaceNewsWindow_Opening_FirstRace2_Format,
                    Strings.PostRaceNewsWindow_Opening_FirstRace3_Format,
                    Strings.PostRaceNewsWindow_Opening_FirstRace4_Format,
                    Strings.PostRaceNewsWindow_Opening_FirstRace5_Format,
                    Strings.PostRaceNewsWindow_Opening_FirstRace6_Format
                };

                article += string.Format(firstRaceOpenings[random.Next(firstRaceOpenings.Length)], openingArgs) + "\n\n";
            }
            else if (isLastRace)
            {
                // Check if winner is also the champion
                bool isChampion = winnerNewPosition == 1;

                if (isChampion)
                {
                    var championFinaleOpenings = new[]
                    {
                        Strings.PostRaceNewsWindow_Opening_FinaleChampion1_Format,
                        Strings.PostRaceNewsWindow_Opening_FinaleChampion2_Format,
                        Strings.PostRaceNewsWindow_Opening_FinaleChampion3_Format,
                        Strings.PostRaceNewsWindow_Opening_FinaleChampion4_Format
                    };

                    article += string.Format(championFinaleOpenings[random.Next(championFinaleOpenings.Length)], openingArgs) + "\n\n";
                }
                else
                {
                    var finaleOpenings = new[]
                    {
                        Strings.PostRaceNewsWindow_Opening_FinaleNonChampion1_Format,
                        Strings.PostRaceNewsWindow_Opening_FinaleNonChampion2_Format,
                        Strings.PostRaceNewsWindow_Opening_FinaleNonChampion3_Format,
                        Strings.PostRaceNewsWindow_Opening_FinaleNonChampion4_Format
                    };

                    article += string.Format(finaleOpenings[random.Next(finaleOpenings.Length)], openingArgs) + "\n\n";
                }
            }
            else
            {
                var standardOpenings = new[]
                {
                    Strings.PostRaceNewsWindow_Opening_Standard1_Format,
                    Strings.PostRaceNewsWindow_Opening_Standard2_Format,
                    Strings.PostRaceNewsWindow_Opening_Standard3_Format,
                    Strings.PostRaceNewsWindow_Opening_Standard4_Format,
                    Strings.PostRaceNewsWindow_Opening_Standard5_Format,
                    Strings.PostRaceNewsWindow_Opening_Standard6_Format,
                    Strings.PostRaceNewsWindow_Opening_Standard7_Format,
                    Strings.PostRaceNewsWindow_Opening_Standard8_Format
                };

                article += string.Format(standardOpenings[random.Next(standardOpenings.Length)], openingArgs) + "\n\n";
            }

            // Winner's performance headline based on reputation
            article += GenerateWinnerHeadline(winnerName, winnerTeam, winnerReputation, winnerNewPosition, previousWinnerStandingPosition, isFirstRace);
            article += "\n\n";

            // Winner analysis based on reputation
            article += GenerateWinnerAnalysis(winnerName, winnerTeam, winnerReputation, raceResult.GrandPrixName);
            article += "\n\n";

            // Grand slam (pole + win + fastest lap in the same race)
            if (isGrandSlam)
            {
                article += GenerateGrandSlamParagraph(winnerName, raceResult);
                article += "\n\n";
            }

            // Race highlights: win streak, 1-2 finish, fastest lap (when not already covered by the grand slam paragraph)
            string highlights = GenerateRaceHighlightsParagraph(
                saveGame, raceResult, winner, winnerName, winnerTeam, second, secondName, third, thirdName, isGrandSlam);
            if (!string.IsNullOrEmpty(highlights))
            {
                article += highlights;
                article += "\n\n";
            }

            // Career milestone (win/podium tally for the driver and constructor)
            article += GenerateCareerMilestoneParagraph(winnerName, winnerTeam, winnerDriverAccolades, winnerTeamAccolades, isMaidenWin);
            article += "\n\n";

            // First-ever career podium for any of today's podium finishers
            string firstPodiumParagraph = GenerateFirstPodiumParagraph(saveGame, winner, winnerName, isMaidenWin, second, secondName, third, thirdName);
            if (!string.IsNullOrEmpty(firstPodiumParagraph))
            {
                article += firstPodiumParagraph;
                article += "\n\n";
            }

            // Bad result for a driver who came into the weekend in the championship's top 3
            string topThreeBadResultParagraph = GenerateTopThreeBadResultParagraph(saveGame, raceResult, previousTopThreeDriverIds);
            if (!string.IsNullOrEmpty(topThreeBadResultParagraph))
            {
                article += topThreeBadResultParagraph;
                article += "\n\n";
            }

            // Championship implications
            article += GenerateChampionshipUpdate(winnerName, winnerNewPosition, previousWinnerStandingPosition, isFirstRace, isLastRace);
            article += "\n\n";

            // Current championship standings (top 3)
            article += Strings.PostRaceNewsWindow_StandingsHeader;
            article += "\n\n";
            var topThree = saveGame.CurrentDriverStandings
                .OrderBy(s => s.Position)
                .Take(3);

            foreach (var standing in topThree)
            {
                string driverName = GetDriverName(saveGame, standing.DriverId);
                string pointsWord = standing.Points != 1
                    ? Strings.PostRaceNewsWindow_PointsWord_Plural
                    : Strings.PostRaceNewsWindow_PointsWord_Singular;
                article += string.Format(Strings.PostRaceNewsWindow_StandingsLine_Format,
                    standing.Position, driverName, standing.Points, pointsWord);
                article += "\n";
            }

            article += "\n";

            if (isFirstRace)
            {
                article += Strings.PostRaceNewsWindow_Closing_FirstRace;
            }
            else
            {
                article += Strings.PostRaceNewsWindow_Closing_Standard;
            }

            ArticleText.Text = article;
        }

        private string BuildHeadline(string winnerName, GrandPrixResult raceResult, bool isMaidenWin, bool isGrandSlam)
        {
            var random = new Random();
            string upperName = winnerName.ToUpper();
            string upperGp = string.Format(Strings.PostRaceNewsWindow_Headline_GpName_Format, raceResult.GrandPrixName, raceResult.Year).ToUpper();

            if (isGrandSlam && isMaidenWin)
            {
                var maidenGrandSlamHeadlines = new[]
                {
                    Strings.PostRaceNewsWindow_Headline_MaidenGrandSlam1_Format,
                    Strings.PostRaceNewsWindow_Headline_MaidenGrandSlam2_Format
                };
                return string.Format(maidenGrandSlamHeadlines[random.Next(maidenGrandSlamHeadlines.Length)], upperName, upperGp);
            }

            if (isGrandSlam)
            {
                var grandSlamHeadlines = new[]
                {
                    Strings.PostRaceNewsWindow_Headline_GrandSlam1_Format,
                    Strings.PostRaceNewsWindow_Headline_GrandSlam2_Format
                };
                return string.Format(grandSlamHeadlines[random.Next(grandSlamHeadlines.Length)], upperName, upperGp);
            }

            if (isMaidenWin)
            {
                var maidenWinHeadlines = new[]
                {
                    Strings.PostRaceNewsWindow_Headline_MaidenWin1_Format,
                    Strings.PostRaceNewsWindow_Headline_MaidenWin2_Format
                };
                return string.Format(maidenWinHeadlines[random.Next(maidenWinHeadlines.Length)], upperName, upperGp);
            }

            return string.Format(Strings.PostRaceNewsWindow_Headline_Standard_Format, upperName, upperGp);
        }

        private string GenerateGrandSlamParagraph(string winnerName, GrandPrixResult raceResult)
        {
            var random = new Random();
            var grandSlamVariants = new[]
            {
                Strings.PostRaceNewsWindow_GrandSlam1_Format,
                Strings.PostRaceNewsWindow_GrandSlam2_Format,
                Strings.PostRaceNewsWindow_GrandSlam3_Format
            };
            return string.Format(grandSlamVariants[random.Next(grandSlamVariants.Length)], winnerName, raceResult.GrandPrixName);
        }

        private string GenerateRaceHighlightsParagraph(
            ISaveGame saveGame,
            GrandPrixResult raceResult,
            SessionResult winner,
            string winnerName,
            string winnerTeam,
            SessionResult second,
            string secondName,
            SessionResult third,
            string thirdName,
            bool isGrandSlam)
        {
            var sentences = new List<string>();

            int winStreak = AccoladesCalculator.GetDriverWinStreak(saveGame, winner.DriverId);
            if (winStreak >= 2)
                sentences.Add(string.Format(Strings.PostRaceNewsWindow_WinStreak_Format, winnerName, OrdinalFormatter.Format(winStreak)));

            bool isOneTwo = second != null
                && !string.IsNullOrWhiteSpace(winner.TeamId) && winner.TeamId != "team_id"
                && winner.TeamId == second.TeamId;
            if (isOneTwo)
                sentences.Add(string.Format(Strings.PostRaceNewsWindow_OneTwo_Format, winnerTeam, secondName, winnerName));

            if (!isGrandSlam)
            {
                if (winner.FastestLap == true)
                    sentences.Add(string.Format(Strings.PostRaceNewsWindow_WinnerFastestLap_Format, winnerName));
                else if (second?.FastestLap == true)
                    sentences.Add(string.Format(Strings.PostRaceNewsWindow_OtherFastestLap_Format, secondName, 2));
                else if (third?.FastestLap == true)
                    sentences.Add(string.Format(Strings.PostRaceNewsWindow_OtherFastestLap_Format, thirdName, 3));
            }

            return sentences.Count > 0 ? string.Join(" ", sentences) : "";
        }

        private string GenerateCareerMilestoneParagraph(
            string driverName,
            string teamName,
            AccoladeSummary driverAccolades,
            AccoladeSummary teamAccolades,
            bool isMaidenWin)
        {
            var random = new Random();

            if (isMaidenWin)
            {
                var maidenWinVariants = new[]
                {
                    Strings.PostRaceNewsWindow_MaidenWinMilestone1_Format,
                    Strings.PostRaceNewsWindow_MaidenWinMilestone2_Format,
                    Strings.PostRaceNewsWindow_MaidenWinMilestone3_Format
                };
                return string.Format(maidenWinVariants[random.Next(maidenWinVariants.Length)], driverName);
            }

            string driverWinPhrase = driverAccolades.HasBaseline
                ? string.Format(Strings.PostRaceNewsWindow_DriverWinPhrase_Baseline_Format, OrdinalFormatter.Format(driverAccolades.Wins))
                : string.Format(Strings.PostRaceNewsWindow_DriverWinPhrase_SinceYear_Format, OrdinalFormatter.Format(driverAccolades.Wins), driverAccolades.StartYear);

            string teamWinPhrase = teamAccolades.HasBaseline
                ? string.Format(Strings.PostRaceNewsWindow_TeamWinPhrase_Baseline_Format, OrdinalFormatter.Format(teamAccolades.Wins))
                : string.Format(Strings.PostRaceNewsWindow_TeamWinPhrase_SinceYear_Format, OrdinalFormatter.Format(teamAccolades.Wins), teamAccolades.StartYear);

            string driverPodiumPhrase = driverAccolades.HasBaseline
                ? string.Format(Strings.PostRaceNewsWindow_DriverPodiumPhrase_Baseline_Format, driverAccolades.Podiums)
                : string.Format(Strings.PostRaceNewsWindow_DriverPodiumPhrase_SinceYear_Format, driverAccolades.Podiums, driverAccolades.StartYear);

            var winsOnlyVariants = new[]
            {
                string.Format(Strings.PostRaceNewsWindow_WinsOnly1_Format, driverName, driverWinPhrase, teamName, teamWinPhrase),
                string.Format(Strings.PostRaceNewsWindow_WinsOnly2_Format, driverName, driverWinPhrase, teamName, teamWinPhrase)
            };

            var winsAndPodiumsVariants = new[]
            {
                string.Format(Strings.PostRaceNewsWindow_WinsAndPodiums1_Format, driverName, driverAccolades.Wins, driverPodiumPhrase, teamName, teamWinPhrase),
                string.Format(Strings.PostRaceNewsWindow_WinsAndPodiums2_Format, driverName, driverWinPhrase, driverPodiumPhrase, teamName, teamWinPhrase)
            };

            var allVariants = winsOnlyVariants.Concat(winsAndPodiumsVariants).ToArray();
            return allVariants[random.Next(allVariants.Length)];
        }

        private string GenerateFirstPodiumParagraph(
            ISaveGame saveGame,
            SessionResult winner, string winnerName, bool isMaidenWin,
            SessionResult second, string secondName,
            SessionResult third, string thirdName)
        {
            var sentences = new List<string>();
            var random = new Random();

            void AddIfMaiden(SessionResult result, string name)
            {
                if (result == null) return;
                var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, result.DriverId);
                if (!(accolades.HasBaseline && accolades.Podiums == 1)) return;

                var variants = new[]
                {
                    Strings.PostRaceNewsWindow_FirstPodium1_Format,
                    Strings.PostRaceNewsWindow_FirstPodium2_Format,
                    Strings.PostRaceNewsWindow_FirstPodium3_Format
                };
                sentences.Add(string.Format(variants[random.Next(variants.Length)], name));
            }

            // Skip the winner if this was already their maiden win - that milestone is already covered above
            if (!isMaidenWin) AddIfMaiden(winner, winnerName);
            AddIfMaiden(second, secondName);
            AddIfMaiden(third, thirdName);

            return sentences.Count > 0 ? string.Join(" ", sentences) : "";
        }

        private string GenerateTopThreeBadResultParagraph(
            ISaveGame saveGame,
            GrandPrixResult raceResult,
            List<string> previousTopThreeDriverIds)
        {
            if (previousTopThreeDriverIds == null) return "";

            var sentences = new List<string>();
            var random = new Random();

            foreach (var driverId in previousTopThreeDriverIds)
            {
                var result = raceResult.RaceResults.FirstOrDefault(r => r.DriverId == driverId);
                if (result == null || result.DidNotPreQualify) continue;

                bool badResult = result.DNF || result.Position > 8;
                if (!badResult) continue;

                string name = GetDriverName(saveGame, driverId);

                if (result.DNF)
                {
                    var dnfVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Dnf1_Format,
                        Strings.PostRaceNewsWindow_Dnf2_Format,
                        Strings.PostRaceNewsWindow_Dnf3_Format
                    };
                    sentences.Add(string.Format(dnfVariants[random.Next(dnfVariants.Length)], name));
                }
                else
                {
                    var badResultVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_BadResult1_Format,
                        Strings.PostRaceNewsWindow_BadResult2_Format,
                        Strings.PostRaceNewsWindow_BadResult3_Format
                    };
                    sentences.Add(string.Format(badResultVariants[random.Next(badResultVariants.Length)], name, result.Position));
                }
            }

            return sentences.Count > 0 ? string.Join(" ", sentences) : "";
        }

        private string GenerateWinnerHeadline(
            string winner,
            string team,
            DriverReputation reputation,
            int newPosition,
            int previousPosition,
            bool isFirstRace)
        {
            var random = new Random();

            // Argument order standardized as (winner, team, newPosition) even for cases whose
            // English prose doesn't use every placeholder, so every language's resx template can
            // freely reorder or drop any of them.
            switch (reputation)
            {
                case DriverReputation.PAY_DRIVER_WILD_CARD:
                    var payDriverWildCardVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_PayDriverWildCard1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PayDriverWildCard2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PayDriverWildCard3_Format
                    };
                    return string.Format(payDriverWildCardVariants[random.Next(payDriverWildCardVariants.Length)], winner, team, newPosition);

                case DriverReputation.PAY_DRIVER_SEASON:
                    var payDriverSeasonVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_PayDriverSeason1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PayDriverSeason2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PayDriverSeason3_Format
                    };
                    return string.Format(payDriverSeasonVariants[random.Next(payDriverSeasonVariants.Length)], winner, team, newPosition);

                case DriverReputation.YOUNG_TALENT:
                    var youngTalentVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_YoungTalent1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_YoungTalent2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_YoungTalent3_Format
                    };
                    return string.Format(youngTalentVariants[random.Next(youngTalentVariants.Length)], winner, team, newPosition);

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    var youngChampUnprovenVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_YoungChampUnproven1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_YoungChampUnproven2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_YoungChampUnproven3_Format
                    };
                    return string.Format(youngChampUnprovenVariants[random.Next(youngChampUnprovenVariants.Length)], winner, team, newPosition);

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    if (newPosition == 1)
                    {
                        var youngChampLeaderVariants = new[]
                        {
                            Strings.PostRaceNewsWindow_WinnerHeadline_YoungChampLeader1_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_YoungChampLeader2_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_YoungChampLeader3_Format
                        };
                        return string.Format(youngChampLeaderVariants[random.Next(youngChampLeaderVariants.Length)], winner, team, newPosition);
                    }
                    else
                    {
                        var youngChampChaserVariants = new[]
                        {
                            Strings.PostRaceNewsWindow_WinnerHeadline_YoungChampChaser1_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_YoungChampChaser2_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_YoungChampChaser3_Format
                        };
                        return string.Format(youngChampChaserVariants[random.Next(youngChampChaserVariants.Length)], winner, team, newPosition);
                    }

                case DriverReputation.PRIME_MIDFIELD:
                    var primeMidfieldVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_PrimeMidfield1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PrimeMidfield2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PrimeMidfield3_Format
                    };
                    return string.Format(primeMidfieldVariants[random.Next(primeMidfieldVariants.Length)], winner, team, newPosition);

                case DriverReputation.PRIME_STRONG_MIDFIELD:
                    var primeStrongMidfieldVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_PrimeStrongMidfield1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PrimeStrongMidfield2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PrimeStrongMidfield3_Format
                    };
                    return string.Format(primeStrongMidfieldVariants[random.Next(primeStrongMidfieldVariants.Length)], winner, team, newPosition);

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    if (newPosition <= 3)
                    {
                        var primeChampUnprovenTopVariants = new[]
                        {
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampUnprovenTop1_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampUnprovenTop2_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampUnprovenTop3_Format
                        };
                        return string.Format(primeChampUnprovenTopVariants[random.Next(primeChampUnprovenTopVariants.Length)], winner, team, newPosition);
                    }
                    else
                    {
                        var primeChampUnprovenVariants = new[]
                        {
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampUnproven1_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampUnproven2_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampUnproven3_Format
                        };
                        return string.Format(primeChampUnprovenVariants[random.Next(primeChampUnprovenVariants.Length)], winner, team, newPosition);
                    }

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                    if (newPosition == 1)
                    {
                        var primeChampLeaderVariants = new[]
                        {
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampLeader1_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampLeader2_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampLeader3_Format
                        };
                        return string.Format(primeChampLeaderVariants[random.Next(primeChampLeaderVariants.Length)], winner, team, newPosition);
                    }
                    else
                    {
                        var primeChampChaserVariants = new[]
                        {
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampChaser1_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampChaser2_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampChaser3_Format
                        };
                        return string.Format(primeChampChaserVariants[random.Next(primeChampChaserVariants.Length)], winner, team, newPosition);
                    }

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED:
                    var primeChampWashedVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampWashed1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampWashed2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_PrimeChampWashed3_Format
                    };
                    return string.Format(primeChampWashedVariants[random.Next(primeChampWashedVariants.Length)], winner, team, newPosition);

                case DriverReputation.AGEING_MIDFIELD:
                    var ageingMidfieldVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_AgeingMidfield1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_AgeingMidfield2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_AgeingMidfield3_Format
                    };
                    return string.Format(ageingMidfieldVariants[random.Next(ageingMidfieldVariants.Length)], winner, team, newPosition);

                case DriverReputation.AGEING_STRONG_MIDFIELD:
                    var ageingStrongMidfieldVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_AgeingStrongMidfield1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_AgeingStrongMidfield2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_AgeingStrongMidfield3_Format
                    };
                    return string.Format(ageingStrongMidfieldVariants[random.Next(ageingStrongMidfieldVariants.Length)], winner, team, newPosition);

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                    if (newPosition <= 2)
                    {
                        var ageingChampTopVariants = new[]
                        {
                            Strings.PostRaceNewsWindow_WinnerHeadline_AgeingChampTop1_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_AgeingChampTop2_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_AgeingChampTop3_Format
                        };
                        return string.Format(ageingChampTopVariants[random.Next(ageingChampTopVariants.Length)], winner, team, newPosition);
                    }
                    else
                    {
                        var ageingChampVariants = new[]
                        {
                            Strings.PostRaceNewsWindow_WinnerHeadline_AgeingChamp1_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_AgeingChamp2_Format,
                            Strings.PostRaceNewsWindow_WinnerHeadline_AgeingChamp3_Format
                        };
                        return string.Format(ageingChampVariants[random.Next(ageingChampVariants.Length)], winner, team, newPosition);
                    }

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                    var ageingChampWashedVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_AgeingChampWashed1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_AgeingChampWashed2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_AgeingChampWashed3_Format
                    };
                    return string.Format(ageingChampWashedVariants[random.Next(ageingChampWashedVariants.Length)], winner, team, newPosition);

                case DriverReputation.JUST_ONE_LAST_DANCE:
                    var lastDanceVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_LastDance1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_LastDance2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_LastDance3_Format
                    };
                    return string.Format(lastDanceVariants[random.Next(lastDanceVariants.Length)], winner, team, newPosition);

                default:
                    var defaultVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_WinnerHeadline_Default1_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_Default2_Format,
                        Strings.PostRaceNewsWindow_WinnerHeadline_Default3_Format
                    };
                    return string.Format(defaultVariants[random.Next(defaultVariants.Length)], winner, team, newPosition);
            }
        }

        private string GenerateWinnerAnalysis(
            string winner,
            string team,
            DriverReputation reputation,
            string raceName)
        {
            var random = new Random();

            switch (reputation)
            {
                case DriverReputation.PAY_DRIVER_WILD_CARD:
                case DriverReputation.PAY_DRIVER_SEASON:
                    var payDriverVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_PayDriver1_Format,
                        Strings.PostRaceNewsWindow_Analysis_PayDriver2_Format,
                        Strings.PostRaceNewsWindow_Analysis_PayDriver3_Format
                    };
                    return string.Format(payDriverVariants[random.Next(payDriverVariants.Length)], winner, team);

                case DriverReputation.YOUNG_TALENT:
                    var youngTalentVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_YoungTalent1_Format,
                        Strings.PostRaceNewsWindow_Analysis_YoungTalent2_Format,
                        Strings.PostRaceNewsWindow_Analysis_YoungTalent3_Format
                    };
                    return string.Format(youngTalentVariants[random.Next(youngTalentVariants.Length)], winner, team);

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    var youngChampVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_YoungChamp1_Format,
                        Strings.PostRaceNewsWindow_Analysis_YoungChamp2_Format,
                        Strings.PostRaceNewsWindow_Analysis_YoungChamp3_Format
                    };
                    return string.Format(youngChampVariants[random.Next(youngChampVariants.Length)], winner, team);

                case DriverReputation.PRIME_MIDFIELD:
                case DriverReputation.PRIME_STRONG_MIDFIELD:
                    var primeMidfieldVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_PrimeMidfield1_Format,
                        Strings.PostRaceNewsWindow_Analysis_PrimeMidfield2_Format,
                        Strings.PostRaceNewsWindow_Analysis_PrimeMidfield3_Format
                    };
                    return string.Format(primeMidfieldVariants[random.Next(primeMidfieldVariants.Length)], winner, team);

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                    var primeChampVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_PrimeChamp1_Format,
                        Strings.PostRaceNewsWindow_Analysis_PrimeChamp2_Format,
                        Strings.PostRaceNewsWindow_Analysis_PrimeChamp3_Format
                    };
                    return string.Format(primeChampVariants[random.Next(primeChampVariants.Length)], winner, team);

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED:
                    var primeChampWashedVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_PrimeChampWashed1_Format,
                        Strings.PostRaceNewsWindow_Analysis_PrimeChampWashed2_Format,
                        Strings.PostRaceNewsWindow_Analysis_PrimeChampWashed3_Format
                    };
                    return string.Format(primeChampWashedVariants[random.Next(primeChampWashedVariants.Length)], winner, team);

                case DriverReputation.AGEING_MIDFIELD:
                case DriverReputation.AGEING_STRONG_MIDFIELD:
                    var ageingMidfieldVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_AgeingMidfield1_Format,
                        Strings.PostRaceNewsWindow_Analysis_AgeingMidfield2_Format,
                        Strings.PostRaceNewsWindow_Analysis_AgeingMidfield3_Format
                    };
                    return string.Format(ageingMidfieldVariants[random.Next(ageingMidfieldVariants.Length)], winner, team);

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                    var ageingChampVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_AgeingChamp1_Format,
                        Strings.PostRaceNewsWindow_Analysis_AgeingChamp2_Format,
                        Strings.PostRaceNewsWindow_Analysis_AgeingChamp3_Format
                    };
                    return string.Format(ageingChampVariants[random.Next(ageingChampVariants.Length)], winner, team);

                case DriverReputation.JUST_ONE_LAST_DANCE:
                    var lastDanceVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_LastDance1_Format,
                        Strings.PostRaceNewsWindow_Analysis_LastDance2_Format,
                        Strings.PostRaceNewsWindow_Analysis_LastDance3_Format
                    };
                    return string.Format(lastDanceVariants[random.Next(lastDanceVariants.Length)], winner, team);

                default:
                    var defaultVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_Analysis_Default1_Format,
                        Strings.PostRaceNewsWindow_Analysis_Default2_Format,
                        Strings.PostRaceNewsWindow_Analysis_Default3_Format
                    };
                    return string.Format(defaultVariants[random.Next(defaultVariants.Length)], winner, team);
            }
        }

        private string GenerateChampionshipUpdate(string winner, int newPosition, int previousPosition, bool isFirstRace, bool isLastRace)
        {
            // For the last race, focus on final championship outcomes
            if (isLastRace)
            {
                if (newPosition == 1)
                {
                    var championClinchVariants = new[]
                    {
                        Strings.PostRaceNewsWindow_ChampClinch1_Format,
                        Strings.PostRaceNewsWindow_ChampClinch2_Format,
                        Strings.PostRaceNewsWindow_ChampClinch3_Format
                    };
                    var random = new Random();
                    return string.Format(championClinchVariants[random.Next(championClinchVariants.Length)], winner);
                }
                else if (newPosition <= 3)
                {
                    return string.Format(Strings.PostRaceNewsWindow_ChampFinaleTop3_Format, winner, newPosition);
                }
                else
                {
                    return string.Format(Strings.PostRaceNewsWindow_ChampFinaleOther_Format, winner, newPosition);
                }
            }

            // For the first race, focus on taking the early lead rather than position changes
            if (isFirstRace)
            {
                if (newPosition == 1)
                {
                    return string.Format(Strings.PostRaceNewsWindow_ChampFirstRaceLeader_Format, winner);
                }
                else if (newPosition <= 3)
                {
                    return string.Format(Strings.PostRaceNewsWindow_ChampFirstRaceTop3_Format, winner, newPosition);
                }
                else
                {
                    return string.Format(Strings.PostRaceNewsWindow_ChampFirstRaceOther_Format, winner, newPosition);
                }
            }

            // Existing logic for subsequent races
            int positionChange = previousPosition - newPosition;

            if (newPosition == 1)
            {
                if (previousPosition == 1)
                {
                    return string.Format(Strings.PostRaceNewsWindow_ChampLeaderContinues_Format, winner);
                }
                else
                {
                    return string.Format(Strings.PostRaceNewsWindow_ChampNewLeader_Format, winner, previousPosition);
                }
            }
            else if (positionChange > 0)
            {
                return string.Format(Strings.PostRaceNewsWindow_ChampPositionUp_Format, winner, previousPosition, newPosition);
            }
            else if (positionChange < 0)
            {
                return string.Format(Strings.PostRaceNewsWindow_ChampPositionDown_Format, winner, previousPosition, newPosition);
            }
            else
            {
                return string.Format(Strings.PostRaceNewsWindow_ChampPositionSame_Format, winner, newPosition);
            }
        }

        private string GetDriverName(ISaveGame saveGame, string driverId)
        {
            if (driverId == saveGame.PlayerData.DriverId)
                return saveGame.PlayerData.Name;

            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);
            return driver?.Name ?? Strings.PostRaceNewsWindow_DefaultDriverName;
        }

        private string GetTeamName(ISaveGame saveGame, string teamId)
        {
            var team = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            return team?.TeamName ?? Strings.PostRaceNewsWindow_DefaultTeamName;
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // This window is shown non-modally (Show(), not ShowDialog()) by RaceWeekendWindow, so
            // DialogResult can't be set here - doing so throws InvalidOperationException and crashes
            // the app.
            this.Close();
        }
    }
}