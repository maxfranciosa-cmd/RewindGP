using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface IAllTimeRecordsLoader
    {
        /// <summary>
        /// The real-world record of every race winner in history. Empty when the file is missing
        /// or unreadable: the hall of fame then only knows the drivers of the save.
        /// </summary>
        IReadOnlyList<AllTimeDriverRecord> LoadDrivers();
    }
}
