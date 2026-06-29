using System.IO.Compression;
using System.Text.Json;
using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Updater;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Ams2ChEd.Business.AMS2.Services
{
    public class ExternalLiveriesInstaller
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
                    var sourceDir = Path.Combine(tempDir, entry.SourcePath);
                    if (!Directory.Exists(sourceDir))
                        continue;

                    var destDir = Path.Combine(seasonDestPath, entry.DestinationPath);
                    CopyDirectory(sourceDir, destDir);
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
                using var archive = ArchiveFactory.Open(archivePath);
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    entry.WriteToDirectory(destinationDir, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);

            foreach (var subDir in Directory.GetDirectories(sourceDir))
                CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
        }
    }
}
