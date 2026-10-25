using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMS2ChEd.Business.Updater
{

    public class SaveGameSeasonChecker
    {
        private readonly ISeasonLoader _seasonLoader;

        public SaveGameSeasonChecker(ISeasonLoader seasonLoader)
        {
            _seasonLoader = seasonLoader;
        }

        /// <summary>
        /// Tells what the save's current season needs to catch up with the season data on disk,
        /// and stamps the save with that data: the caller is expected to carry out the refresh.
        /// </summary>
        public SaveGameSeasonCheckerResult CheckIfSaveGameNeedsRefresh(ISaveGame save)
        {
            var year = save.CurrentSeason.Year;
            if (!_seasonLoader.GetAvailableSeasons().Contains(year.ToString()))
                return SaveGameSeasonCheckerResult.Proceed;

            var current = _seasonLoader.GetSeasonDataStamp(year);
            var result = Compare(save, current);

            if (current != null)
                save.SeasonDataStamp = current;

            return result;
        }

        private SaveGameSeasonCheckerResult Compare(ISaveGame save, SeasonDataStamp current)
        {
            var year = save.CurrentSeason.Year;
            var stamp = save.SeasonDataStamp;

            // saves from before the stamp was kept, or stamped for an earlier season of the
            // career: the file times decide, as they used to
            if (current == null || stamp == null || stamp.Year != year)
            {
                return save.Timestamp.ToUniversalTime() < _seasonLoader.GetSeasonUpdateDate(year)
                    ? SaveGameSeasonCheckerResult.NeedsRefresh
                    : SaveGameSeasonCheckerResult.Proceed;
            }

            if (stamp.Baked != current.Baked)
                return SaveGameSeasonCheckerResult.NeedsRefresh;

            if (stamp.Pack == current.Pack)
                return SaveGameSeasonCheckerResult.Proceed;

            // over the app's data a pack brings only artwork; without it, the pack is the season
            return current.Baked != null
                ? SaveGameSeasonCheckerResult.NeedsArtworkRefresh
                : SaveGameSeasonCheckerResult.NeedsRefresh;
        }
    }
}
