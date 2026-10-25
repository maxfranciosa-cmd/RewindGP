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
        {
            if (pack == null)
                return baked;

            if (!string.IsNullOrWhiteSpace(pack.Slug))
                baked.Slug = pack.Slug;

            var packTeams = (pack.Teams ?? Enumerable.Empty<AMS2ChEd.Business.Models.ITeamEntry>())
                .OfType<Ams2TeamEntry>()
                .Where(t => !string.IsNullOrEmpty(t.TeamId))
                .GroupBy(t => t.TeamId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var team in (baked.Teams ?? Enumerable.Empty<AMS2ChEd.Business.Models.ITeamEntry>()).OfType<Ams2TeamEntry>())
            {
                if (!packTeams.TryGetValue(team.TeamId, out var packTeam))
                    continue;

                // A cosmetic pack names the car its artwork was painted for; one that names none
                // is taken at its word.
                if (!string.IsNullOrEmpty(packTeam.Ams2Car) && !string.Equals(packTeam.Ams2Car, team.Ams2Car, StringComparison.OrdinalIgnoreCase))
                {
                    warnings?.Add($"{baked.Year} {team.TeamId}: the season pack's artwork is for {packTeam.Ams2Car}, but the team races the {team.Ams2Car}; the pack's artwork for this team is ignored.");
                    continue;
                }

                CopyTeamArtwork(packTeam, team);
            }

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
        {
            if (pack == null)
                return baked;

            foreach (var (id, driver) in baked)
            {
                if (!pack.TryGetValue(id, out var packDriver))
                    continue;

                CopyDriverArtwork(packDriver, driver);
            }
            return baked;
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
