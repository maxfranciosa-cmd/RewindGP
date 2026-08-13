namespace Ams2ChEd.Business.AMS2.Helpers
{
    public interface IAms2DlcOwnershipChecker
    {
        /// <summary>Whether the player owns the AMS2 DLC identified by <paramref name="dlcId"/>.</summary>
        bool IsOwned(string dlcId);
    }
}
