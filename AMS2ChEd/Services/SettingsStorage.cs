using Ams2ChEd.Business.AMS2.Settings;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using Microsoft.Win32;
using System.Configuration;
using System.IO;

namespace AMS2ChEd.Services
{
    public class SettingsStorage : IAms2AppSettingsStorage
    {
        private const string AMS2_REGISTRY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1066890";
        private const string DEFAULT_STEAM_PATH = @"C:\Program Files (x86)\Steam\steamapps\common\Automobilista 2";
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

        private string GetAMS2InstallPath()
        {
            try
            {
                // Try to get from registry
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(AMS2_REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        string installLocation = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                        {
                            return installLocation;
                        }
                    }
                }

                // Try common Steam library locations
                string[] commonPaths = new[]
                {
                    DEFAULT_STEAM_PATH,
                    @"D:\SteamLibrary\steamapps\common\Automobilista 2",
                    @"E:\SteamLibrary\steamapps\common\Automobilista 2",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        @"Steam\steamapps\common\Automobilista 2")
                };

                foreach (var path in commonPaths)
                {
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }
                return DEFAULT_STEAM_PATH;
            }
            catch (Exception ex)
            {
                return DEFAULT_STEAM_PATH;
            }
        }
    }
}
