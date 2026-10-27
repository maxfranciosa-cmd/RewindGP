using System.Security.Cryptography;
using System.Text;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AMS2ChEd.Business.Artwork
{
    /// <summary>
    /// A driver's portrait when they have no picture: a faceless head and shoulders in a race suit,
    /// drawn from simple shapes in the driver's skin tone, haircut and facial hair (GenericAvatar),
    /// lit from the upper left. Drivers with no GenericAvatar get one picked from their id, so the
    /// same driver always looks the same.
    /// </summary>
    public static class AvatarComposer
    {
        public const int Width = 400, Height = 500;

        public static readonly string[] HairStyles = { "bald", "buzz", "short", "side_part", "centre_part", "longer_top", "medium", "long", "curly", "mullet", "receding" };

        // what an avatar picked from an id chooses from: kept as it was, so those drivers keep their look
        public static readonly string[] PickedHairStyles = { "bald", "buzz", "short", "side_part", "medium", "long", "curly", "mullet", "receding" };
        public static readonly string[] FacialHairStyles = { "none", "stubble", "moustache", "goatee", "full_beard", "sideburns" };

        public static readonly string[] SkinTones = { "#F1D3BC", "#E8BC98", "#D9A47C", "#C68A62", "#A86E4A", "#8A5636", "#6A3E26", "#4A2A1A" };
        public static readonly string[] HairColours = { "#1C1814", "#2E2119", "#3B2A1E", "#5C3E26", "#8A6440", "#C9A866", "#9A4A24", "#8C8C8C" };

        private static readonly object DrawLock = new();

        // The figure is drawn in its own coordinates and framed as a portrait: enlarged about the
        // head and moved down, so the head fills the upper half and the shoulders run off the edges.
        private static readonly System.Numerics.Matrix3x2 Frame =
            System.Numerics.Matrix3x2.CreateScale(1.3f, new System.Numerics.Vector2(200, 200)) * System.Numerics.Matrix3x2.CreateTranslation(0, 25);

        /// <summary>
        /// The avatar file for a driver, drawn the first time and then reused (avatars that look the
        /// same share a file).
        /// </summary>
        public static string AvatarFor(IDriverData driver) =>
            driver == null ? null : AvatarFor(driver.DriverId ?? driver.Name, driver.GenericAvatar);

        /// <summary>As AvatarFor(driver), from a driver id and their GenericAvatar (null: picked from the id).</summary>
        public static string AvatarFor(string driverId, GenericAvatar generic)
        {
            var avatar = generic ?? FromId(driverId ?? "");
            string key = $"{avatar.Skin}|{avatar.Hair}|{avatar.HairColour}|{avatar.FacialHair}|{avatar.FacialHairColour}|v12";
            string name = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();
            string output = System.IO.Path.Combine(AppPaths.GeneratedArtworkFolder, "avatars", name + ".png");
            lock (DrawLock)
            {
                if (!File.Exists(output))
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
                    using var image = Compose(avatar);
                    image.SaveAsPng(output);
                }
            }
            return output;
        }

        /// <summary>An avatar picked from a driver id: the same id always gives the same one.</summary>
        public static GenericAvatar FromId(string id)
        {
            var hash = SHA1.HashData(Encoding.UTF8.GetBytes(id ?? ""));
            int Pick(int index, int count) => hash[index] % count;
            string facial = (hash[4] % 10) switch { < 5 => "none", 5 => "stubble", 6 => "moustache", 7 => "goatee", 8 => "full_beard", _ => "sideburns" };
            return new GenericAvatar
            {
                Skin = SkinTones[Pick(0, SkinTones.Length)],
                Hair = PickedHairStyles[Pick(1, PickedHairStyles.Length)],
                HairColour = HairColours[Pick(2, HairColours.Length - 1)],
                FacialHair = facial
            };
        }

        public static Image<Rgba32> Compose(GenericAvatar avatar)
        {
            var skin = Colour(avatar.Skin, "#D9A47C").Value;
            var hair = Colour(avatar.HairColour, "#3B2A1E").Value;
            var beard = Colour(avatar.FacialHairColour, null) ?? hair;
            string style = (avatar.Hair ?? "short").ToLowerInvariant();
            string facial = (avatar.FacialHair ?? "none").ToLowerInvariant();

            var image = new Image<Rgba32>(Width, Height);
            image.Mutate(c =>
            {
                // transparent around the figure: the window behind shows through

                // hair that falls behind the head
                var back = BackHair(style);
                if (back != null)
                    Shade(c, back, hair, 0.18f);

                // ears, neck, suit
                Shade(c, new EllipsePolygon(127, 215, 28, 48), skin, 0.16f);
                Shade(c, new EllipsePolygon(273, 215, 28, 48), skin, 0.26f);
                var neck = new RectangularPolygon(158, 265, 84, 90);
                Shade(c, neck, skin, 0.22f);
                c.Fill(Color.FromRgba(0, 0, 0, 70), new EllipsePolygon(200, 290, 92, 40).Clip(Outside(neck)).Transform(Frame));
                var suit = Path(p => p.MoveTo(-40, 520).LineTo(-40, 420).CubicTo(10, 365, 70, 347, 160, 335).LineTo(240, 335)
                    .CubicTo(330, 347, 390, 365, 440, 420).LineTo(440, 520));
                Shade(c, suit, Rgba32.ParseHex("#30353D"), 0.22f);
                var collar = Path(p => p.MoveTo(150, 347).CubicTo(155, 325, 245, 325, 250, 347).CubicTo(240, 360, 160, 360, 150, 347));
                c.Fill(Color.ParseHex("#454B55"), collar.Transform(Frame));

                // head
                var head = Head();
                Shade(c, head, skin, 0.24f);

                // haircut, then facial hair, both kept on the head
                var front = FrontHair(style);
                if (front != null)
                {
                    // the haircut's soft shadow on the forehead, so it sits on the head
                    c.Fill(Color.FromRgba(0, 0, 0, 42), front.Translate(0, 5).Clip(Outside(head)).Transform(Frame));
                    // a side parting shows the hair cropped close to the scalp beneath, a little darker
                    if (style == "side_part")
                        Shade(c, FrontHair("buzz"), Mix(hair, Rgba32.ParseHex("#000000"), 0.3f), 0.2f);
                    Shade(c, front, hair, 0.2f);
                }

                foreach (var (shape, alpha) in FacialHair(facial))
                    Shade(c, shape.Clip(Outside(head)), beard, 0.2f, alpha);
            });
            return image;
        }

        // Fills a shape in a colour, darker towards the right and the bottom (light from the upper left).
        private static void Shade(IImageProcessingContext c, IPath shape, Rgba32 colour, float darkness, float alpha = 1f)
        {
            shape = shape.Transform(Frame);
            var bounds = shape.Bounds;
            var baseColour = Color.FromRgba(colour.R, colour.G, colour.B, (byte)(255 * alpha));
            c.Fill(baseColour, shape);
            var shadow = new LinearGradientBrush(new PointF(bounds.Left + bounds.Width * 0.3f, bounds.Top + bounds.Height * 0.2f),
                new PointF(bounds.Right, bounds.Bottom), GradientRepetitionMode.None,
                new ColorStop(0, Color.FromRgba(255, 255, 255, (byte)(28 * alpha))),
                new ColorStop(0.45f, Color.FromRgba(0, 0, 0, 0)),
                new ColorStop(1, Color.FromRgba(0, 0, 0, (byte)(255 * darkness * alpha))));
            c.Fill(shadow, shape);
        }

        private static IPath Head() =>
            Path(p => p.MoveTo(200, 105).CubicTo(256, 105, 277, 150, 276, 200).CubicTo(275, 252, 256, 292, 200, 302)
                .CubicTo(144, 292, 125, 252, 124, 200).CubicTo(123, 150, 144, 105, 200, 105));

        // Everything but the given shape: clipping a shape with it keeps only what lies inside.
        private static IPath Outside(IPath shape) => new RectangularPolygon(-10, -10, Width + 20, Height + 20).Clip(shape);

        // Both shapes as one (Clip subtracts, so: what is outside neither).
        private static IPath Union(IPath a, IPath b) => Outside(Outside(a).Clip(b));

        private static IPath BackHair(string style) => style switch
        {
            "long" => Path(p => p.MoveTo(118, 175).CubicTo(104, 260, 110, 335, 140, 352).LineTo(260, 352).CubicTo(290, 335, 296, 260, 282, 175)),
            "mullet" => Path(p => p.MoveTo(138, 225).CubicTo(132, 300, 148, 352, 168, 365).LineTo(232, 365).CubicTo(252, 352, 268, 300, 262, 225)),
            "curly" => new EllipsePolygon(200, 185, 210, 205),
            "medium" => Path(p => p.MoveTo(120, 190).CubicTo(115, 240, 125, 270, 140, 280).LineTo(260, 280).CubicTo(275, 270, 285, 240, 280, 190)),
            _ => null
        };

        private static IPath FrontHair(string style)
        {
            // the outer edge: a little above and around the skull
            IPath Cap(float top, float sideBottom) =>
                Path(p => p.MoveTo(124, sideBottom).CubicTo(118, 140, 148, top, 200, top).CubicTo(252, top, 282, 140, 276, sideBottom)
                    .CubicTo(270, 175, 266, 158, 252, 150).CubicTo(228, 138, 172, 138, 148, 150).CubicTo(134, 158, 130, 175, 124, sideBottom));
            switch (style)
            {
                case "buzz":
                    return Path(p => p.MoveTo(126, 190).CubicTo(124, 140, 150, 104, 200, 104).CubicTo(250, 104, 276, 140, 274, 190)
                        .CubicTo(268, 168, 262, 156, 250, 150).CubicTo(226, 140, 174, 140, 150, 150).CubicTo(138, 156, 132, 168, 126, 190));
                case "short":
                    return Cap(94, 200);
                case "side_part":
                    // a sweep from a parting high on the left of the picture over the top and down to
                    // the right side, its edge running diagonally, the forehead open on the left;
                    // a small lock falls on the other side of the parting
                    return Union(
                        Path(p => p.MoveTo(158, 116).CubicTo(166, 90, 230, 78, 266, 106).CubicTo(281, 124, 282, 152, 279, 174)
                            .CubicTo(236, 172, 182, 150, 160, 118)),
                        Path(p => p.MoveTo(156, 116).CubicTo(137, 112, 122, 130, 121, 160).CubicTo(120, 170, 121, 178, 123, 184)
                            .CubicTo(128, 162, 140, 134, 156, 116)));
                case "centre_part":
                    // a band over the top and down the sides to the ears, the hairline two soft arches
                    // dipping under the parting, which shows as a notch in the top
                    return Path(p => p.MoveTo(117, 208).LineTo(117, 152).CubicTo(117, 112, 140, 92, 170, 92).CubicTo(184, 92, 192, 93, 197, 92)
                        .LineTo(200, 103).LineTo(203, 92).CubicTo(208, 93, 216, 92, 230, 92).CubicTo(260, 92, 283, 112, 283, 152)
                        .LineTo(283, 208).CubicTo(279, 213, 274, 213, 269, 208).LineTo(269, 162).CubicTo(268, 141, 258, 129, 240, 129)
                        .CubicTo(222, 129, 208, 133, 200, 141).CubicTo(192, 133, 178, 129, 160, 129).CubicTo(142, 129, 132, 141, 131, 162)
                        .LineTo(131, 208).CubicTo(126, 213, 121, 213, 117, 208));
                case "longer_top":
                    // short at the sides, more volume on top
                    return Path(p => p.MoveTo(126, 186).CubicTo(120, 146, 121, 104, 152, 88).CubicTo(174, 73, 226, 73, 248, 88)
                        .CubicTo(279, 104, 280, 146, 274, 186).CubicTo(268, 166, 263, 152, 250, 146).CubicTo(230, 139, 212, 142, 200, 138)
                        .CubicTo(188, 142, 170, 139, 150, 146).CubicTo(137, 152, 132, 166, 126, 186));
                case "receding":
                    return Path(p => p.MoveTo(124, 205).CubicTo(118, 140, 148, 94, 200, 94).CubicTo(252, 94, 282, 140, 276, 205)
                        .CubicTo(270, 160, 266, 128, 256, 124).CubicTo(238, 120, 222, 128, 200, 142).CubicTo(178, 128, 162, 120, 144, 124)
                        .CubicTo(134, 128, 130, 160, 124, 205));
                case "medium":
                case "long":
                    return Path(p => p.MoveTo(116, 250).CubicTo(102, 150, 140, 88, 200, 88).CubicTo(260, 88, 298, 150, 284, 250)
                        .LineTo(272, 250).CubicTo(270, 205, 266, 172, 252, 160).CubicTo(236, 150, 214, 160, 200, 152)
                        .CubicTo(184, 160, 164, 150, 148, 160).CubicTo(134, 172, 130, 205, 128, 250));
                case "curly":
                {
                    IPath curls = Cap(96, 205);
                    for (int i = 0; i <= 10; i++)
                    {
                        double a = Math.PI * (1.05 + i * 0.09);
                        curls = Union(curls, new EllipsePolygon((float)(200 + 84 * Math.Cos(a)), (float)(170 + 72 * Math.Sin(a)), 46, 46));
                    }
                    return curls;
                }
                case "mullet":
                    return Cap(96, 205);
                default:
                    return null;
            }
        }

        private static IEnumerable<(IPath shape, float alpha)> FacialHair(string style)
        {
            var moustache = Path(p => p.MoveTo(168, 246).CubicTo(182, 234, 218, 234, 232, 246).CubicTo(222, 256, 178, 256, 168, 246));
            var beard = Path(p => p.MoveTo(124, 205).CubicTo(126, 262, 150, 304, 200, 314).CubicTo(250, 304, 274, 262, 276, 205)
                .CubicTo(266, 236, 252, 254, 236, 258).CubicTo(220, 270, 180, 270, 164, 258).CubicTo(148, 254, 134, 236, 124, 205));
            switch (style)
            {
                case "stubble":
                    yield return (beard, 0.24f);
                    break;
                case "moustache":
                    yield return (moustache, 1f);
                    break;
                case "goatee":
                    yield return (moustache, 1f);
                    yield return (Path(p => p.MoveTo(178, 270).CubicTo(186, 262, 214, 262, 222, 270).CubicTo(224, 292, 212, 306, 200, 308)
                        .CubicTo(188, 306, 176, 292, 178, 270)), 1f);
                    break;
                case "full_beard":
                    yield return (beard, 1f);
                    yield return (moustache, 1f);
                    break;
                case "sideburns":
                    yield return (new RectangularPolygon(124, 190, 16, 62), 1f);
                    yield return (new RectangularPolygon(260, 190, 16, 62), 1f);
                    break;
            }
        }

        private static IPath Path(Action<PathBuilderFluent> build)
        {
            var fluent = new PathBuilderFluent();
            build(fluent);
            return fluent.Build();
        }

        // a colour part of the way towards another
        private static Rgba32 Mix(Rgba32 a, Rgba32 b, float t) =>
            new((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

        private static Rgba32? Colour(string hex, string fallback)
        {
            if (!string.IsNullOrEmpty(hex) && Rgba32.TryParseHex(hex, out var parsed))
                return parsed;
            return fallback == null ? null : Rgba32.ParseHex(fallback);
        }

        // A small fluent wrapper over PathBuilder: MoveTo starts a figure, Build closes it.
        private sealed class PathBuilderFluent
        {
            private readonly PathBuilder builder = new();
            private PointF current;

            public PathBuilderFluent MoveTo(float x, float y) { builder.StartFigure(); current = new PointF(x, y); return this; }
            public PathBuilderFluent LineTo(float x, float y) { var next = new PointF(x, y); builder.AddLine(current, next); current = next; return this; }
            public PathBuilderFluent CubicTo(float x1, float y1, float x2, float y2, float x, float y)
            {
                var next = new PointF(x, y);
                builder.AddCubicBezier(current, new PointF(x1, y1), new PointF(x2, y2), next);
                current = next;
                return this;
            }
            public IPath Build() { builder.CloseFigure(); return builder.Build(); }
        }
    }
}
