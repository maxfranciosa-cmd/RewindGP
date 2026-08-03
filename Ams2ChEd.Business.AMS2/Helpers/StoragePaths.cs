using System.Net.NetworkInformation;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    public static class StoragePaths
    {
        private static string _teamsFilePath = null;
        public static string TeamsFilePath
        {
            get
            {
                if(string.IsNullOrEmpty(_teamsFilePath))
                {
                    _teamsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Teams", "teams.json");
                }
                return _teamsFilePath;
            }
        }

        public static string DriversFilePath(int seasonYear)
        {
            return Path.Combine(SeasonsFolder, seasonYear.ToString(), "drivers.json");
        }

        public static string AccoladesFilePath(int seasonYear)
        {
            return Path.Combine(SeasonsFolder, seasonYear.ToString(), "accolades.json");
        }

        private static string _seasonsFilePath = null;
        public static string SeasonsFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_seasonsFilePath))
                {
                    _seasonsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Seasons");
                }
                return _seasonsFilePath;
            }
        }

        public static string SeasonFilePath(int seasonYear)
        {
            return Path.Combine(SeasonsFolder, seasonYear.ToString(), "season.json");
        }

        public static string ExternalLiveriesFilePath(int seasonYear)
        {
            return Path.Combine(SeasonsFolder, seasonYear.ToString(), "external_liveries.json");
        }

        private static string _savesPath = null;
        public static string SavesFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_savesPath))
                {
                    _savesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Saves");
                }
                return _savesPath;
            }
        }

        private static string _currentVersionCheck = null;
        public static string CurrentVersionCheckPath
        {
            get
            {
                if (string.IsNullOrEmpty(_currentVersionCheck))
                {
                    _currentVersionCheck = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                            "RewindGP", "preferences.json");
                }
                return _currentVersionCheck;
            }
        }

        private static string _carModelCapacitiesFilePath = null;
        public static string CarModelCapacitiesFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_carModelCapacitiesFilePath))
                {
                    _carModelCapacitiesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CarModels", "car_model_capacities.json");
                }
                return _carModelCapacitiesFilePath;
            }
        }

        private static string _vehiclePakBackupsFolder = null;
        public static string VehiclePakBackupsFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_vehiclePakBackupsFolder))
                {
                    _vehiclePakBackupsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                            "RewindGP", "vehicle_pak_backups");
                }
                return _vehiclePakBackupsFolder;
            }
        }

        private static string _baseHelmetLiveriesPath = null;
        public static string BaseHelmetLiveriesPath
        {
            get
            {
                if (string.IsNullOrEmpty(_baseHelmetLiveriesPath))
                {
                    _baseHelmetLiveriesPath = Path.Combine("..", "..", "Drivers", "base_helmet_liveries");
                }
                return _baseHelmetLiveriesPath;
            }
        }
    }
}
