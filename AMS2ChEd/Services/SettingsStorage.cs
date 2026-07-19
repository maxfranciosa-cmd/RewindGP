using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Settings;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using System.Configuration;
using System.IO;

namespace AMS2ChEd.Services
{
    public class SettingsStorage : IAms2AppSettingsStorage
    {
        private const string FOLDERPATH_SETTINGS_KEY = "AMS2FolderPath";
        private const string DRIVERNAME_SETTINGS_KEY = "AMS2DriverName";
        public void SaveSettings(Ams2AppSettings settings)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY] != null)
            {
                config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY].Value = settings.Ams2Folder;
            }
            else
            {
                config.AppSettings.Settings.Add(FOLDERPATH_SETTINGS_KEY, settings.Ams2Folder);
            }
            if (config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY] != null)
            {
                config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY].Value = settings.Ams2InGameName;
            }
            else
            {
                config.AppSettings.Settings.Add(DRIVERNAME_SETTINGS_KEY, settings.Ams2InGameName);
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        Ams2AppSettings IAms2AppSettingsStorage.LoadSettings()
        {
            // Try to get AMS2 folder from saved settings first
            string savedPath = GetSavedPath();
            string inGameName = GetInGameName();

            return new Ams2AppSettings
            {
                Ams2Folder = Directory.Exists(savedPath) ? savedPath : GetAMS2InstallPath(),
                Ams2InGameName = inGameName,
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

        private string GetAMS2InstallPath() => Ams2InstallPathDetector.DetectInstallPath();
    }
}
