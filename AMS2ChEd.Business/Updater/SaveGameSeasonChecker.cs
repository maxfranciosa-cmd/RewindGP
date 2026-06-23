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

        public SaveGameSeasonCheckerResult CheckIfSaveGameNeedsRefresh(ISaveGame save)
        {
            if (!_seasonLoader.GetAvailableSeasons().Contains(save.CurrentSeason.Year.ToString()))
                return SaveGameSeasonCheckerResult.Proceed;

            var seasonLastModified = _seasonLoader.GetSeasonUpdateDate(save.CurrentSeason.Year);

            if (save.Timestamp < seasonLastModified)
            {
                return SaveGameSeasonCheckerResult.NeedsRefresh;
            }

            return SaveGameSeasonCheckerResult.Proceed;
        }
    }
}
