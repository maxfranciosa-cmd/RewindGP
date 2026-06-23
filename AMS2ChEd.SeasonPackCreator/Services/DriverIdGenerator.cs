namespace AMS2ChEd.SeasonPackCreator.Services
{
    public class DriverIdGenerator
    {
        public static string GenerateDriverId(string driverName)
        {
            if (string.IsNullOrWhiteSpace(driverName))
                return "unknown";

            // Convert to lowercase
            string result = driverName.ToLowerInvariant();

            // Remove accents/diacritics
            result = RemoveDiacritics(result);

            // Replace any non-letter character with underscore
            result = System.Text.RegularExpressions.Regex.Replace(result, @"[^a-z]", "_");

            // Remove consecutive underscores
            result = System.Text.RegularExpressions.Regex.Replace(result, @"_+", "_");

            // Trim underscores from start and end
            result = result.Trim('_');

            return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
