using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace AMS2ChEd.SeasonPackEditor.HelmetEditor.Models
{
    // ─── Era ─────────────────────────────────────────────────────────────────

    public enum HelmetEra { Seventies, Eighties, Nineties, Modern }

    // ─── Key Colors ───────────────────────────────────────────────────────────
    // The 6 primary/secondary colors used as zone/slot keys in template textures.

    public enum KeyColor { Red, Green, Blue, Cyan, Magenta, Yellow }

    public static class KeyColorValues
    {
        public static readonly Dictionary<KeyColor, (byte R, byte G, byte B)> Rgb = new()
        {
            { KeyColor.Red,     (255,   0,   0) },
            { KeyColor.Green,   (  0, 255,   0) },
            { KeyColor.Blue,    (  0,   0, 255) },
            { KeyColor.Cyan,    (  0, 255, 255) },
            { KeyColor.Magenta, (255,   0, 255) },
            { KeyColor.Yellow,  (255, 255,   0) },
        };

        /// <summary>
        /// Returns the KeyColor for an RGB pixel within the given per-channel
        /// tolerance, or null if no key matches.
        /// </summary>
        public static KeyColor? Identify(byte r, byte g, byte b, byte tolerance = 12)
        {
            foreach (var (key, (kr, kg, kb)) in Rgb)
            {
                if (Math.Abs(r - kr) <= tolerance &&
                    Math.Abs(g - kg) <= tolerance &&
                    Math.Abs(b - kb) <= tolerance)
                    return key;
            }
            return null;
        }

        public static Color ToWpfColor(KeyColor key)
        {
            var (r, g, b) = Rgb[key];
            return Color.FromRgb(r, g, b);
        }
    }

    // ─── Sticker Slot Definition ──────────────────────────────────────────────
    // One entry per key color in the era manifest.
    // Describes how to crop + transform the sticker image before
    // warping it into the matching colored quad on the sticker template.

    public class StickerSlotDefinition
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public KeyColor Color { get; set; }

        /// <summary>Human-readable name shown in the UI ("Left Panel", "Chin" …).</summary>
        public string DisplayName { get; set; } = string.Empty;

        // ── Source crop from the user's sticker PNG ───────────────────────────
        // In pixels, relative to the sticker image size.
        // Width/Height of 0 means "use the full image dimension".

        public int SourceX { get; set; } = 0;
        public int SourceY { get; set; } = 0;
        public int SourceWidth { get; set; } = 0;   // 0 = full width
        public int SourceHeight { get; set; } = 0;   // 0 = full height

        // ── Transforms applied BEFORE warping ────────────────────────────────

        /// <summary>Clockwise rotation in degrees. Valid values: 0, 90, 180, 270.</summary>
        public int Rotation { get; set; } = 0;
        public bool FlipHorizontal { get; set; } = false;
        public bool FlipVertical { get; set; } = false;

        // ── Reference dimensions ──────────────────────────────────────────────
        // The nominal size the slot was designed for. Used to scale the crop
        // correctly when the user supplies an image of a different size.

        public int ImageBaseWidth { get; set; } = 512;
        public int ImageBaseHeight { get; set; } = 512;
    }

    // ─── Era Asset Manifest ───────────────────────────────────────────────────
    // Everything the compositor needs to locate an era's template files.
    // Lives at  Assets/Helmets/{Era}/manifest.json

    public class HelmetEraManifest
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HelmetEra Era { get; set; }

        /// <summary>
        /// Relative path (from the manifest folder) to the color-zone texture.
        /// Each paintable area is shaded in one of the 6 key colors.
        /// Typically: {Era}_colors.png
        /// </summary>
        public string ColorsTexturePath { get; set; } = string.Empty;

        /// <summary>
        /// Relative path to the sticker template texture.
        /// Each sticker slot is a flat-colored warped quad — one color per slot.
        /// Typically: {Era}_stickers.png
        /// </summary>
        public string StickersTemplatePath { get; set; } = string.Empty;

        /// <summary>
        /// Relative path to the wireframe PNG shown as a UI overlay.
        /// Typically: {Era}_wireframe.png
        /// </summary>
        public string WireframePath { get; set; } = string.Empty;

        /// <summary>All template textures must share these pixel dimensions.</summary>
        public int TextureWidth { get; set; } = 2048;
        public int TextureHeight { get; set; } = 2048;

        /// <summary>
        /// Per-channel tolerance for key color identification.
        /// 0 = exact match. 12 is safe for cleanly-painted flat-color templates.
        /// Raise to ~20 if you have mild JPEG compression artifacts on the template.
        /// </summary>
        public byte KeyColorTolerance { get; set; } = 12;

        /// <summary>
        /// One entry per sticker slot (= per key color used in the sticker template).
        /// Defines the crop/transform applied to the user's sticker before warping.
        /// </summary>
        public List<StickerSlotDefinition> StickerSlots { get; set; } = new();
    }

    // ─── Driver Helmet Definition ─────────────────────────────────────────────
    // Per-driver save data. Saved as {DriverId}.helmet.json in the season pack.

    public class DriverHelmetDefinition
    {
        public string DriverId { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HelmetEra Era { get; set; } = HelmetEra.Nineties;

        /// <summary>
        /// Maps KeyColor.ToString() → hex color string (#AARRGGBB or #RRGGBB).
        /// Only colors present in the era's color texture need entries.
        /// </summary>
        public Dictionary<string, string> ZoneColors { get; set; } = new();

        /// <summary>
        /// Maps KeyColor.ToString() → absolute path to the user's sticker PNG.
        /// Only present when a sticker has been assigned to that slot.
        /// </summary>
        public Dictionary<string, string> StickerPaths { get; set; } = new();
    }

    // ─── Runtime UI State ─────────────────────────────────────────────────────

    public class ColorZoneState
    {
        public KeyColor Key { get; }
        public string DisplayName { get; }
        public Color CurrentColor { get; set; }
        public bool IsSelected { get; set; }

        public ColorZoneState(KeyColor key, string displayName, Color initialColor)
        {
            Key = key;
            DisplayName = displayName;
            CurrentColor = initialColor;
        }
    }

    public class StickerSlotState
    {
        public StickerSlotDefinition Definition { get; }
        public string? AssignedImagePath { get; set; }
        public bool IsSelected { get; set; }

        public StickerSlotState(StickerSlotDefinition def) => Definition = def;
        public bool HasSticker => !string.IsNullOrWhiteSpace(AssignedImagePath);
    }
}

// ─── Compositor Input ─────────────────────────────────────────────────────
// Replaces DriverHelmetDefinition. A simple value object the ViewModel
// builds on the fly from its in-memory dictionaries — nothing is saved.

public class HelmetCompositeInput
{
    public Dictionary<string, string> ZoneColors { get; }
    public Dictionary<string, string> StickerPaths { get; }

    public HelmetCompositeInput(
        Dictionary<string, string> zoneColors,
        Dictionary<string, string> stickerPaths)
    {
        ZoneColors = zoneColors;
        StickerPaths = stickerPaths;
    }
}