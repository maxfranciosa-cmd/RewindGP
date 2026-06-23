using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Extensions;
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
            GenerateArticle(saveGame, raceResult, previousWinnerStandingPosition);
        }

        private void GenerateArticle(
            ISaveGame saveGame,
            GrandPrixResult raceResult,
            int previousWinnerStandingPosition)
        {
            // Get podium finishers
            var winner = raceResult.RaceResults.FirstOrDefault(d => d.Position == 1);
            var second = raceResult.RaceResults.FirstOrDefault(d => d.Position == 2);
            var third = raceResult.RaceResults.FirstOrDefault(d => d.Position == 3);

            if (winner == null)
            {
                ArticleText.Text = "Race results not available.";
                return;
            }

            // Check if this is the first race of the season
            bool isFirstRace = (saveGame.NextGpIndex - 1) == 0;
            bool isLastRace = (saveGame.NextGpIndex) == saveGame.CurrentSeason.Races.Count();

            // Get driver names and teams
            string winnerName = GetDriverName(saveGame, winner.DriverId);

            // Set the headline
            HeadlineText.Text = $"{winnerName.ToUpper()} WINS THE {raceResult.Year} {raceResult.GrandPrixName.ToUpper()}";


            string winnerTeam = GetTeamName(saveGame, winner.TeamId);
            DriverReputation winnerReputation = GetDriverReputation(saveGame, winner.DriverId);

            string secondName = second != null ? GetDriverName(saveGame, second.DriverId) : "Unknown";
            string secondTeam = second != null ? GetTeamName(saveGame, second.TeamId) : "Unknown";

            string thirdName = third != null ? GetDriverName(saveGame, third.DriverId) : "Unknown";
            string thirdTeam = third != null ? GetTeamName(saveGame, third.TeamId) : "Unknown";

            // Get winner's new standing position
            var winnerStanding = saveGame.CurrentDriverStandings
                .FirstOrDefault(s => s.DriverId == winner.DriverId);
            int winnerNewPosition = winnerStanding?.Position ?? 1;

            // Build the article
            string article = "";
            var random = new Random();

            // Opening paragraph with podium
            if (isFirstRace)
            {
                var firstRaceOpenings = new[]
                {
                    // Variant 1 - Spectacular opening
                    $"The {raceResult.Year} season opened in spectacular fashion as {winnerName} claimed victory at the {raceResult.GrandPrixName}, " +
                    $"giving {winnerTeam} the perfect start to the championship. " +
                    $"{secondName} finished second for {secondTeam}, while {thirdName} completed the opening podium for {thirdTeam}.\n\n",
                    
                    // Variant 2 - Championship launch
                    $"The {raceResult.Year} championship burst into life at the {raceResult.GrandPrixName} with {winnerName} seizing the early initiative. " +
                    $"{winnerTeam} couldn't have asked for a better start, with {secondName} trailing home for {secondTeam} ahead of {thirdName} in third for {thirdTeam}.\n\n",
                    
                    // Variant 3 - Season kickoff
                    $"{winnerName} fired the opening shot in the {raceResult.Year} championship battle with victory at the {raceResult.GrandPrixName}. " +
                    $"The {winnerTeam} driver heads the standings after the opening round, pursued by {secondName} of {secondTeam} and {thirdName} for {thirdTeam}.\n\n",
                    
                    // Variant 4 - Drawing first blood
                    $"Drawing first blood in the {raceResult.Year} season, {winnerName} dominated the {raceResult.GrandPrixName} to give {winnerTeam} the early championship lead. " +
                    $"{secondName} claimed second for {secondTeam}, with {thirdName} rounding out the season-opening podium for {thirdTeam}.\n\n",
                    
                    // Variant 5 - Bold statement
                    $"{winnerName} made a bold statement of intent to open the {raceResult.Year} season, commanding the {raceResult.GrandPrixName} from start to finish. " +
                    $"{winnerTeam} leads the championship after round one, ahead of {secondName} for {secondTeam} and {thirdName} for {thirdTeam} who completed the podium.\n\n",
                    
                    // Variant 6 - New season dawn
                    $"As the {raceResult.Year} season dawned at the {raceResult.GrandPrixName}, {winnerName} struck gold for {winnerTeam} with a commanding opening victory. " +
                    $"{secondName} finished runner-up for {secondTeam}, while {thirdName} claimed the final podium spot for {thirdTeam} in the championship opener.\n\n"
                };

                article += firstRaceOpenings[random.Next(firstRaceOpenings.Length)];
            }
            else if (isLastRace)
            {
                // Check if winner is also the champion
                bool isChampion = winnerNewPosition == 1;

                if (isChampion)
                {
                    var championFinaleOpenings = new[]
                    {
                        // Variant 1 - Perfect ending
                        $"The {raceResult.Year} season concluded in perfect fashion as {winnerName} claimed victory at the {raceResult.GrandPrixName}, " +
                        $"crowning a championship-winning campaign with {winnerTeam}. " +
                        $"{secondName} finished second for {secondTeam}, while {thirdName} completed the final podium of the season for {thirdTeam}.\n\n",
                        
                        // Variant 2 - Title sealed
                        $"{winnerName} sealed the {raceResult.Year} Drivers' Championship in style, dominating the season finale at the {raceResult.GrandPrixName}. " +
                        $"The {winnerTeam} driver's victory confirmed what had been building all season, with {secondName} taking second for {secondTeam} and {thirdName} third for {thirdTeam}.\n\n",
                        
                        // Variant 3 - Coronation
                        $"In a coronation worthy of champions, {winnerName} took the checkered flag at the {raceResult.GrandPrixName} to cap an extraordinary {raceResult.Year} season. " +
                        $"The {winnerTeam} driver's final victory put the exclamation point on their title triumph, ahead of {secondName} for {secondTeam} and {thirdName} for {thirdTeam}.\n\n",
                        
                        // Variant 4 - Emphatic conclusion
                        $"An emphatic season finale victory for {winnerName} at the {raceResult.GrandPrixName} sealed the {raceResult.Year} title in the most dominant fashion. " +
                        $"{winnerTeam} celebrated championship glory as {secondName} claimed second for {secondTeam}, with {thirdName} rounding out the podium for {thirdTeam}.\n\n"
                    };

                    article += championFinaleOpenings[random.Next(championFinaleOpenings.Length)];
                }
                else
                {
                    var finaleOpenings = new[]
                    {
                        // Variant 1 - Season curtain
                        $"The curtain fell on the {raceResult.Year} season as {winnerName} claimed a memorable victory at the {raceResult.GrandPrixName}, " +
                        $"giving {winnerTeam} a perfect way to sign off the year. " +
                        $"{secondName} finished second for {secondTeam}, while {thirdName} completed the final podium for {thirdTeam}.\n\n",
                        
                        // Variant 2 - Final flourish
                        $"{winnerName} produced a final flourish at the {raceResult.GrandPrixName}, ending the {raceResult.Year} season on a high note for {winnerTeam}. " +
                        $"The season finale saw {secondName} take second for {secondTeam}, ahead of {thirdName} who claimed the last podium of the year for {thirdTeam}.\n\n",
                        
                        // Variant 3 - Last word
                        $"Having the last word in the {raceResult.Year} season, {winnerName} dominated the {raceResult.GrandPrixName} to give {winnerTeam} a season-ending victory. " +
                        $"{secondName} secured second place for {secondTeam}, while {thirdName} rounded out the final rostrum for {thirdTeam}.\n\n",
                        
                        // Variant 4 - Season bookend
                        $"As the {raceResult.Year} season reached its conclusion at the {raceResult.GrandPrixName}, {winnerName} delivered a commanding performance for {winnerTeam}. " +
                        $"The finale podium was completed by {secondName} in second for {secondTeam} and {thirdName} taking third for {thirdTeam}.\n\n"
                    };

                    article += finaleOpenings[random.Next(finaleOpenings.Length)];
                }
            }
            else
            {
                var standardOpenings = new[]
                {
                    // Variant 1 - Thrilling display
                    $"In a thrilling display of motorsport at its finest, {winnerName} claimed victory at the {raceResult.GrandPrixName}, " +
                    $"leading {winnerTeam} to a well-earned triumph. " +
                    $"{secondName} secured second place for {secondTeam}, while {thirdName} completed the podium for {thirdTeam}.\n\n",
                    
                    // Variant 2 - Commanding performance
                    $"{winnerName} delivered a commanding performance at the {raceResult.GrandPrixName}, steering {winnerTeam} to victory in dominant fashion. " +
                    $"The podium was completed by {secondName} in second for {secondTeam} and {thirdName} taking third for {thirdTeam}.\n\n",
                    
                    // Variant 3 - Checkered flag
                    $"The checkered flag fell at the {raceResult.GrandPrixName} with {winnerName} taking the honors for {winnerTeam}. " +
                    $"Behind the victor, {secondName} claimed a solid second place for {secondTeam}, ahead of {thirdName} who secured the final podium position for {thirdTeam}.\n\n",
                    
                    // Variant 4 - Masterful drive
                    $"A masterful drive from {winnerName} secured victory for {winnerTeam} at the {raceResult.GrandPrixName} today. " +
                    $"{secondName} put in a strong performance for second with {secondTeam}, while {thirdName} rounded out the top three for {thirdTeam}.\n\n",
                    
                    // Variant 5 - Lights to flag
                    $"{winnerName} controlled the {raceResult.GrandPrixName} to claim victory for {winnerTeam} in convincing style. " +
                    $"{secondName} followed the winner home in second place for {secondTeam}, with {thirdName} completing the podium for {thirdTeam}.\n\n",
                    
                    // Variant 6 - Hard-fought triumph
                    $"After a hard-fought battle at the {raceResult.GrandPrixName}, {winnerName} emerged victorious to deliver crucial points for {winnerTeam}. " +
                    $"The podium positions were completed by {secondName} for {secondTeam} in second and {thirdName} for {thirdTeam} in third.\n\n",
                    
                    // Variant 7 - Circuit mastery
                    $"{winnerName} mastered the {raceResult.GrandPrixName} circuit to secure a well-deserved victory for {winnerTeam}. " +
                    $"Second place went to {secondName} of {secondTeam}, while {thirdName} stood on the third step of the podium for {thirdTeam}.\n\n",
                    
                    // Variant 8 - Clinical precision
                    $"With clinical precision, {winnerName} navigated the {raceResult.GrandPrixName} to deliver {winnerTeam}'s latest triumph. " +
                    $"{secondName} drove strongly to second for {secondTeam}, ahead of third-placed {thirdName} representing {thirdTeam}.\n\n"
                };

                article += standardOpenings[random.Next(standardOpenings.Length)];
            }

            // Winner's performance headline based on reputation
            article += GenerateWinnerHeadline(winnerName, winnerTeam, winnerReputation, winnerNewPosition, previousWinnerStandingPosition, isFirstRace);
            article += "\n\n";

            // Winner analysis based on reputation
            article += GenerateWinnerAnalysis(winnerName, winnerTeam, winnerReputation, raceResult.GrandPrixName);
            article += "\n\n";

            // Championship implications
            article += GenerateChampionshipUpdate(winnerName, winnerNewPosition, previousWinnerStandingPosition, isFirstRace, isLastRace);
            article += "\n\n";

            // Current championship standings (top 3)
            article += "CHAMPIONSHIP STANDINGS (TOP 3):\n\n";
            var topThree = saveGame.CurrentDriverStandings
                .OrderBy(s => s.Position)
                .Take(3);

            foreach (var standing in topThree)
            {
                string driverName = GetDriverName(saveGame, standing.DriverId);
                article += $"{standing.Position}. {driverName} - {standing.Points} points\n";
            }

            article += "\n";

            if (isFirstRace)
            {
                article += $"The championship has been set in motion, with all eyes now turning to the next race as teams and drivers look to build on their opening performances.";
            }
            else
            {
                article += $"The championship battle continues to intensify as teams and drivers prepare for the next round of this captivating season.";
            }

            ArticleText.Text = article;
        }

        private string GenerateWinnerHeadline(
            string winner,
            string team,
            DriverReputation reputation,
            int newPosition,
            int previousPosition,
            bool isFirstRace)
        {
            int positionChange = previousPosition - newPosition;
            var random = new Random();

            switch (reputation)
            {
                case DriverReputation.PAY_DRIVER_WILD_CARD:
                    var payDriverWildCardVariants = new[]
                    {
                        $"{winner} defied expectations with a stunning victory for {team}, proving doubters wrong with a masterful drive.",
                        $"Against all odds, {winner} silenced the skeptics with a commanding performance that validated {team}'s faith in them.",
                        $"{winner} shocked the paddock with a brilliant victory, demonstrating that raw pace transcends funding concerns."
                    };
                    return payDriverWildCardVariants[random.Next(payDriverWildCardVariants.Length)];

                case DriverReputation.PAY_DRIVER_SEASON:
                    var payDriverSeasonVariants = new[]
                    {
                        $"In a remarkable turn of events, {winner} silenced critics with a commanding performance, delivering {team}'s first win of the partnership.",
                        $"{winner} answered every question with authority, proving their {team} seat was earned through speed, not sponsorship.",
                        $"The critics are eating their words as {winner} delivered a masterclass for {team}, justifying their place on the grid with undeniable skill."
                    };
                    return payDriverSeasonVariants[random.Next(payDriverSeasonVariants.Length)];

                case DriverReputation.YOUNG_TALENT:
                    var youngTalentVariants = new[]
                    {
                        $"Rising star {winner} announced their arrival on the big stage with a brilliant victory, showcasing the raw talent that {team} bet on.",
                        $"The future arrived early as {winner} claimed a memorable first victory, giving {team} a glimpse of what's to come.",
                        $"{winner} showed maturity beyond their years, delivering a composed victory that marks them as a star in the making."
                    };
                    return youngTalentVariants[random.Next(youngTalentVariants.Length)];

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    var youngChampUnprovenVariants = new[]
                    {
                        $"{winner} surged to P{newPosition} in the standings with a dominant display, edging closer to proving their championship credentials.",
                        $"The potential became reality as {winner} delivered a statement win, moving to P{newPosition} with championship-caliber pace.",
                        $"{winner} took another step toward greatness, claiming victory and rising to P{newPosition} in a performance that silenced any remaining doubters."
                    };
                    return youngChampUnprovenVariants[random.Next(youngChampUnprovenVariants.Length)];

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    if (newPosition == 1)
                    {
                        var youngChampLeaderVariants = new[]
                        {
                            $"{winner} extended their championship lead with a flawless performance, showing why they're the driver to beat this season.",
                            $"Championship leader {winner} tightened their grip on the title with another clinical victory, putting pressure on their rivals.",
                            $"{winner} continues to set the pace, stretching their championship advantage with a display of pure dominance."
                        };
                        return youngChampLeaderVariants[random.Next(youngChampLeaderVariants.Length)];
                    }
                    else
                    {
                        var youngChampChaserVariants = new[]
                        {
                            $"{winner} fought back brilliantly, moving to P{newPosition} in the standings with a statement victory.",
                            $"Refusing to surrender, {winner} claimed a crucial win and climbed to P{newPosition}, keeping championship hopes alive.",
                            $"{winner} struck back with authority, rising to P{newPosition} with a victory that reshapes the title battle."
                        };
                        return youngChampChaserVariants[random.Next(youngChampChaserVariants.Length)];
                    }

                case DriverReputation.PRIME_MIDFIELD:
                    var primeMidfieldVariants = new[]
                    {
                        $"{winner} delivered the drive of their career, elevating themselves and {team} with an unexpected but thoroughly deserved victory.",
                        $"Lightning struck as {winner} seized their moment of glory, rewarding {team}'s faith with a fairytale triumph.",
                        $"{winner} broke through in spectacular fashion, claiming a maiden victory that validates years of solid midfield performances."
                    };
                    return primeMidfieldVariants[random.Next(primeMidfieldVariants.Length)];

                case DriverReputation.PRIME_STRONG_MIDFIELD:
                    var primeStrongMidfieldVariants = new[]
                    {
                        $"{winner} seized the opportunity with both hands, converting {team}'s strong package into a memorable triumph.",
                        $"Years of consistency paid off as {winner} delivered a polished victory, proving they belong among the elite.",
                        $"{winner} graduated from solid performer to race winner, giving {team} a triumph built on unwavering reliability."
                    };
                    return primeStrongMidfieldVariants[random.Next(primeStrongMidfieldVariants.Length)];

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    if (newPosition <= 3)
                    {
                        var primeChampUnprovenTopVariants = new[]
                        {
                            $"{winner} moved into championship contention at P{newPosition}, delivering the breakthrough performance that could define their season.",
                            $"Now at P{newPosition}, {winner} has firmly entered the title conversation with a victory that showcased championship credentials.",
                            $"{winner} announced their arrival as a genuine title threat, climbing to P{newPosition} with a commanding display of speed and racecraft."
                        };
                        return primeChampUnprovenTopVariants[random.Next(primeChampUnprovenTopVariants.Length)];
                    }
                    else
                    {
                        var primeChampUnprovenVariants = new[]
                        {
                            $"{winner} announced their title intentions with authority, moving to P{newPosition} after a masterclass in race craft.",
                            $"A statement victory propels {winner} to P{newPosition}, proving they have the speed to challenge for the championship.",
                            $"{winner} delivered a performance worthy of champions, rising to P{newPosition} with a display of pure class."
                        };
                        return primeChampUnprovenVariants[random.Next(primeChampUnprovenVariants.Length)];
                    }

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                    if (newPosition == 1)
                    {
                        var primeChampLeaderVariants = new[]
                        {
                            $"Championship leader {winner} demonstrated why they're at the top, extending their advantage with another clinical victory.",
                            $"{winner} maintained their stranglehold on the championship, adding another win to their growing tally with ruthless efficiency.",
                            $"Unstoppable at the front, {winner} stretched their championship lead with a masterful performance that demoralized the opposition."
                        };
                        return primeChampLeaderVariants[random.Next(primeChampLeaderVariants.Length)];
                    }
                    else
                    {
                        var primeChampChaserVariants = new[]
                        {
                            $"{winner} refused to give up the fight, moving to P{newPosition} with a champion's drive that keeps their title hopes alive.",
                            $"The championship battle intensifies as {winner} strikes back, climbing to P{newPosition} with a victory that keeps the pressure on.",
                            $"{winner} demonstrated true champion mentality, fighting to P{newPosition} with a win that reignites their title challenge."
                        };
                        return primeChampChaserVariants[random.Next(primeChampChaserVariants.Length)];
                    }

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED:
                    var primeChampWashedVariants = new[]
                    {
                        $"{winner} turned back the clock with a vintage performance, proving there's still fire in the belly of this former champion.",
                        $"Reports of {winner}'s decline were greatly exaggerated, as the former champion delivered a reminder of their championship pedigree.",
                        $"{winner} silenced the critics with a commanding victory, showing flashes of the brilliance that once dominated the sport."
                    };
                    return primeChampWashedVariants[random.Next(primeChampWashedVariants.Length)];

                case DriverReputation.AGEING_MIDFIELD:
                    var ageingMidfieldVariants = new[]
                    {
                        $"Veteran {winner} showed that experience counts, delivering a calculated drive that maximized every opportunity.",
                        $"{winner} proved there's life in the old dog yet, combining cunning with speed to claim an unlikely victory.",
                        $"Age and treachery beat youth and skill as {winner} crafted a tactical masterpiece to secure an impressive win."
                    };
                    return ageingMidfieldVariants[random.Next(ageingMidfieldVariants.Length)];

                case DriverReputation.AGEING_STRONG_MIDFIELD:
                    var ageingStrongMidfieldVariants = new[]
                    {
                        $"{winner} combined experience with pace to perfection, adding another chapter to an illustrious career with a well-earned victory.",
                        $"The veteran showed the youngsters how it's done, with {winner} delivering a clinic in racecraft and precision.",
                        $"{winner} demonstrated that class is permanent, claiming a victory that proves experience remains invaluable in Formula 1."
                    };
                    return ageingStrongMidfieldVariants[random.Next(ageingStrongMidfieldVariants.Length)];

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                    if (newPosition <= 2)
                    {
                        var ageingChampTopVariants = new[]
                        {
                            $"{winner} proved age is just a number, moving to P{newPosition} in the standings with a performance that belied their years.",
                            $"Still competitive at P{newPosition}, {winner} showed the younger generation they're far from finished with a commanding victory.",
                            $"The old master remains at the sharp end, with {winner} rising to P{newPosition} through a display of timeless racecraft."
                        };
                        return ageingChampTopVariants[random.Next(ageingChampTopVariants.Length)];
                    }
                    else
                    {
                        var ageingChampVariants = new[]
                        {
                            $"The veteran {winner} showed the youngsters how it's done, claiming victory with the racecraft that comes only with years of experience.",
                            $"{winner} demonstrated that speed isn't everything, using decades of knowledge to outthink and outrace the opposition.",
                            $"Experience triumphed over youth as {winner} delivered a masterclass, proving their championship mettle remains intact."
                        };
                        return ageingChampVariants[random.Next(ageingChampVariants.Length)];
                    }

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                    var ageingChampWashedVariants = new[]
                    {
                        $"In a stunning resurgence, {winner} recaptured past glory with a victory that shocked the paddock and delighted fans.",
                        $"Like a phoenix from the ashes, {winner} rose to claim an emotional victory that reignited memories of championship glory.",
                        $"{winner} wound back the years with a performance that proved champions never forget how to win, regardless of recent struggles."
                    };
                    return ageingChampWashedVariants[random.Next(ageingChampWashedVariants.Length)];

                case DriverReputation.JUST_ONE_LAST_DANCE:
                    var lastDanceVariants = new[]
                    {
                        $"In what may be their final season, {winner} delivered a poignant reminder of their brilliance, savoring a victory that transcends the championship standings.",
                        $"{winner} added another golden memory to a storied career, claiming a bittersweet victory in their swansong season.",
                        $"Racing against the sunset, {winner} proved the magic hasn't faded, delivering a triumphant farewell performance that fans will treasure forever."
                    };
                    return lastDanceVariants[random.Next(lastDanceVariants.Length)];

                default:
                    var defaultVariants = new[]
                    {
                        $"{winner} claimed a well-deserved victory for {team}, moving to P{newPosition} in the championship standings.",
                        $"A commanding performance from {winner} secured victory for {team}, advancing to P{newPosition} in the points.",
                        $"{winner} delivered a polished drive to win for {team}, now sitting at P{newPosition} in the championship."
                    };
                    return defaultVariants[random.Next(defaultVariants.Length)];
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
                        $"Despite questions about their credentials, {winner} delivered a flawless performance that silenced doubters. " +
                        $"The {team} driver controlled the race from start to finish, demonstrating that talent, not just funding, earned them this result.",
                        $"{winner} answered every criticism with pace and precision today. The {team} driver's racecraft was exemplary, " +
                        $"proving conclusively that they deserve their place on merit alone.",
                        $"Money may open doors, but {winner} showed that only skill wins races. The {team} driver's performance was clinical, " +
                        $"leaving no doubt that speed, not sponsorship, delivered this triumph."
                    };
                    return payDriverVariants[random.Next(payDriverVariants.Length)];

                case DriverReputation.YOUNG_TALENT:
                    var youngTalentVariants = new[]
                    {
                        $"The youngster showed maturity beyond their years, making no mistakes under pressure. " +
                        $"While there's still room to grow, this performance suggests {winner} could be a future championship contender.",
                        $"{winner} handled the pressure of leading like a seasoned veteran, never putting a wheel wrong. " +
                        $"This breakthrough victory marks the arrival of a major talent on the world stage.",
                        $"Raw speed met composure under pressure as {winner} delivered a faultless performance. " +
                        $"The {team} driver's potential is clear - this may be just the first of many victories to come."
                    };
                    return youngTalentVariants[random.Next(youngTalentVariants.Length)];

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    var youngChampVariants = new[]
                    {
                        $"{winner}'s pace was relentless from lights to flag. The {team} driver handled pressure from behind with composure, " +
                        $"executing a race strategy that maximized their machinery's potential.",
                        $"From pole to checkered flag, {winner} dominated with a display of pure speed. The {team} driver left nothing to chance, " +
                        $"controlling the race with the confidence of a future champion.",
                        $"{winner} made it look easy, but this victory required perfection. Every overtake, every defensive move, " +
                        $"every stint was executed flawlessly by the {team} driver who continues to impress."
                    };
                    return youngChampVariants[random.Next(youngChampVariants.Length)];

                case DriverReputation.PRIME_MIDFIELD:
                case DriverReputation.PRIME_STRONG_MIDFIELD:
                    var primeMidfieldVariants = new[]
                    {
                        $"This victory represents the culmination of years of solid performances. {winner} seized the moment when it arrived, " +
                        $"driving with a confidence that suggests this may not be their last trip to the top step.",
                        $"{winner} has consistently delivered strong results, and today those efforts were rewarded with ultimate glory. " +
                        $"The {team} driver's consistency and speed combined perfectly when it mattered most.",
                        $"Patience and persistence paid dividends as {winner} finally claimed the victory their performances have long deserved. " +
                        $"The {team} driver showed that steady improvement eventually reaches the summit."
                    };
                    return primeMidfieldVariants[random.Next(primeMidfieldVariants.Length)];

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                    var primeChampVariants = new[]
                    {
                        $"As expected from a driver of {winner}'s caliber, there were no gifts today. " +
                        $"Pure speed combined with strategic brilliance earned {team} a victory that looked comfortable but required perfection.",
                        $"{winner} dominated from start to finish with ruthless efficiency. The {team} driver gave a masterclass in race management, " +
                        $"controlling every aspect with championship-winning precision.",
                        $"This is what elite-level performance looks like. {winner} extracted every tenth from the {team} machinery, " +
                        $"combining qualifying pace with race management in a display that left rivals with no answers."
                    };
                    return primeChampVariants[random.Next(primeChampVariants.Length)];

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED:
                    var primeChampWashedVariants = new[]
                    {
                        $"Critics who wrote off {winner} were silenced today. The {team} driver showed flashes of the form that once made them champion, " +
                        $"suggesting rumors of their decline may have been premature.",
                        $"{winner} reminded everyone why they were once at the top, delivering a performance that recaptured their peak form. " +
                        $"The speed and racecraft that defined their championship years returned when it mattered most.",
                        $"Perhaps reports of {winner}'s demise were exaggerated. The {team} driver rolled back the years with a vintage display, " +
                        $"proving the instincts of a champion never truly disappear."
                    };
                    return primeChampWashedVariants[random.Next(primeChampWashedVariants.Length)];

                case DriverReputation.AGEING_MIDFIELD:
                case DriverReputation.AGEING_STRONG_MIDFIELD:
                    var ageingMidfieldVariants = new[]
                    {
                        $"Experience proved invaluable as {winner} navigated a complex race with veteran savvy. " +
                        $"Where younger drivers might have made mistakes, the {team} driver's composure was key to victory.",
                        $"{winner} demonstrated that racecraft improves with age. The {team} driver read the race perfectly, " +
                        $"positioning themselves ideally at every critical moment through superior tactical awareness.",
                        $"While the young guns brought speed, {winner} brought wisdom. The {team} veteran exploited every opportunity with the cunning " +
                        $"that comes only from years of experience at the highest level."
                    };
                    return ageingMidfieldVariants[random.Next(ageingMidfieldVariants.Length)];

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                    var ageingChampVariants = new[]
                    {
                        $"Age may bring physical challenges, but {winner} demonstrated that racecraft and strategic thinking can overcome pure speed. " +
                        $"This victory adds another highlight to an already storied career.",
                        $"{winner} showed that champions are made of more than just reflexes. The {team} driver's ability to read a race " +
                        $"and execute perfectly under pressure proved that experience remains Formula 1's most valuable asset.",
                        $"The veteran {winner} delivered a clinic in race management, proving that decades of knowledge trump raw speed. " +
                        $"Every decision, every defensive move was executed with the confidence of a driver who's seen it all before."
                    };
                    return ageingChampVariants[random.Next(ageingChampVariants.Length)];

                case DriverReputation.JUST_ONE_LAST_DANCE:
                    var lastDanceVariants = new[]
                    {
                        $"Racing against time in their farewell season, {winner} showed they still have the fire and skill that defined their career. " +
                        $"Every lap, every corner carried the weight of finality, yet the {team} driver performed with the joy of someone who knows each moment counts. " +
                        $"This is racing at its most pure - a champion refusing to go quietly into the night.",
                        $"There's something magical about {winner}'s swansong season. Each race carries extra meaning, and today the veteran delivered " +
                        $"a performance filled with the passion of someone savoring every final moment. The {team} driver proved that legends never lose their touch.",
                        $"In their final act, {winner} reminded us all why we fell in love with their racing. The {team} driver raced with freedom and joy, " +
                        $"unburdened by championship pressure, producing a pure display of skill that will be remembered long after they hang up the helmet."
                    };
                    return lastDanceVariants[random.Next(lastDanceVariants.Length)];

                default:
                    var defaultVariants = new[]
                    {
                        $"{winner} drove with precision and pace throughout, never putting a wheel wrong on the way to a commanding victory.",
                        $"A professional performance from {winner} saw the {team} driver control the race with consistent pace and smart strategy.",
                        $"{winner} delivered exactly what {team} needed - a clean, mistake-free drive that maximized the machinery's potential."
                    };
                    return defaultVariants[random.Next(defaultVariants.Length)];
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
                        $"{winner} claims the championship title with this season-ending victory, capping a dominant campaign in style. " +
                        $"The title may have been inevitable, but the final flourish underscores their superiority throughout the year.",

                        $"The championship is sealed as {winner} takes the checkered flag in the season finale. " +
                        $"This victory crowns a remarkable year and cements their place in the record books.",

                        $"With this final victory, {winner} concludes a championship-winning season that will be remembered for years to come. " +
                        $"The title triumph is complete, the legacy secured."
                    };
                    var random = new Random();
                    return championClinchVariants[random.Next(championClinchVariants.Length)];
                }
                else if (newPosition <= 3)
                {
                    return $"{winner} finishes the season in P{newPosition}, a respectable final standing that reflects consistent performances throughout the year. " +
                           $"While the title eluded them, there's much to build on for next season.";
                }
                else
                {
                    return $"{winner} concludes the championship in P{newPosition}, ending the season on a high note with this victory. " +
                           $"Though the final standings may not reflect it, today's performance shows promise for the future.";
                }
            }

            // For the first race, focus on taking the early lead rather than position changes
            if (isFirstRace)
            {
                if (newPosition == 1)
                {
                    return $"{winner} takes the early championship lead, drawing first blood in what promises to be a season-long battle. " +
                           $"The momentum from this opening victory could prove crucial as the championship unfolds.";
                }
                else if (newPosition <= 3)
                {
                    return $"{winner} starts the championship in P{newPosition}, a solid opening result that keeps them in early contention. " +
                           $"With a long season ahead, this foundation could prove valuable in the title fight.";
                }
                else
                {
                    return $"{winner} opens their championship account with a victory from P{newPosition}, proving that early points can come from anywhere. " +
                           $"This result establishes them as a dark horse in the championship battle.";
                }
            }

            // Existing logic for subsequent races
            int positionChange = previousPosition - newPosition;

            if (newPosition == 1)
            {
                if (previousPosition == 1)
                {
                    return $"With this victory, {winner} extends their championship lead, putting further pressure on rivals to respond. " +
                           $"The momentum is firmly with them as the season progresses.";
                }
                else
                {
                    return $"This result propels {winner} to the top of the championship standings - " +
                           $"From P{previousPosition} to P1 - a remarkable turnaround that reshapes the title battle.";
                }
            }
            else if (positionChange > 0)
            {
                return $"The victory elevates {winner} from P{previousPosition} to P{newPosition} in the championship, " +
                       $"keeping them firmly in the hunt for the title with races still to come.";
            }
            else if (positionChange < 0)
            {
                return $"Despite the win, {winner} slips from P{previousPosition} to P{newPosition} in the standings, " +
                       $"highlighting the fierce competition at the top of the championship table.";
            }
            else
            {
                return $"{winner} maintains their P{newPosition} position in the championship, but this victory adds crucial points " +
                       $"and momentum in what promises to be a closely fought title battle.";
            }
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}