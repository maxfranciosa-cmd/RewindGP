using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Extensions;
using AMS2ChEd.Resources;
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
            IGameDataFactory storageFactory,
            ISeason newSeason)
        {
            InitializeComponent();

            DateText.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            HeadlineText.Text = string.Format(Strings.NewSeasonRosterWindow_Headline_Format, newSeason.Year);

            // Generate intro text
            IntroText.Text = string.Format(Strings.NewSeasonRosterWindow_Intro_Format, newSeason.Year);

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
                string teamName = team.TeamName ?? teamData?.TeamName ?? Strings.NewSeasonRosterWindow_DefaultTeamName;

                var driver1 = driversDictionary[team.Driver1Contract.DriverId];

                // a team with no second car this season has an empty Driver2Contract.DriverId
                var driver2Id = team.Driver2Contract?.DriverId;
                var driver2 = string.IsNullOrEmpty(driver2Id) ? null : driversDictionary.GetValueOrDefault(driver2Id);

                // Check if lineup is unchanged from previous season
                bool isUnchanged = IsLineupUnchanged(previousSeason, team.TeamId,
                    team.Driver1Contract.DriverId, driver2Id);

                // Get driver reputations
                var driver1Reputation = driver1?.Reputation;
                var driver2Reputation = driver2?.Reputation;

                string statusText;
                string descriptionText;
                string driversText;
                var portraitPathDriver1 = driver1?.PictureUrl;
                string portraitPathDriver2 = null;

                if (driver2 != null)
                {
                    statusText = BuildStatusText(team.Driver1Contract.DriverId, driver2Id,
                        driver1.Name, driver2.Name, championDriverId, runnerUpDriverId, isUnchanged, constructorChampionTeamId);
                    descriptionText = BuildDescriptionText(driver1.Name, driver2.Name,
                        driver1Reputation, driver2Reputation);
                    driversText = string.Format(Strings.NewSeasonRosterWindow_DriversText_Format, driver1.Name, driver2.Name);
                    portraitPathDriver2 = driver2.PictureUrl;
                }
                else
                {
                    statusText = BuildSoloStatusText(team.Driver1Contract.DriverId, driver1.Name, championDriverId, runnerUpDriverId, isUnchanged);
                    descriptionText = BuildSoloDescriptionText(driver1.Name, driver1Reputation);
                    driversText = driver1.Name;
                }

                rosterItems.Add(new TeamRosterItem
                {
                    TeamName = teamName,
                    DriversText = driversText,
                    StatusText = statusText,
                    StatusTextVisibility = string.IsNullOrEmpty(statusText) ? Visibility.Collapsed : Visibility.Visible,
                    DescriptionText = descriptionText,
                    Driver1Portrait = LoadDriverPortrait(portraitPathDriver1),
                    Driver2Portrait = LoadDriverPortrait(portraitPathDriver2)
                });
            }

            RosterList.ItemsSource = rosterItems;

            // Generate closing text
            ClosingText.Text = string.Format(Strings.NewSeasonRosterWindow_Closing_Format, newSeason.Year);
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
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_UnchangedChampion_Format, champName));
                return narratives[_random.Next(narratives.Count)];
            }

            if (isUnchanged && (driver1IsRunnerUp || driver2IsRunnerUp))
            {
                string runnerUpName = driver1IsRunnerUp ? driver1Name : driver2Name;
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_UnchangedRunnerUp_Format, runnerUpName));
                return narratives[_random.Next(narratives.Count)];
            }

            // Handle championship status with new partnerships
            if (driver1IsChamp && driver2IsRunnerUp)
            {
                return string.Format(Strings.NewSeasonRosterWindow_Status_ChampPartnersRunnerUp_Format, driver1Name, driver2Name);
            }

            if (driver2IsChamp && driver1IsRunnerUp)
            {
                return string.Format(Strings.NewSeasonRosterWindow_Status_ChampPartnersRunnerUp_Format, driver2Name, driver1Name);
            }

            if (driver1IsChamp)
            {
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_ChampNewPartnership1_Format, driver1Name, driver2Name));
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_ChampNewPartnership2_Format, driver1Name, driver2Name));
                return narratives[_random.Next(narratives.Count)];
            }

            if (driver2IsChamp)
            {
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_ChampNewPartnership1_Format, driver2Name, driver1Name));
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_ChampNewPartnership2_Format, driver2Name, driver1Name));
                return narratives[_random.Next(narratives.Count)];
            }

            if (driver1IsRunnerUp)
            {
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_RunnerUpNewPartnership1_Format, driver1Name, driver2Name));
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_RunnerUpNewPartnership2_Format, driver1Name, driver2Name));
                return narratives[_random.Next(narratives.Count)];
            }

            if (driver2IsRunnerUp)
            {
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_RunnerUpNewPartnership1_Format, driver2Name, driver1Name));
                narratives.Add(string.Format(Strings.NewSeasonRosterWindow_Status_RunnerUpNewPartnership2_Format, driver2Name, driver1Name));
                return narratives[_random.Next(narratives.Count)];
            }

            // Just unchanged lineup
            if (isUnchanged)
            {
                narratives.Add(Strings.NewSeasonRosterWindow_Status_UnchangedGeneric1);
                narratives.Add(Strings.NewSeasonRosterWindow_Status_UnchangedGeneric2);
                narratives.Add(Strings.NewSeasonRosterWindow_Status_UnchangedGeneric3);
                return narratives[_random.Next(narratives.Count)];
            }

            // No special status to report
            return "";
        }

        private string BuildSoloStatusText(string driverId, string driverName, string championId, string runnerUpId, bool isUnchanged)
        {
            if (driverId == championId)
                return string.Format(Strings.NewSeasonRosterWindow_SoloStatus_Champion_Format, driverName);

            if (driverId == runnerUpId)
                return string.Format(Strings.NewSeasonRosterWindow_SoloStatus_RunnerUp_Format, driverName);

            if (isUnchanged)
                return string.Format(Strings.NewSeasonRosterWindow_SoloStatus_Unchanged_Format, driverName);

            return "";
        }

        private string BuildSoloDescriptionText(string driverName, DriverReputation? rep)
        {
            return rep.HasValue
                ? string.Format(Strings.NewSeasonRosterWindow_SoloDescription_WithRep_Format, driverName, GetReputationDescription(rep.Value))
                : string.Format(Strings.NewSeasonRosterWindow_SoloDescription_NoRep_Format, driverName);
        }

        private string BuildDescriptionText(string driver1Name, string driver2Name,
            DriverReputation? driver1Rep, DriverReputation? driver2Rep)
        {
            // Build a flowing narrative sentence combining both drivers
            if (!driver1Rep.HasValue && !driver2Rep.HasValue)
                return string.Format(Strings.NewSeasonRosterWindow_Description_NoReps_Format, driver1Name, driver2Name);

            if (!driver1Rep.HasValue)
                return string.Format(Strings.NewSeasonRosterWindow_Description_Driver1NoRep_Format, driver1Name, driver2Name, GetReputationDescription(driver2Rep.Value));

            if (!driver2Rep.HasValue)
                return string.Format(Strings.NewSeasonRosterWindow_Description_Driver2NoRep_Format, driver1Name, GetReputationDescription(driver1Rep.Value), driver2Name);

            // Both have reputations - create a flowing sentence about the pairing
            return BuildPairingNarrative(driver1Name, driver2Name, driver1Rep.Value, driver2Rep.Value);
        }

        private string BuildPairingNarrative(string driver1Name, string driver2Name,
            DriverReputation driver1Rep, DriverReputation driver2Rep)
        {
            string rep1 = GetReputationDescription(driver1Rep);
            string rep2 = GetReputationDescription(driver2Rep);

            var narratives = new List<string>
            {
                string.Format(Strings.NewSeasonRosterWindow_Pairing1_Format, driver1Name, rep1, driver2Name, rep2),
                string.Format(Strings.NewSeasonRosterWindow_Pairing2_Format, driver1Name, rep1, driver2Name, rep2),
                string.Format(Strings.NewSeasonRosterWindow_Pairing3_Format, driver1Name, rep1, driver2Name, rep2)
            };

            return narratives[_random.Next(narratives.Count)];
        }

        // Bare 3rd-person-singular verb-phrase fragments, no leading "who"/"che" and no trailing
        // period - the composing templates above (SoloDescription/Description/Pairing) decide
        // whether to prepend a relative pronoun and always supply the closing punctuation. This
        // is what lets the same fragment slot into "{name}, who {fragment}." and "{name} {fragment},"
        // in both English and Italian without the fragment needing two different grammatical forms.
        private string GetReputationDescription(DriverReputation reputation)
        {
            var descriptions = new Dictionary<DriverReputation, List<string>>
            {
                [DriverReputation.PAY_DRIVER_WILD_CARD] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_PayDriverWildCard1,
                    Strings.NewSeasonRosterWindow_RepDesc_PayDriverWildCard2,
                    Strings.NewSeasonRosterWindow_RepDesc_PayDriverWildCard3
                },
                [DriverReputation.PAY_DRIVER_SEASON] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_PayDriverSeason1,
                    Strings.NewSeasonRosterWindow_RepDesc_PayDriverSeason2,
                    Strings.NewSeasonRosterWindow_RepDesc_PayDriverSeason3
                },
                [DriverReputation.AGEING_MIDFIELD] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingMidfield1,
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingMidfield2,
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingMidfield3
                },
                [DriverReputation.YOUNG_TALENT] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_YoungTalent1,
                    Strings.NewSeasonRosterWindow_RepDesc_YoungTalent2,
                    Strings.NewSeasonRosterWindow_RepDesc_YoungTalent3
                },
                [DriverReputation.PRIME_MIDFIELD] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeMidfield1,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeMidfield2,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeMidfield3
                },
                [DriverReputation.AGEING_STRONG_MIDFIELD] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingStrongMidfield1,
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingStrongMidfield2,
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingStrongMidfield3
                },
                [DriverReputation.PRIME_STRONG_MIDFIELD] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeStrongMidfield1,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeStrongMidfield2,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeStrongMidfield3
                },
                [DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingChampionshipWashed1,
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingChampionshipWashed2,
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingChampionshipWashed3
                },
                [DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeChampionshipWashed1,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeChampionshipWashed2,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeChampionshipWashed3
                },
                [DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeChampionshipUnproven1,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeChampionshipUnproven2,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeChampionshipUnproven3
                },
                [DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_YoungChampionshipUnproven1,
                    Strings.NewSeasonRosterWindow_RepDesc_YoungChampionshipUnproven2,
                    Strings.NewSeasonRosterWindow_RepDesc_YoungChampionshipUnproven3
                },
                [DriverReputation.AGEING_CHAMPIONSHIP_LEVEL] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingChampionship1,
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingChampionship2,
                    Strings.NewSeasonRosterWindow_RepDesc_AgeingChampionship3
                },
                [DriverReputation.PRIME_CHAMPIONSHIP_LEVEL] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeChampionship1,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeChampionship2,
                    Strings.NewSeasonRosterWindow_RepDesc_PrimeChampionship3
                },
                [DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_YoungChampionship1,
                    Strings.NewSeasonRosterWindow_RepDesc_YoungChampionship2,
                    Strings.NewSeasonRosterWindow_RepDesc_YoungChampionship3
                },
                [DriverReputation.JUST_ONE_LAST_DANCE] = new List<string>
                {
                    Strings.NewSeasonRosterWindow_RepDesc_LastDance1,
                    Strings.NewSeasonRosterWindow_RepDesc_LastDance2,
                    Strings.NewSeasonRosterWindow_RepDesc_LastDance3,
                    Strings.NewSeasonRosterWindow_RepDesc_LastDance4,
                    Strings.NewSeasonRosterWindow_RepDesc_LastDance5
                }
            };

            if (descriptions.ContainsKey(reputation))
            {
                var options = descriptions[reputation];
                return options[_random.Next(options.Count)];
            }

            return Strings.NewSeasonRosterWindow_RepDesc_Default;
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