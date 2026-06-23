using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.AMS2.DataLoaders.Mocks
{
    public class CommonStaticData
    {
        public static Dictionary<string, double> GOOD_DRIVER_RATING_VALUES = new()
        {
            { "aggression", 0.92 },
            { "avoidance_of_forced_mistakes", 0.85},
            { "avoidance_of_mistakes", 0.85},
            { "blue_flag_conceding", 1.0},
            { "consistency", 0.99},
            { "defending", 0.94},
            { "fuel_management", 0.85},
            { "qualifying_skill", 0.97},
            { "race_skill", 0.98},
            { "stamina", 0.96},
            { "start_reactions", 1.0},
            { "tyre_management", 0.95},
            { "weather_tyre_changes", 0.97},
            { "wet_skill", 0.99 },
        };

        public static Dictionary<string, double> MEDIUM_DRIVER_RATING_VALUES = new()
        {
            { "aggression", 0.82 },
            { "avoidance_of_forced_mistakes", 0.75},
            { "avoidance_of_mistakes", 0.75},
            { "blue_flag_conceding", 0.9},
            { "consistency", 0.89},
            { "defending", 0.84},
            { "fuel_management", 0.75},
            { "qualifying_skill", 0.87},
            { "race_skill", 0.88},
            { "stamina", 0.86},
            { "start_reactions", 0.9},
            { "tyre_management", 0.85},
            { "weather_tyre_changes", 0.87},
            { "wet_skill", 0.89 },
        };

        public static Dictionary<string, double> BAD_DRIVER_RATING_VALUES = new()
        {
            { "aggression", 0.82 },
            { "avoidance_of_forced_mistakes", 0.75},
            { "avoidance_of_mistakes", 0.75},
            { "blue_flag_conceding", 0.9},
            { "consistency", 0.89},
            { "defending", 0.84},
            { "fuel_management", 0.75},
            { "qualifying_skill", 0.87},
            { "race_skill", 0.88},
            { "stamina", 0.86},
            { "start_reactions", 0.9},
            { "tyre_management", 0.85},
            { "weather_tyre_changes", 0.87},
            { "wet_skill", 0.89 },
        };

        public static Dictionary<string, int> FIRST6_POINTS_SYSTEM = new()
        {
            { "1", 10 },
            { "2", 6 },
            { "3", 4 },
            { "4", 3 },
            { "5", 2 },
            { "6", 1 },
        };

        public static List<Race> RACES = new()
        {
            new Race { RaceId = 1, RaceName = "Australia", Circuit = "Melbourne", RaceDate = "1996-03-10" },
            new Race { RaceId = 2, RaceName = "Brazil", Circuit = "Interlagos", RaceDate = "1996-04-10" },
            new Race { RaceId = 4, RaceName = "San Marino", Circuit = "Imola", RaceDate = "1996-05-10" },
        };

        public static Dictionary<string, double> GetPerformanceMalus(double malus)
        {
            return new Dictionary<string, double>()
            {
                { "consistency", malus },
                { "defending", malus},
                { "fuel_management", malus},
                { "qualifying_skill", malus},
                { "race_skill", malus},
                { "tyre_management", malus}
            };
        }
    }
}
