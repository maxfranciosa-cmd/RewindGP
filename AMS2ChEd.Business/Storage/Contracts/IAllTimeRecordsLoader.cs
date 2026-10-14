using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface IAllTimeRecordsLoader
    {
        /// <summary>
        /// The real-world record of every driver and team in history. Empty (never null) when the
        /// file is missing or unreadable: accolades and the hall of fame then only know what
        /// happened in the save.
        /// </summary>
        AllTimeRecords Load();
    }
}
