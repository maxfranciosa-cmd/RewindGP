using AMS2ChEd.Business.Services;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.Views
{
    public class MissingDriverEntry : INotifyPropertyChanged
    {
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public int Number { get; set; }
        public bool IsPlayer { get; set; }
        public bool MissingFromQuali { get; set; }
        public bool MissingFromRace { get; set; }

        private string _qualiPositionText = "";
        public string QualiPositionText
        {
            get => _qualiPositionText;
            set { _qualiPositionText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QualiPositionText))); }
        }

        private string _racePositionText = "";
        public string RacePositionText
        {
            get => _racePositionText;
            set { _racePositionText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RacePositionText))); }
        }

        private bool _isDnf;
        public bool IsDnf
        {
            get => _isDnf;
            set { _isDnf = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDnf))); }
        }

        private bool _isFastestLap;
        public bool IsFastestLap
        {
            get => _isFastestLap;
            set { _isFastestLap = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFastestLap))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class MissingDriversResultWindow : Window
    {
        private readonly List<MissingDriverEntry> _entries;

        public List<ParticipantData> QualiResults { get; private set; }
        public List<ParticipantData> RaceResults { get; private set; }

        public MissingDriversResultWindow(List<MissingDriverEntry> entries)
        {
            InitializeComponent();
            _entries = entries;
            DriversPanel.ItemsSource = _entries;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var incomplete = _entries.Where(entry =>
                (entry.MissingFromQuali && !int.TryParse(entry.QualiPositionText, out _)) ||
                (entry.MissingFromRace && !int.TryParse(entry.RacePositionText, out _))
            ).ToList();

            if (incomplete.Any())
            {
                var names = string.Join("\n", incomplete.Select(entry => $"  • {entry.DriverName}"));
                System.Windows.MessageBox.Show(
                    $"The following drivers still need a position:\n\n{names}",
                    "Incomplete Results",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            QualiResults = _entries
                .Where(entry => entry.MissingFromQuali)
                .Select(entry => new ParticipantData
                {
                    DriverId = entry.DriverId,
                    DriverName = entry.DriverName,
                    TeamId = entry.TeamId,
                    TeamName = entry.TeamName,
                    Number = entry.Number,
                    IsPlayer = entry.IsPlayer,
                    Position = int.Parse(entry.QualiPositionText)
                })
                .ToList();

            RaceResults = _entries
                .Where(entry => entry.MissingFromRace)
                .Select(entry => new ParticipantData
                {
                    DriverId = entry.DriverId,
                    DriverName = entry.DriverName,
                    TeamId = entry.TeamId,
                    TeamName = entry.TeamName,
                    Number = entry.Number,
                    IsPlayer = entry.IsPlayer,
                    Position = int.Parse(entry.RacePositionText),
                    DNF = entry.IsDnf,
                    IsSessionBestLap = entry.IsFastestLap
                })
                .ToList();

            DialogResult = true;
            Close();
        }
    }
}
