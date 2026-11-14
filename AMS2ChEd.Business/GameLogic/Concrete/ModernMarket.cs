using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// The modern driver market, from the superlicence-points era on: nobody is a wild card any
    /// more and race seats are no longer sold race by race (PaidRides). In the off-season the year
    /// is the season being hired for; during a season it's the current one.
    /// </summary>
    public static class ModernMarket
    {
        public const int Year = 2016;

        public static bool IsModern(int seasonYear) => seasonYear >= Year;

        // WILD_CARD becomes SEASON in modern seasons; anything else is unchanged
        public static DriverReputation Clamp(DriverReputation reputation, int seasonYear) =>
            IsModern(seasonYear) && reputation == DriverReputation.PAY_DRIVER_WILD_CARD
                ? DriverReputation.PAY_DRIVER_SEASON
                : reputation;

        // every driver clamped to that season, in place
        public static void ClampAll(IEnumerable<IDriverData> drivers, int seasonYear)
        {
            if (drivers == null || !IsModern(seasonYear))
                return;

            foreach (var driver in drivers)
            {
                driver.Reputation = Clamp(driver.Reputation, seasonYear);
            }
        }
    }
}
