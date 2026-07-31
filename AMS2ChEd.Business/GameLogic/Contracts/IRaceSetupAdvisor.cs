using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public interface IRaceSetupAdvisor
    {
        /// <summary>
        /// Suggested AI difficulty delta for the given team/driver slot. When <paramref name="poolEntries"/>
        /// is null, the suggestion is based on the team's overall reputation (full-grid race). When supplied,
        /// the suggestion is based on car-performance data rebased across just the given pool (pre-qualifying).
        /// </summary>
        int GetSuggestedAiDifficulty(ISeason season, string teamId, int driverSlot, IEnumerable<EntryListEntry>? poolEntries = null);

        string GetCarDisplayName(ISeason season, string teamId, int driverSlot);

        bool SeasonUsesPerformanceScalars(ISeason season);

        /// <summary>
        /// Deep-clones the season and rebases each represented car's performance malus so the fastest
        /// car in the pool lands at 0 - used both for the pre-qualifying difficulty suggestion and as
        /// the season passed to race preparation, so AI opponents reflect the pool's actual spread
        /// rather than the full grid's.
        /// </summary>
        ISeason NormalisePreQualiPool(ISeason season, IEnumerable<EntryListEntry> poolEntries);
    }
}
