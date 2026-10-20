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

        public static string SeasonsManifestPath(string fileName) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

        /// <summary>
        /// Real-world record of every driver and team in history: what a save's accolades and
        /// hall of fame start from.
        /// </summary>
        public static string AllTimeRecordsPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HallOfFame", "all_time_records.json");

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

        /// <summary>
        /// Downloaded copies of the pictures season packs link to by URL (driver photos, race
        /// covers), so each is fetched once - see RemoteImageCache. Outside the app folder, so it
        /// survives updates and reinstalls.
        /// </summary>
        public static string ImageCacheFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RewindGP", "ImageCache");

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
