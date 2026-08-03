namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Shared numbering conventions between the livery-XML generator (Services\Ams2LiveryService.cs,
    /// which numbers each race's livery overrides starting at this value) and the .rcf slot patcher,
    /// so the two can never drift apart - a .rcf's declared LIVERY slot IDs must cover at least
    /// [BaseLiveryNumber, BaseLiveryNumber + requiredSlotCount).
    /// </summary>
    public static class Ams2LiveryConventions
    {
        public const int BaseLiveryNumber = 51;
    }
}
