using System.Globalization;
using AMS2ChEd.Resources;

namespace AMS2ChEd.Localization
{
    /// <summary>
    /// Central lookup used by <see cref="LocExtension"/> (XAML) and directly from code-behind for
    /// strings that need a resx key but aren't natural static-property call sites (e.g. building a
    /// key at runtime). Prefer <see cref="Strings"/>'s static properties directly in C# where the
    /// key is known at compile time - this class exists mainly for the markup extension and for the
    /// missing-translation fallback behavior described below.
    /// </summary>
    public static class Loc
    {
        /// <summary>
        /// Resolves <paramref name="key"/> against the current UI culture. English (the neutral
        /// resx, Strings.resx) is always authoritative for a key's existence. For any other culture,
        /// if that culture's satellite resx doesn't actually define the key, the value is wrapped as
        /// "[[key]]" instead of silently falling back to English - a missing translation should be
        /// visually obvious when clicking through the app under a non-English culture, not invisible.
        /// </summary>
        public static string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            var neutralValue = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
            if (neutralValue == null)
            {
                // Key doesn't exist in the base English resx at all - a typo/bug, not a missing translation.
                return $"[[{key}]]";
            }

            var culture = CultureInfo.CurrentUICulture;
            if (culture.TwoLetterISOLanguageName == "en")
            {
                return neutralValue;
            }

            var cultureResourceSet = Strings.ResourceManager.GetResourceSet(culture, true, false);
            var translated = cultureResourceSet?.GetString(key);
            return translated ?? $"[[{key}]]";
        }
    }
}
