using System.IO.Compression;
using System.Text.Json;
using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Updater;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Ams2ChEd.Business.AMS2.Services
{
    public class ExternalLiveriesInstaller : IExternalLiveriesInstaller
    {
        private readonly string _baseDirectory;

        public ExternalLiveriesInstaller(string baseDirectory = null)
        {
            _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        public bool HasExternalLiveries(int seasonYear)
        {
            return File.Exists(StoragePaths.ExternalLiveriesFilePath(seasonYear));
        }

        public async Task<bool> InstallAsync(int seasonYear, IExternalLiveriesPrompt prompt)
        {
            var configPath = StoragePaths.ExternalLiveriesFilePath(seasonYear);
            var configJson = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<ExternalLiveriesConfig>(configJson, DefaultJsonSerializerOptions.Instance);

            var downloadedPath = await prompt.PromptDownloadAsync(config.Url);
            if (downloadedPath == null)
                return false;

            var tempDir = Path.Combine(Path.GetTempPath(), $"ams2extliveries_{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(tempDir);
                ExtractArchive(downloadedPath, tempDir);

                var seasonDestPath = Path.Combine(_baseDirectory, "Seasons", seasonYear.ToString());
                foreach (var entry in config.Entries)
                {
                    var sourcePath = Path.Combine(tempDir, entry.SourcePath);
                    if (!File.Exists(sourcePath))
                        continue;

                    var destPath = Path.Combine(seasonDestPath, entry.DestinationPath);
                    var destPathFolder = Path.GetDirectoryName(destPath);

                    if(!Directory.Exists(destPathFolder))
                        Directory.CreateDirectory(destPathFolder);


                    File.Copy(sourcePath, destPath, true);
                }

                return true;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); }
                    catch { }
                }
            }
        }

        private static void ExtractArchive(string archivePath, string destinationDir)
        {
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, destinationDir, overwriteFiles: true);
            }
            else
            {
                var options = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };
                using var archive = ArchiveFactory.OpenArchive(archivePath);

                // Solid archives (the usual 7z, solid rar) need one sequential pass: extracting entry
                // by entry restarts decompression from the start of the block for every file, which
                // turns a minute's work into most of an hour on a large livery pack. SharpCompress only
                // allows that pass on solid or 7z archives, so anything else goes entry by entry.
                if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
                {
                    using var reader = archive.ExtractAllEntries();
                    reader.WriteAllToDirectory(destinationDir, options);
                }
                else
                {
                    foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                        entry.WriteToDirectory(destinationDir, options);
                }
            }
        }
    }
}
