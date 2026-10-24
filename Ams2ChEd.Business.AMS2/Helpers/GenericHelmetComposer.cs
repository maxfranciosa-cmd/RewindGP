using System.Text.Json;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// A driver's generic_helmet: one of the generic designs, colour-coded (red its largest
    /// colour, then green, blue, yellow), recoloured with the driver's palette for the helmet
    /// model their car's drivers wear, with that model's template overlay (visor fittings,
    /// vents, liner) laid back on top. A key the driver's palette leaves out keeps the design's
    /// own colour. See baked_season_data.md, "Helmets".
    /// </summary>
    public static class GenericHelmetComposer
    {
        public static string DesignFile(string folder, string design, HelmetEra era) => Path.Combine(folder, Prefix(era) + design + ".png");

        private static string Prefix(HelmetEra era) => era switch
        {
            HelmetEra.Nineties => "90s_",
            HelmetEra.Eighties => "80s_",
            HelmetEra.Seventies => "70s_",
            _ => ""
        };

        /// <summary>The design's own colours (&lt;design&gt;.json), the driver's palette on top.</summary>
        public static Dictionary<string, string> Palette(string folder, GenericHelmet helmet)
        {
            var palette = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string defaults = Path.Combine(folder, helmet.Design + ".json");
            if (File.Exists(defaults))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(defaults));
                if (document.RootElement.TryGetProperty("palette", out var own))
                    foreach (var key in own.EnumerateObject())
                        palette[key.Name] = key.Value.GetString();
            }
            foreach (var key in helmet.Palette ?? new Dictionary<string, string>())
                palette[key.Key] = key.Value;
            return palette;
        }

        /// <summary>The driver's helmet texture for that era; null when the design is missing.</summary>
        public static Image<Rgba32> Build(GenericHelmet helmet, HelmetEra era, string folder = null)
        {
            folder ??= StoragePaths.GenericHelmetDesignsFolder;
            if (string.IsNullOrEmpty(helmet?.Design))
                return null;
            string designFile = DesignFile(folder, helmet.Design, era);
            if (!File.Exists(designFile))
                return null;

            var image = Image.Load<Rgba32>(designFile);
            TemplateRecolour.Apply(image, TemplateRecolour.ParsePalette(Palette(folder, helmet)));
            string overlayFile = OverlayFile(folder, era);
            if (File.Exists(overlayFile))
            {
                using var overlay = Image.Load<Rgba32>(overlayFile);
                if (overlay.Width == image.Width && overlay.Height == image.Height)
                    image.Mutate(c => c.DrawImage(overlay, 1f));
            }
            return image;
        }

        public static string OverlayFile(string folder, HelmetEra era) => Path.Combine(folder, "overlays", era + ".png");
    }
}
