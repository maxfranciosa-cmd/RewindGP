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
using RectangleF = SixLabors.ImageSharp.RectangleF;
using HorizontalAlignment = SixLabors.Fonts.HorizontalAlignment;
using VerticalAlignment = SixLabors.Fonts.VerticalAlignment;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// The car-select preview of a driver with no season-pack preview: their helmet beside a
    /// team-colour square with the car number. The helmet is drawn without 3D, from the lookup maps baked
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
            /// <summary>When the maps were baked, so a rebake can retire previews made with the old ones.</summary>
            public string Stamp { get; init; }
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
            var maps = new HelmetMaps { Size = uvImage.Width, Stamp = File.GetLastWriteTimeUtc(uvPath).Ticks.ToString(), Uv = new Rgba64[uvImage.Width * uvImage.Height], Shading = new Rgba32[shadingImage.Width * shadingImage.Height] };
            uvImage.CopyPixelDataTo(maps.Uv);
            shadingImage.CopyPixelDataTo(maps.Shading);
            return Maps.GetOrAdd(folder, maps);
        }

        // The visor (blue in the UV map, from the game's helmet model) samples the driver's visor
        // texture; where that is see-through, or missing, it shows dark smoke: the inside.
        private static readonly Rgba32 Visor = new Rgba32(18, 20, 28);

        /// <summary>
        /// The helmet texture seen through the maps: each covered pixel samples the texture at its
        /// UV (bilinear; transparent texels show the bare grey shell, as the creator's previews
        /// do), times its light; visor pixels sample the visor texture the same way. Transparent
        /// where there is no helmet.
        /// </summary>
        public static Image<Rgba32> RenderHelmet(Image<Rgba32> texture, HelmetMaps maps, Image<Rgba32> visorTexture = null)
        {
            int size = maps.Size;
            var shell = new Texels(texture, new Rgba32(205, 205, 205));
            var visor = visorTexture != null ? new Texels(visorTexture, Visor) : null;
            var pixels = new Rgba32[size * size];

            for (int i = 0; i < pixels.Length; i++)
            {
                var uv = maps.Uv[i];
                var light = maps.Shading[i];
                if (uv.A == 0 || light.A == 0)
                    continue;
                float shade = light.R / 255f;
                var (r, g, b) = uv.B == 0 ? shell.Sample(uv) : visor?.Sample(uv) ?? (Visor.R, Visor.G, Visor.B);
                pixels[i] = new Rgba32(Clamp(r * shade), Clamp(g * shade), Clamp(b * shade), light.A);
            }
            return Image.LoadPixelData<Rgba32>(pixels, size, size);
        }

        // A texture's pixels, sampled bilinearly at a 16-bit UV; transparent texels show the backdrop.
        private sealed class Texels
        {
            private readonly Rgba32[] texels;
            private readonly int width, height;
            private readonly Rgba32 backdrop;

            public Texels(Image<Rgba32> texture, Rgba32 backdrop)
            {
                width = texture.Width;
                height = texture.Height;
                texels = new Rgba32[width * height];
                texture.CopyPixelDataTo(texels);
                this.backdrop = backdrop;
            }

            public (float r, float g, float b) Sample(Rgba64 uv)
            {
                float fx = uv.R / 65535f * (width - 1), fy = uv.G / 65535f * (height - 1);
                int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(x0 + 1, width - 1), y1 = Math.Min(y0 + 1, height - 1);
                float ax = fx - x0, ay = fy - y0;
                float r = 0, g = 0, b = 0;
                void Add(int tx, int ty, float weight)
                {
                    var c = texels[ty * width + tx];
                    float alpha = c.A / 255f;
                    r += (c.R * alpha + backdrop.R * (1 - alpha)) * weight;
                    g += (c.G * alpha + backdrop.G * (1 - alpha)) * weight;
                    b += (c.B * alpha + backdrop.B * (1 - alpha)) * weight;
                }
                Add(x0, y0, (1 - ax) * (1 - ay));
                Add(x1, y0, ax * (1 - ay));
                Add(x0, y1, (1 - ax) * ay);
                Add(x1, y1, ax * ay);
                return (r, g, b);
            }
        }

        /// <summary>
        /// The driver's card, on a transparent background like a skin pack's previews: the helmet,
        /// and beside it a square in the team colour carrying the car number (from the number
        /// sheet when there is one, in the team's own style; otherwise lettered in black or
        /// white, whichever reads on the colour), the two centred together.
        /// </summary>
        public static Image<Rgba32> ComposeCard(Image<Rgba32> helmet, Rgba32 squareColour, int carNumber, Image<Rgba32> numberSheet)
        {
            var card = new Image<Rgba32>(CardWidth, CardHeight, new Rgba32(0, 0, 0, 0));
            const int helmetSize = CardHeight - 60, squareSize = 520, gap = 60;
            int left = (CardWidth - helmetSize - gap - squareSize) / 2;
            int squareLeft = left + helmetSize + gap, squareTop = (CardHeight - squareSize) / 2;
            card.Mutate(c =>
            {
                using var scaled = helmet.Clone(h => h.Resize(helmetSize, helmetSize));
                c.DrawImage(scaled, new Point(left, 30), 1f);

                c.Fill(squareColour, new RectangleF(squareLeft, squareTop, squareSize, squareSize));
                var box = new Rectangle(squareLeft + 50, squareTop + 70, squareSize - 100, squareSize - 140);
                if (numberSheet != null && carNumber >= 0 && carNumber < 100)
                {
                    DrawSheetNumber(c, numberSheet, carNumber, box);
                }
                else
                {
                    var options = new RichTextOptions(Font(box.Height))
                    {
                        Origin = new PointF(box.X + box.Width / 2f, box.Y + box.Height / 2f),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    c.DrawText(options, carNumber.ToString(), Contrast(squareColour));
                }
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

        // Digits are cells of a ten-digit sheet, side by side, as large as fits the box, centred.
        private static void DrawSheetNumber(IImageProcessingContext c, Image<Rgba32> sheet, int number, Rectangle box)
        {
            string digits = number.ToString();
            int cell = sheet.Width / 10;
            float scale = Math.Min(box.Height / (float)sheet.Height, box.Width / (float)(cell * digits.Length));
            int glyphWidth = Math.Max(1, (int)(cell * scale)), glyphHeight = Math.Max(1, (int)(sheet.Height * scale));
            int x = box.X + (box.Width - glyphWidth * digits.Length) / 2, y = box.Y + (box.Height - glyphHeight) / 2;
            foreach (char digit in digits)
            {
                int d = digit - '0';
                using var glyph = sheet.Clone(g => g.Crop(new Rectangle(d * cell, 0, cell, sheet.Height)).Resize(glyphWidth, glyphHeight));
                c.DrawImage(glyph, new Point(x, y), 1f);
                x += glyphWidth;
            }
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
