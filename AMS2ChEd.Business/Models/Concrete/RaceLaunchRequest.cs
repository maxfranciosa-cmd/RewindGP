namespace AMS2ChEd.Business.Models.Concrete
{
    /// <summary>
    /// Bundles the data needed to launch the game and auto-configure a race/pre-qualifying
    /// session via <see cref="GameLogic.Contracts.IRaceLaunchAssistant"/>. Mirrors
    /// <see cref="GameLogic.Contracts.IRacePreparator.PrepareRace"/>'s parameter shape so both
    /// call sites (real race, pre-quali) can build it from data they already have.
    /// </summary>
    public class RaceLaunchRequest
    {
        public int RaceId { get; set; }

        public ISeason Season { get; set; }

        /// <summary>NextGpEntryList for a real race, PreQualiPoolEntries for pre-quali.</summary>
        public IEnumerable<EntryListEntry> EntryList { get; set; }

        public IEnumerable<IDriverData> Drivers { get; set; }

        public string PlayerTeamId { get; set; }

        public string PlayerDriverId { get; set; }

        /// <summary>Which driver slot (1 or 2) the player occupies on their team.</summary>
        public int PlayerDriverSlot { get; set; }

        public bool IsPreQuali { get; set; }
    }
}
