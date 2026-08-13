namespace Ams2ChEd.Business.AMS2.Helpers
{
    public interface IAms2DlcOwnershipChecker
    {
        /// <summary>Whether the player owns the AMS2 DLC identified by <paramref name="dlcId"/>.</summary>
        bool IsOwned(string dlcId);

        /// <summary>
        /// Resolves and caches ownership for every known DLC via the AMS2ChEd.SteamDlcCheck helper
        /// process, if it hasn't happened yet this process. Callers that know AMS2 isn't running yet
        /// should await this before launching it - see Ams2DlcOwnershipChecker's class doc comment
        /// for why this runs in a separate short-lived process rather than in-process. Safe to call
        /// more than once (no-ops after the first) and safe to skip (IsOwned warms the cache lazily
        /// itself if needed, blocking synchronously to do so).
        /// </summary>
        Task WarmUpAsync();
    }
}
