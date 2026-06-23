using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public interface IRacePreparator
    {
        void PrepareRace(int raceId, IEnumerable<EntryListEntry> raceEntryList, IEnumerable<IDriverData> drivers, ISeason season);
    }
}
