using AMS2ChEd.Business.Artwork;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Extensions
{
    public static class RaceCoverExtension
    {
        /// <summary>
        /// The race's programme cover: the season pack's own when it has one that loads, otherwise
        /// one drawn by RaceCoverComposer in the race's colours (null if that can't be drawn either).
        /// </summary>
        public static string CoverPicture(this Race race, int year)
        {
            if (race == null)
                return null;
            if (PictureUrlLoaderExtension.TryLoadBitmap(race.CoverPictureUrl, out _))
                return race.CoverPictureUrl;
            try
            {
                return RaceCoverComposer.CoverFor(year, race);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: race cover for {year} {race.RaceName} could not be drawn: {ex.Message}");
                return null;
            }
        }
    }
}
