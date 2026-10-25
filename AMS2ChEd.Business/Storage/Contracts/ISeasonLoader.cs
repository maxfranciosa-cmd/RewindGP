using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Updater.Models;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface ISeasonLoader
    {
        /// <summary>Every season that can be played: with season data shipped in the app, a season pack installed, or both.</summary>
        IEnumerable<string> GetAvailableSeasons();

        /// <summary>When the season's data last changed (app data or pack, whichever is newer).</summary>
        DateTime GetSeasonUpdateDate(int seasonYear);

        ISeason LoadBaseSeason(int seasonYear);

        /// <summary>
        /// The seasons whose season pack is installed - what the season manifest's install and
        /// update checks are about. A season the app ships data for can be playable with no pack.
        /// </summary>
        IEnumerable<string> GetInstalledPackSeasons() => GetAvailableSeasons();

        /// <summary>When the installed season pack last changed.</summary>
        DateTime GetPackUpdateDate(int seasonYear) => GetSeasonUpdateDate(seasonYear);

        /// <summary>
        /// The seasons the app ships data for. Such a season is playable with no pack, and a pack
        /// installed for it only brings artwork.
        /// </summary>
        IEnumerable<string> GetBakedSeasons() => Enumerable.Empty<string>();

        /// <summary>
        /// What the season's data is made of right now, for telling a save what changed since it
        /// was last refreshed. Null when the loader can't tell, and the file times decide instead.
        /// </summary>
        SeasonDataStamp GetSeasonDataStamp(int seasonYear) => null;
    }

    public interface ISeasonLoader<TSeason> : ISeasonLoader
        where TSeason : ISeason
    {
        TSeason LoadSeason(int seasonYear);
    }
}
