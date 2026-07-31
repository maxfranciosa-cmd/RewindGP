namespace AMS2ChEd.Business.Storage
{
    /// <summary>
    /// App-level (not game-specific) file locations, relative to AppDomain.CurrentDomain.BaseDirectory
    /// or %LocalAppData%. Genuinely game-agnostic subset of what used to live only in the AMS2 project's
    /// StoragePaths helper.
    /// </summary>
    public static class AppPaths
    {
        private static string _seasonsFolder = null;
        public static string SeasonsFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_seasonsFolder))
                {
                    _seasonsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Seasons");
                }
                return _seasonsFolder;
            }
        }

        private static string _seasonsManifestPath = null;
        public static string SeasonsManifestPath
        {
            get
            {
                if (string.IsNullOrEmpty(_seasonsManifestPath))
                {
                    _seasonsManifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seasons_manifest.json");
                }
                return _seasonsManifestPath;
            }
        }

        private static string _savesFolder = null;
        public static string SavesFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_savesFolder))
                {
                    _savesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Saves");
                }
                return _savesFolder;
            }
        }

        private static string _currentVersionCheckPath = null;
        public static string CurrentVersionCheckPath
        {
            get
            {
                if (string.IsNullOrEmpty(_currentVersionCheckPath))
                {
                    _currentVersionCheckPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                            "RewindGP", "preferences.json");
                }
                return _currentVersionCheckPath;
            }
        }
    }
}
