using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AMS2ChEd.Business.Artwork
{
    /// <summary>
    /// The programme cover of a race with no cover picture: the packs' shared poster, cut into
    /// layers (RaceCovers/), in one of a list of colour sets picked by year and race_id, with the
    /// race name, date and year written on its lower panel. See baked_season_data.md, "Generated
    /// race covers".
    /// </summary>
    public static class RaceCoverComposer
    {
        public sealed class CoverColours
        {
            [JsonPropertyName("panel")] public string Panel { get; set; }
            [JsonPropertyName("rule")] public string Rule { get; set; }
            [JsonPropertyName("title")] public string Title { get; set; }
            [JsonPropertyName("chequer")] public string Chequer { get; set; }
        }

        private sealed class ColourSets
        {
            [JsonPropertyName("sets")] public List<CoverColours> Sets { get; set; }
        }

        private sealed class Box
        {
            [JsonPropertyName("x")] public int X { get; set; }
            [JsonPropertyName("y")] public int Y { get; set; }
            [JsonPropertyName("width")] public int Width { get; set; }
            [JsonPropertyName("height")] public int Height { get; set; }
            [JsonPropertyName("align")] public string Align { get; set; }
        }

        private sealed class Template
        {
            [JsonPropertyName("size")] public int[] Size { get; set; }
            [JsonPropertyName("chequer")] public string Chequer { get; set; }
            [JsonPropertyName("title")] public string Title { get; set; }
            [JsonPropertyName("fixed")] public string Fixed { get; set; }
            [JsonPropertyName("rules")] public List<Box> Rules { get; set; }
            [JsonPropertyName("texts")] public Dictionary<string, Box> Texts { get; set; }
        }

        private static readonly object DrawLock = new();

        /// <summary>
        /// The generated cover's file for a race, drawn the first time it is asked for and then
        /// reused; null when the template is not installed or the race has no name.
        /// </summary>
        public static string CoverFor(int year, Race race)
        {
            string folder = AppPaths.RaceCoversFolder;
            if (race == null || string.IsNullOrEmpty(race.RaceName) || !File.Exists(Path.Combine(folder, "template.json")))
                return null;

            var colours = PickColours(folder, year, race.RaceId);
            string output = Path.Combine(AppPaths.GeneratedArtworkFolder, "covers", $"{year}_{race.RaceId}.png");
            string key = string.Join("|", race.RaceName, year, race.RaceDate, colours.Panel, colours.Rule, colours.Title, colours.Chequer,
                Directory.GetFiles(folder).Select(f => File.GetLastWriteTimeUtc(f).Ticks).DefaultIfEmpty().Max());
            lock (DrawLock)
            {
                if (File.Exists(output) && File.Exists(output + ".key") && File.ReadAllText(output + ".key") == key)
                    return output;
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                using var cover = Compose(folder, race.RaceName, year, race.RaceDate, colours);
                cover.SaveAsPng(output);
                File.WriteAllText(output + ".key", key);
            }
            return output;
        }

        /// <summary>
        /// The race's colour set: stepping through the list by race_id, so neighbouring races never
        /// share one, starting from a place set by the year.
        /// </summary>
        public static CoverColours PickColours(string folder, int year, int raceId)
        {
            var sets = JsonSerializer.Deserialize<ColourSets>(File.ReadAllText(Path.Combine(folder, "colours.json"))).Sets;
            int index = (int)(((long)year * 7 + (long)raceId * 5) % sets.Count);
            return sets[(index + sets.Count) % sets.Count];
        }

        public static Image<Rgba32> Compose(string folder, string raceName, int year, string raceDate, CoverColours colours)
        {
            var template = JsonSerializer.Deserialize<Template>(File.ReadAllText(Path.Combine(folder, "template.json")));
            var panel = Rgba32.ParseHex(colours.Panel);
            var cover = new Image<Rgba32>(template.Size[0], template.Size[1], panel);
            var ink = Contrast(panel);

            using var chequer = Tinted(Path.Combine(folder, template.Chequer), Rgba32.ParseHex(colours.Chequer));
            using var title = Tinted(Path.Combine(folder, template.Title), Rgba32.ParseHex(colours.Title));
            using var fixedLayer = Image.Load<Rgba32>(Path.Combine(folder, template.Fixed));
            cover.Mutate(c =>
            {
                c.DrawImage(chequer, 1f);
                c.DrawImage(title, 1f);
                foreach (var rule in template.Rules)
                    c.Fill(Rgba32.ParseHex(colours.Rule), new RectangleF(rule.X, rule.Y, rule.Width, rule.Height));
                c.DrawImage(fixedLayer, 1f);

                DrawText(c, template.Texts["race_name"], (raceName ?? "").ToUpperInvariant(), ink);
                if (DateTime.TryParseExact(raceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    DrawText(c, template.Texts["date"], date.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant(), ink);
                DrawText(c, template.Texts["year"], year.ToString(CultureInfo.InvariantCulture), ink);
            });
            return cover;
        }

        // A white mask (coverage in alpha) in the given colour.
        private static Image<Rgba32> Tinted(string path, Rgba32 colour)
        {
            var image = Image.Load<Rgba32>(path);
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                    foreach (ref var pixel in accessor.GetRowSpan(y))
                        pixel = new Rgba32(colour.R, colour.G, colour.B, pixel.A);
            });
            return image;
        }

        // The text as tall as its box allows (capitals filling the height), narrower if it would
        // overflow the width, aligned left or right in the box.
        private static void DrawText(IImageProcessingContext c, Box box, string text, Color ink)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var family = FontFamilyFor();
            var probe = family.CreateFont(100);
            var bounds = TextMeasurer.MeasureBounds(text, new TextOptions(probe));
            float size = 100 * Math.Min(box.Height / bounds.Height, box.Width / bounds.Width);
            var font = family.CreateFont(size);
            var measured = TextMeasurer.MeasureBounds(text, new TextOptions(font));
            float x = box.Align == "right" ? box.X + box.Width - measured.Right : box.X - measured.Left;
            float y = box.Y + box.Height - measured.Bottom;
            c.DrawText(text, font, ink, new PointF(x, y));
        }

        private static FontFamily? family;

        private static FontFamily FontFamilyFor()
        {
            lock (DrawLock)
            {
                if (family == null)
                {
                    if (File.Exists(AppPaths.AntonFontPath))
                        family = new FontCollection().Add(AppPaths.AntonFontPath);
                    else if (SystemFonts.TryGet("Impact", out var impact))
                        family = impact;
                    else
                        family = SystemFonts.Families.First();
                }
                return family.Value;
            }
        }

        /// <summary>Black or white, whichever contrasts more with the colour.</summary>
        public static Color Contrast(Rgba32 colour) =>
            0.299 * colour.R + 0.587 * colour.G + 0.114 * colour.B > 150 ? Color.Black : Color.White;
    }
}
