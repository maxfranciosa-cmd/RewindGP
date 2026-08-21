using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using Ams2ChEd.Business.AMS2.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using static AMS2ChEd.SeasonPackEditor.MainWindow;

namespace AMS2ChEd.SeasonPackEditor.Services
{
    /// <summary>
    /// Loads an already-installed (or previously exported/extracted) Seasons/&lt;year&gt;/ folder back
    /// into an in-memory <see cref="SeasonPackProject"/>, the inverse of the editor's export flow. This
    /// lets an existing season pack be re-opened for editing and re-exported.
    ///
    /// Texture/livery-xml references on the loaded season/teams/drivers are left exactly as they appear
    /// in the source JSON (they're already relative paths in the same convention the editor and export
    /// step use as TextureFiles keys, see SeasonPackPathResolver) - this method's job is only to find the
    /// file each such reference points to on disk and register it in TextureFiles/XmlFiles so export can
    /// find it again without the project needing to live at the canonical path.
    /// </summary>
    public static class SeasonFolderImportService
    {
        public class ImportResult
        {
            public SeasonPackProject Project { get; set; }

            /// <summary>Texture/livery-xml references found on the loaded data that could not be
            /// resolved to a file on disk. The field values themselves are left untouched, so these
            /// can still be fixed up by hand afterward (e.g. via the various "Browse..." buttons).</summary>
            public List<string> MissingSourceFiles { get; } = new();
        }

        public static ImportResult ImportFromFolder(string seasonFolderPath)
        {
            var seasonJsonPath = Path.Combine(seasonFolderPath, "season.json");
            var driversJsonPath = Path.Combine(seasonFolderPath, "drivers.json");

            if (!File.Exists(seasonJsonPath))
                throw new FileNotFoundException($"No season.json found in {seasonFolderPath}.");
            if (!File.Exists(driversJsonPath))
                throw new FileNotFoundException($"No drivers.json found in {seasonFolderPath}.");

            var season = JsonSerializer.Deserialize<Ams2Season>(File.ReadAllText(seasonJsonPath), DefaultJsonSerializerOptions.Instance);

            // Same normalization SeasonLoader.LoadSeason / CalibrateInstalledSeasonJson_Click apply: a team
            // with no second car is a real Driver2Contract with an empty DriverId, not a null contract.
            if (season?.Teams != null)
                foreach (var team in season.Teams)
                    team.Driver2Contract ??= new DriverContract();

            var driversDb = JsonSerializer.Deserialize<DriverRatingsDatabase>(File.ReadAllText(driversJsonPath), DefaultJsonSerializerOptions.Instance);
            var drivers = (driversDb?.Drivers ?? Enumerable.Empty<IDriverData>()).Cast<Ams2DriverData>().ToList();

            var accoladesPath = Path.Combine(seasonFolderPath, "accolades.json");
            var accolades = File.Exists(accoladesPath)
                ? JsonSerializer.Deserialize<HistoricalAccolades>(File.ReadAllText(accoladesPath), DefaultJsonSerializerOptions.Instance)
                : null;
            accolades ??= new HistoricalAccolades { DriverAccolades = new(), TeamsAccolades = new() };
            accolades.DriverAccolades ??= new();
            accolades.TeamsAccolades ??= new();

            var externalLiveriesPath = Path.Combine(seasonFolderPath, "external_liveries.json");
            var externalLiveries = File.Exists(externalLiveriesPath)
                ? JsonSerializer.Deserialize<ExternalLiveriesConfig>(File.ReadAllText(externalLiveriesPath), DefaultJsonSerializerOptions.Instance)
                : null;
            externalLiveries ??= new ExternalLiveriesConfig();

            var result = new ImportResult
            {
                Project = new SeasonPackProject
                {
                    Season = season,
                    Drivers = drivers,
                    TextureFiles = new Dictionary<string, string>(),
                    XmlFiles = new Dictionary<string, string>(),
                    StaticAssetFiles = new List<StaticAssetFile>(),
                    Scenarios = new List<ScenarioEntry>(),
                    ExternalLiveriesConfig = externalLiveries,
                    Accolades = accolades
                }
            };
            var project = result.Project;

            void Track(string relativePath)
            {
                if (string.IsNullOrWhiteSpace(relativePath)) return;
                if (Path.IsPathRooted(relativePath) || Uri.IsWellFormedUriString(relativePath, UriKind.Absolute)) return;

                var absolutePath = ResolveReferencedFile(seasonFolderPath, relativePath);
                if (absolutePath == null)
                {
                    result.MissingSourceFiles.Add(relativePath);
                    return;
                }

                project.TextureFiles[relativePath] = absolutePath;
            }

            foreach (var race in season?.Races ?? Enumerable.Empty<Race>())
                Track(race.CoverPictureUrl);

            foreach (var team in (season?.Teams ?? Enumerable.Empty<ITeamEntry>()).OfType<Ams2TeamEntry>())
            {
                Track(team.BaseLiveryDriver1);
                Track(team.BaseLiveryDriver2);
                Track(team.HelmetSponsors);
                Track(team.VisorSponsors);
                Track(team.LiveryPreview);

                if (team.DriversSpecificHelmet != null)
                    foreach (var texture in team.DriversSpecificHelmet.Values)
                        Track(texture);

                if (team.NumbersPlacements != null)
                    foreach (var placement in team.NumbersPlacements)
                        Track(placement.NumbersTexture);

                if (team.LiveryOverrides != null)
                {
                    foreach (var overrideEntry in team.LiveryOverrides)
                    {
                        Track(overrideEntry.Driver1Livery);
                        Track(overrideEntry.Driver2Livery);
                        Track(overrideEntry.HelmetSponsors);
                        Track(overrideEntry.VisorSponsors);
                        Track(overrideEntry.LiveryPreview);

                        if (overrideEntry.DriversSpecificHelmet != null)
                            foreach (var texture in overrideEntry.DriversSpecificHelmet.Values)
                                Track(texture);

                        if (overrideEntry.NumbersPlacements != null)
                            foreach (var placement in overrideEntry.NumbersPlacements)
                                Track(placement.NumbersTexture);
                    }
                }

                // Livery XML lives on disk as liveries_xml/<teamId>.xml, not embedded in season.json
                // (see Ams2LiveryService, which falls back to that file when team.LiveryXml is empty).
                var xmlPath = Path.Combine(seasonFolderPath, "liveries_xml", $"{team.TeamId}.xml");
                if (File.Exists(xmlPath))
                    project.XmlFiles[team.TeamId] = File.ReadAllText(xmlPath);
            }

            foreach (var driver in drivers)
            {
                Track(driver.PictureUrl);
                Track(driver.BaseHelmetFile);
                Track(driver.BaseVisorFile);
                Track(driver.BaseHelmetFile90s);
                Track(driver.BaseHelmetFile80s);
                Track(driver.BaseVisorFile80s);
                Track(driver.BaseHelmetFile70s);
                Track(driver.BaseVisorFile70s);
            }

            // Static assets: any file under static_assets/ is tracked wholesale, same as browsing a folder
            // for them in the editor (LoadStaticAssetFiles), just rooted at the imported season folder.
            var staticAssetsDir = Path.Combine(seasonFolderPath, "static_assets");
            if (Directory.Exists(staticAssetsDir))
            {
                foreach (var file in Directory.GetFiles(staticAssetsDir, "*.*", SearchOption.AllDirectories).OrderBy(f => f))
                {
                    var relativePath = file.Substring(staticAssetsDir.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var fileInfo = new FileInfo(file);

                    project.StaticAssetFiles.Add(new StaticAssetFile
                    {
                        FilePath = relativePath,
                        FullPath = file,
                        Size = fileInfo.Length,
                        SizeFormatted = FormatFileSize(fileInfo.Length)
                    });
                }
            }

            // Scenarios: each Scenarios/<name>.json only records name/description and the *generated*
            // picture/save file paths - the original wizard SaveConfig isn't exported, so a re-imported
            // scenario can't be reopened in the wizard. It comes back as a legacy entry (name, description,
            // picture, and the existing save file re-attached as GameFileFullPath) that still round-trips
            // through export unchanged.
            var scenariosDir = Path.Combine(seasonFolderPath, "Scenarios");
            if (Directory.Exists(scenariosDir))
            {
                foreach (var scenarioJsonFile in Directory.GetFiles(scenariosDir, "*.json"))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(scenarioJsonFile));
                        var root = doc.RootElement;

                        var entry = new ScenarioEntry
                        {
                            Name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : Path.GetFileNameWithoutExtension(scenarioJsonFile),
                            Description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() : ""
                        };

                        if (root.TryGetProperty("picture", out var pictureProp))
                            entry.PictureFullPath = ResolveScenarioAsset(seasonFolderPath, pictureProp.GetString());

                        if (root.TryGetProperty("game_file", out var gameFileProp))
                            entry.GameFileFullPath = ResolveScenarioAsset(seasonFolderPath, gameFileProp.GetString());

                        project.Scenarios.Add(entry);
                    }
                    catch
                    {
                        // Not a scenario metadata file (or malformed) - skip it rather than fail the whole import.
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a texture/livery-xml relative-path reference to a file that actually exists on disk,
        /// relative to the imported season folder. Handles the two path conventions used elsewhere in the
        /// editor besides plain "subfolder/file.ext": "../&lt;year&gt;/..." (a sibling season folder, resolved
        /// naturally via GetFullPath) and the legacy "Seasons/&lt;year&gt;/..." form (resolved against the
        /// Seasons folder containing the imported one).
        /// </summary>
        private static string ResolveReferencedFile(string seasonFolderPath, string relativePath)
        {
            var direct = SafeGetFullPath(Path.Combine(seasonFolderPath, relativePath));
            if (direct != null && File.Exists(direct))
                return direct;

            var normalized = relativePath.Replace('\\', '/');
            if (normalized.StartsWith("Seasons/", StringComparison.OrdinalIgnoreCase))
            {
                var seasonsRoot = Path.GetDirectoryName(seasonFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var afterSeasonsPrefix = normalized.Substring("Seasons/".Length);
                var legacy = SafeGetFullPath(Path.Combine(seasonsRoot ?? "", afterSeasonsPrefix));
                if (legacy != null && File.Exists(legacy))
                    return legacy;
            }

            return null;
        }

        private static string ResolveScenarioAsset(string seasonFolderPath, string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return null;

            var normalized = storedPath.Replace('\\', '/');
            var scenariosIndex = normalized.IndexOf("Scenarios/", StringComparison.OrdinalIgnoreCase);
            var relative = scenariosIndex >= 0 ? normalized.Substring(scenariosIndex) : normalized;

            var full = SafeGetFullPath(Path.Combine(seasonFolderPath, relative));
            return full != null && File.Exists(full) ? full : null;
        }

        private static string SafeGetFullPath(string path)
        {
            try { return Path.GetFullPath(path); }
            catch { return null; }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
