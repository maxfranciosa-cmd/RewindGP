using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace AMS2ChEd
{
    public class RaceSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public int RoundNumber { get; set; }
        public string RaceName { get; set; }
        public string CircuitName { get; set; }
        public string RaceDate { get; set; }
        public string FormattedDate { get; set; }
        public int RaceId { get; set; }
        public bool IsEven { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class RaceCalendarSelectionWindow : Window
    {
        private ObservableCollection<RaceSelectionItem> _raceItems;
        public List<int> RacesToRemove { get; private set; } = new List<int>();

        public RaceCalendarSelectionWindow(IEnumerable<Race> races, int seasonYear)
        {
            InitializeComponent();

            SeasonText.Text = $"{seasonYear} Season";

            // Create observable collection for races
            _raceItems = new ObservableCollection<RaceSelectionItem>();

            int roundNumber = 1;
            int index = 0;
            foreach (var race in races)
            {
                var item = new RaceSelectionItem
                {
                    RoundNumber = roundNumber,
                    RaceName = race.RaceName,
                    CircuitName = race.Circuit,
                    RaceDate = race.RaceDate,
                    FormattedDate = FormatRaceDate(race.RaceDate),
                    RaceId = race.RaceId,
                    IsEven = index % 2 == 1,
                    IsSelected = true
                };

                // Subscribe to property changes to update counts
                item.PropertyChanged += RaceItem_PropertyChanged;

                _raceItems.Add(item);
                roundNumber++;
                index++;
            }

            RaceItems.ItemsSource = _raceItems;

            // Update initial counts
            UpdateCounts();
        }

        private string FormatRaceDate(string dateString)
        {
            if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime date))
            {
                return date.ToString("d MMM yyyy");
            }
            return dateString;
        }

        private void RaceItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RaceSelectionItem.IsSelected))
            {
                UpdateCounts();
            }
        }

        private void UpdateCounts()
        {
            int selectedCount = _raceItems.Count(r => r.IsSelected);
            int totalCount = _raceItems.Count;

            SelectedCountText.Text = selectedCount.ToString();
            TotalCountText.Text = totalCount.ToString();

            // Show warning if trying to deselect all races
            if (selectedCount == 0)
            {
                WarningText.Visibility = Visibility.Visible;
                ConfirmButton.IsEnabled = false;
            }
            else
            {
                WarningText.Visibility = Visibility.Collapsed;
                ConfirmButton.IsEnabled = true;
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Ensure at least one race is selected
            int selectedCount = _raceItems.Count(r => r.IsSelected);
            if (selectedCount == 0)
            {
                MessageBox.Show("At least one race must remain in the calendar.",
                    "Invalid Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Collect races to remove
            RacesToRemove = _raceItems
                .Where(r => !r.IsSelected)
                .Select(r => r.RaceId)
                .ToList();

            DialogResult = true;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Unsubscribe from all property changed events
            foreach (var item in _raceItems)
            {
                item.PropertyChanged -= RaceItem_PropertyChanged;
            }

            base.OnClosing(e);
        }
    }
}