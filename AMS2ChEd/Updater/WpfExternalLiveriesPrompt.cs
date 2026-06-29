using AMS2ChEd.Business.Updater;
using System.Diagnostics;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace AMS2ChEd.Updater
{
    public class WpfExternalLiveriesPrompt : IExternalLiveriesPrompt
    {
        public async Task<string?> PromptDownloadAsync(string url)
        {
            var confirm = MessageBox.Show(
                "This season pack requires an external livery pack.\n\nClicking OK will open the download page. Download the file, then come back here.",
                "External Livery Pack Required",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (confirm != MessageBoxResult.OK) return null;

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            var ready = MessageBox.Show(
                "Click OK once you have downloaded the livery pack.",
                "Locate Downloaded File",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (ready != MessageBoxResult.OK) return null;

            var dialog = new OpenFileDialog
            {
                Title = "Locate the downloaded livery pack",
                Filter = "Archive files (*.zip;*.rar)|*.zip;*.rar|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != DialogResult.OK) return null;

            return await Task.FromResult(dialog.FileName);
        }
    }
}
