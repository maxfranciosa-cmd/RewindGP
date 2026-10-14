using System.Configuration;

namespace AMS2ChEd.Business.Settings
{
    public enum AppStyle
    {
        Retro,
        Charcoal
    }

    /// <summary>
    /// The app's visual style preference. Game-agnostic and static for the same reasons as
    /// AppLanguageSettings: App.xaml.cs needs it before any window (or DI container) exists, as the
    /// style's resources must be merged before the first window is constructed.
    /// Switching is restart-based - see App.xaml.cs's ApplyStyle.
    /// </summary>
    public static class AppStyleSettings
    {
        private const string STYLE_SETTINGS_KEY = "AppStyle";
        private const AppStyle DefaultStyle = AppStyle.Retro;

        public static AppStyle LoadStyle()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                return Parse(config.AppSettings.Settings[STYLE_SETTINGS_KEY]?.Value);
            }
            catch
            {
                // Ignore errors, will use default
            }
            return DefaultStyle;
        }

        public static void SaveStyle(AppStyle style)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[STYLE_SETTINGS_KEY] != null)
            {
                config.AppSettings.Settings[STYLE_SETTINGS_KEY].Value = style.ToString();
            }
            else
            {
                config.AppSettings.Settings.Add(STYLE_SETTINGS_KEY, style.ToString());
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        /// <summary>An unknown or missing name gives the default style.</summary>
        public static AppStyle Parse(string styleName)
        {
            return Enum.TryParse<AppStyle>(styleName, ignoreCase: true, out var style) && Enum.IsDefined(style)
                ? style
                : DefaultStyle;
        }
    }
}
