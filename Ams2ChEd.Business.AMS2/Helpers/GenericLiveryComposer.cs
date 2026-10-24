using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// The pieces of a generic livery: a kit template recoloured with the team palette, number
    /// sheets in the team's number style and colours, and suit textures from the kit's outfit
    /// shading. See baked_season_data.md, "Template kits and generic liveries".
    /// </summary>
    public static class GenericLiveryComposer
    {
        /// <summary>
        /// The kit template recoloured with the palette. Fully opaque: AMS2 reads a livery's alpha
        /// as material data, and generic paint is plain paint, like the solid-colour fallback.
        /// </summary>
        public static Image<Rgba32> BuildBody(string templatePath, IReadOnlyDictionary<string, string> palette)
        {
            var image = Image.Load<Rgba32>(templatePath);
            TemplateRecolour.Apply(image, TemplateRecolour.ParsePalette(palette));
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                    foreach (ref var pixel in accessor.GetRowSpan(y))
                        pixel.A = 255;
            });
            return image;
        }

        /// <summary>
        /// A ten-digit number sheet of the given size from a style's master strip. A two-colour
        /// strip (fill in the red key, outline in the green key) is recoloured before it is
        /// resampled, while its colours are still exact keys; a single-colour one is white digits
        /// that take the fill colour, keeping their alpha.
        /// </summary>
        public static Image<Rgba32> BuildNumberSheet(string stylePath, bool twoColour, int width, int height, string fill, string outline)
        {
            var sheet = Image.Load<Rgba32>(stylePath);
            var fillColour = TemplateRecolour.ParseColour(string.IsNullOrEmpty(fill) ? "#FFFFFF" : fill);
            if (twoColour)
            {
                var outlineColour = TemplateRecolour.ParseColour(string.IsNullOrEmpty(outline) ? "#000000" : outline);
                TemplateRecolour.Apply(sheet, new Dictionary<string, Rgba32> { { "red", fillColour }, { "green", outlineColour } });
            }
            else
            {
                sheet.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                        foreach (ref var pixel in accessor.GetRowSpan(y))
                            pixel = new Rgba32(fillColour.R, fillColour.G, fillColour.B, pixel.A);
                });
            }
            if (width > 0 && height > 0 && (sheet.Width != width || sheet.Height != height))
                sheet.Mutate(c => c.Resize(width, height));
            return sheet;
        }

        /// <summary>
        /// One suit texture: the greyscale fabric shading times the colour, scaled so the part's
        /// typical fabric brightness (the median shade) comes out as the colour itself.
        /// </summary>
        public static Image<Rgba32> BuildOutfitPart(string shadingPath, Rgba32 colour)
        {
            using var shading = Image.Load<L8>(shadingPath);
            var histogram = new long[256];
            shading.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                    foreach (var pixel in accessor.GetRowSpan(y))
                        histogram[pixel.PackedValue]++;
            });
            long half = ((long)shading.Width * shading.Height + 1) / 2, seen = 0;
            int median = 0;
            while (median < 255 && (seen += histogram[median]) < half)
                median++;
            float scale = 1f / Math.Max(1, median);

            var part = new Image<Rgba32>(shading.Width, shading.Height);
            shading.ProcessPixelRows(part, (source, target) =>
            {
                for (int y = 0; y < source.Height; y++)
                {
                    var from = source.GetRowSpan(y);
                    var to = target.GetRowSpan(y);
                    for (int x = 0; x < from.Length; x++)
                    {
                        float s = from[x].PackedValue * scale;
                        to[x] = new Rgba32(Scale(colour.R, s), Scale(colour.G, s), Scale(colour.B, s), 255);
                    }
                }
            });
            return part;
        }

        private static byte Scale(byte channel, float factor) => (byte)Math.Clamp((int)(channel * factor + 0.5f), 0, 255);

        /// <summary>
        /// True when <paramref name="outputPath"/> was made from exactly these inputs (its key is
        /// stored beside it), so a race weekend that changed nothing redraws nothing.
        /// </summary>
        public static bool IsCached(string outputPath, string key) =>
            File.Exists(outputPath) && File.Exists(outputPath + ".key") && File.ReadAllText(outputPath + ".key") == key;

        public static void MarkCached(string outputPath, string key) => File.WriteAllText(outputPath + ".key", key);

        /// <summary>A palette as a stable string, for cache keys.</summary>
        public static string Describe(IReadOnlyDictionary<string, string> palette) =>
            palette == null ? "" : string.Join(",", palette.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => $"{p.Key.ToLowerInvariant()}={p.Value}"));
    }
}
