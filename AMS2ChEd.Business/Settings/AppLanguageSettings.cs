using System.Configuration;

namespace AMS2ChEd.Business.Settings
{
    /// <summary>
    /// The app's UI language preference. Game-agnostic (unlike AMS2FolderPath/AMS2RaceLength in
    /// Ams2GameInstallSettingsStorage) - a UI language isn't an AMS2-specific concern, so it doesn't
    /// belong on IGameInstallSettingsStorage. Static, mirroring how Ams2GameInstallSettingsStorage
    /// itself is a thin wrapper over ConfigurationManager: App.xaml.cs needs to read this before any
    /// DI container exists (culture must be set before the first window is constructed).
    /// Switching is restart-based - see App.xaml.cs's ApplyCulture.
    /// </summary>
    public static class AppLanguageSettings
    {
        private const string LANGUAGE_SETTINGS_KEY = "AppLanguage";
        private const string DefaultLanguageCode = "en";

        public static string LoadLanguageCode()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var value = config.AppSettings.Settings[LANGUAGE_SETTINGS_KEY]?.Value;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            catch
            {
                // Ignore errors, will use default
            }
            return DefaultLanguageCode;
        }

        public static void SaveLanguageCode(string languageCode)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[LANGUAGE_SETTINGS_KEY] != null)
            {
                config.AppSettings.Settings[LANGUAGE_SETTINGS_KEY].Value = languageCode;
            }
            else
            {
                config.AppSettings.Settings.Add(LANGUAGE_SETTINGS_KEY, languageCode);
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }
    }
}
