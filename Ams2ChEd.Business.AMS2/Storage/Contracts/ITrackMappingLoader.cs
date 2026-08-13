using AMS2ChEd.Business.AMS2.Models;

namespace AMS2ChEd.Business.AMS2.Storage.Contracts
{
    public interface ITrackMappingLoader
    {
        /// <summary>
        /// Returns every configured Grand-Prix-name-pattern -> track mapping entry, or an empty
        /// list if the registry file doesn't exist (never throws - absence just means "no
        /// automated track selection is available yet").
        /// </summary>
        IReadOnlyList<Ams2TrackMappingEntry> GetAll();
    }
}
