using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.Settings.Contracts;
using System.Configuration;

namespace Ams2ChEd.Business.AMS2.Settings
{
    public class Ams2GameInstallSettingsStorage : IGameInstallSettingsStorage
    {
        private const string FOLDERPATH_SETTINGS_KEY = "AMS2FolderPath";
        private const string DRIVERNAME_SETTINGS_KEY = "AMS2DriverName";

        public void SaveSettings(GameInstallSettings settings)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY] != null)
            {
                config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY].Value = settings.GameInstallFolder;
            }
            else
            {
                config.AppSettings.Settings.Add(FOLDERPATH_SETTINGS_KEY, settings.GameInstallFolder);
            }
            if (config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY] != null)
            {
                config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY].Value = settings.PlayerInGameName;
            }
            else
            {
                config.AppSettings.Settings.Add(DRIVERNAME_SETTINGS_KEY, settings.PlayerInGameName);
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public GameInstallSettings LoadSettings()
        {
            string savedPath = GetSavedPath();
            string inGameName = GetInGameName();

            return new GameInstallSettings
            {
                GameInstallFolder = Directory.Exists(savedPath) ? savedPath : Ams2InstallPathDetector.DetectInstallPath(),
                PlayerInGameName = inGameName,
            };
        }

        private string GetInGameName()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY] != null)
                {
                    return config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY].Value;
                }
            }
            catch
            {
                // Ignore errors, will use default path
            }
            return string.Empty;
        }

        private string GetSavedPath()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY] != null)
                {
                    return config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY].Value;
                }
            }
            catch
            {
                // Ignore errors, will use default path
            }
            return string.Empty;
        }
    }
}
