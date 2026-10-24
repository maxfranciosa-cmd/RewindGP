using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.CrashDetection;
using AMS2ChEd.Resources;
using System.Windows;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// The crashes found in the telemetry of a race the player drove, before any of them counts: the
    /// player keeps the ones that happened and drops the misfires, so a false detection never harms
    /// a career (crash-incidents-design.md, section 4). Closing the window keeps them all.
    /// </summary>
    public partial class CrashReviewWindow : Window
    {
        private class CrashRow
        {
            public RaceCrash Crash { get; init; }
            public string DriverName { get; init; }
            public string TeamName { get; init; }
            public string SeverityText { get; init; }
            public string DetailText { get; init; }
            public bool Keep { get; set; } = true;
        }

        private readonly List<CrashRow> _rows;

        public IReadOnlyList<RaceCrash> ConfirmedCrashes => _rows.Where(r => r.Keep).Select(r => r.Crash).ToList();

        public CrashReviewWindow(ISaveGame saveGame, IEnumerable<RaceCrash> crashes, string grandPrixName)
        {
            InitializeComponent();

            HeaderText.Text = string.Format(Strings.CrashReviewWindow_Header_Format, grandPrixName?.ToUpper());

            _rows = crashes.Select(c => new CrashRow
            {
                Crash = c,
                DriverName = DriverName(saveGame, c.DriverId),
                TeamName = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == c.TeamId)?.TeamName ?? "",
                SeverityText = c.Severity switch
                {
                    CrashSeverity.Severe => Strings.CrashReviewWindow_Severity_Severe,
                    CrashSeverity.Moderate => Strings.CrashReviewWindow_Severity_Moderate,
                    _ => Strings.CrashReviewWindow_Severity_Minor
                },
                DetailText = string.Format(c.DroveOn ? Strings.CrashReviewWindow_Detail_DroveOn_Format : Strings.CrashReviewWindow_Detail_Format,
                    c.Lap ?? 0, Math.Round(c.ImpactSpeedKmh ?? 0))
            }).ToList();

            CrashesPanel.ItemsSource = _rows;
        }

        private static string DriverName(ISaveGame saveGame, string driverId)
        {
            if (driverId == saveGame.PlayerData.DriverId)
                return saveGame.PlayerData.Name;

            return saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId)?.Name ?? driverId;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
