using System.Text.Json;
using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Updater;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ams2ChEd.Business.AMS2.Services
{
    /// <summary>
    /// Imports a season's community skin pack as the season's cosmetics.json describes it: copies
    /// the files it names into Seasons/&lt;year&gt;/, builds each team's helmet and visor sponsor
    /// layers from the pack's own helmets, and records what it wrote (skin_pack.json) so the
    /// season loader can see it and a removal can undo it.
    /// </summary>
    public class SkinPackImporter : ISkinPackImporter
    {
        // A year is the archive's when at least this share of its identifying files is in it.
        private const double DetectShare = 0.5;

        private readonly string _bakedSeasonsFolder;
        private readonly string _seasonsFolder;
        private readonly string _visorMasksFolder;
        private readonly string _helmetMasksFolder;
        private readonly string _sampleSponsorsFolder;

        public SkinPackImporter(string bakedSeasonsFolder = null, string seasonsFolder = null, string visorMasksFolder = null, string helmetMasksFolder = null, string sampleSponsorsFolder = null)
        {
            _sampleSponsorsFolder = sampleSponsorsFolder ?? StoragePaths.SampleHelmetSponsorsFolder;
            _bakedSeasonsFolder = bakedSeasonsFolder ?? StoragePaths.BakedSeasonsFolder;
            _seasonsFolder = seasonsFolder ?? StoragePaths.SeasonsFolder;
            _visorMasksFolder = visorMasksFolder ?? StoragePaths.VisorMasksFolder;
            _helmetMasksFolder = helmetMasksFolder ?? StoragePaths.HelmetSponsorMasksFolder;
        }

        public IReadOnlyList<string> ArchiveExtensions => ArchiveFiles.Extensions;

        public bool HasSkinPack(int seasonYear) => SkinPack(seasonYear) != null;

        public string SkinPackUrl(int seasonYear) => SkinPack(seasonYear)?.Url;

        public string SkinPackName(int seasonYear) => SkinPack(seasonYear)?.Name;

        public bool IsImported(int seasonYear) => File.Exists(RecordPath(seasonYear));

        private SkinPackSource SkinPack(int year)
        {
            var pack = SeasonCosmeticsFiles.Read(year, _bakedSeasonsFolder)?.SkinPack;
            return pack?.Entries?.Count > 0 ? pack : null;
        }

        private string SeasonFolder(int year) => Path.Combine(_seasonsFolder, year.ToString());

        private string RecordPath(int year) => Path.Combine(SeasonFolder(year), "skin_pack.json");

        public IReadOnlyList<int> DetectSeasons(string archivePath)
        {
            var files = ArchiveFiles.ListFiles(archivePath);
            return DetectSeasons(files);
        }

        public IReadOnlyList<int> DetectSeasons(IReadOnlyCollection<string> archiveFiles)
        {
            var scores = new List<(int Year, double Share)>();
            foreach (var year in KnownYears())
            {
                var pack = SkinPack(year);
                if (pack == null)
                    continue;

                // the identifying files, or the first files the season takes
                var wanted = pack.Detect?.Count > 0 ? pack.Detect : pack.Entries.Select(e => e.SourcePath).Take(8).ToList();
                double share = (double)wanted.Count(w => ArchiveFiles.Find(archiveFiles, w) != null) / wanted.Count;
                if (share >= DetectShare)
                    scores.Add((year, share));
            }
            return scores.OrderByDescending(s => s.Share).ThenBy(s => s.Year).Select(s => s.Year).ToList();
        }

        private IEnumerable<int> KnownYears()
        {
            if (!Directory.Exists(_bakedSeasonsFolder))
                return Enumerable.Empty<int>();

            return Directory.GetDirectories(_bakedSeasonsFolder)
                .Select(d => int.TryParse(Path.GetFileName(d), out var y) ? y : 0)
                .Where(y => y > 0)
                .OrderBy(y => y);
        }

        public Task<SkinPackImportResult> ImportAsync(int seasonYear, string archivePath, IProgress<string> progress = null, CancellationToken cancellationToken = default)
            => Task.Run(() => Import(seasonYear, archivePath, progress, cancellationToken), cancellationToken);

        private SkinPackImportResult Import(int year, string archivePath, IProgress<string> progress, CancellationToken ct)
        {
            var result = new SkinPackImportResult();
            var cosmetics = SeasonCosmeticsFiles.Read(year, _bakedSeasonsFolder);
            var pack = cosmetics?.SkinPack;
            if (pack == null || pack.Entries.Count == 0)
            {
                result.Message = $"The app knows no skin pack for {year}.";
                return result;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), $"rewindgp_skinpack_{Guid.NewGuid():N}");
            try
            {
                progress?.Report("Unpacking the skin pack...");
                Directory.CreateDirectory(tempDir);
                ArchiveFiles.ExtractAll(archivePath, tempDir);
                ct.ThrowIfCancellationRequested();

                var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)
                    .Select(f => ArchiveFiles.Normalize(Path.GetRelativePath(tempDir, f)))
                    .ToList();
                string Source(string wanted) => ArchiveFiles.Find(files, wanted) is string found ? Path.Combine(tempDir, found) : null;

                // a fresh import replaces the previous one
                Remove(year);
                var record = new SkinPackRecord { Name = pack.Name, Archive = Path.GetFileName(archivePath), Imported = DateTime.Now };
                var seasonFolder = SeasonFolder(year);

                progress?.Report("Copying the liveries...");
                foreach (var entry in pack.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    var source = Source(entry.SourcePath);
                    if (source == null)
                    {
                        result.FilesMissing++;
                        continue;
                    }

                    var destination = Path.GetFullPath(Path.Combine(seasonFolder, entry.DestinationPath));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(source, destination, true);
                    record.Files.Add(ArchiveFiles.Normalize(entry.DestinationPath));
                    result.FilesCopied++;
                }

                progress?.Report("Building the helmet sponsors...");
                BuildSponsorLayers(year, cosmetics, Source, seasonFolder, record, ct);

                Directory.CreateDirectory(seasonFolder);
                File.WriteAllText(RecordPath(year), JsonSerializer.Serialize(record, DefaultJsonSerializerOptions.Instance));

                result.Notes = record.Notes;
                result.Success = result.FilesCopied > 0;
                result.Message = result.Success
                    ? $"Imported {result.FilesCopied} files of {pack.Name ?? "the skin pack"} for {year}" + (result.FilesMissing > 0 ? $"; {result.FilesMissing} the app expected were not in the archive." : ".")
                    : $"None of the files the {year} season needs were in this archive.";
                if (!result.Success)
                    Remove(year);
                return result;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        // Per team: the shipped sample with the pack helmet seen through the team's sponsor map (else
        // the helmet model's generic one) over it, and the pack visor seen through the model's visor
        // strip.
        private void BuildSponsorLayers(int year, SeasonCosmetics cosmetics, Func<string, string> source, string seasonFolder, SkinPackRecord record, CancellationToken ct)
        {
            var model = cosmetics.SkinPack.HelmetModel ?? "Modern";
            var cosmeticsFolder = Path.Combine(_bakedSeasonsFolder, year.ToString(), "cosmetics");

            foreach (var (teamId, team) in cosmetics.SkinPack.Helmets ?? new Dictionary<string, SkinPackTeamHelmets>())
            {
                ct.ThrowIfCancellationRequested();
                cosmetics.Teams.TryGetValue(teamId, out var shipped);

                var helmet = team.Helmet == null ? null : source(team.Helmet);
                string problem = null;
                Image<Rgba32> cut = null;
                if (helmet != null)
                {
                    var map = string.IsNullOrEmpty(team.Map)
                        ? SponsorLayerCutter.HelmetMaskPath(model, _helmetMasksFolder)
                        : Path.GetFullPath(Path.Combine(cosmeticsFolder, team.Map));
                    cut = SponsorLayerCutter.Cut(helmet, map, out problem);
                }
                else if (team.Helmet != null)
                {
                    problem = $"{Path.GetFileName(team.Helmet)} is not in the archive";
                }

                var samples = new List<string>();
                foreach (var sample in team.Samples ?? new List<string>())
                {
                    if (File.Exists(Path.Combine(_sampleSponsorsFolder, sample)))
                        samples.Add(sample);
                    else
                        record.Notes.Add($"{teamId}: the sample {sample} is not shipped");
                }

                var mapName = string.IsNullOrEmpty(team.Map) ? $"the generic {model} map" : "its own map";
                using (cut)
                {
                    foreach (var sample in samples)
                    {
                        using var layer = SponsorLayerCutter.OverSample(Path.GetFullPath(Path.Combine(_sampleSponsorsFolder, sample)), cut);
                        Save(layer, seasonFolder, SkinPackTeamHelmets.LayerPath(teamId, sample), record);
                        record.Notes.Add($"{teamId}: helmet sponsors from the sample {sample}{(cut != null ? $" with {mapName} over it" : "")}");
                    }

                    if (samples.Count == 0 && cut != null)
                    {
                        Save(cut, seasonFolder, shipped?.HelmetSponsors ?? $"external_liveries/helmet_sponsors/{teamId}/default.png", record);
                        record.Notes.Add($"{teamId}: helmet sponsors from {mapName}");
                    }
                    else if (samples.Count == 0 && problem != null)
                    {
                        record.Notes.Add($"{teamId}: no helmet sponsors - {problem}");
                    }
                }

                var visor = team.Visor == null ? null : source(team.Visor);
                if (visor != null)
                {
                    var layer = SponsorLayerCutter.Cut(visor, SponsorLayerCutter.VisorMaskPath(model, _visorMasksFolder), out var visorProblem);
                    if (layer != null)
                    {
                        Save(layer, seasonFolder, shipped?.VisorSponsors ?? $"external_liveries/helmet_sponsors/{teamId}/default_visor.png", record);
                        layer.Dispose();
                    }
                    else
                    {
                        record.Notes.Add($"{teamId}: no visor sponsors - {visorProblem}");
                    }
                }
                else if (team.Visor != null)
                {
                    record.Notes.Add($"{teamId}: no visor sponsors - {Path.GetFileName(team.Visor)} is not in the archive");
                }
            }
        }

        private static void Save(SixLabors.ImageSharp.Image layer, string seasonFolder, string path, SkinPackRecord record)
        {
            var full = Path.GetFullPath(Path.Combine(seasonFolder, path));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            layer.SaveAsPng(full);
            record.Files.Add(ArchiveFiles.Normalize(path));
        }

        public void Remove(int seasonYear)
        {
            var recordPath = RecordPath(seasonYear);
            if (!File.Exists(recordPath))
                return;

            var seasonFolder = SeasonFolder(seasonYear);
            SkinPackRecord record = null;
            try
            {
                record = JsonSerializer.Deserialize<SkinPackRecord>(File.ReadAllText(recordPath), DefaultJsonSerializerOptions.Instance);
            }
            catch { }

            foreach (var file in record?.Files ?? new List<string>())
            {
                var full = Path.GetFullPath(Path.Combine(seasonFolder, file));
                // only ever inside the season's own folder
                if (full.StartsWith(Path.GetFullPath(seasonFolder), StringComparison.OrdinalIgnoreCase))
                    try { File.Delete(full); } catch { }
            }
            File.Delete(recordPath);
            DeleteEmptyFolders(seasonFolder);
        }

        private static void DeleteEmptyFolders(string folder)
        {
            if (!Directory.Exists(folder))
                return;
            foreach (var sub in Directory.GetDirectories(folder))
                DeleteEmptyFolders(sub);
            if (!Directory.EnumerateFileSystemEntries(folder).Any())
                try { Directory.Delete(folder); } catch { }
        }
    }
}
