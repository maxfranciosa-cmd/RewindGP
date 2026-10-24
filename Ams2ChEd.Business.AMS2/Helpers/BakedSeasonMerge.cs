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

                team.BaseLiveryDriver1 = packTeam.BaseLiveryDriver1;
                team.BaseLiveryDriver2 = packTeam.BaseLiveryDriver2;
                team.LiveryPreview = packTeam.LiveryPreview;
                team.NumbersPlacements = packTeam.NumbersPlacements;
                team.HelmetSponsors = packTeam.HelmetSponsors;
                team.VisorSponsors = packTeam.VisorSponsors;
                team.DriversSpecificHelmet = packTeam.DriversSpecificHelmet;
                team.LiveryOverrides = packTeam.LiveryOverrides;
                team.LiveryXml = packTeam.LiveryXml;
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

                driver.BaseHelmetFile = packDriver.BaseHelmetFile;
                driver.BaseVisorFile = packDriver.BaseVisorFile;
                driver.BaseHelmetFile90s = packDriver.BaseHelmetFile90s;
                driver.BaseHelmetFile80s = packDriver.BaseHelmetFile80s;
                driver.BaseVisorFile80s = packDriver.BaseVisorFile80s;
                driver.BaseHelmetFile70s = packDriver.BaseHelmetFile70s;
                driver.BaseVisorFile70s = packDriver.BaseVisorFile70s;
                driver.PictureUrl = packDriver.PictureUrl;
            }
            return baked;
        }
    }
}
