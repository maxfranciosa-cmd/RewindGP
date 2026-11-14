using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// Paddock news before a race weekend: the academy juniors given a race driver's car for FP1
    /// (PracticeOutings), and how each did against the team's lead driver.
    /// </summary>
    public partial class PracticeOutingNewsWindow : Window
    {
        public PracticeOutingNewsWindow(ISaveGame saveGame, IReadOnlyList<PracticeOutingResult> results, DateTime raceDate, string grandPrixName)
        {
            InitializeComponent();

            DateText.Text = raceDate.ToString("dddd, MMMM dd, yyyy");
            HeadlineText.Text = BuildHeadline(saveGame, results, grandPrixName);
            ArticleText.Text = string.Join("\n\n", results.Select(r => BuildParagraph(saveGame, r, grandPrixName)));
        }

        // the player's own outing makes the headline
        private static string BuildHeadline(ISaveGame saveGame, IReadOnlyList<PracticeOutingResult> results, string grandPrixName)
        {
            var playerOuting = results.FirstOrDefault(r => r.Outing.DriverId == saveGame.PlayerData.DriverId).Outing;
            if (playerOuting == null)
                return string.Format(Strings.PracticeOutingNewsWindow_Headline_Generic_Format, grandPrixName).ToUpper();

            return string.Format(Strings.PracticeOutingNewsWindow_Headline_Player_Format,
                GetDriverName(saveGame, playerOuting.DriverId), GetTeamName(saveGame, playerOuting.TeamId)).ToUpper();
        }

        // (driver, replaced driver, team, grand prix, benchmark)
        private static string BuildParagraph(ISaveGame saveGame, PracticeOutingResult result, string grandPrixName)
        {
            var format = result.Outcome switch
            {
                // the player drives it himself this weekend: no result yet
                null => Strings.PracticeOutingNewsWindow_WillDrive_Format,
                PracticeOutcome.Ahead => Strings.PracticeOutingNewsWindow_Ahead_Format,
                PracticeOutcome.WithinReach => Strings.PracticeOutingNewsWindow_WithinReach_Format,
                _ => Strings.PracticeOutingNewsWindow_FurtherBack_Format
            };

            var outing = result.Outing;
            return string.Format(format,
                GetDriverName(saveGame, outing.DriverId),
                GetDriverName(saveGame, outing.ReplacedDriverId),
                GetTeamName(saveGame, outing.TeamId),
                grandPrixName,
                GetDriverName(saveGame, outing.BenchmarkDriverId));
        }

        private static string GetDriverName(ISaveGame saveGame, string driverId)
        {
            if (string.IsNullOrEmpty(driverId))
                return Strings.PaddockNewsWindow_DefaultDriverName;

            if (driverId == saveGame.PlayerData.DriverId)
                return saveGame.PlayerData.Name;

            return saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId)?.Name ?? Strings.PaddockNewsWindow_DefaultDriverName;
        }

        private static string GetTeamName(ISaveGame saveGame, string teamId) =>
            saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId)?.TeamName ?? Strings.PaddockNewsWindow_DefaultTeamName;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
