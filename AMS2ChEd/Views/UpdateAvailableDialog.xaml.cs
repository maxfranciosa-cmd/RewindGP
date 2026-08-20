using AMS2ChEd.Business.Updater.Models;
using AMS2ChEd.Resources;
using AMS2ChEd.Views;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace AMS2ChEd.Dialogs
{
    public partial class UpdateAvailableDialog : Window
    {
        private readonly UpdateCheckResult _update;
        private readonly string[] _originalArgs;

        public UpdateAvailableDialog(UpdateCheckResult update, string[] originalArgs)
        {
            InitializeComponent();
            _update = update;
            CurrentVersionText.Text = update.CurrentVersion;
            LatestVersionText.Text = update.LatestVersion;
            _originalArgs = originalArgs;
        }

        // -------------------------------------------------------------------------
        // Automatic download & install
        // -------------------------------------------------------------------------

        private async void OnDownloadAndInstallClicked(object sender, RoutedEventArgs e)
        {
            AutoPanel.IsEnabled = false;

            var progressWindow = new ProgressWindow(Strings.UpdateAvailableDialog_DownloadingMessage);
            progressWindow.Owner = this;
            progressWindow.Show();

            var tempZipPath = Path.Combine(Path.GetTempPath(), $"RewindGP-Update-{Guid.NewGuid()}.zip");

            try
            {
                await Task.Run(async () =>
                {
                    using var http = new HttpClient();
                    using var response = await http.GetAsync(_update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = File.Create(tempZipPath);
                    await contentStream.CopyToAsync(fileStream);
                });

                progressWindow.Close();
                LaunchUpdater(tempZipPath);
            }
            catch (Exception ex)
            {
                if (progressWindow.IsLoaded)
                    progressWindow.Close();

                if (File.Exists(tempZipPath))
                {
                    try { File.Delete(tempZipPath); } catch { /* best effort */ }
                }

                AutoPanel.IsEnabled = true;

                MessageBox.Show(
                    $"{Strings.UpdateAvailableDialog_DownloadFailedMessage}\n\n{ex.Message}",
                    Strings.UpdateAvailableDialog_DownloadFailedTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // -------------------------------------------------------------------------
        // Manual fallback — Step 1: open download page in browser
        // -------------------------------------------------------------------------

        private void OnManualFallbackClicked(object sender, RoutedEventArgs e)
        {
            AutoPanel.Visibility = Visibility.Collapsed;
            Step1Panel.Visibility = Visibility.Visible;
        }

        private void OnGoToDownloadPageClicked(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(_update.PageUrl) { UseShellExecute = true });

            // Advance to step 2
            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Visible;
        }

        // -------------------------------------------------------------------------
        // Manual fallback — Step 2: locate downloaded file and launch updater
        // -------------------------------------------------------------------------

        private void OnLocateFileClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = Strings.UpdateAvailableDialog_LocateFileTitle,
                Filter = Strings.UpdateAvailableDialog_FileFilter,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            LaunchUpdater(dialog.FileName);
        }

        // -------------------------------------------------------------------------
        // Shared: hand a downloaded/located zip off to AMS2ChEd.Updater
        // -------------------------------------------------------------------------

        private void LaunchUpdater(string zipPath)
        {
            var installDir = AppDomain.CurrentDomain.BaseDirectory;
            var updaterDir = Path.Combine(installDir, "Updater");

            // Copy entire Updater folder to temp
            var tempUpdaterDir = Path.Combine(Path.GetTempPath(), $"AMS2ChEd.Updater-{Guid.NewGuid()}");
            CopyDirectory(updaterDir, tempUpdaterDir);

            var updaterExe = Path.Combine(tempUpdaterDir, "AMS2ChEd.Updater.exe");
            var pid = Process.GetCurrentProcess().Id;
            var arguments = $"{pid}|{installDir}|{zipPath}|{_originalArgs}|{_update.LatestVersion}";

            Process.Start(new ProcessStartInfo(updaterExe, arguments)
            {
                UseShellExecute = true,
                WorkingDirectory = tempUpdaterDir
            });

            Application.Current.Shutdown();
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }

        // -------------------------------------------------------------------------
        // Skip
        // -------------------------------------------------------------------------

        private void OnSkipClicked(object sender, RoutedEventArgs e) => Close();
    }
}
