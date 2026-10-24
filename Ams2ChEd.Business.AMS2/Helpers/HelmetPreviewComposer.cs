using System.Collections.Concurrent;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Font = SixLabors.Fonts.Font;
using FontFamily = SixLabors.Fonts.FontFamily;
using SystemFonts = SixLabors.Fonts.SystemFonts;
using Point = SixLabors.ImageSharp.Point;
using PointF = SixLabors.ImageSharp.PointF;
using Rectangle = SixLabors.ImageSharp.Rectangle;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// The car-select preview of a driver with no season-pack preview: their helmet, car number
    /// and name on the team colour. The helmet is drawn without 3D, from the lookup maps baked
    /// once per helmet model (HelmetPreviews/&lt;era&gt;: the texture coordinate each pixel sees,
    /// and its light). See baked_season_data.md, "Helmet-based previews".
    /// </summary>
    public static class HelmetPreviewComposer
    {
        public const int CardWidth = 2048, CardHeight = 768;

        /// <summary>One helmet model's maps, unpacked into arrays (read once per preview pixel).</summary>
        public sealed class HelmetMaps
        {
            public int Size { get; init; }
            public Rgba64[] Uv { get; init; }
            public Rgba32[] Shading { get; init; }
        }

        private static readonly ConcurrentDictionary<string, HelmetMaps> Maps = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The maps in a folder (uv.png, shading.png), loaded once; null when missing.</summary>
        public static HelmetMaps LoadMaps(string folder)
        {
            if (Maps.TryGetValue(folder, out var cached))
                return cached;
            string uvPath = Path.Combine(folder, "uv.png"), shadingPath = Path.Combine(folder, "shading.png");
            if (!File.Exists(uvPath) || !File.Exists(shadingPath))
                return null;
            using var uvImage = Image.Load<Rgba64>(uvPath);
            using var shadingImage = Image.Load<Rgba32>(shadingPath);
            var maps = new HelmetMaps { Size = uvImage.Width, Uv = new Rgba64[uvImage.Width * uvImage.Height], Shading = new Rgba32[shadingImage.Width * shadingImage.Height] };
            uvImage.CopyPixelDataTo(maps.Uv);
            shadingImage.CopyPixelDataTo(maps.Shading);
            return Maps.GetOrAdd(folder, maps);
        }

        // The helmet models have no visor: their face opening shows the inside of the shell (blue
        // in the UV map), which the preview closes with a dark smoked visor in the shell's light.
        private static readonly Rgba32 Visor = new Rgba32(24, 26, 34);

        /// <summary>
        /// The helmet texture seen through the maps: each covered pixel samples the texture at its
        /// UV (bilinear; transparent texels show the bare grey shell, as the creator's previews
        /// do), times its light; the face opening is a smoked visor. Transparent where there is no
        /// helmet.
        /// </summary>
        public static Image<Rgba32> RenderHelmet(Image<Rgba32> texture, HelmetMaps maps)
        {
            int size = maps.Size;
            int tw = texture.Width, th = texture.Height;
            var texels = new Rgba32[tw * th];
            texture.CopyPixelDataTo(texels);
            var pixels = new Rgba32[size * size];

            for (int i = 0; i < pixels.Length; i++)
            {
                var uv = maps.Uv[i];
                var light = maps.Shading[i];
                if (uv.A == 0 || light.A == 0)
                    continue;
                if (uv.B != 0)
                {
                    float glint = 0.6f + 0.8f * (light.R / 255f);
                    pixels[i] = new Rgba32(Clamp(Visor.R * glint), Clamp(Visor.G * glint), Clamp(Visor.B * glint), light.A);
                    continue;
                }

                float fx = uv.R / 65535f * (tw - 1), fy = uv.G / 65535f * (th - 1);
                int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(x0 + 1, tw - 1), y1 = Math.Min(y0 + 1, th - 1);
                float ax = fx - x0, ay = fy - y0;
                float r = 0, g = 0, b = 0;
                void Add(int tx, int ty, float weight)
                {
                    var c = texels[ty * tw + tx];
                    float alpha = c.A / 255f;
                    r += (c.R * alpha + 205 * (1 - alpha)) * weight;
                    g += (c.G * alpha + 205 * (1 - alpha)) * weight;
                    b += (c.B * alpha + 205 * (1 - alpha)) * weight;
                }
                Add(x0, y0, (1 - ax) * (1 - ay));
                Add(x1, y0, ax * (1 - ay));
                Add(x0, y1, (1 - ax) * ay);
                Add(x1, y1, ax * ay);
                float shade = light.R / 255f;
                pixels[i] = new Rgba32(Clamp(r * shade), Clamp(g * shade), Clamp(b * shade), light.A);
            }
            return Image.LoadPixelData<Rgba32>(pixels, size, size);
        }

        /// <summary>
        /// The driver's card: team colour background, the helmet on the left, then the car number
        /// (from the number sheet when there is one, in the team's own style; otherwise lettered)
        /// and the driver's and team's names, in black or white, whichever reads on the colour.
        /// </summary>
        public static Image<Rgba32> ComposeCard(Image<Rgba32> helmet, Rgba32 background, int carNumber, Image<Rgba32> numberSheet, string driverName, string teamName)
        {
            var card = new Image<Rgba32>(CardWidth, CardHeight, background);
            var ink = Contrast(background);
            card.Mutate(c =>
            {
                int helmetSize = CardHeight - 60;
                using var scaled = helmet.Clone(h => h.Resize(helmetSize, helmetSize));
                c.DrawImage(scaled, new Point(60, 30), 1f);

                const int textLeft = 820, numberTop = 70, numberHeight = 330;
                int nameTop = numberTop + numberHeight + 40;
                if (numberSheet != null && carNumber >= 0 && carNumber < 100)
                {
                    DrawSheetNumber(c, numberSheet, carNumber, textLeft, numberTop, numberHeight);
                }
                else
                {
                    var numberFont = Font(numberHeight * 0.95f);
                    c.DrawText(carNumber.ToString(), numberFont, ink, new PointF(textLeft, numberTop - numberHeight * 0.1f));
                }

                float available = CardWidth - textLeft - 60;
                string name = (driverName ?? "").ToUpperInvariant();
                c.DrawText(name, FittingFont(name, 140, available), ink, new PointF(textLeft, nameTop));
                if (!string.IsNullOrEmpty(teamName))
                    c.DrawText(teamName.ToUpperInvariant(), FittingFont(teamName.ToUpperInvariant(), 70, available), ink, new PointF(textLeft, nameTop + 160));
            });
            return card;
        }

        /// <summary>A team's menu tile: its drivers' helmets side by side on the team colour.</summary>
        public static Image<Rgba32> ComposeTeamTile(IReadOnlyList<Image<Rgba32>> helmets, Rgba32 background)
        {
            var tile = new Image<Rgba32>(CardWidth, CardHeight, background);
            if (helmets.Count == 0)
                return tile;
            int size = CardHeight - 60;
            int gap = 80, total = helmets.Count * size + (helmets.Count - 1) * gap, left = (CardWidth - total) / 2;
            tile.Mutate(c =>
            {
                for (int i = 0; i < helmets.Count; i++)
                {
                    using var scaled = helmets[i].Clone(h => h.Resize(size, size));
                    c.DrawImage(scaled, new Point(left + i * (size + gap), 30), 1f);
                }
            });
            return tile;
        }

        // Digits are cells of a ten-digit sheet, scaled to the given height, side by side.
        private static void DrawSheetNumber(IImageProcessingContext c, Image<Rgba32> sheet, int number, int left, int top, int height)
        {
            int cell = sheet.Width / 10;
            float scale = height / (float)sheet.Height;
            int x = left;
            foreach (char digit in number.ToString())
            {
                int d = digit - '0';
                using var glyph = sheet.Clone(g => g.Crop(new Rectangle(d * cell, 0, cell, sheet.Height)).Resize((int)(cell * scale), height));
                c.DrawImage(glyph, new Point(x, top), 1f);
                x += glyph.Width;
            }
        }

        // The font at the given size, shrunk until the text fits the width.
        private static Font FittingFont(string text, float size, float width)
        {
            var font = Font(size);
            while (size > 24 && TextMeasurer.MeasureSize(text, new TextOptions(font)).Width > width)
                font = Font(size *= 0.92f);
            return font;
        }

        /// <summary>Black or white, whichever contrasts more with the colour.</summary>
        public static Rgba32 Contrast(Rgba32 colour) =>
            0.299 * colour.R + 0.587 * colour.G + 0.114 * colour.B > 150 ? new Rgba32(0, 0, 0) : new Rgba32(255, 255, 255);

        private static FontFamily? family;
        private static readonly object FontLock = new();

        // Anton (OFL), shipped with the app; a system font when it can't be found.
        private static Font Font(float size)
        {
            lock (FontLock)
            {
                if (family == null)
                {
                    string anton = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "Anton-Regular.ttf");
                    if (File.Exists(anton))
                        family = new FontCollection().Add(anton);
                    else if (SystemFonts.TryGet("Impact", out var impact))
                        family = impact;
                    else
                        family = SystemFonts.Families.First();
                }
                return family.Value.CreateFont(size);
            }
        }

        private static byte Clamp(float value) => (byte)Math.Clamp((int)(value + 0.5f), 0, 255);
    }
}
