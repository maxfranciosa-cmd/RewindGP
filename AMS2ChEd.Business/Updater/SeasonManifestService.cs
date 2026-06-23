using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AMS2ChEd.Business.Updater
{
    /// <summary>
    /// Reads the local seasons.json manifest and compares it against installed
    /// season packs on disk to determine availability state per year.
    ///
    /// Multiple manifest entries can share the same Year (different slugs).
    /// The installed season.json contains a "slug" field identifying which
    /// variant is active for that year — update checks use only that slug.
    /// </summary>
    public class SeasonManifestService
    {
        private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(5);

        private readonly string _seasonsDir;
        private readonly string _manifestPath;
        private readonly ISeasonLoader _seasonLoader;
        private readonly Func<string, string> _readAllTextFunc;
        private readonly bool _forceUpdate;

        private List<SeasonManifestEntry>? _cache;

        public SeasonManifestService(
            string seasonsDir,
            string manifestPath,
            ISeasonLoader seasonLoader,
            Func<string, string> readAllTextFunc,
            bool forceUpdate)
        {
            _seasonsDir = seasonsDir;
            _manifestPath = manifestPath;
            _seasonLoader = seasonLoader;
            _readAllTextFunc = readAllTextFunc;
            _forceUpdate = forceUpdate;
        }

        // -------------------------------------------------------------------------
        // Manifest access
        // -------------------------------------------------------------------------

        public List<SeasonManifestEntry> GetManifest()
        {
            if (_cache != null) return _cache;
            var json = _readAllTextFunc(_manifestPath);
            var manifest = JsonSerializer.Deserialize<SeasonsManifest>(json)
                ?? throw new InvalidDataException("seasons.json is empty or malformed.");
            _cache = manifest.Seasons;
            return _cache;
        }

        /// <summary>Returns all manifest entries for a given year (may be multiple slugs).</summary>
        public List<SeasonManifestEntry> GetEntriesForYear(int year) =>
            GetManifest().Where(s => s.Year == year).ToList();

        /// <summary>Returns the manifest entry matching a specific slug, or null.</summary>
        public SeasonManifestEntry? GetEntryBySlug(string slug) =>
            GetManifest().FirstOrDefault(s => s.Slug == slug);

        /// <summary>
        /// Returns the manifest entry for a year, using the installed slug if one exists.
        /// Falls back to the first entry for that year if nothing is installed.
        /// </summary>
        public SeasonManifestEntry? GetEntry(int year)
        {
            var installedSlug = GetInstalledSlug(year);
            if (installedSlug != null)
                return GetEntryBySlug(installedSlug);
            return GetEntriesForYear(year).FirstOrDefault();
        }

        // -------------------------------------------------------------------------
        // Installation state
        // -------------------------------------------------------------------------

        public bool IsInstalled(int year) =>
            _seasonLoader.GetAvailableSeasons().Any(y => y == year.ToString());

        /// <summary>
        /// Reads the slug from the installed season.json.
        /// Returns null if the season is not installed or has no slug field.
        /// </summary>
        public string? GetInstalledSlug(int year)
        {
            if (!IsInstalled(year)) return null;

            try
            {
                var season = _seasonLoader.LoadBaseSeason(year);
                return string.IsNullOrWhiteSpace(season?.Slug) ? null : season.Slug;
            }
            catch { return null; }
        }

        /// <summary>
        /// Returns true if the installed season has an update available.
        /// Compares the installed slug's manifest LastUpdated against the filesystem timestamp.
        /// </summary>
        private SeasonAvailability UpdateCheck(int year)
        {
            if (!IsInstalled(year)) return SeasonAvailability.UpToDate;

            var slug = GetInstalledSlug(year);
            var entry = slug != null ? GetEntryBySlug(slug) : GetEntriesForYear(year).FirstOrDefault();
            if (entry == null) return SeasonAvailability.UpToDate;

            if (entry.IsDefault) return SeasonAvailability.DefaultSeason;

            var fileDate = _seasonLoader.GetSeasonUpdateDate(year);
            return (entry.LastUpdated > fileDate.Add(TimestampTolerance) || _forceUpdate) ? SeasonAvailability.UpdateAvailable : SeasonAvailability.UpToDate;
        }

        public SeasonAvailability GetAvailability(int year)
        {
            var installed = IsInstalled(year);
            var entries = GetEntriesForYear(year);
            var entriesExist = entries.Any();

            if (!installed && !entriesExist) return SeasonAvailability.NotAvailable;
            if (!installed) return SeasonAvailability.NotInstalled;
            if (!entriesExist) return SeasonAvailability.InstalledNoRemote;
            return UpdateCheck(year);
        }

        /// <summary>
        /// Returns one display item per year for the catalog dialog.
        /// Includes not-installed years from the manifest.
        /// </summary>
        public List<SeasonDisplayItem> GetSeasonCatalog()
        {
            var manifest = GetManifest();
            var manifestYears = manifest.Select(s => s.Year).ToHashSet();

            var fromManifest = manifest
                .Select(s => s.Year).Distinct()
                .Select(y => BuildDisplayItem(y));

            var fromDisk = Directory.Exists(_seasonsDir)
                ? Directory.GetDirectories(_seasonsDir)
                    .Select(d => int.TryParse(Path.GetFileName(d), out var y) ? y : -1)
                    .Where(y => y > 0 && !manifestYears.Contains(y) && IsInstalled(y))
                    .Select(y => new SeasonDisplayItem
                    {
                        Year = y,
                        DisplayName = y.ToString(),
                        InstalledSlug = GetInstalledSlug(y),
                        Availability = SeasonAvailability.InstalledNoRemote
                    })
                : Enumerable.Empty<SeasonDisplayItem>();

            return fromManifest.Concat(fromDisk)
                .OrderBy(s => s.Year)
                .ToList();
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private SeasonDisplayItem BuildDisplayItem(int year)
        {
            var installedSlug = GetInstalledSlug(year);
            var availability = GetAvailability(year);

            // Use the installed slug's entry for display name if installed,
            // otherwise fall back to first entry for that year
            var entry = installedSlug != null
                ? GetEntryBySlug(installedSlug)
                : GetEntriesForYear(year).FirstOrDefault();

            return new SeasonDisplayItem
            {
                Year = year,
                DisplayName = entry?.DisplayName ?? year.ToString(),
                FileSizeMb = entry?.FileSizeMb ?? 0,
                InstalledSlug = installedSlug,
                Availability = availability
            };
        }
    }
}