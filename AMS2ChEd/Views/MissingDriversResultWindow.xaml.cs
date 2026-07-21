using AMS2ChEd.Business.Services;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.Views
{
    public class MissingDriverOption
    {
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public int Number { get; set; }
        public bool IsPlayer { get; set; }

        public string DisplayName => IsPlayer ? $"{DriverName} (You)" : $"{DriverName} - {TeamName}";
    }

    public class MissingPositionRow : INotifyPropertyChanged
    {
        public int Position { get; set; }
        public List<MissingDriverOption> AvailableDrivers { get; set; }

        private MissingDriverOption _selectedDriver;
        public MissingDriverOption SelectedDriver
        {
            get => _selectedDriver;
            set { _selectedDriver = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDriver))); }
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
        public List<MissingPositionRow> QualiRows { get; }
        public List<MissingPositionRow> RaceRows { get; }

        public List<ParticipantData> QualiResults { get; private set; }
        public List<ParticipantData> RaceResults { get; private set; }

        public MissingDriversResultWindow(
            List<int> missingQualiPositions, List<MissingDriverOption> qualiMissingDrivers,
            List<int> missingRacePositions, List<MissingDriverOption> raceMissingDrivers)
        {
            InitializeComponent();

            QualiRows = (missingQualiPositions ?? new List<int>())
                .Select(pos => new MissingPositionRow { Position = pos, AvailableDrivers = qualiMissingDrivers })
                .ToList();

            RaceRows = (missingRacePositions ?? new List<int>())
                .Select(pos => new MissingPositionRow { Position = pos, AvailableDrivers = raceMissingDrivers })
                .ToList();

            QualiSection.Visibility = QualiRows.Any() ? Visibility.Visible : Visibility.Collapsed;
            RaceSection.Visibility = RaceRows.Any() ? Visibility.Visible : Visibility.Collapsed;

            QualiPanel.ItemsSource = QualiRows;
            RacePanel.ItemsSource = RaceRows;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            QualiResults = QualiRows
                .Where(row => row.SelectedDriver != null)
                .Select(row => ToParticipantData(row, dnf: false, fastestLap: false))
                .ToList();

            RaceResults = RaceRows
                .Where(row => row.SelectedDriver != null)
                .Select(row => ToParticipantData(row, row.IsDnf, row.IsFastestLap))
                .ToList();

            DialogResult = true;
            Close();
        }

        private static ParticipantData ToParticipantData(MissingPositionRow row, bool dnf, bool fastestLap)
        {
            var driver = row.SelectedDriver;
            return new ParticipantData
            {
                DriverId = driver.DriverId,
                DriverName = driver.DriverName,
                TeamId = driver.TeamId,
                TeamName = driver.TeamName,
                Number = driver.Number,
                IsPlayer = driver.IsPlayer,
                Position = row.Position,
                DNF = dnf,
                IsSessionBestLap = fastestLap
            };
        }
    }
}
