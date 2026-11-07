using System.Configuration;

namespace Ams2ChEd.Business.AMS2.Settings
{
    /// <summary>
    /// How much the career dresses AMS2 up, both on unless the player turns them off:
    /// - the player's name: the AMS2 profile takes the career driver's name, handle and
    ///   nationality for the race (Ams2RaceLaunchAssistant.ApplyPlayerIdentity), put back afterwards;
    /// - borrowed liveries: a baked team with no livery of its own for the season wears a
    ///   similar one from another season's installed pack (its cosmetic_fallbacks, see
    ///   BakedSeasonMerge); off, it keeps the generic livery.
    /// Static, like CrashIncidentSettings, as the season loader has no settings storage injected.
    /// </summary>
    public static class Ams2CareerLookSettings
    {
        private const string CHANGE_PLAYER_NAME_KEY = "AMS2ChangePlayerName";
        private const string BORROW_LIVERIES_KEY = "AMS2BorrowLiveries";

        public static bool LoadChangePlayerName() => Load(CHANGE_PLAYER_NAME_KEY);
        public static bool LoadBorrowLiveries() => Load(BORROW_LIVERIES_KEY);

        public static void SaveChangePlayerName(bool enabled) => Save(CHANGE_PLAYER_NAME_KEY, enabled);
        public static void SaveBorrowLiveries(bool enabled) => Save(BORROW_LIVERIES_KEY, enabled);

        private static bool Load(string key)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var value = config.AppSettings.Settings[key]?.Value;
                if (!string.IsNullOrEmpty(value) && bool.TryParse(value, out var enabled))
                    return enabled;
            }
            catch
            {
                // Ignore errors, on by default
            }
            return true;
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
}
