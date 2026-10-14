using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Resources;
using System;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// Technical news ahead of a race: which teams have installed an improvement package for
    /// it, on whose instruction, and what it did to the car.
    /// </summary>
    public partial class ImprovementPackageNewsWindow : Window
    {
        private readonly Random _random = new Random();
        private readonly IReadOnlyList<ImprovementStat> _stats;

        public ImprovementPackageNewsWindow(ISaveGame saveGame, IReadOnlyList<InstalledImprovementPackage> items, IReadOnlyList<ImprovementStat> stats, DateTime raceDate, string grandPrixName)
        {
            InitializeComponent();
            _stats = stats;

            // the player's own team makes the top of the page
            var orderedItems = items.OrderByDescending(i => i.TeamId == saveGame.PlayerData.TeamId).ToList();

            DateText.Text = raceDate.ToString("dddd, MMMM dd, yyyy");
            HeadlineText.Text = BuildHeadline(saveGame, orderedItems, grandPrixName);
            ArticleText.Text = string.Join("\n\n", orderedItems.Select(i => BuildParagraph(saveGame, i, grandPrixName)));
        }

        private static string BuildHeadline(ISaveGame saveGame, IReadOnlyList<InstalledImprovementPackage> items, string grandPrixName)
        {
            var headlineItem = items.Count == 1 || items.First().TeamId == saveGame.PlayerData.TeamId
                ? items.First()
                : null;

            if (headlineItem == null)
                return string.Format(Strings.ImprovementPackageNewsWindow_Headline_Generic_Format, grandPrixName).ToUpper();

            return string.Format(Strings.ImprovementPackageNewsWindow_Headline_Team_Format, GetTeamName(saveGame, headlineItem.TeamId), grandPrixName).ToUpper();
        }

        // Argument order standardized as (driver, team, grand prix) so each language's resx
        // template can reorder or drop any of them.
        private string BuildParagraph(ISaveGame saveGame, InstalledImprovementPackage item, string grandPrixName)
        {
            var introVariants = new[]
            {
                Strings.ImprovementPackageNewsWindow_Intro1_Format,
                Strings.ImprovementPackageNewsWindow_Intro2_Format,
                Strings.ImprovementPackageNewsWindow_Intro3_Format
            };

            var intro = string.Format(Pick(introVariants), GetDriverName(saveGame, item.Package.DriverId), GetTeamName(saveGame, item.TeamId), grandPrixName);

            return intro + " " + BuildEffects(item);
        }

        private string BuildEffects(InstalledImprovementPackage item)
        {
            var gains = new List<string>();
            var losses = new List<string>();

            foreach (var stat in _stats)
            {
                if (item.Package.Values == null || !item.Package.Values.TryGetValue(stat.Key, out var value) || value == 0)
                    continue;

                if ((value > 0) == stat.HigherIsBetter)
                    gains.Add(stat.ImprovedText);
                else
                    losses.Add(stat.WorsenedText);
            }

            if (gains.Any() && losses.Any())
                return string.Format(Strings.ImprovementPackageNewsWindow_Effects_GainsAndLosses_Format, JoinList(gains), JoinList(losses));
            if (gains.Any())
                return string.Format(Strings.ImprovementPackageNewsWindow_Effects_OnlyGains_Format, JoinList(gains));
            if (losses.Any())
                return string.Format(Strings.ImprovementPackageNewsWindow_Effects_OnlyLosses_Format, JoinList(losses));

            return Strings.ImprovementPackageNewsWindow_Effects_None;
        }

        private static string JoinList(List<string> parts)
        {
            if (parts.Count == 1)
                return parts[0];

            return string.Join(", ", parts.Take(parts.Count - 1)) + Strings.ImprovementPackageNewsWindow_ListAnd + parts.Last();
        }

        private string Pick(string[] variants) => variants[_random.Next(variants.Length)];

        private static string GetDriverName(ISaveGame saveGame, string driverId)
        {
            if (driverId == saveGame.PlayerData.DriverId)
                return saveGame.PlayerData.Name;

            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);
            return driver?.Name ?? Strings.PaddockNewsWindow_DefaultDriverName;
        }

        private static string GetTeamName(ISaveGame saveGame, string teamId)
        {
            return saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId)?.TeamName ?? Strings.PaddockNewsWindow_DefaultTeamName;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
