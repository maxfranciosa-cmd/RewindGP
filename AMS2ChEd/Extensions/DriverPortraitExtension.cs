using AMS2ChEd.Business.Artwork;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Extensions
{
    public static class DriverPortraitExtension
    {
        /// <summary>
        /// The driver's portrait: their picture when it loads, otherwise their generic avatar drawn
        /// by AvatarComposer (one picked from their id when they have none).
        /// </summary>
        public static string Portrait(this IDriverData driver) =>
            driver == null ? null : Portrait(driver.DriverId, driver.PictureUrl, driver.GenericAvatar);

        public static string Portrait(string driverId, string pictureUrl, GenericAvatar avatar)
        {
            if (PictureUrlLoaderExtension.TryLoadBitmap(pictureUrl, out _))
                return pictureUrl;
            if (string.IsNullOrEmpty(driverId) && avatar == null)
                return null;
            try
            {
                return AvatarComposer.AvatarFor(driverId, avatar);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: avatar for {driverId} could not be drawn: {ex.Message}");
                return null;
            }
        }
    }
}
