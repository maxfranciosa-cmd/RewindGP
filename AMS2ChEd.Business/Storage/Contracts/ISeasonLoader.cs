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

        /// <summary>
        /// The folders holding each season's scenarios (one *.json per scenario), by year: those of
        /// the installed season packs, unless the loader has scenarios of its own for that year.
        /// </summary>
        IEnumerable<(int Year, string Folder)> GetScenarioFolders() => ScenarioFolders(AppPaths.SeasonsFolder);

        /// <summary>The "scenarios" folders of the year-named folders under <paramref name="root"/>.</summary>
        public static IEnumerable<(int Year, string Folder)> ScenarioFolders(string root)
        {
            if (!Directory.Exists(root))
                yield break;

            foreach (var yearFolder in Directory.GetDirectories(root))
            {
                var scenarios = Path.Combine(yearFolder, "scenarios");
                if (int.TryParse(Path.GetFileName(yearFolder), out var year) && Directory.Exists(scenarios))
                    yield return (year, scenarios);
            }
        }
    }

    public interface ISeasonLoader<TSeason> : ISeasonLoader
        where TSeason : ISeason
    {
        TSeason LoadSeason(int seasonYear);
    }
}
