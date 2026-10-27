using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Views;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace AMS2ChEd.Updater
{
    /// <summary>
    /// The player's side of bringing artwork in: getting a season's community skin pack (open its
    /// page, pick the downloaded archive, import it), and importing any file handed over - a skin
    /// pack archive, whose season is worked out from its contents, or a season pack, installed as
    /// ever. Messages are not localized yet, like the rest of the updater prompts.
    /// </summary>
    public class SkinPackImportFlow
    {
        private readonly ISkinPackImporter _importer;
        private readonly ISeasonPackInstaller _packInstaller;
        private readonly IExternalLiveriesInstaller _externalLiveriesInstaller;
        private readonly IExternalLiveriesPrompt _externalLiveriesPrompt;

        public SkinPackImportFlow(ISkinPackImporter importer, ISeasonPackInstaller packInstaller,
            IExternalLiveriesInstaller externalLiveriesInstaller, IExternalLiveriesPrompt externalLiveriesPrompt)
        {
            _importer = importer;
            _packInstaller = packInstaller;
            _externalLiveriesInstaller = externalLiveriesInstaller;
            _externalLiveriesPrompt = externalLiveriesPrompt;
        }

        public bool HasSkinPack(int year) => _importer?.HasSkinPack(year) == true;

        public bool IsImported(int year) => _importer?.IsImported(year) == true;

        /// <summary>The file types the import accepts, for a file picker's filter.</summary>
        public string FileFilter
        {
            get
            {
                var patterns = (_importer?.ArchiveExtensions ?? Array.Empty<string>())
                    .Append(_packInstaller.PackFileExtension)
                    .Select(e => "*" + e);
                return $"Livery or season packs ({string.Join(";", patterns)})|{string.Join(";", patterns)}|All files (*.*)|*.*";
            }
        }

        /// <summary>
        /// Guided: opens the season's skin pack page, then asks for the downloaded archive and
        /// imports it. True when it was imported.
        /// </summary>
        public async Task<bool> GetLiveriesAsync(int year, Window owner)
        {
            if (!HasSkinPack(year))
                return false;

            var name = _importer.SkinPackName(year) ?? $"the {year} skin pack";
            if (MessageBox.Show(owner,
                    $"The {year} liveries come from a community skin pack, {name}.\n\nClicking OK opens its download page. Download the file, then come back here.",
                    "Get the liveries", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
                return false;

            Process.Start(new ProcessStartInfo(_importer.SkinPackUrl(year)) { UseShellExecute = true });

            if (MessageBox.Show(owner, $"Click OK to select the downloaded file of {name}.", "Locate the downloaded file",
                    MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
                return false;

            var dialog = new OpenFileDialog
            {
                Title = $"Locate {name}",
                Filter = $"Archives ({string.Join(";", _importer.ArchiveExtensions.Select(e => "*" + e))})|{string.Join(";", _importer.ArchiveExtensions.Select(e => "*" + e))}|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return false;

            return await ImportArchiveAsync(year, dialog.FileName, owner);
        }

        /// <summary>A file picker over every importable file type, then ImportFileAsync.</summary>
        public async Task<bool> PickAndImportAsync(Window owner)
        {
            var dialog = new OpenFileDialog { Title = "Import a livery or season pack", Filter = FileFilter, CheckFileExists = true };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return false;
            return await ImportFileAsync(dialog.FileName, owner);
        }

        /// <summary>
        /// Any file handed over (picked, dropped): a season pack is installed as ever; a skin pack
        /// archive goes to the season it belongs to. True when something was imported.
        /// </summary>
        public async Task<bool> ImportFileAsync(string path, Window owner)
        {
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, _packInstaller.PackFileExtension, StringComparison.OrdinalIgnoreCase))
                return await InstallSeasonPackAsync(path, owner);

            if (_importer == null || !_importer.ArchiveExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show(owner, $"{Path.GetFileName(path)} is neither a skin pack archive nor a season pack.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            IReadOnlyList<int> years;
            try
            {
                years = await Task.Run(() => _importer.DetectSeasons(path));
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"{Path.GetFileName(path)} could not be read: {ex.Message}", "Import", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (years.Count == 0)
            {
                MessageBox.Show(owner, $"{Path.GetFileName(path)} is not a skin pack Rewind GP knows for any of its seasons.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            bool any = false;
            foreach (var year in years)
            {
                // one archive may serve several seasons: ask for each beyond the best match
                if (years.Count > 1 && MessageBox.Show(owner, $"This archive has the liveries of {year}. Import them for {year}?", "Import",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    continue;
                any |= await ImportArchiveAsync(year, path, owner);
            }
            return any;
        }

        private async Task<bool> ImportArchiveAsync(int year, string archive, Window owner)
        {
            var progressWindow = new ProgressWindow($"Importing the {year} liveries...") { Owner = owner };
            progressWindow.Show();
            SkinPackImportResult result;
            try
            {
                var progress = new Progress<string>(message => progressWindow.MessageText.Text = message);
                result = await _importer.ImportAsync(year, archive, progress);
            }
            catch (Exception ex)
            {
                result = new SkinPackImportResult { Message = ex.Message };
            }
            finally
            {
                progressWindow.Close();
            }

            var notes = result.Notes?.Count > 0 ? "\n\n" + string.Join("\n", result.Notes) : "";
            MessageBox.Show(owner, result.Message + notes, result.Success ? "Liveries imported" : "Import failed",
                MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            return result.Success;
        }

        private async Task<bool> InstallSeasonPackAsync(string path, Window owner)
        {
            var progressWindow = new ProgressWindow { Owner = owner };
            progressWindow.Show();
            SeasonModInstallResult result;
            try
            {
                result = await Task.Run(() => _packInstaller.InstallSeasonMod(path));
            }
            finally
            {
                progressWindow.Close();
            }

            if (result.Success && _externalLiveriesInstaller.HasExternalLiveries(result.SeasonYear))
                await _externalLiveriesInstaller.InstallAsync(result.SeasonYear, _externalLiveriesPrompt);

            MessageBox.Show(owner, result.Message, "Season pack", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            return result.Success;
        }
    }
}
