using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Models;
using AMS2ChEd.Dialogs;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace AMS2ChEd.Updater
{
    public class WpfSeasonDownloadPrompt : ISeasonDownloadPrompt
    {
        private readonly SeasonModInstaller _seasonModInstaller;
        private readonly ExternalLiveriesInstaller _externalLiveriesInstaller;
        private readonly IExternalLiveriesPrompt _externalLiveriesPrompt;
        private readonly string _downloadUrlFormat;

        public WpfSeasonDownloadPrompt(
            string downloadUrlFormat,
            SeasonModInstaller seasonModInstaller,
            ExternalLiveriesInstaller externalLiveriesInstaller,
            IExternalLiveriesPrompt externalLiveriesPrompt)
        {
            _seasonModInstaller = seasonModInstaller;
            _externalLiveriesInstaller = externalLiveriesInstaller;
            _externalLiveriesPrompt = externalLiveriesPrompt;
            _downloadUrlFormat = downloadUrlFormat;
        }

        // -------------------------------------------------------------------------
        // Download (not installed)
        // -------------------------------------------------------------------------

        public async Task<bool> PromptDownloadAsync(SeasonManifestEntry entry, bool isUpdate = false)
        {
            // Dialog 1 — inform user browser is about to open
            
            var confirm = MessageBox.Show(
                $"{(isUpdate ? $"({entry.DisplayName} is not currently installed in RewindGP.\n\n" : "")}Clicking OK will open the download page for this season.\n\nDownload the file, then come back here.",
                "Download Season Pack",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (confirm != MessageBoxResult.OK) return false;

            // Open browser
            Process.Start(new ProcessStartInfo(string.Format(_downloadUrlFormat, entry.PageUrl)) { UseShellExecute = true });

            // Dialog 2 — wait for user to finish downloading
            var ready = MessageBox.Show(
                $"Click OK to select the downloaded file for {entry.DisplayName}.",
                "Locate Downloaded File",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (ready != MessageBoxResult.OK) return false;

            // File picker
            var dialog = new OpenFileDialog
            {
                Title = $"Locate the downloaded file for {entry.DisplayName}",
                Filter = "Season pack files (*.rwgp;*.zip)|*.rwgp;*.zip|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != DialogResult.OK) return false;

            var rwgpPath = await ResolveRwgpPathAsync(dialog.FileName);
            if (rwgpPath == null) return false;

            var result = await Task.Run(() => _seasonModInstaller.InstallSeasonMod(rwgpPath));

            if (result.Success && _externalLiveriesInstaller.HasExternalLiveries(result.SeasonYear))
            {
                var liveriesInstalled = await _externalLiveriesInstaller.InstallAsync(result.SeasonYear, _externalLiveriesPrompt);
                if (!liveriesInstalled)
                {
                    MessageBox.Show(
                        "The external livery pack was not downloaded. You can get it later by reinstalling the season pack.",
                        "External Liveries Skipped",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            return result.Success;
        }

        // -------------------------------------------------------------------------
        // Update (installed but outdated)
        // -------------------------------------------------------------------------

        public async Task<bool> PromptUpdateAsync(SeasonManifestEntry entry)
        {
            var want = MessageBox.Show(
                $"An update is available for {entry.DisplayName}.\n\nDo you want to update now?",
                "Season Pack Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (want != MessageBoxResult.Yes) return false;

            return await PromptDownloadAsync(entry);
        }

        // -------------------------------------------------------------------------
        // Slug picker (multiple variants for the same year, nothing installed)
        // -------------------------------------------------------------------------

        public async Task<bool> PromptSlugPickerAsync(int year, List<SeasonManifestEntry> options)
        {
            // Show a dialog with hyperlinks for each option
            var dialog = new SlugPickerDialog(year, options, _downloadUrlFormat);
            if (dialog.ShowDialog() != true) return false;

            // File picker
            var fileDialog = new OpenFileDialog
            {
                Title = $"Locate the downloaded {year} season pack",
                Filter = "Season pack files (*.rwgp;*.zip)|*.rwgp;*.zip|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (fileDialog.ShowDialog() != DialogResult.OK) return false;

            var rwgpPath = await ResolveRwgpPathAsync(fileDialog.FileName);
            if (rwgpPath == null) return false;

            var result = await Task.Run(() => _seasonModInstaller.InstallSeasonMod(rwgpPath));
            return result.Success;
        }

        private static async Task<string?> ResolveRwgpPathAsync(string selectedPath)
        {
            if (selectedPath.EndsWith(".rwgp", StringComparison.OrdinalIgnoreCase))
                return selectedPath;

            // It's a ZIP — check it contains exactly one .rwgp
            using var archive = System.IO.Compression.ZipFile.OpenRead(selectedPath);
            var rwgpEntries = archive.Entries
                .Where(e => e.FullName.EndsWith(".rwgp", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rwgpEntries.Count == 0)
            {
                MessageBox.Show(
                    "The selected ZIP file does not contain a season pack (.rwgp) file.",
                    "Invalid File",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }

            if (rwgpEntries.Count > 1)
            {
                MessageBox.Show(
                    "The selected ZIP file contains multiple season pack files. Please extract manually.",
                    "Invalid File",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }

            // Extract the single .rwgp to temp
            var tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"{System.IO.Path.GetFileNameWithoutExtension(rwgpEntries[0].FullName)}-{Guid.NewGuid()}.rwgp");

            await Task.Run(() => rwgpEntries[0].ExtractToFile(tempPath));
            return tempPath;
        }
    }
}
