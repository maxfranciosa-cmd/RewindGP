using System.IO.Compression;
using System.Text.Json;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;

namespace AMS2ChEd.Business.AMS2.Services
{
    public class SeasonModInstaller
    {
        private readonly IDriversLoader<Ams2DriverData> _driversLoader;
        private readonly ITeamsLoader _teamsLoader;
        private readonly string _baseDirectory;

        public SeasonModInstaller(
            IDriversLoader<Ams2DriverData> driversLoader,
            ITeamsLoader teamsLoader,
            string baseDirectory = null)
        {
            _driversLoader = driversLoader;
            _teamsLoader = teamsLoader;
            _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        public SeasonModInstallResult InstallSeasonMod(string zipFilePath)
        {
            var result = new SeasonModInstallResult();
            string tempExtractPath = null;

            try
            {
                // Validate zip file exists
                if (!File.Exists(zipFilePath))
                {
                    throw new FileNotFoundException($"Mod file not found: {zipFilePath}");
                }

                // Create temporary extraction directory
                tempExtractPath = Path.Combine(Path.GetTempPath(), $"ams2mod_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempExtractPath);

                // Extract zip file
                ZipFile.ExtractToDirectory(zipFilePath, tempExtractPath);

                // Find the season folder (should be the only directory in the zip root)
                var seasonFolders = Directory.GetDirectories(tempExtractPath);
                if (seasonFolders.Length == 0)
                {
                    throw new InvalidOperationException("Mod package must contain a season folder");
                }
                if (seasonFolders.Length > 1)
                {
                    throw new InvalidOperationException("Mod package must contain only one season folder");
                }

                string seasonFolderPath = seasonFolders[0];
                string seasonFolderName = Path.GetFileName(seasonFolderPath);

                // Read season.json from inside the season folder
                string seasonJsonPath = Path.Combine(seasonFolderPath, "season.json");
                if (!File.Exists(seasonJsonPath))
                {
                    throw new InvalidOperationException($"Season folder '{seasonFolderName}' must contain season.json");
                }

                string seasonJson = File.ReadAllText(seasonJsonPath);
                var seasonData = JsonSerializer.Deserialize<Ams2Season>(seasonJson, DefaultJsonSerializerOptions.Instance);
                int seasonYear = seasonData.Year;
                result.SeasonYear = seasonYear;

                // Verify folder name matches season year
                if (seasonFolderName != seasonYear.ToString())
                {
                    throw new InvalidOperationException(
                        $"Season folder name '{seasonFolderName}' does not match season year '{seasonYear}' in season.json");
                }

                // Check if season already exists
                string seasonDestPath = Path.Combine(_baseDirectory, "seasons", seasonYear.ToString());
                result.IsUpdate = Directory.Exists(seasonDestPath) && File.Exists(Path.Combine(seasonDestPath, "season.json"));

                // Process season files (first 6 folders + season.json & drivers.json)
                ProcessSeasonFiles(seasonFolderPath, seasonYear, result);

                result.Success = true;
                result.Message = result.IsUpdate
                    ? $"Successfully updated season mod for {seasonYear}"
                    : $"Successfully installed season mod for {seasonYear}";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Failed to install season mod: {ex.Message}";
                result.Exception = ex;
            }
            finally
            {
                // Clean up temporary directory
                if (tempExtractPath != null && Directory.Exists(tempExtractPath))
                {
                    try
                    {
                        Directory.Delete(tempExtractPath, true);
                    }
                    catch (Exception ex)
                    {
                        result.CleanupWarning = $"Warning: Could not delete temporary files: {ex.Message}";
                    }
                }
            }

            return result;
        }

        private void ProcessSeasonFiles(string seasonFolderPath, int seasonYear, SeasonModInstallResult result)
        {
            string seasonDestPath = Path.Combine(_baseDirectory, "seasons", seasonYear.ToString());
            Directory.CreateDirectory(seasonDestPath);

            var seasonFolders = new[]
            {
                "car_liveries",
                "gpcovers",
                "helmet_liveries",
                "helmet_sponsors",
                "liveries_xml",
                "static_assets",
                "scenarios",
                "previews"
            };

            foreach (var folder in seasonFolders)
            {
                string sourcePath = Path.Combine(seasonFolderPath, folder);
                if (Directory.Exists(sourcePath))
                {
                    string destPath = Path.Combine(seasonDestPath, folder);
                    CopyDirectory(sourcePath, destPath, true, result);
                    result.CopiedFolders.Add(folder);
                }
            }

            // Copy season.json
            string seasonJsonSource = Path.Combine(seasonFolderPath, "season.json");
            string seasonJsonDest = Path.Combine(seasonDestPath, "season.json");
            bool seasonJsonExists = File.Exists(seasonJsonDest);

            File.Copy(seasonJsonSource, seasonJsonDest, true);
            result.CopiedFiles.Add("season.json");

            if (seasonJsonExists)
            {
                result.OverwrittenFiles.Add("season.json");
            }

            // Copy drivers.json
            string driversJsonSource = Path.Combine(seasonFolderPath, "drivers.json");
            string driversJsonDest = Path.Combine(seasonDestPath, "drivers.json");
            bool driversJsonExists = File.Exists(driversJsonDest);

            File.Copy(driversJsonSource, driversJsonDest, true);
            result.CopiedFiles.Add("drivers.json");

            if (driversJsonExists)
            {
                result.OverwrittenFiles.Add("drivers.json");
            }
        }

        private void CopyDirectory(string sourceDir, string destDir, bool overwrite, SeasonModInstallResult result = null)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                bool fileExists = File.Exists(destFile);

                File.Copy(file, destFile, overwrite);

                if (fileExists && result != null)
                {
                    result.OverwrittenFiles.Add(Path.GetFileName(destFile));
                }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir, overwrite, result);
            }
        }
    }

    public class SeasonModInstallResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int SeasonYear { get; set; }
        public bool IsUpdate { get; set; }
        public Exception Exception { get; set; }
        public string CleanupWarning { get; set; }

        public List<string> CopiedFolders { get; set; } = new List<string>();
        public List<string> CopiedFiles { get; set; } = new List<string>();
        public List<string> OverwrittenFiles { get; set; } = new List<string>();

        public string GetDetailedReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Season Mod {(IsUpdate ? "Update" : "Installation")} Report - Year {SeasonYear}");
            report.AppendLine($"Status: {(Success ? "SUCCESS" : "FAILED")}");
            report.AppendLine($"Message: {Message}");
            report.AppendLine();

            if (IsUpdate)
            {
                report.AppendLine("⚠ This was an update to an existing season.");
                report.AppendLine();
            }

            if (CopiedFolders.Any())
            {
                report.AppendLine($"Copied Folders: {string.Join(", ", CopiedFolders)}");
            }

            if (OverwrittenFiles.Any())
            {
                report.AppendLine($"⚠ Overwritten Files ({OverwrittenFiles.Count}): {string.Join(", ", OverwrittenFiles.Take(5))}");
                if (OverwrittenFiles.Count > 5)
                {
                    report.AppendLine($"   ...and {OverwrittenFiles.Count - 5} more files");
                }
            }

            if (!string.IsNullOrEmpty(CleanupWarning))
            {
                report.AppendLine();
                report.AppendLine(CleanupWarning);
            }

            return report.ToString();
        }
    }

    public class SeasonExistsCheckResult
    {
        public bool Success { get; set; }
        public int SeasonYear { get; set; }
        public bool SeasonExists { get; set; }
        public Exception Exception { get; set; }
    }
}