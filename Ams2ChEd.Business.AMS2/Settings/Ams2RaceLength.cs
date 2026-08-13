namespace Ams2ChEd.Business.AMS2.Settings
{
    /// <summary>
    /// How long a race should be, as a fraction of the mapped track's default lap count
    /// (see <see cref="AMS2ChEd.Business.AMS2.Models.Ams2TrackMappingEntry.DefaultNumberOfLaps"/>).
    /// AMS2-concrete only - this is a live-race-configuration concept, not a game-agnostic setting.
    /// </summary>
    public enum Ams2RaceLength
    {
        /// <summary>Don't force a race duration - leave whatever's already set in-game.</summary>
        Default,
        OneThird,
        Half,
        Full
    }
}
