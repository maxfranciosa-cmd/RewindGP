using AMS2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace AMS2ChEd.Dialogs
{
    public partial class SeasonCatalogDialog : Window
    {
        private readonly SeasonManifestService _manifest;
        private readonly ObservableCollection<CatalogRow> _rows = new();
        private readonly SeasonModInstaller _seasonModInstaller;
        private readonly string _downloadUrlFormat;
        /// <summary>
        /// True if at least one season was installed — callers use this to
        /// know whether to refresh their ComboBoxes.
        /// </summary>
        public bool AnyDownloaded { get; private set; }

        public SeasonCatalogDialog(
            SeasonManifestService manifest,
            SeasonModInstaller seasonModInstaller,
            string downloadUrlFormat)
        {
            InitializeComponent();
            _manifest = manifest;
            _seasonModInstaller = seasonModInstaller;
            _downloadUrlFormat = downloadUrlFormat;
            LoadRows();
        }

        // -------------------------------------------------------------------------
        // Setup
        // -------------------------------------------------------------------------

        private void LoadRows()
        {
            _rows.Clear();

            foreach (var item in _manifest.GetSeasonCatalog())
            {
                var row = new CatalogRow(item);
                row.PropertyChanged += OnRowPropertyChanged;
                _rows.Add(row);
            }

            SeasonsItemsControl.ItemsSource = _rows;
            RefreshDownloadButton();
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CatalogRow.IsSelected))
                RefreshDownloadButton();
        }

        private void RefreshDownloadButton()
        {
            DownloadButton.IsEnabled = _rows.Any(r => r.IsSelected);
        }

        // -------------------------------------------------------------------------
        // Button handlers
        // -------------------------------------------------------------------------

        private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
        private async void OnDownloadClicked(object sender, RoutedEventArgs e)
        {
            var selected = _rows.Where(r => r.IsSelected).ToList();
            if (!selected.Any()) return;

            SetBusy(true);

            try
            {
                foreach (var row in selected)
                {
                    var entry = _manifest.GetEntry(row.Item.Year);
                    if (entry == null) continue;

                    // Step 1 — open browser
                    if (!string.IsNullOrEmpty(row.Item.PageUrl))
                        Process.Start(new ProcessStartInfo(string.Format(_downloadUrlFormat,row.Item.PageUrl)) { UseShellExecute = true });

                    // Step 2 — wait for user to confirm download
                    var confirmed = MessageBox.Show(
                        $"Please download {row.Item.DisplayName} from the browser window that just opened.\n\nClick OK when you have downloaded the file.",
                        "Download Season Pack",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Information);

                    if (confirmed != MessageBoxResult.OK) continue;

                    // Step 3 — locate file
                    var fileDialog = new OpenFileDialog
                    {
                        Title = $"Locate the downloaded file for {row.Item.DisplayName}",
                        Filter = "Season pack files (*.rwgp)|*.rwgp|All files (*.*)|*.*",
                        CheckFileExists = true
                    };

                    if (fileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) continue;

                    // Step 4 — install
                    StatusLabel.Text = $"INSTALLING {row.Item.DisplayName}...";
                    await Task.Run(() => _seasonModInstaller.InstallSeasonMod(fileDialog.FileName));
                    
                    AnyDownloaded = true;

                    row.Item.Availability = SeasonAvailability.UpToDate;
                    row.IsSelected = false;
                    row.NotifyRefresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Installation failed:\n\n{ex.Message}",
                    "Install Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
                RefreshDownloadButton();
                StatusLabel.Text = string.Empty;
            }
        }

        // -------------------------------------------------------------------------
        // Busy state
        // -------------------------------------------------------------------------

        private void SetBusy(bool busy)
        {
            DownloadButton.IsEnabled = !busy;
            CloseButton.IsEnabled = !busy;
            SeasonsItemsControl.IsEnabled = !busy;
            StatusPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // -------------------------------------------------------------------------
    // Row view model
    // -------------------------------------------------------------------------

    public class CatalogRow : INotifyPropertyChanged
    {
        public SeasonDisplayItem Item { get; }

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool CanSelect => Item.Availability is SeasonAvailability.NotInstalled
                                                   or SeasonAvailability.UpdateAvailable;

        public string DisplayName => Item.DisplayName;
        public string StatusLabel => Item.StatusLabel;
        public string FileSizeLabel => Item.FileSizeLabel;
        public SeasonAvailability Availability => Item.Availability;

        public CatalogRow(SeasonDisplayItem item)
        {
            Item = item;
        }

        public void NotifyRefresh()
        {
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(Availability));
            OnPropertyChanged(nameof(CanSelect));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}