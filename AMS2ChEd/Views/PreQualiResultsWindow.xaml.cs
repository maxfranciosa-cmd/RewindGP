using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Resources;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace AMS2ChEd.Views
{
    public class PreQualiResultEntry
    {
        public int Position { get; set; }
        public int Number { get; set; }
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string TeamName { get; set; }
        public string BestLapTime { get; set; }
        public bool IsPlayer { get; set; }

        // Divider shown above first DNPQ row
        public Visibility DividerVisibility { get; set; } = Visibility.Collapsed;

        // Row styling — qualified rows are white, DNPQ rows are light grey
        public Brush RowBackground { get; set; }
        public Brush RowForeground { get; set; }

        // Status badge
        public string StatusText { get; set; }
        public Brush StatusBackground { get; set; }
        public Brush StatusForeground { get; set; }
    }

    public partial class PreQualiResultsWindow : Window
    {
        private readonly List<ParticipantData> _results;
        private readonly int _passCount;

        public PreQualiResultsWindow(
            List<ParticipantData> results,
            int passCount,
            string grandPrixName = "",
            int seasonYear = 0)
        {
            InitializeComponent();
            _results = results;
            _passCount = passCount;

            LoadResults(grandPrixName, seasonYear);
        }

        private void LoadResults(string grandPrixName, int seasonYear)
        {
            // Header texts
            GrandPrixText.Text = string.IsNullOrEmpty(grandPrixName)
                ? string.Empty
                : grandPrixName.ToUpper();

            int qualifiedCount = Math.Min(_passCount, _results.Count);
            int eliminatedCount = Math.Max(0, _results.Count - _passCount);

            SessionSummaryText.Text = string.Format(
                qualifiedCount == 1 ? Strings.PreQualiResultsWindow_SessionSummary_Singular : Strings.PreQualiResultsWindow_SessionSummary_Plural,
                qualifiedCount, eliminatedCount);

            FooterText.Text = seasonYear > 0
                ? string.Format(Strings.PreQualiResultsWindow_Footer_WithYear, seasonYear)
                : Strings.PreQualiResultsWindow_Footer_NoYear;

            // Build display entries
            var entries = new List<PreQualiResultEntry>();

            for (int i = 0; i < _results.Count; i++)
            {
                var result = _results[i];
                bool qualified = result.Position <= _passCount;
                bool isFirstDnpq = result.Position == _passCount + 1;

                entries.Add(new PreQualiResultEntry
                {
                    Position      = result.Position,
                    Number        = result.Number,
                    DriverId      = result.DriverId,
                    DriverName    = result.DriverName,
                    TeamName      = result.TeamName,
                    BestLapTime   = result.BestLapTime,
                    IsPlayer      = result.IsPlayer,

                    DividerVisibility = isFirstDnpq
                        ? Visibility.Visible
                        : Visibility.Collapsed,

                    // Qualified rows: white bg, black text
                    // DNPQ rows: light grey bg, dark grey text
                    RowBackground = qualified
                        ? Brushes.White
                        : new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)),

                    RowForeground = qualified
                        ? Brushes.Black
                        : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),

                    // Status badge
                    StatusText = qualified ? Strings.PreQualiResultsWindow_StatusQualified : Strings.PreQualiResultsWindow_StatusDnpq,

                    StatusBackground = qualified
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)) // gold
                        : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), // dark grey

                    StatusForeground = qualified
                        ? Brushes.Black
                        : Brushes.White,
                });
            }

            ResultsItems.ItemsSource = entries;
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
