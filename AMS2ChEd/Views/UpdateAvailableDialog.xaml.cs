using AMS2ChEd.Business.Updater.Models;
using System.Diagnostics;
using System.IO;
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
        // Step 1 — open download page in browser
        // -------------------------------------------------------------------------

        private void OnGoToDownloadPageClicked(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(_update.PageUrl) { UseShellExecute = true });

            // Advance to step 2
            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Visible;
        }

        // -------------------------------------------------------------------------
        // Step 2 — locate downloaded file and launch updater
        // -------------------------------------------------------------------------

        private void OnLocateFileClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Locate the downloaded Rewind GP ZIP file",
                Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var zipPath = dialog.FileName;
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