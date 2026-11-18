using System.Globalization;
using System.Resources;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.Localization
{
    /// <summary>
    /// The name a race is shown with, in the app's language. A race names its resource key in
    /// race_name_key (Resources/RaceNames.resx, + RaceNames.&lt;culture&gt;.resx satellites); its
    /// race_name stays the English name the game logic matches on (results, calendar refresh,
    /// track resolution) and is what gets shown when the race has no key or a key this app version
    /// doesn't know, as a season pack can carry one. Lives in Business, not in the app's Strings,
    /// because the AMS2 layer and the cover drawer show race names too.
    /// A known key with no translation for the current language shows as "[[key]]", like every
    /// other missing translation in the app (see AMS2ChEd's Loc).
    /// </summary>
    public static class RaceNames
    {
        public static ResourceManager ResourceManager { get; } =
            new ResourceManager("AMS2ChEd.Business.Resources.RaceNames", typeof(RaceNames).Assembly);

        public static string DisplayName(this Race race) =>
            race == null ? null : DisplayName(race.RaceNameKey, race.RaceName);

        /// <summary>
        /// The shown name of the race a result was recorded at. A result stores only the English
        /// name (it is matched on), so the key comes from that race in <paramref name="season"/>;
        /// a result from another season, or one the season no longer has, keeps its English name.
        /// </summary>
        public static string DisplayName(this GrandPrixResult result, ISeason season)
        {
            if (result == null)
                return null;
            var race = season?.Year == result.Year
                ? season.Races?.FirstOrDefault(r => r.RaceId == result.RaceId && r.RaceName == result.GrandPrixName)
                : null;
            return race != null ? race.DisplayName() : result.GrandPrixName;
        }

        /// <summary>
        /// The key whose English name is exactly <paramref name="englishName"/>, or null: lets
        /// the season pack creator fill in race_name_key for the Grands Prix already translated.
        /// </summary>
        public static string KeyFor(string englishName)
        {
            if (string.IsNullOrEmpty(englishName))
                return null;
            var neutral = ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            if (neutral == null)
                return null;
            foreach (System.Collections.DictionaryEntry entry in neutral)
            {
                if (entry.Value is string value && value == englishName)
                    return (string)entry.Key;
            }
            return null;
        }

        public static string DisplayName(string key, string englishName)
        {
            if (string.IsNullOrEmpty(key))
                return englishName;

            var neutralValue = ResourceManager.GetString(key, CultureInfo.InvariantCulture);
            if (neutralValue == null)
                return englishName;

            var culture = CultureInfo.CurrentUICulture;
            if (culture.TwoLetterISOLanguageName == "en")
                return neutralValue;

            // pt-BR is translated by RaceNames.pt.resx: walk up to the neutral culture, but never
            // to the invariant one, whose English would hide a missing translation
            for (; !culture.Equals(CultureInfo.InvariantCulture); culture = culture.Parent)
            {
                var translated = ResourceManager.GetResourceSet(culture, true, false)?.GetString(key);
                if (translated != null)
                    return translated;
            }
            return $"[[{key}]]";
        }
    }
}
