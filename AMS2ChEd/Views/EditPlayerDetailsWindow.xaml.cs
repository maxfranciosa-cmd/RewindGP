using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace AMS2ChEd.Views
{
    public class DefaultHelmetDesign : INotifyPropertyChanged
    {
        public GenericHelmetDesign HelmetDesign { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class EditPlayerDetailsWindow : Window
    {
        private IPlayerData _playerData;
        private ISaveGame _saveGame;
        private Ams2DriverData _playerDriverData;

        // Per-era helmet lists (filtered from all designs)
        private List<DefaultHelmetDesign> _modernHelmets;
        private List<DefaultHelmetDesign> _ninetyHelmets;
        private List<DefaultHelmetDesign> _eightyHelmets;
        private List<DefaultHelmetDesign> _seventyHelmets;

        public EditPlayerDetailsWindow(IPlayerData playerData, ISaveGame saveGame)
        {
            InitializeComponent();
            _playerData = playerData;
            _saveGame = saveGame;

            _playerDriverData = _saveGame.Drivers.FirstOrDefault(d => d.DriverId == _playerData.DriverId) as Ams2DriverData;

            LoadDefaultHelmets();
            LoadPlayerData();
            PreSelectEraTab();
        }

        private void PreSelectEraTab()
        {
            int year = _saveGame.CurrentSeason.Year;

            int tabIndex;
            if (year >= HelmetPicker.HELMET_MODERN_EARLIEST_YEAR)
                tabIndex = 0; // Modern
            else if (year >= HelmetPicker.HELMET_90s_EARLIEST_YEAR)
                tabIndex = 1; // 1990s
            else if (year >= HelmetPicker.HELMET_80s_EARLIEST_YEAR)
                tabIndex = 2; // 1980s
            else
                tabIndex = 3; // 1970s

            EraTabControl.SelectedIndex = tabIndex;
        }

        // ─── Helmet Loading ────────────────────────────────────────────────────────

        private void LoadDefaultHelmets()
        {
            var all = HelmetPicker.LoadGenericHelmetDesigns();

            _modernHelmets = ToViewModels(all, HelmetEra.Modern);
            _ninetyHelmets = ToViewModels(all, HelmetEra.Nineties);
            _eightyHelmets = ToViewModels(all, HelmetEra.Eighties);
            _seventyHelmets = ToViewModels(all, HelmetEra.Seventies);

            DefaultHelmetItemsControl_Modern.ItemsSource = _modernHelmets;
            DefaultHelmetItemsControl_90s.ItemsSource = _ninetyHelmets;
            DefaultHelmetItemsControl_80s.ItemsSource = _eightyHelmets;
            DefaultHelmetItemsControl_70s.ItemsSource = _seventyHelmets;

            // Default selection: first item in each list
            SelectFirstIfAny(_modernHelmets);
            SelectFirstIfAny(_ninetyHelmets);
            SelectFirstIfAny(_eightyHelmets);
            SelectFirstIfAny(_seventyHelmets);
        }

        private static List<DefaultHelmetDesign> ToViewModels(IEnumerable<GenericHelmetDesign> all, HelmetEra era)
            => all.Where(h => h.Era == era)
                  .Select(h => new DefaultHelmetDesign { HelmetDesign = h })
                  .ToList();

        private static void SelectFirstIfAny(List<DefaultHelmetDesign> list)
        {
            if (list.Any()) list[0].IsSelected = true;
        }

        // ─── Player Data Loading ───────────────────────────────────────────────────

        private void LoadPlayerData()
        {
            PlayerNameTextBox.Text = _playerData.Name;

            if (_playerDriverData == null) return;

            NationalityTextBox.Text = _playerDriverData.Nationality ?? string.Empty;

            var pictureUrl = _playerDriverData.PictureUrl;
            if (!string.IsNullOrEmpty(pictureUrl))
            {
                bool isUrl = pictureUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                          || pictureUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                if (isUrl)
                    PhotoURLTextBox.Text = pictureUrl;
                else
                    PhotoFileTextBox.Text = pictureUrl;
            }

            // Modern
            LoadEraHelmet(
                helmetFile: _playerDriverData.BaseHelmetFile,
                visorFile: _playerDriverData.BaseVisorFile,
                helmets: _modernHelmets,
                defaultRadio: UseDefaultHelmetRadio_Modern,
                customRadio: UseCustomHelmetRadio_Modern,
                helmetTextBox: HelmetFileTextBox_Modern,
                visorTextBox: VisorFileTextBox_Modern);

            // 1990s
            LoadEraHelmet(
                helmetFile: _playerDriverData.BaseHelmetFile90s,
                visorFile: null, // no visor for 90s
                helmets: _ninetyHelmets,
                defaultRadio: UseDefaultHelmetRadio_90s,
                customRadio: UseCustomHelmetRadio_90s,
                helmetTextBox: HelmetFileTextBox_90s,
                visorTextBox: null);

            // 1980s
            LoadEraHelmet(
                helmetFile: _playerDriverData.BaseHelmetFile80s,
                visorFile: _playerDriverData.BaseVisorFile80s,
                helmets: _eightyHelmets,
                defaultRadio: UseDefaultHelmetRadio_80s,
                customRadio: UseCustomHelmetRadio_80s,
                helmetTextBox: HelmetFileTextBox_80s,
                visorTextBox: VisorFileTextBox_80s);

            // 1970s
            LoadEraHelmet(
                helmetFile: _playerDriverData.BaseHelmetFile70s,
                visorFile: _playerDriverData.BaseVisorFile70s,
                helmets: _seventyHelmets,
                defaultRadio: UseDefaultHelmetRadio_70s,
                customRadio: UseCustomHelmetRadio_70s,
                helmetTextBox: HelmetFileTextBox_70s,
                visorTextBox: VisorFileTextBox_70s);
        }

        /// <summary>
        /// Restores saved helmet/visor selection for a single era:
        /// tries to match a default design first, falls back to custom.
        /// </summary>
        private static void LoadEraHelmet(
            string? helmetFile,
            string? visorFile,
            List<DefaultHelmetDesign> helmets,
            System.Windows.Controls.RadioButton defaultRadio,
            System.Windows.Controls.RadioButton customRadio,
            System.Windows.Controls.TextBox helmetTextBox,
            System.Windows.Controls.TextBox? visorTextBox)
        {
            if (!string.IsNullOrEmpty(helmetFile))
                helmetTextBox.Text = helmetFile;

            if (visorTextBox is not null && !string.IsNullOrEmpty(visorFile))
                visorTextBox.Text = visorFile;

            // Try to find a matching default design
            var match = helmets.FirstOrDefault(h =>
                h.HelmetDesign.HelmetFile == helmetFile &&
                (h.HelmetDesign.VisorFile ?? "") == (visorFile ?? ""));

            if (match != null)
            {
                defaultRadio.IsChecked = true;
                // Clear previous default selection and mark the match
                foreach (var h in helmets) h.IsSelected = false;
                match.IsSelected = true;
            }
            else if (!string.IsNullOrEmpty(helmetFile))
            {
                customRadio.IsChecked = true;
            }
            // else: nothing saved yet — leave default radio checked, first item pre-selected
        }

        // ─── Helmet Mode Radio Handlers ────────────────────────────────────────────

        private void HelmetModeChanged_Modern(object sender, RoutedEventArgs e)
            => UpdateHelmetPanelVisibility(
                UseDefaultHelmetRadio_Modern, DefaultHelmetPanel_Modern, CustomHelmetPanel_Modern);

        private void HelmetModeChanged_90s(object sender, RoutedEventArgs e)
            => UpdateHelmetPanelVisibility(
                UseDefaultHelmetRadio_90s, DefaultHelmetPanel_90s, CustomHelmetPanel_90s);

        private void HelmetModeChanged_80s(object sender, RoutedEventArgs e)
            => UpdateHelmetPanelVisibility(
                UseDefaultHelmetRadio_80s, DefaultHelmetPanel_80s, CustomHelmetPanel_80s);

        private void HelmetModeChanged_70s(object sender, RoutedEventArgs e)
            => UpdateHelmetPanelVisibility(
                UseDefaultHelmetRadio_70s, DefaultHelmetPanel_70s, CustomHelmetPanel_70s);

        private static void UpdateHelmetPanelVisibility(
            System.Windows.Controls.RadioButton defaultRadio,
            StackPanel defaultPanel,
            StackPanel customPanel)
        {
            if (defaultPanel == null || customPanel == null) return;

            bool useDefault = defaultRadio.IsChecked == true;
            defaultPanel.Visibility = useDefault ? Visibility.Visible : Visibility.Collapsed;
            customPanel.Visibility = useDefault ? Visibility.Collapsed : Visibility.Visible;
        }

        // ─── Helmet Preview Click ──────────────────────────────────────────────────

        private void HelmetPreview_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not DefaultHelmetDesign clickedDesign)
                return;

            // Find which era list this design belongs to and deselect others in that list
            var eraList = GetEraListForDesign(clickedDesign);
            if (eraList == null) return;

            foreach (var h in eraList) h.IsSelected = false;
            clickedDesign.IsSelected = true;
        }

        private List<DefaultHelmetDesign>? GetEraListForDesign(DefaultHelmetDesign design)
        {
            if (_modernHelmets.Contains(design)) return _modernHelmets;
            if (_ninetyHelmets.Contains(design)) return _ninetyHelmets;
            if (_eightyHelmets.Contains(design)) return _eightyHelmets;
            if (_seventyHelmets.Contains(design)) return _seventyHelmets;
            return null;
        }

        // ─── Tab Selection (kept for future use / extensibility) ──────────────────

        private void EraTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // No action needed — each tab is self-contained
        }

        // ─── Browse Buttons ────────────────────────────────────────────────────────

        private void BrowsePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Player Photo",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PhotoFileTextBox.Text = dlg.FileName;
                PhotoURLTextBox.Text = string.Empty;
            }
        }

        private void BrowseHelmetButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Helmet Texture",
                Filter = "PNG Files (*.png)|*.png|DDS Files (*.dds)|*.dds|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var era = (sender as System.Windows.Controls.Button)?.Tag as string;
            switch (era)
            {
                case "Nineties": HelmetFileTextBox_90s.Text = dlg.FileName; break;
                case "Eighties": HelmetFileTextBox_80s.Text = dlg.FileName; break;
                case "Seventies": HelmetFileTextBox_70s.Text = dlg.FileName; break;
                default: HelmetFileTextBox_Modern.Text = dlg.FileName; break;
            }
        }

        private void BrowseVisorButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Visor Texture",
                Filter = "PNG Files (*.png)|*.png|DDS Files (*.dds)|*.dds|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var era = (sender as System.Windows.Controls.Button)?.Tag as string;
            switch (era)
            {
                case "Eighties": VisorFileTextBox_80s.Text = dlg.FileName; break;
                case "Seventies": VisorFileTextBox_70s.Text = dlg.FileName; break;
                default: VisorFileTextBox_Modern.Text = dlg.FileName; break;
            }
        }

        // ─── Save ──────────────────────────────────────────────────────────────────

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PlayerNameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter a player name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nationality = NationalityTextBox.Text.Trim().ToUpper();
            if (!string.IsNullOrEmpty(nationality) && nationality.Length != 3)
            {
                System.Windows.MessageBox.Show("Nationality must be exactly 3 characters (e.g., GBR, USA, ITA).",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _playerData.Name = PlayerNameTextBox.Text.Trim();

            if (_playerDriverData != null)
            {
                _playerDriverData.Name = _playerData.Name;
                _playerDriverData.Nationality = string.IsNullOrWhiteSpace(nationality) ? null : nationality;

                // Picture
                if (!string.IsNullOrWhiteSpace(PhotoFileTextBox.Text))
                    _playerDriverData.PictureUrl = PhotoFileTextBox.Text.Trim();
                else if (!string.IsNullOrWhiteSpace(PhotoURLTextBox.Text))
                    _playerDriverData.PictureUrl = PhotoURLTextBox.Text.Trim();
                else
                    _playerDriverData.PictureUrl = null;

                // Modern helmet
                SaveEraHelmet(
                    useDefault: UseDefaultHelmetRadio_Modern.IsChecked == true,
                    helmets: _modernHelmets,
                    helmetTextBox: HelmetFileTextBox_Modern,
                    visorTextBox: VisorFileTextBox_Modern,
                    setHelmet: v => _playerDriverData.BaseHelmetFile = v,
                    setVisor: v => _playerDriverData.BaseVisorFile = v);

                // 1990s helmet (no visor)
                SaveEraHelmet(
                    useDefault: UseDefaultHelmetRadio_90s.IsChecked == true,
                    helmets: _ninetyHelmets,
                    helmetTextBox: HelmetFileTextBox_90s,
                    visorTextBox: null,
                    setHelmet: v => _playerDriverData.BaseHelmetFile90s = v,
                    setVisor: _ => { });

                // 1980s helmet
                SaveEraHelmet(
                    useDefault: UseDefaultHelmetRadio_80s.IsChecked == true,
                    helmets: _eightyHelmets,
                    helmetTextBox: HelmetFileTextBox_80s,
                    visorTextBox: VisorFileTextBox_80s,
                    setHelmet: v => _playerDriverData.BaseHelmetFile80s = v,
                    setVisor: v => _playerDriverData.BaseVisorFile80s = v);

                // 1970s helmet
                SaveEraHelmet(
                    useDefault: UseDefaultHelmetRadio_70s.IsChecked == true,
                    helmets: _seventyHelmets,
                    helmetTextBox: HelmetFileTextBox_70s,
                    visorTextBox: VisorFileTextBox_70s,
                    setHelmet: v => _playerDriverData.BaseHelmetFile70s = v,
                    setVisor: v => _playerDriverData.BaseVisorFile70s = v);
            }

            DialogResult = true;
            Close();
        }

        private static void SaveEraHelmet(
            bool useDefault,
            List<DefaultHelmetDesign> helmets,
            System.Windows.Controls.TextBox helmetTextBox,
            System.Windows.Controls.TextBox? visorTextBox,
            Action<string?> setHelmet,
            Action<string?> setVisor)
        {
            if (useDefault)
            {
                var selected = helmets.FirstOrDefault(h => h.IsSelected);
                if (selected != null)
                {
                    setHelmet(selected.HelmetDesign.HelmetFile);
                    setVisor(selected.HelmetDesign.VisorFile);
                }
            }
            else
            {
                var helmetPath = helmetTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(helmetPath))
                {
                    setHelmet(helmetPath);
                    setVisor(visorTextBox?.Text.Trim());
                }
            }
        }

        // ─── Cancel ────────────────────────────────────────────────────────────────

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}