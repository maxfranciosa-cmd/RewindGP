using System.Configuration;

namespace AMS2ChEd.Business.Settings
{
    /// <summary>
    /// The player's choices about crash consequences (crash-incidents-design.md, section 7). Injuries
    /// always happen; these turn on what goes beyond them. All off unless the player turns them on:
    /// - fatalities: a crash can kill a driver (off: the worst crashes end in a long injury instead);
    /// - the player's death: the same for the player's own driver, only with fatalities on;
    /// - crash detection: crashes in the races the player drives are found in the game's telemetry.
    ///   Off by default until it has been tried in real race weekends; simulated races have their
    ///   crashes either way.
    /// Game-agnostic and static, like AppStyleSettings.
    /// </summary>
    public static class CrashIncidentSettings
    {
        private const string FATALITIES_KEY = "CrashFatalities";
        private const string PLAYER_DEATH_KEY = "CrashPlayerDeath";
        private const string DETECTION_KEY = "CrashDetection";

        public static bool LoadFatalitiesEnabled() => Load(FATALITIES_KEY);
        public static bool LoadPlayerDeathEnabled() => Load(PLAYER_DEATH_KEY);
        public static bool LoadDetectionEnabled() => Load(DETECTION_KEY);

        public static void SaveFatalitiesEnabled(bool enabled) => Save(FATALITIES_KEY, enabled);
        public static void SavePlayerDeathEnabled(bool enabled) => Save(PLAYER_DEATH_KEY, enabled);
        public static void SaveDetectionEnabled(bool enabled) => Save(DETECTION_KEY, enabled);

        public static CrashOptions Load() => new()
        {
            FatalitiesEnabled = LoadFatalitiesEnabled(),
            PlayerDeathEnabled = LoadPlayerDeathEnabled()
        };

        private static bool Load(string key)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                return bool.TryParse(config.AppSettings.Settings[key]?.Value, out var enabled) && enabled;
            }
            catch
            {
                // Ignore errors, off by default
            }
            return false;
        }

        private static void Save(string key, bool enabled)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[key] != null)
            {
                config.AppSettings.Settings[key].Value = enabled.ToString();
            }
            else
            {
                config.AppSettings.Settings.Add(key, enabled.ToString());
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }
    }

    // what CrashOutcomeRoller may do, read from CrashIncidentSettings (or set directly in tests)
    public class CrashOptions
    {
        public bool FatalitiesEnabled { get; set; }

        // only with FatalitiesEnabled
        public bool PlayerDeathEnabled { get; set; }
    }
}
