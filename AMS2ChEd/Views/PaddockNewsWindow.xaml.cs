using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Resources;
using System;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// Paddock gossip after a race: team-orders reprimands, final warnings and drivers released
    /// by their teams, across the whole grid.
    /// </summary>
    public partial class PaddockNewsWindow : Window
    {
        private readonly Random _random = new Random();

        public PaddockNewsWindow(ISaveGame saveGame, IReadOnlyList<ReprimandNewsItem> items, DateTime raceDate, string grandPrixName)
        {
            InitializeComponent();

            DateText.Text = raceDate.ToString("dddd, MMMM dd, yyyy");
            HeadlineText.Text = BuildHeadline(saveGame, items, grandPrixName);
            ArticleText.Text = string.Join("\n\n", items.Select(i => BuildParagraph(saveGame, i)));
        }

        private string BuildHeadline(ISaveGame saveGame, IReadOnlyList<ReprimandNewsItem> items, string grandPrixName)
        {
            var headlineItem = items.FirstOrDefault(i => i.Consequence == ReprimandConsequence.RELEASED)
                ?? items.FirstOrDefault(i => i.IsPlayer);

            if (headlineItem == null)
                return string.Format(Strings.PaddockNewsWindow_Headline_Generic_Format, grandPrixName).ToUpper();

            var driverName = GetDriverName(saveGame, headlineItem.DriverId).ToUpper();
            var teamName = GetTeam(saveGame, headlineItem.TeamId)?.TeamName?.ToUpper() ?? Strings.PaddockNewsWindow_DefaultTeamName;

            return headlineItem.Consequence == ReprimandConsequence.RELEASED
                ? string.Format(Strings.PaddockNewsWindow_Headline_Released_Format, driverName, teamName)
                : string.Format(Strings.PaddockNewsWindow_Headline_Reprimand_Format, driverName, teamName);
        }

        // Argument order standardized as (driver, team, principal, teammate) for the reprimand
        // itself and (driver, team, principal, replacement) for its consequence, so each language's
        // resx template can reorder or drop any of them.
        private string BuildParagraph(ISaveGame saveGame, ReprimandNewsItem item)
        {
            var team = GetTeam(saveGame, item.TeamId);
            var driverName = GetDriverName(saveGame, item.DriverId);
            var teamName = team?.TeamName ?? Strings.PaddockNewsWindow_DefaultTeamName;
            var principal = string.IsNullOrEmpty(team?.TeamPrincipal) ? Strings.PaddockNewsWindow_DefaultTeamPrincipal : team.TeamPrincipal;
            var teammateName = GetDriverName(saveGame, item.TeammateId);

            var reprimandVariants = item.Reason == ReprimandReason.CONTACT_WITH_TEAMMATE
                ? new[] { Strings.PaddockNewsWindow_Contact1_Format, Strings.PaddockNewsWindow_Contact2_Format, Strings.PaddockNewsWindow_Contact3_Format }
                : new[] { Strings.PaddockNewsWindow_Reprimand1_Format, Strings.PaddockNewsWindow_Reprimand2_Format, Strings.PaddockNewsWindow_Reprimand3_Format };

            var paragraph = string.Format(Pick(reprimandVariants), driverName, teamName, principal, teammateName);

            string[] consequenceVariants = item.Consequence switch
            {
                ReprimandConsequence.FINAL_WARNING => new[] { Strings.PaddockNewsWindow_FinalWarning1_Format, Strings.PaddockNewsWindow_FinalWarning2_Format },
                ReprimandConsequence.SPARED_AS_LEADER => new[] { Strings.PaddockNewsWindow_SparedAsLeader1_Format, Strings.PaddockNewsWindow_SparedAsLeader2_Format },
                ReprimandConsequence.RELEASED when string.IsNullOrEmpty(item.ReplacementDriverId) => new[] { Strings.PaddockNewsWindow_ReleasedNoReplacement_Format },
                ReprimandConsequence.RELEASED => new[] { Strings.PaddockNewsWindow_Released1_Format, Strings.PaddockNewsWindow_Released2_Format },
                _ => null
            };

            if (consequenceVariants != null)
            {
                var replacementName = GetDriverName(saveGame, item.ReplacementDriverId);
                paragraph += " " + string.Format(Pick(consequenceVariants), driverName, teamName, principal, replacementName);
            }

            return paragraph;
        }

        private string Pick(string[] variants) => variants[_random.Next(variants.Length)];

        private static string GetDriverName(ISaveGame saveGame, string driverId)
        {
            if (string.IsNullOrEmpty(driverId))
                return Strings.PaddockNewsWindow_DefaultDriverName;

            if (driverId == saveGame.PlayerData.DriverId)
                return saveGame.PlayerData.Name;

            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);
            return driver?.Name ?? Strings.PaddockNewsWindow_DefaultDriverName;
        }

        private static ITeamEntry GetTeam(ISaveGame saveGame, string teamId)
        {
            return saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // shown non-modally (like PostRaceNewsWindow), so DialogResult can't be set here
            this.Close();
        }
    }
}
