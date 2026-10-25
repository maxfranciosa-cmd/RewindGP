using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Extensions;
using AMS2ChEd.Resources;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace AMS2ChEd.Views
{
    public class DriverDatabaseRow
    {
        public DriverDatabaseEntry Entry { get; set; }
        public string DriverId { get; set; }
        public string FullName { get; set; }
        public string FirstName { get; set; }
        public string Surname { get; set; }
        public string Nationality { get; set; }
        public string AgeText { get; set; }
        public string CarNumber { get; set; }
        public string TeamShortName { get; set; }
        public string StatusText { get; set; }
        // "Racing", "FreeAgent" or "Retired": what the visual style tells the drivers apart by
        public string Status { get; set; }
        public bool IsPlayer { get; set; }
        public string PictureUrl { get; set; }
        public ImageSource Photo { get; set; }
        // the team colour behind the photo (none without a seat) and under the name (the style's own colour without a seat)
        public Brush TeamBrush { get; set; }
        public Brush TeamRuleBrush { get; set; }
        public Visibility PlaceholderVisibility { get; set; }
        public Visibility NumberVisibility { get; set; }
        public Visibility StatusBandVisibility { get; set; }
        public Visibility PlayerBadgeVisibility { get; set; }
        // what the accolades window shows under the name
        public string AccoladesTeamName { get; set; }
    }

    /// <summary>
    /// Every driver in the save - racing this season, without a seat or retired - as cards that can
    /// be filtered by status, searched and sorted; double-clicking a card opens the driver's accolades.
    /// </summary>
    public partial class DriverDatabaseWindow : Window
    {
        private readonly ISaveGame _saveGame;
        private readonly IAllTimeRecordsLoader _allTimeRecordsLoader;
        private readonly List<DriverDatabaseRow> _rows = new();
        private readonly bool _ready;

        public DriverDatabaseWindow(ISaveGame saveGame, IAllTimeRecordsLoader allTimeRecordsLoader = null)
        {
            InitializeComponent();

            _saveGame = saveGame;
            _allTimeRecordsLoader = allTimeRecordsLoader;

            SubtitleText.Text = string.Format(Strings.ConstructorStandingsGridWindow_SeasonYear_Format, saveGame.CurrentSeason.Year);

            var noTeamBrush = (Brush)FindResource("DriverDatabase.NoTeamBrush");
            _rows.AddRange(DriverDatabase.Build(saveGame).Select(entry => ToRow(entry, noTeamBrush)));

            AllTab.Content = TabLabel(Strings.DriverDatabaseWindow_TabAll, _rows.Count);
            RacingTab.Content = TabLabel(Strings.DriverDatabaseWindow_TabRacing, _rows.Count(r => r.Entry.Status == DriverDatabaseStatus.RACING));
            FreeAgentsTab.Content = TabLabel(Strings.DriverDatabaseWindow_TabFreeAgents, _rows.Count(r => r.Entry.Status == DriverDatabaseStatus.FREE_AGENT));
            RetiredTab.Content = TabLabel(Strings.DriverDatabaseWindow_TabRetired, _rows.Count(r => r.Entry.Status == DriverDatabaseStatus.RETIRED));

            _ready = true;
            ShowDrivers();
        }

        private static string TabLabel(string label, int count) => $"{label}  {count}";

        private void ShowDrivers()
        {
            // the tabs and sort options raise Checked while the window is still being built
            if (!_ready)
                return;

            string query = SearchBox.Text?.Trim() ?? string.Empty;
            SearchHintText.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

            var shown = Sort(_rows.Where(r => HasSelectedStatus(r) && Matches(r, query))).ToList();

            DriversItems.ItemsSource = shown;
            NoDriversText.Visibility = shown.Any() ? Visibility.Collapsed : Visibility.Visible;
            CountText.Text = string.Format(Strings.DriverDatabaseWindow_Count_Format, shown.Count, _rows.Count);
        }

        private bool HasSelectedStatus(DriverDatabaseRow row)
        {
            if (RacingTab.IsChecked == true)
                return row.Entry.Status == DriverDatabaseStatus.RACING;
            if (FreeAgentsTab.IsChecked == true)
                return row.Entry.Status == DriverDatabaseStatus.FREE_AGENT;
            if (RetiredTab.IsChecked == true)
                return row.Entry.Status == DriverDatabaseStatus.RETIRED;
            return true;
        }

        // name, team or nationality, ignoring case and accents ("hakkinen" finds Häkkinen)
        private static bool Matches(DriverDatabaseRow row, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            var compare = CultureInfo.CurrentCulture.CompareInfo;
            const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            return new[] { row.FullName, row.Entry.TeamName, row.Entry.TeamShortName, row.Nationality }
                .Any(field => !string.IsNullOrEmpty(field) && compare.IndexOf(field, query, options) >= 0);
        }

        // the rows come sorted by surname, and the sorts below are stable, so drivers level on the key stay in that order
        private IEnumerable<DriverDatabaseRow> Sort(IEnumerable<DriverDatabaseRow> rows)
        {
            if (SortByAgeOption.IsChecked == true)
                return rows.OrderBy(r => r.Entry.Age ?? int.MaxValue);

            // the grid team by team, in car-number order; then the free agents and the retired
            if (SortByTeamOption.IsChecked == true)
                return rows
                    .OrderBy(r => r.Entry.Status)
                    .ThenBy(r => r.Entry.TeamShortName ?? string.Empty, System.StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(r => r.Entry.CarNumber ?? int.MaxValue);

            return rows;
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => ShowDrivers();

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ShowDrivers();

        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Focus();

                if (e.ClickCount == 2 && element.DataContext is DriverDatabaseRow row)
                {
                    e.Handled = true;
                    OpenAccolades(row);
                }
            }
        }

        private void Card_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is FrameworkElement { DataContext: DriverDatabaseRow row })
            {
                e.Handled = true;
                OpenAccolades(row);
            }
        }

        private void OpenAccolades(DriverDatabaseRow row)
        {
            var window = new DriverAccoladesWindow(_saveGame, row.DriverId, row.FullName, row.AccoladesTeamName, row.PictureUrl, _allTimeRecordsLoader);
            window.Owner = this;
            window.ShowDialog();
        }

        private static DriverDatabaseRow ToRow(DriverDatabaseEntry entry, Brush noTeamBrush)
        {
            bool isRacing = entry.Status == DriverDatabaseStatus.RACING;
            var teamBrush = isRacing ? ParseColor(entry.TeamColor) : null;
            string portrait = DriverPortraitExtension.Portrait(entry.DriverId, entry.PictureUrl, entry.Avatar);
            bool hasPhoto = PictureUrlLoaderExtension.TryLoadBitmap(portrait, out var photo);

            return new DriverDatabaseRow
            {
                Entry = entry,
                DriverId = entry.DriverId,
                FullName = entry.FullName,
                FirstName = entry.FirstName,
                Surname = entry.Surname,
                Nationality = entry.Nationality ?? string.Empty,
                AgeText = entry.Age.HasValue ? string.Format(Strings.DriverDatabaseWindow_Age_Format, entry.Age.Value) : string.Empty,
                CarNumber = entry.CarNumber?.ToString() ?? string.Empty,
                TeamShortName = isRacing ? entry.TeamShortName : string.Empty,
                Status = entry.Status switch
                {
                    DriverDatabaseStatus.RACING => "Racing",
                    DriverDatabaseStatus.FREE_AGENT => "FreeAgent",
                    _ => "Retired"
                },
                StatusText = entry.Status switch
                {
                    DriverDatabaseStatus.FREE_AGENT => Strings.DriverDatabaseWindow_StatusFreeAgent,
                    DriverDatabaseStatus.RETIRED => Strings.DriverDatabaseWindow_StatusRetired,
                    _ => string.Empty
                },
                IsPlayer = entry.IsPlayer,
                PictureUrl = portrait,
                Photo = hasPhoto ? photo : null,
                TeamBrush = teamBrush,
                TeamRuleBrush = teamBrush ?? noTeamBrush,
                PlaceholderVisibility = hasPhoto ? Visibility.Collapsed : Visibility.Visible,
                NumberVisibility = entry.CarNumber.HasValue ? Visibility.Visible : Visibility.Collapsed,
                StatusBandVisibility = isRacing ? Visibility.Collapsed : Visibility.Visible,
                PlayerBadgeVisibility = entry.IsPlayer ? Visibility.Visible : Visibility.Collapsed,
                AccoladesTeamName = entry.Status switch
                {
                    DriverDatabaseStatus.RACING => entry.TeamName,
                    DriverDatabaseStatus.FREE_AGENT => Strings.DriverDatabaseWindow_AccoladesFreeAgent,
                    _ => Strings.DriverDatabaseWindow_AccoladesRetired
                }
            };
        }

        private static Brush ParseColor(string color)
        {
            if (string.IsNullOrEmpty(color))
                return null;

            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                brush.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }
    }
}
