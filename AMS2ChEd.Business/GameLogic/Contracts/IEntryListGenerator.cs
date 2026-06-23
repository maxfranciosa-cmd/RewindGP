using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public interface IEntryListGenerator
    {
        event EventHandler<EntryListGeneratedEventArgs> EntryListGenerated;

        List<EntryListEntry> GenerateEntryList(ISaveGame saveGame);

        List<Absence> GetAbsencesForGrandPrix(ISaveGame saveGame);
    }
    public class EntryListGeneratedEventArgs : EventArgs
    {
        public List<EntryListEntry> EntryList { get; set; }
        public int RaceIndex { get; set; }
    }
}
