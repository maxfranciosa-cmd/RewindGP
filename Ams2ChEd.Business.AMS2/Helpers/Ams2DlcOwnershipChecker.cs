namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// v1 stub: no DLC-ownership detection is implemented yet, so every DLC-gated track
    /// conservatively resolves to its DefaultTrackId rather than BestTrackId. This is the seam to
    /// replace with real detection later (e.g. parsing the AMS2 install's Steam appmanifest
    /// InstalledDepots, or a marker-file check per DLC) - callers only depend on the interface.
    /// </summary>
    public class Ams2DlcOwnershipChecker : IAms2DlcOwnershipChecker
    {
        public bool IsOwned(string dlcId) => true;
    }
}
