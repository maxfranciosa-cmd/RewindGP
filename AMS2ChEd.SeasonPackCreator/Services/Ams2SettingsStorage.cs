using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Settings;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using System.Configuration;
using System.IO;

namespace AMS2ChEd.SeasonPackEditor.Services
{
    /// <summary>
    /// IAms2AppSettingsStorage for SeasonPackCreator. Persists the AMS2 install folder in this exe's
    /// own .exe.config (SeasonPackCreator has no reference to the main Rewind GP app, so it can't read
    /// that app's setting - the value is necessarily separate), falling back to auto-detection when
    /// unset. Needed now that the calibration export writes real CustomAI/livery files directly into
    /// the configured install. Ams2InGameName stays unused/empty - shared-memory reading during
    /// calibration identifies cars by team roster, never by player identity.
    /// </summary>
    public class Ams2SettingsStorage : IAms2AppSettingsStorage
    {
        private const string FOLDERPATH_SETTINGS_KEY = "AMS2FolderPath";

        public Ams2AppSettings LoadSettings()
        {
            string savedPath = GetSavedPath();

            return new Ams2AppSettings
            {
                Ams2Folder = Directory.Exists(savedPath) ? savedPath : Ams2InstallPathDetector.DetectInstallPath(),
                Ams2InGameName = string.Empty
            };
        }

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
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
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
                // Ignore errors, will fall back to auto-detection
            }
            return string.Empty;
        }
    }
}
