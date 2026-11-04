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
        /// are. An entry with no usable track gives TrackId null with its lap count. Returns null
        /// if no entry matches or none of the matches' Year is &lt;= seasonYear. Without a TrackId
        /// the race is set up on whatever track AMS2 already has selected.
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

            // An entry can legitimately have no usable track (default_track_id: null, e.g. a Grand
            // Prix whose venue AMS2 doesn't have): its lap count still sets the race distance, on
            // whatever track the player has selected. With neither, it's as good as no match.
            if (string.IsNullOrEmpty(trackId))
            {
                return entry.DefaultNumberOfLaps > 0
                    ? new Ams2TrackResolution { TrackId = null, DefaultNumberOfLaps = entry.DefaultNumberOfLaps }
                    : null;
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
