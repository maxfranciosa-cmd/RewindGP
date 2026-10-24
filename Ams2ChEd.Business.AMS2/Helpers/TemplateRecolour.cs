using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Recolours a colour-coded template: every pixel in one of the six key colours takes that
    /// key's colour from a palette. Shared by the Season Pack Creator's livery creator and the
    /// app's generic liveries.
    /// </summary>
    public static class TemplateRecolour
    {
        /// <summary>The six key colours, by palette key (matched ignoring case).</summary>
        public static readonly IReadOnlyDictionary<string, Rgba32> KeyColours = new Dictionary<string, Rgba32>(StringComparer.OrdinalIgnoreCase)
        {
            { "red", new Rgba32(255, 0, 0) },
            { "green", new Rgba32(0, 255, 0) },
            { "blue", new Rgba32(0, 0, 255) },
            { "yellow", new Rgba32(255, 255, 0) },
            { "magenta", new Rgba32(255, 0, 255) },
            { "cyan", new Rgba32(0, 255, 255) }
        };

        /// <summary>
        /// Replaces each key colour pixel (matched on RGB, whatever its alpha) with the palette's
        /// colour for that key, in one pass: a pixel is recoloured at most once, so a key mapped
        /// to another key's colour is not recoloured again by that key. Keys missing from the
        /// palette keep their template colour.
        /// </summary>
        public static void Apply(Image<Rgba32> image, IReadOnlyDictionary<string, Rgba32> palette)
        {
            // Every key colour has each channel at 0 or 255, so the channels' top bits index a
            // table of eight; black (0) and white (7) are not keys.
            var replacements = new Rgba32?[8];
            bool any = false;
            foreach (var key in palette)
            {
                if (!KeyColours.TryGetValue(key.Key, out var keyColour))
                    throw new ArgumentException($"'{key.Key}' is not a template key colour.", nameof(palette));
                if (key.Value.Equals(keyColour))
                    continue;
                replacements[Index(keyColour)] = key.Value;
                any = true;
            }
            if (!any)
                return;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        if ((pixel.R == 0 || pixel.R == 255) && (pixel.G == 0 || pixel.G == 255) && (pixel.B == 0 || pixel.B == 255))
                        {
                            var replacement = replacements[Index(pixel)];
                            if (replacement.HasValue)
                                row[x] = replacement.Value;
                        }
                    }
                }
            });
        }

        /// <summary>Parses a palette of "#RRGGBB" (or "#AARRGGBB") strings, as baked data stores it.</summary>
        public static Dictionary<string, Rgba32> ParsePalette(IReadOnlyDictionary<string, string> palette)
        {
            var parsed = new Dictionary<string, Rgba32>(StringComparer.OrdinalIgnoreCase);
            if (palette == null)
                return parsed;
            foreach (var entry in palette)
                parsed[entry.Key] = ParseColour(entry.Value);
            return parsed;
        }

        public static Rgba32 ParseColour(string hex)
        {
            var digits = hex?.TrimStart('#') ?? "";
            if ((digits.Length != 6 && digits.Length != 8) || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"'{hex}' is not a #RRGGBB colour.");
            if (digits.Length == 6)
                return new Rgba32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
            return new Rgba32((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24));
        }

        private static int Index(Rgba32 colour) => (colour.R >> 7) << 2 | (colour.G >> 7) << 1 | colour.B >> 7;
    }
}
