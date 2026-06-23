using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    public class StubRacePreparator : IRacePreparator
    {
        public void PrepareRace(int raceId, IEnumerable<EntryListEntry> raceEntryList, IEnumerable<IDriverData> drivers, ISeason season)
        {
            
        }
    }
}
