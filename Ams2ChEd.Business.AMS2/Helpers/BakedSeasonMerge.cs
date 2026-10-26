using System.Text.RegularExpressions;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.AMS2.Models;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Lays a season pack's artwork over the app's baked season data. Baked data wins for
    /// everything that affects play; the pack supplies only artwork, team by team, driver by
    /// driver and race by race, and whatever it lacks stays empty (the app then generates it).
    /// </summary>
    public static class BakedSeasonMerge
    {
        /// <summary>
        /// Copies the pack's artwork fields onto the baked teams with the same team_id and onto
        /// the baked races with the same race_id. A pack team painted for a different car model
        /// than the baked one is skipped (its textures would not fit the car), with a warning.
        /// Teams and races the baked season does not have are ignored, as are every gameplay
        /// field and the pack's year. The pack's slug is kept, since it identifies the pack.
        /// </summary>
        public static Ams2Season MergeSeason(Ams2Season baked, Ams2Season pack, ICollection<string> warnings)
            => MergeSeason(baked, pack, _ => null, warnings);

        /// <summary>
        /// As above; a team the season's own pack paints no body livery for then borrows the
        /// artwork of the first of its cosmetic_fallbacks that loadOtherPack (another year's raw
        /// pack, null when not installed) has, on the same car. Borrowed paths are rebased to
        /// resolve from this season's folder, and the team keeps its own livery XML (the kit's).
        /// </summary>
        public static Ams2Season MergeSeason(Ams2Season baked, Ams2Season pack, Func<int, Ams2Season> loadOtherPack, ICollection<string> warnings, string seasonsFolder = null)
        {
            if (pack != null && !string.IsNullOrWhiteSpace(pack.Slug))
                baked.Slug = pack.Slug;

            var packTeams = (pack?.Teams ?? Enumerable.Empty<AMS2ChEd.Business.Models.ITeamEntry>())
                .OfType<Ams2TeamEntry>()
                .Where(t => !string.IsNullOrEmpty(t.TeamId))
                .GroupBy(t => t.TeamId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var team in (baked.Teams ?? Enumerable.Empty<AMS2ChEd.Business.Models.ITeamEntry>()).OfType<Ams2TeamEntry>())
            {
                packTeams.TryGetValue(team.TeamId, out var packTeam);

                // A cosmetic pack names the car its artwork was painted for; one that names none
                // is taken at its word.
                if (packTeam != null && !string.IsNullOrEmpty(packTeam.Ams2Car) && !string.Equals(packTeam.Ams2Car, team.Ams2Car, StringComparison.OrdinalIgnoreCase))
                {
                    warnings?.Add($"{baked.Year} {team.TeamId}: the season pack's artwork is for {packTeam.Ams2Car}, but the team races the {team.Ams2Car}; the pack's artwork for this team is ignored.");
                    packTeam = null;
                }

                if (packTeam != null && HasBodyLivery(packTeam))
                {
                    CopyTeamArtwork(packTeam, team);
                    continue;
                }

                if (BorrowTeamArtwork(baked.Year, team, loadOtherPack, warnings, seasonsFolder))
                    continue;

                // a pack team with no body livery still brings whatever else it has
                if (packTeam != null)
                    CopyTeamArtwork(packTeam, team);
            }

            if (pack == null)
                return baked;

            var packCovers = (pack.Races ?? Enumerable.Empty<AMS2ChEd.Business.Models.Concrete.Race>())
                .GroupBy(r => r.RaceId)
                .ToDictionary(g => g.Key, g => g.First().CoverPictureUrl);
            foreach (var race in baked.Races ?? Enumerable.Empty<AMS2ChEd.Business.Models.Concrete.Race>())
            {
                if (packCovers.TryGetValue(race.RaceId, out var cover) && !string.IsNullOrEmpty(cover))
                    race.CoverPictureUrl = cover;
            }

            return baked;
        }

        /// <summary>
        /// Copies the pack's helmet, visor and portrait fields onto the baked drivers with the
        /// same driver_id. Drivers the baked season does not have are ignored.
        /// </summary>
        public static Dictionary<string, Ams2DriverData> MergeDrivers(Dictionary<string, Ams2DriverData> baked, Dictionary<string, Ams2DriverData> pack)
            => MergeDrivers(baked, pack, 0, new Dictionary<int, Dictionary<string, Ams2DriverData>>());

        /// <summary>
        /// As above, one asset slot at a time (a helmet era, the portrait): a slot the own year's
        /// pack leaves empty comes from the nearest other installed pack (otherPacks, the raw
        /// drivers of each year) that has the driver with that slot, the earlier year on a tie,
        /// its paths rebased to resolve from this season's folder.
        /// </summary>
        public static Dictionary<string, Ams2DriverData> MergeDrivers(
            Dictionary<string, Ams2DriverData> baked,
            Dictionary<string, Ams2DriverData> pack,
            int year,
            IReadOnlyDictionary<int, Dictionary<string, Ams2DriverData>> otherPacks,
            string seasonsFolder = null)
        {
            foreach (var (id, driver) in baked)
            {
                Ams2DriverData packDriver = null;
                pack?.TryGetValue(id, out packDriver);

                foreach (var slot in DriverSlots)
                {
                    if (packDriver != null && !string.IsNullOrEmpty(slot.Key(packDriver)))
                    {
                        slot.Copy(packDriver, driver, p => p);
                        continue;
                    }

                    var nearest = (otherPacks ?? new Dictionary<int, Dictionary<string, Ams2DriverData>>())
                        .Where(kv => kv.Key != year && kv.Value != null)
                        .Select(kv => (Year: kv.Key, Driver: kv.Value.TryGetValue(id, out var d) ? d : null))
                        .Where(c => c.Driver != null && !string.IsNullOrEmpty(slot.Key(c.Driver)))
                        .OrderBy(c => Math.Abs(c.Year - year))
                        .ThenBy(c => c.Year)
                        .FirstOrDefault();
                    if (nearest.Driver != null)
                        slot.Copy(nearest.Driver, driver, p => RebaseFromSeason(p, nearest.Year, seasonsFolder));
                }
            }
            return baked;
        }

        // The driver's artwork in slots that resolve independently, each named by its key field:
        // the helmet of each era (with its visor) and the portrait. The portrait is relative to
        // the app's folder or a URL, so it never needs rebasing.
        private static readonly (Func<Ams2DriverData, string> Key, Action<Ams2DriverData, Ams2DriverData, Func<string, string>> Copy)[] DriverSlots =
        {
            (d => d.BaseHelmetFile, (from, to, map) => { to.BaseHelmetFile = map(from.BaseHelmetFile); to.BaseVisorFile = map(from.BaseVisorFile); }),
            (d => d.BaseHelmetFile90s, (from, to, map) => to.BaseHelmetFile90s = map(from.BaseHelmetFile90s)),
            (d => d.BaseHelmetFile80s, (from, to, map) => { to.BaseHelmetFile80s = map(from.BaseHelmetFile80s); to.BaseVisorFile80s = map(from.BaseVisorFile80s); }),
            (d => d.BaseHelmetFile70s, (from, to, map) => { to.BaseHelmetFile70s = map(from.BaseHelmetFile70s); to.BaseVisorFile70s = map(from.BaseVisorFile70s); }),
            (d => d.PictureUrl, (from, to, _) => to.PictureUrl = from.PictureUrl),
        };

        private static bool HasBodyLivery(Ams2TeamEntry team) =>
            !string.IsNullOrEmpty(team.BaseLiveryDriver1) || !string.IsNullOrEmpty(team.BaseLiveryDriver2);

        // The first of the team's cosmetic fallbacks that is installed and paints a body livery for
        // the same car gives the team its artwork; false when none does.
        private static bool BorrowTeamArtwork(int year, Ams2TeamEntry team, Func<int, Ams2Season> loadOtherPack, ICollection<string> warnings, string seasonsFolder)
        {
            foreach (var fallback in team.CosmeticFallbacks ?? Enumerable.Empty<CosmeticFallback>())
            {
                if (fallback.Season == year)
                    continue;

                var source = loadOtherPack?.Invoke(fallback.Season);
                var sourceTeamId = fallback.TeamId ?? team.TeamId;
                var sourceTeam = source?.Teams?.OfType<Ams2TeamEntry>().FirstOrDefault(t => t.TeamId == sourceTeamId);
                if (sourceTeam == null || !HasBodyLivery(sourceTeam))
                    continue;

                // borrowed artwork must say it fits the car
                if (string.IsNullOrEmpty(sourceTeam.Ams2Car) || !string.Equals(sourceTeam.Ams2Car, team.Ams2Car, StringComparison.OrdinalIgnoreCase))
                {
                    warnings?.Add($"{year} {team.TeamId}: the {fallback.Season} pack's {sourceTeamId} is painted for {(string.IsNullOrEmpty(sourceTeam.Ams2Car) ? "no named car" : sourceTeam.Ams2Car)}, but the team races the {team.Ams2Car}; that fallback is ignored.");
                    continue;
                }

                // a copy: the paths are rebased in place, and the same source may serve another team
                sourceTeam = sourceTeam.DeepClone();

                if (fallback.LiveryOverrideRaceId is int raceId)
                {
                    var liveryOverride = sourceTeam.LiveryOverrides?.FirstOrDefault(o => o.RaceId == raceId);
                    if (liveryOverride == null)
                    {
                        warnings?.Add($"{year} {team.TeamId}: the {fallback.Season} pack's {sourceTeamId} has no livery override for race {raceId}; that fallback is ignored.");
                        continue;
                    }

                    sourceTeam.BaseLiveryDriver1 = liveryOverride.Driver1Livery ?? sourceTeam.BaseLiveryDriver1;
                    sourceTeam.BaseLiveryDriver2 = liveryOverride.Driver2Livery ?? sourceTeam.BaseLiveryDriver2;
                    sourceTeam.LiveryPreview = liveryOverride.LiveryPreview ?? sourceTeam.LiveryPreview;
                    sourceTeam.NumbersPlacements = liveryOverride.NumbersPlacements ?? sourceTeam.NumbersPlacements;
                    sourceTeam.HelmetSponsors = liveryOverride.HelmetSponsors ?? sourceTeam.HelmetSponsors;
                    sourceTeam.VisorSponsors = liveryOverride.VisorSponsors ?? sourceTeam.VisorSponsors;
                    sourceTeam.DriversSpecificHelmet = liveryOverride.DriversSpecificHelmet ?? sourceTeam.DriversSpecificHelmet;
                }

                // The source's overrides are keyed by its own season's races, and its livery XML
                // was written for its own pack: the team keeps the XML it has without a pack.
                var ownLiveryXml = team.LiveryXml;
                CopyTeamArtwork(sourceTeam, team);
                team.LiveryOverrides = null;
                team.LiveryXml = ownLiveryXml;
                MapTeamPaths(team, p => RebaseFromSeason(p, fallback.Season, seasonsFolder));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Rewrites a path relative to Seasons/&lt;sourceYear&gt;/ so it resolves to the same file
        /// from any Seasons/&lt;year&gt;/ folder: "car_liveries/rb.dds" becomes
        /// "../2022/car_liveries/rb.dds"; a path leading out of the seasons folder, a rooted path
        /// and a URL stay as they are.
        /// </summary>
        public static string RebaseFromSeason(string path, int sourceYear, string seasonsFolder = null)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("://") || Path.IsPathRooted(path))
                return path;

            seasonsFolder ??= StoragePaths.SeasonsFolder;
            var full = Path.GetFullPath(Path.Combine(seasonsFolder, sourceYear.ToString(), path));
            var rebased = Path.GetRelativePath(Path.Combine(seasonsFolder, "0"), full).Replace('\\', '/');

            // unchanged but for its separators: keep it exactly as written
            return rebased == path.Replace('\\', '/') ? path : rebased;
        }

        /// <summary>
        /// The year of the season folder a path leads into when it starts by stepping out to
        /// another one ("../2022/..."; of "../2023/../2022/..." the last, 2022), otherwise null.
        /// </summary>
        public static int? OtherSeasonYear(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var match = OtherSeasonPrefix.Match(path);
            return match.Success ? int.Parse(match.Groups[1].Captures[^1].Value) : null;
        }

        private static readonly Regex OtherSeasonPrefix = new(@"^(?:\.\.[\\/](\d+)[\\/])+");

        /// <summary>
        /// Rewrites every texture path of a team - body liveries, preview, sponsors, specific
        /// helmets, number sheets, and the same in its livery overrides. Not the livery XML,
        /// which holds XML, not a path.
        /// </summary>
        public static void MapTeamPaths(Ams2TeamEntry team, Func<string, string> map)
        {
            string Map(string path) => string.IsNullOrEmpty(path) ? path : map(path);

            team.BaseLiveryDriver1 = Map(team.BaseLiveryDriver1);
            team.BaseLiveryDriver2 = Map(team.BaseLiveryDriver2);
            team.HelmetSponsors = Map(team.HelmetSponsors);
            team.VisorSponsors = Map(team.VisorSponsors);
            team.LiveryPreview = Map(team.LiveryPreview);
            team.DriversSpecificHelmet = team.DriversSpecificHelmet?.ToDictionary(kv => kv.Key, kv => Map(kv.Value));

            foreach (var placement in team.NumbersPlacements ?? Enumerable.Empty<NumbersPlacement>())
            {
                placement.NumbersTexture = Map(placement.NumbersTexture);
                placement.NumbersTextureDriver2 = Map(placement.NumbersTextureDriver2);
            }

            foreach (var liveryOverride in team.LiveryOverrides ?? Enumerable.Empty<LiveryOverride>())
            {
                liveryOverride.Driver1Livery = Map(liveryOverride.Driver1Livery);
                liveryOverride.Driver2Livery = Map(liveryOverride.Driver2Livery);
                liveryOverride.HelmetSponsors = Map(liveryOverride.HelmetSponsors);
                liveryOverride.VisorSponsors = Map(liveryOverride.VisorSponsors);
                liveryOverride.LiveryPreview = Map(liveryOverride.LiveryPreview);
                liveryOverride.DriversSpecificHelmet = liveryOverride.DriversSpecificHelmet?.ToDictionary(kv => kv.Key, kv => Map(kv.Value));

                foreach (var placement in liveryOverride.NumbersPlacements ?? Enumerable.Empty<NumbersPlacement>())
                {
                    placement.NumbersTexture = Map(placement.NumbersTexture);
                    placement.NumbersTextureDriver2 = Map(placement.NumbersTextureDriver2);
                }
            }
        }

        /// <summary>The team fields a season pack supplies over baked data: its artwork.</summary>
        public static void CopyTeamArtwork(Ams2TeamEntry from, Ams2TeamEntry to)
        {
            to.BaseLiveryDriver1 = from.BaseLiveryDriver1;
            to.BaseLiveryDriver2 = from.BaseLiveryDriver2;
            to.LiveryPreview = from.LiveryPreview;
            to.NumbersPlacements = from.NumbersPlacements;
            to.HelmetSponsors = from.HelmetSponsors;
            to.VisorSponsors = from.VisorSponsors;
            to.DriversSpecificHelmet = from.DriversSpecificHelmet;
            to.LiveryOverrides = from.LiveryOverrides;
            to.LiveryXml = from.LiveryXml;
        }

        /// <summary>The driver fields a season pack supplies over baked data: helmet, visor and portrait.</summary>
        public static void CopyDriverArtwork(Ams2DriverData from, Ams2DriverData to)
        {
            to.BaseHelmetFile = from.BaseHelmetFile;
            to.BaseVisorFile = from.BaseVisorFile;
            to.BaseHelmetFile90s = from.BaseHelmetFile90s;
            to.BaseHelmetFile80s = from.BaseHelmetFile80s;
            to.BaseVisorFile80s = from.BaseVisorFile80s;
            to.BaseHelmetFile70s = from.BaseHelmetFile70s;
            to.BaseVisorFile70s = from.BaseVisorFile70s;
            to.PictureUrl = from.PictureUrl;
        }
    }
}
