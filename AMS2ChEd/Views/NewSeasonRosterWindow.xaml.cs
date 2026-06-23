using Ams2ChEd.Business.AMS2.DependencyInjection;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AMS2ChEd.Views
{
    public class TeamRosterItem
    {
        public string TeamName { get; set; }
        public string DriversText { get; set; }
        public string StatusText { get; set; }
        public Visibility StatusTextVisibility { get; set; }
        public string DescriptionText { get; set; }
        public BitmapImage Driver1Portrait { get; set; }
        public BitmapImage Driver2Portrait { get; set; }
    }

    public partial class NewSeasonRosterWindow : Window
    {
        private Random _random = new Random();

        public NewSeasonRosterWindow(
            ISaveGame saveGame,
            Ams2StorageFactory storageFactory,
            ISeason newSeason)
        {
            InitializeComponent();

            DateText.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            HeadlineText.Text = $"THE {newSeason.Year} SEASON LINEUP REVEALED";

            // Generate intro text
            IntroText.Text = $"As pre-season testing approaches, teams have finalized their driver lineups for the {newSeason.Year} " +
                           $"season. The off-season saw dramatic movements across the grid, with several drivers finding new homes " +
                           $"and fresh partnerships forming. Here's the complete roster that will contest this year's championship:";

            // Get previous season for comparison (if exists)
            ISeason previousSeason = saveGame.CurrentSeason;
            string championDriverId = GetChampionDriverId(saveGame);
            string runnerUpDriverId = GetRunnerUpDriverId(saveGame);
            string constructorChampionTeamId = saveGame.CurrentConstructorStandings.FirstOrDefault(c => c.Position == 1)?.TeamId ?? "";
            var driversDictionary = saveGame.Drivers.ToDictionary(d => d.DriverId, d => d);

            // Generate roster list
            var teamsCache = storageFactory.TeamsLoader.LoadTeams();
            var rosterItems = new List<TeamRosterItem>();

            foreach (var team in newSeason.Teams.OrderByDescending(t => t.Reputation))
            {
                var teamData = teamsCache.ContainsKey(team.TeamId) ? teamsCache[team.TeamId] : null;
                string teamName = team.TeamName ?? teamData?.TeamName ?? "Unknown Team";

                var driver1 = driversDictionary[team.Driver1Contract.DriverId];
                var driver2 = driversDictionary[team.Driver2Contract.DriverId];

                // Check if lineup is unchanged from previous season
                bool isUnchanged = IsLineupUnchanged(previousSeason, team.TeamId,
                    team.Driver1Contract.DriverId, team.Driver2Contract.DriverId);

                // Get driver reputations
                var driver1Reputation = driver1?.Reputation;
                var driver2Reputation = driver2?.Reputation;

                // Build status text
                string statusText = BuildStatusText(team.Driver1Contract.DriverId, team.Driver2Contract.DriverId,
                    driver1.Name, driver2.Name, championDriverId, runnerUpDriverId, isUnchanged, constructorChampionTeamId);

                // Build description text
                string descriptionText = BuildDescriptionText(driver1.Name, driver2.Name,
                    driver1Reputation, driver2Reputation);

                var portraitPathDriver1 = driver1?.PictureUrl;
                var portraitPathDriver2 = driver2?.PictureUrl;

                rosterItems.Add(new TeamRosterItem
                {
                    TeamName = teamName,
                    DriversText = $"{driver1.Name}  •  {driver2.Name}",
                    StatusText = statusText,
                    StatusTextVisibility = string.IsNullOrEmpty(statusText) ? Visibility.Collapsed : Visibility.Visible,
                    DescriptionText = descriptionText,
                    Driver1Portrait = LoadDriverPortrait(portraitPathDriver1),
                    Driver2Portrait = LoadDriverPortrait(portraitPathDriver2)
                });
            }

            RosterList.ItemsSource = rosterItems;

            // Generate closing text
            ClosingText.Text = $"With the grid now set, anticipation builds for the season opener. " +
                             $"New partnerships will be tested, rivalries renewed, and championship dreams pursued. " +
                             $"The {newSeason.Year} season promises to be one of the most competitive in recent memory.";
        }

        private string GetChampionDriverId(ISaveGame saveGame)
        {
            if (saveGame.CurrentDriverStandings == null || !saveGame.CurrentDriverStandings.Any()) return null;
            return saveGame.CurrentDriverStandings.First(s => s.Position == 1).DriverId;
        }

        private string GetRunnerUpDriverId(ISaveGame saveGame)
        {
            if (saveGame.CurrentDriverStandings == null || !saveGame.CurrentDriverStandings.Any()) return null;
            return saveGame.CurrentDriverStandings.First(s => s.Position == 2).DriverId;
        }

        private bool IsLineupUnchanged(ISeason previousSeason, string teamId, string driver1Id, string driver2Id)
        {
            if (previousSeason == null) return false;

            var previousTeam = previousSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            if (previousTeam == null) return false;

            return (previousTeam.Driver1Contract.DriverId == driver1Id && previousTeam.Driver2Contract.DriverId == driver2Id) ||
                   (previousTeam.Driver1Contract.DriverId == driver2Id && previousTeam.Driver2Contract.DriverId == driver1Id);
        }

        private string BuildStatusText(string driver1Id, string driver2Id, string driver1Name, string driver2Name,
            string championId, string runnerUpId, bool isUnchanged, string constructorChampionTeamId)
        {
            bool driver1IsChamp = driver1Id == championId;
            bool driver2IsChamp = driver2Id == championId;
            bool driver1IsRunnerUp = driver1Id == runnerUpId;
            bool driver2IsRunnerUp = driver2Id == runnerUpId;

            // Build flowing narrative based on the combination of facts
            var narratives = new List<string>();

            // Handle championship status combined with unchanged lineup
            if (isUnchanged && (driver1IsChamp || driver2IsChamp))
            {
                string champName = driver1IsChamp ? driver1Name : driver2Name;
                narratives.Add($"The defending champion {champName} continues with an unchanged lineup from last season.");
                return narratives[_random.Next(narratives.Count)];
            }

            if (isUnchanged && (driver1IsRunnerUp || driver2IsRunnerUp))
            {
                string runnerUpName = driver1IsRunnerUp ? driver1Name : driver2Name;
                narratives.Add($"Last season's runner-up {runnerUpName} remains with the same teammate for another campaign.");
                return narratives[_random.Next(narratives.Count)];
            }

            // Handle championship status with new partnerships
            if (driver1IsChamp && driver2IsRunnerUp)
            {
                return $"In a remarkable partnership, defending champion {driver1Name} joins forces with last season's runner-up {driver2Name}.";
            }

            if (driver2IsChamp && driver1IsRunnerUp)
            {
                return $"In a remarkable partnership, defending champion {driver2Name} joins forces with last season's runner-up {driver1Name}.";
            }

            if (driver1IsChamp)
            {
                narratives.Add($"Defending champion {driver1Name} begins a new chapter with a fresh partnership alongside {driver2Name}.");
                narratives.Add($"The title-holder {driver1Name} forms a new alliance with {driver2Name} for the upcoming season.");
                return narratives[_random.Next(narratives.Count)];
            }

            if (driver2IsChamp)
            {
                narratives.Add($"Defending champion {driver2Name} begins a new chapter with a fresh partnership alongside {driver1Name}.");
                narratives.Add($"The title-holder {driver2Name} forms a new alliance with {driver1Name} for the upcoming season.");
                return narratives[_random.Next(narratives.Count)];
            }

            if (driver1IsRunnerUp)
            {
                narratives.Add($"Last season's runner-up {driver1Name} looks to go one better with new teammate {driver2Name}.");
                narratives.Add($"Having finished second last year, {driver1Name} seeks redemption alongside new partner {driver2Name}.");
                return narratives[_random.Next(narratives.Count)];
            }

            if (driver2IsRunnerUp)
            {
                narratives.Add($"Last season's runner-up {driver2Name} looks to go one better with new teammate {driver1Name}.");
                narratives.Add($"Having finished second last year, {driver2Name} seeks redemption alongside new partner {driver1Name}.");
                return narratives[_random.Next(narratives.Count)];
            }

            // Just unchanged lineup
            if (isUnchanged)
            {
                narratives.Add("Continuity prevails as both drivers return for another season together.");
                narratives.Add("The partnership remains intact with both drivers retained for the new campaign.");
                narratives.Add("Stability defines this lineup as the team keeps faith with both drivers.");
                return narratives[_random.Next(narratives.Count)];
            }

            // No special status to report
            return "";
        }

        private string BuildDescriptionText(string driver1Name, string driver2Name,
            DriverReputation? driver1Rep, DriverReputation? driver2Rep)
        {
            // Build a flowing narrative sentence combining both drivers
            if (!driver1Rep.HasValue && !driver2Rep.HasValue)
                return $"{driver1Name} and {driver2Name} join forces for the upcoming campaign.";

            if (!driver1Rep.HasValue)
                return $"{driver1Name} partners with {driver2Name}, who {GetReputationDescription(driver2Rep.Value)}";

            if (!driver2Rep.HasValue)
                return $"{driver1Name}, who {GetReputationDescription(driver1Rep.Value)}, is joined by {driver2Name}.";

            // Both have reputations - create a flowing sentence about the pairing
            return BuildPairingNarrative(driver1Name, driver2Name, driver1Rep.Value, driver2Rep.Value);
        }

        private string BuildPairingNarrative(string driver1Name, string driver2Name,
            DriverReputation driver1Rep, DriverReputation driver2Rep)
        {
            var narratives = new List<string>
            {
                $"{driver1Name}, who {GetReputationDescription(driver1Rep)}, partners with {driver2Name}, who {GetReputationDescription(driver2Rep)}",
                $"The pairing of {driver1Name}, who {GetReputationDescription(driver1Rep)}, alongside {driver2Name}, who {GetReputationDescription(driver2Rep)}, forms an intriguing partnership.",
                $"{driver1Name} {GetReputationDescription(driver1Rep)}, while {driver2Name} {GetReputationDescription(driver2Rep)}"
            };

            return narratives[_random.Next(narratives.Count)];
        }

        private string GetReputationDescription(DriverReputation reputation)
        {
            var descriptions = new Dictionary<DriverReputation, List<string>>
            {
                [DriverReputation.PAY_DRIVER_WILD_CARD] = new List<string>
                {
                    "will step in as substitute whenever opportunities arise.",
                    "remains on standby without a guaranteed race seat.",
                    "hopes to prove their worth through substitute appearances."
                },
                [DriverReputation.PAY_DRIVER_SEASON] = new List<string>
                {
                    "brings crucial financial backing to secure their seat.",
                    "relies on sponsorship support to maintain their position.",
                    "combines commercial value with racing ambitions."
                },
                [DriverReputation.AGEING_MIDFIELD] = new List<string>
                {
                    "brings veteran experience to the midfield battle.",
                    "looks to extend their career with consistent performances.",
                    "provides steady reliability in the midfield ranks."
                },
                [DriverReputation.YOUNG_TALENT] = new List<string>
                {
                    "arrives with high expectations and raw potential.",
                    "represents the next generation of racing talent.",
                    "aims to make an immediate impact in their breakthrough season."
                },
                [DriverReputation.PRIME_MIDFIELD] = new List<string>
                {
                    "is a proven midfield contender hitting their stride.",
                    "consistently able to deliver strong points finishes.",
                    "has established themselves as a reliable performer."
                },
                [DriverReputation.AGEING_STRONG_MIDFIELD] = new List<string>
                {
                    "combines years of experience with competitive pace.",
                    "remains a formidable force despite advancing years.",
                    "continues to extract maximum performance from the machinery."
                },
                [DriverReputation.PRIME_STRONG_MIDFIELD] = new List<string>
                {
                    "proved to be at peak performance in the midfield's upper echelons.",
                    "regularly challenges for podium positions.",
                    "has emerged as a serious contender for top results."
                },
                [DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED] = new List<string>
                {
                    "seeks to recapture past championship-winning form.",
                    "faces questions about whether they can rediscover their peak.",
                    "hopes experience can compensate for diminished pace."
                },
                [DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED] = new List<string>
                {
                    "looks to rebuild their reputation after recent struggles.",
                    "attempts to prove their championship credentials remain intact.",
                    "faces pressure to justify their elite status."
                },
                [DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN] = new List<string>
                {
                    "possesses championship-caliber talent waiting to be unleashed.",
                    "has shown flashes of brilliance but lacks consistency.",
                    "stands on the cusp of greatness with untapped potential."
                },
                [DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN] = new List<string>
                {
                    "bursts onto the scene with electrifying potential.",
                    "is widely tipped as a future world champion.",
                    "brings youthful exuberance and blistering pace."
                },
                [DriverReputation.AGEING_CHAMPIONSHIP_LEVEL] = new List<string>
                {
                    "remains a title contender despite advancing years.",
                    "combines championship experience with enduring competitiveness.",
                    "proves age is no barrier to championship ambitions."
                },
                [DriverReputation.PRIME_CHAMPIONSHIP_LEVEL] = new List<string>
                {
                    "enters as a genuine championship favorite.",
                    "operates at the absolute peak of their powers.",
                    "stands among the grid's elite title contenders."
                },
                [DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL] = new List<string>
                {
                    "combines youth with proven championship credentials.",
                    "has already demonstrated title-winning capability.",
                    "represents the sport's brightest star for years to come."
                },
                [DriverReputation.JUST_ONE_LAST_DANCE] = new List<string>
                {
                    "returns for one final season to end their storied career.",
                    "embarks on a farewell campaign to close an illustrious chapter.",
                    "seeks a fitting conclusion to their legendary tenure in the sport.",
                    "comes back for one last shot at glory before retirement.",
                    "makes a nostalgic return to say a proper goodbye to racing."
                }
            };

            if (descriptions.ContainsKey(reputation))
            {
                var options = descriptions[reputation];
                return options[_random.Next(options.Count)];
            }

            return "joins the grid for the upcoming season.";
        }

        private BitmapImage LoadDriverPortrait(string portraitPath)
        {
            try
            {
                return PictureUrlLoaderExtension.LoadBitmap(portraitPath);
            }
            catch (Exception ex)
            {
                // If image fails to load, return null
                System.Diagnostics.Debug.WriteLine($"Failed to load driver portrait: {ex.Message}");
                return null;
            }
        }

        private void StartSeasonButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}