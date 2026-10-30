using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// A team's sponsor layer from a texture a skin pack painted (a driver's helmet, or a visor),
    /// seen through a map: a PNG of the texture's layout whose alpha says where the sponsors are.
    /// The map is a shape, nothing more; what shows through comes from the player's download.
    /// Visor maps (Drivers/visor_masks/&lt;helmet model&gt;.png) mark the sponsor strip; helmet maps
    /// are drawn per team (shipped with the season's cosmetics), else the helmet model's generic
    /// one is used (Drivers/helmet_sponsor_masks/&lt;helmet model&gt;.png: the Nineties helmet's marks
    /// the visor strip painted on its shell, the others are empty for now).
    /// </summary>
    public static class SponsorLayerCutter
    {
        public static string VisorMaskPath(string helmetModel, string masksFolder = null) =>
            Path.Combine(masksFolder ?? StoragePaths.VisorMasksFolder, $"{helmetModel}.png");

        public static string HelmetMaskPath(string helmetModel, string masksFolder = null) =>
            Path.Combine(masksFolder ?? StoragePaths.HelmetSponsorMasksFolder, $"{helmetModel}.png");

        /// <summary>The layer, or null with the reason when the map is missing or empty.</summary>
        public static Image<Rgba32> Cut(string texturePath, string maskPath, out string problem)
        {
            if (!File.Exists(maskPath))
            {
                problem = $"no sponsor map ({Path.GetFileName(maskPath)})";
                return null;
            }

            using var mask = SixLabors.ImageSharp.Image.Load<Rgba32>(maskPath);
            if (IsEmpty(mask))
            {
                problem = "the sponsor map is empty";
                return null;
            }

            using var texture = DdsTextureComposer.LoadImage(texturePath);
            problem = null;
            return Cut(texture, mask);
        }

        public static Image<Rgba32> Cut(Image<Rgba32> texture, Image<Rgba32> mask)
        {
            using var sized = texture.Width == mask.Width && texture.Height == mask.Height
                ? texture.Clone()
                : texture.Clone(x => x.Resize(mask.Width, mask.Height));

            var layer = new Image<Rgba32>(mask.Width, mask.Height);
            layer.ProcessPixelRows(sized, mask, (outRows, textureRows, maskRows) =>
            {
                for (int y = 0; y < outRows.Height; y++)
                {
                    var o = outRows.GetRowSpan(y);
                    var t = textureRows.GetRowSpan(y);
                    var m = maskRows.GetRowSpan(y);
                    for (int x = 0; x < o.Length; x++)
                        o[x] = m[x].A == 0 ? new Rgba32(0, 0, 0, 0) : new Rgba32(t[x].R, t[x].G, t[x].B, (byte)(t[x].A * m[x].A / 255));
                }
            });
            return layer;
        }

        /// <summary>
        /// A shipped sample layer with a cut laid over it (scaled to the sample), as a new layer: on
        /// the 90s helmet, the visor strip the skin pack painted on the shell. No cut: the sample.
        /// </summary>
        public static Image<Rgba32> OverSample(string samplePath, Image<Rgba32> cut)
        {
            var layer = DdsTextureComposer.LoadImage(samplePath);
            if (cut == null)
                return layer;

            using var sized = cut.Clone(x => x.Resize(layer.Width, layer.Height));
            layer.Mutate(x => x.DrawImage(sized, 1f));
            return layer;
        }

        private static bool IsEmpty(Image<Rgba32> mask)
        {
            bool empty = true;
            mask.ProcessPixelRows(rows =>
            {
                for (int y = 0; y < rows.Height && empty; y++)
                    foreach (var p in rows.GetRowSpan(y))
                        if (p.A != 0) { empty = false; break; }
            });
            return empty;
        }
    }
}
