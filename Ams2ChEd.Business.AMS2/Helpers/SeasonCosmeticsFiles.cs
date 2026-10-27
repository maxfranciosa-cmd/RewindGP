using System.Diagnostics;
using System.Text.Json;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Reads the cosmetics the app ships for a baked season and narrows them to what is on this
    /// machine: a skin pack's files exist only once the player has imported it, so a team counts
    /// only when its body liveries are on disk, and any other field whose file is missing is left
    /// out. Paths stay as written (relative to Seasons/&lt;year&gt;/), so they resolve exactly like a
    /// season pack's.
    /// </summary>
    public static class SeasonCosmeticsFiles
    {
        /// <summary>The year's shipped cosmetics, null when the app ships none or they can't be read.</summary>
        public static SeasonCosmetics Read(int year, string bakedSeasonsFolder = null)
        {
            var path = bakedSeasonsFolder == null
                ? StoragePaths.CosmeticsFilePath(year)
                : Path.Combine(bakedSeasonsFolder, year.ToString(), "cosmetics", "cosmetics.json");
            if (!File.Exists(path))
                return null;

            try
            {
                return JsonSerializer.Deserialize<SeasonCosmetics>(File.ReadAllText(path), DefaultJsonSerializerOptions.Instance);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"The {year} cosmetics could not be read: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// The teams whose artwork is on disk, as a season (for BakedSeasonMerge); null when none is.
        /// </summary>
        public static Ams2Season EffectiveSeason(SeasonCosmetics cosmetics, int year, string seasonsFolder = null)
        {
            if (cosmetics?.Teams == null)
                return null;

            var teams = new List<Ams2TeamEntry>();
            foreach (var (teamId, source) in cosmetics.Teams)
            {
                if (source == null)
                    continue;

                var team = source.DeepClone();
                team.TeamId = teamId;
                if (!Narrow(team, year, seasonsFolder))
                    continue;
                teams.Add(team);
            }

            return teams.Count == 0 ? null : new Ams2Season { Year = year, Teams = teams.Cast<AMS2ChEd.Business.Models.ITeamEntry>().ToList() };
        }

        /// <summary>The drivers' artwork fields whose files are on disk, per driver_id.</summary>
        public static Dictionary<string, Ams2DriverData> EffectiveDrivers(SeasonCosmetics cosmetics, int year, string seasonsFolder = null)
        {
            var drivers = new Dictionary<string, Ams2DriverData>();
            foreach (var (driverId, source) in cosmetics?.Drivers ?? new Dictionary<string, Ams2DriverData>())
            {
                if (source == null)
                    continue;

                string Keep(string path) => Exists(path, year, seasonsFolder) ? path : null;
                var driver = new Ams2DriverData { DriverId = driverId, PictureUrl = source.PictureUrl };

                // a helmet without its visor is fine (the default visor fills in); a visor alone is not
                driver.BaseHelmetFile = Keep(source.BaseHelmetFile);
                driver.BaseVisorFile = driver.BaseHelmetFile == null ? null : Keep(source.BaseVisorFile);
                driver.BaseHelmetFile90s = Keep(source.BaseHelmetFile90s);
                driver.BaseHelmetFile80s = Keep(source.BaseHelmetFile80s);
                driver.BaseVisorFile80s = driver.BaseHelmetFile80s == null ? null : Keep(source.BaseVisorFile80s);
                driver.BaseHelmetFile70s = Keep(source.BaseHelmetFile70s);
                driver.BaseVisorFile70s = driver.BaseHelmetFile70s == null ? null : Keep(source.BaseVisorFile70s);
                drivers[driverId] = driver;
            }
            return drivers;
        }

        // Drops what is not on disk; false when the team's body liveries are not.
        private static bool Narrow(Ams2TeamEntry team, int year, string seasonsFolder)
        {
            bool Has(string path) => Exists(path, year, seasonsFolder);
            string Keep(string path) => Has(path) ? path : null;

            if (string.IsNullOrEmpty(team.BaseLiveryDriver1) && string.IsNullOrEmpty(team.BaseLiveryDriver2))
                return false;
            if (!string.IsNullOrEmpty(team.BaseLiveryDriver1) && !Has(team.BaseLiveryDriver1))
                return false;
            if (!string.IsNullOrEmpty(team.BaseLiveryDriver2) && !Has(team.BaseLiveryDriver2))
                return false;

            team.LiveryPreview = Keep(team.LiveryPreview);
            team.HelmetSponsors = Keep(team.HelmetSponsors);
            team.VisorSponsors = Keep(team.VisorSponsors);
            team.DriversSpecificHelmet = team.DriversSpecificHelmet?
                .Where(kv => Has(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            team.NumbersPlacements = NarrowPlacements(team.NumbersPlacements, Has);

            // a race's livery counts only when its body is on disk
            team.LiveryOverrides = team.LiveryOverrides?
                .Where(o => o != null)
                .Where(o => (string.IsNullOrEmpty(o.Driver1Livery) || Has(o.Driver1Livery))
                         && (string.IsNullOrEmpty(o.Driver2Livery) || Has(o.Driver2Livery)))
                .Select(o =>
                {
                    o.LiveryPreview = Keep(o.LiveryPreview);
                    o.HelmetSponsors = Keep(o.HelmetSponsors);
                    o.VisorSponsors = Keep(o.VisorSponsors);
                    o.DriversSpecificHelmet = o.DriversSpecificHelmet?.Where(kv => Has(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value);
                    o.NumbersPlacements = NarrowPlacements(o.NumbersPlacements, Has);
                    return o;
                })
                .ToList();
            return true;
        }

        private static List<NumbersPlacement> NarrowPlacements(IEnumerable<NumbersPlacement> placements, Func<string, bool> has)
        {
            return placements?
                .Where(p => p != null && has(p.NumbersTexture) && (string.IsNullOrEmpty(p.NumbersTextureDriver2) || has(p.NumbersTextureDriver2)))
                .ToList();
        }

        /// <summary>Whether a path written relative to Seasons/&lt;year&gt;/ names a file on disk.</summary>
        public static bool Exists(string path, int year, string seasonsFolder = null)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("://"))
                return false;

            seasonsFolder ??= StoragePaths.SeasonsFolder;
            return File.Exists(Path.GetFullPath(Path.Combine(seasonsFolder, year.ToString(), path)));
        }
    }
}
