using AMS2ChEd.Business.AMS2.Storage.Contracts;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    public class Ams2TrackResolution
    {
        public string TrackId { get; set; }
        public int DefaultNumberOfLaps { get; set; }
    }

    public interface IAms2GrandPrixTrackResolver
    {
        /// <summary>
        /// Resolves which AMS2 track id + default lap count to use for a Grand Prix in a given
        /// season, matching <paramref name="grandPrixName"/>/<paramref name="grandPrixShortName"/>
        /// against the configured GrandPrixNamePatterns (case-insensitive substring match), then,
        /// among the entries that match, picking the one with the largest Year that is still
        /// &lt;= <paramref name="seasonYear"/> (so a venue/GP with several dated entries resolves to
        /// whichever era applies to this season), then walking that entry's TrackOptions in order
        /// and picking the first one whose DlcId is owned, falling back to DefaultTrackId if none
        /// are. Returns null if no entry matches or none of the matches' Year is
        /// &lt;= seasonYear - callers should fall back to manual instructions in that case, since
        /// Ams2RaceConfigurator.ApplyRaceConfigAsync requires a track for its single SetCar call.
        /// </summary>
        Ams2TrackResolution ResolveTrack(string grandPrixName, string grandPrixShortName, int seasonYear);
    }

    public class Ams2GrandPrixTrackResolver : IAms2GrandPrixTrackResolver
    {
        private readonly ITrackMappingLoader _trackMappingLoader;
        private readonly IAms2DlcOwnershipChecker _dlcOwnershipChecker;

        public Ams2GrandPrixTrackResolver(ITrackMappingLoader trackMappingLoader, IAms2DlcOwnershipChecker dlcOwnershipChecker)
        {
            _trackMappingLoader = trackMappingLoader;
            _dlcOwnershipChecker = dlcOwnershipChecker;
        }

        public Ams2TrackResolution ResolveTrack(string grandPrixName, string grandPrixShortName, int seasonYear)
        {
            var entry = _trackMappingLoader.GetAll()
                .Where(e => Matches(e, grandPrixName, grandPrixShortName) && e.Year <= seasonYear)
                .OrderByDescending(e => e.Year)
                .FirstOrDefault();
            if (entry == null)
            {
                return null;
            }

            string trackId = entry.TrackOptions?
                .FirstOrDefault(o => !string.IsNullOrEmpty(o.DlcId) && _dlcOwnershipChecker.IsOwned(o.DlcId))
                ?.TrackId
                ?? entry.DefaultTrackId;

            // An entry can legitimately have no usable track yet (default_track_id: null in the
            // registry, e.g. a Grand Prix whose venue isn't in the installed track catalog) -
            // treat that exactly like "no entry matched" rather than handing back a resolution
            // with a null TrackId that would blow up the caller's hash-catalog lookup.
            if (string.IsNullOrEmpty(trackId))
            {
                return null;
            }

            return new Ams2TrackResolution
            {
                TrackId = trackId,
                DefaultNumberOfLaps = entry.DefaultNumberOfLaps
            };
        }

        private static bool Matches(AMS2ChEd.Business.AMS2.Models.Ams2TrackMappingEntry entry, string grandPrixName, string grandPrixShortName)
        {
            if (entry.GrandPrixNamePatterns == null)
            {
                return false;
            }

            return entry.GrandPrixNamePatterns.Any(pattern =>
                !string.IsNullOrEmpty(pattern) &&
                ((!string.IsNullOrEmpty(grandPrixName) && grandPrixName.Contains(pattern, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrEmpty(grandPrixShortName) && grandPrixShortName.Contains(pattern, StringComparison.OrdinalIgnoreCase))));
        }
    }
}
