using System.Globalization;

namespace AMS2ChEd.Localization
{
    /// <summary>
    /// Formats an ordinal number for narrative text, per the current UI culture. English uses
    /// digit-dependent suffixes (1st, 2nd, 3rd, 4th, 11th...); Italian uses a single ordinal
    /// indicator regardless of the trailing digit (1°, 2°, 21°...), so this can't be done with a
    /// plain resx string swap - the suffix *rule*, not just the text, differs per language.
    /// Shared by every narrative window that builds "Nth title/win/podium" phrases.
    /// </summary>
    public static class OrdinalFormatter
    {
        public static string Format(int number)
        {
            if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it")
                return $"{number}°";

            if (number % 100 is 11 or 12 or 13)
                return $"{number}th";

            return (number % 10) switch
            {
                1 => $"{number}st",
                2 => $"{number}nd",
                3 => $"{number}rd",
                _ => $"{number}th"
            };
        }
    }
}
