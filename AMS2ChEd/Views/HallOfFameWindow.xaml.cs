using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.Views
{
    public class HallOfFameRow
    {
        public int Rank { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
        public string Years { get; set; }
        // "Active", "Retired" or "Historic": what the visual style tells the drivers apart by
        public string Status { get; set; }
        public bool IsPlayer { get; set; }
    }

    /// <summary>
    /// The drivers with the most championships and the most wins, as history stands in the save:
    /// the champions of the past (who are in no season pack), the retired and the ones still racing.
    /// </summary>
    public partial class HallOfFameWindow : Window
    {
        private const int ROWS_PER_LIST = 25;

        public HallOfFameWindow(ISaveGame saveGame, IAllTimeRecordsLoader allTimeRecordsLoader)
        {
            InitializeComponent();

            var entries = HallOfFameCalculator.Build(saveGame, allTimeRecordsLoader?.Load());

            var champions = entries
                .Where(e => e.Championships > 0)
                .OrderByDescending(e => e.Championships)
                .ThenByDescending(e => e.Wins)
                .ThenBy(e => e.Name)
                .Take(ROWS_PER_LIST)
                .ToList();
            ChampionshipsItems.ItemsSource = champions
                .Select(e => ToRow(e, e.Championships, 1 + champions.Count(other => other.Championships > e.Championships)))
                .ToList();
            NoChampionsText.Visibility = champions.Any() ? Visibility.Collapsed : Visibility.Visible;

            var winners = entries
                .Where(e => e.Wins > 0)
                .OrderByDescending(e => e.Wins)
                .ThenByDescending(e => e.Championships)
                .ThenBy(e => e.Name)
                .Take(ROWS_PER_LIST)
                .ToList();
            WinsItems.ItemsSource = winners
                .Select(e => ToRow(e, e.Wins, 1 + winners.Count(other => other.Wins > e.Wins)))
                .ToList();
            NoWinnersText.Visibility = winners.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        // drivers level on the count share a rank
        private static HallOfFameRow ToRow(HallOfFameEntry entry, int value, int rank)
        {
            return new HallOfFameRow
            {
                Rank = rank,
                Name = entry.Name,
                Value = value,
                Years = string.Join("  ", entry.ChampionshipYears),
                Status = entry.Status switch
                {
                    HallOfFameStatus.ACTIVE => "Active",
                    HallOfFameStatus.RETIRED => "Retired",
                    _ => "Historic"
                },
                IsPlayer = entry.IsPlayer
            };
        }

        // the page text grows with the window; the active visual style gives the two sizes
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double fontSize = (double)FindResource(ActualWidth >= 1400 ? "HallOfFame.WideFontSize" : "HallOfFame.FontSize");

            System.Windows.Documents.TextElement.SetFontSize(ScreenRoot, fontSize);
            TitleText.FontSize = fontSize * 1.75;
        }
    }
}
