using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models.Concrete;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Point = SixLabors.ImageSharp.Point;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;
using Rectangle = SixLabors.ImageSharp.Rectangle;
using Image = SixLabors.ImageSharp.Image;

public class DdsTextureComposer
{
    // BcEncoder defaults Options.TaskCount to Environment.ProcessorCount and parallelizes block
    // compression across all of them for every single EncodeToDds call. GenerateRaceFiles calls
    // into this class once per texture per driver (livery/helmet/visor) in a sequential loop with
    // no outer-level parallelism of its own, so at the default setting each of those calls briefly
    // pins every logical core to 100% before the next one starts - a train of full-system CPU spikes
    // for the whole grid rather than one contained, sustained load. Capping it to half the machine's
    // cores (min 1) keeps each encode reasonably fast while leaving headroom for the rest of the
    // system instead of repeatedly claiming every core.
    private static readonly int EncoderTaskCount = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>A .dds or ordinary image file, decoded.</summary>
    public static Image<Rgba32> LoadImage(string texturePath) => LoadTexture(texturePath);

    private static Image<Rgba32> LoadTexture(string texturePath)
    {
        var isDds = Path.GetExtension(texturePath) == ".dds";
        using (var baseStream = File.OpenRead(texturePath))
        {
            if (isDds)
            {
                var decoder = new BcDecoder();
                return decoder.DecodeToImageRgba32(baseStream);
            }
            else
            {
                return Image.Load<Rgba32>(texturePath);
            }
        }
    }

    // a texture (DDS or any image format) re-encoded as PNG, for showing it in the app's UI:
    // scaled down to maxWidth first, as encoding a full-size texture costs far more than resizing it
    public static byte[] LoadAsPng(string texturePath, int maxWidth)
    {
        using var image = LoadTexture(texturePath);
        if (image.Width > maxWidth)
            image.Mutate(x => x.Resize(maxWidth, 0));

        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    public static void Compose(string basePath, string sponsorPath, string outputPath)
    {

        Image<Rgba32> baseImage = LoadTexture(basePath);

        if (!string.IsNullOrEmpty(sponsorPath))
        {
            // Load the sponsor overlay DDS
            Image<Rgba32> sponsorImage = LoadTexture(sponsorPath);

            // A base painted at another resolution of the same layout (e.g. a 4096x4096 helmet under a
            // 2048x2048 sponsor layer) gets the overlay stretched to cover it; a different aspect ratio
            // means a different layout, which scaling would not fix.
            bool sameSize = sponsorImage.Width == baseImage.Width && sponsorImage.Height == baseImage.Height;
            bool sameAspectRatio = (long)sponsorImage.Width * baseImage.Height == (long)sponsorImage.Height * baseImage.Width;
            if (!sameSize && sameAspectRatio)
            {
                sponsorImage.Mutate(ctx => ctx.Resize(baseImage.Width, baseImage.Height));
            }

            // Apply alpha blending
            baseImage.Mutate(ctx => ctx.DrawImage(sponsorImage, 1f));

            // Dispose sponsor image immediately after blending
            sponsorImage.Dispose();

        }

        // Encode to DDS with mipmaps
        var encoder = new BcEncoder(BCnEncoder.Shared.CompressionFormat.Bc3)
        {
            OutputOptions = {
                GenerateMipMaps = true,
                Quality = CompressionQuality.Balanced,
                DdsPreferDxt10Header = true,
                MaxMipMapLevel = 0
            },
            Options = {
                TaskCount = EncoderTaskCount
            }
        };

        var ddsFile = encoder.EncodeToDds(baseImage);

        // Dispose base image after encoding
        baseImage.Dispose();

        using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            ddsFile.Write(outputStream);
        }
    }

    /// <summary>
    /// Parses a fill color hex string, supporting both "#RRGGBB" (opaque, via
    /// ColorTranslator) and "#AARRGGBB" (with alpha) since ColorTranslator.FromHtml
    /// only understands the 3/6-digit forms.
    /// </summary>
    private static System.Drawing.Color ParseFillColor(string colorText)
    {
        string trimmed = colorText.Trim();
        string hexPart = trimmed.StartsWith("#") ? trimmed.Substring(1) : trimmed;

        if (hexPart.Length == 8)
        {
            byte a = Convert.ToByte(hexPart.Substring(0, 2), 16);
            byte r = Convert.ToByte(hexPart.Substring(2, 2), 16);
            byte g = Convert.ToByte(hexPart.Substring(4, 2), 16);
            byte b = Convert.ToByte(hexPart.Substring(6, 2), 16);
            return System.Drawing.Color.FromArgb(a, r, g, b);
        }

        return System.Drawing.ColorTranslator.FromHtml(trimmed);
    }

    /// <summary>
    /// Apply car numbers to a livery DDS texture at multiple placements
    /// </summary>
    /// <param name="baseLiveryPath">Path to the base livery DDS file</param>
    /// <param name="placements">List of number placements to apply</param>
    /// <param name="carNumber">The car number to apply (e.g., 5)</param>
    /// <param name="seasonDirectory">Season directory to resolve numbers texture paths</param>
    /// <param name="outputPath">Path to save the resulting DDS file</param>
    public static void ApplyCarNumbers(
        string baseLiveryPath,
        List<NumberPlacementData> placements,
        int carNumber,
        string seasonDirectory,
        string outputPath)
    {

        // Load base livery
        ApplyCarNumbers(LoadTexture(baseLiveryPath), placements, carNumber, seasonDirectory, outputPath);
    }

    /// <summary>
    /// As above, for a livery already in memory (a generic livery just recoloured from its kit
    /// template). Takes ownership of <paramref name="liveryImage"/> and disposes it.
    /// </summary>
    public static void ApplyCarNumbers(
        Image<Rgba32> liveryImage,
        List<NumberPlacementData> placements,
        int carNumber,
        string seasonDirectory,
        string outputPath)
    {
        // A livery's alpha channel is material data for AMS2 (how the paint shines), not transparency.
        // Blending onto it as-is turns every pixel whose mask is 0 black wherever a digit cell's
        // transparent margin is drawn, so paint on an opaque copy and put the mask back afterwards.
        byte[] materialMask = ExtractAlphaAndMakeOpaque(liveryImage);

        // Get digits of the car number once
        string numberString = carNumber.ToString();

        // Apply each placement
        foreach (var placement in placements)
        {
            // Resolve numbers texture path (already includes car_liveries/car_numbers from JSON)
            string numbersTexturePath = Path.Combine(seasonDirectory, placement.NumbersTexture);

            if (!File.Exists(numbersTexturePath))
            {
                Console.WriteLine($"Warning: Numbers texture not found: {numbersTexturePath}");
                continue;
            }

            Image<Rgba32> numbersImage = LoadTexture(numbersTexturePath);

            // Calculate digit dimensions
            int digitWidth = numbersImage.Width / 10;
            int digitHeight = numbersImage.Height;

            // A plate at any angle other than a quarter turn has its own path. Quarter turns keep
            // the original one below untouched, so every existing plate renders exactly as before.
            if (!IsQuarterTurn(placement.Rotation, out int rotation))
            {
                ApplyAngledPlate(liveryImage, numbersImage, numberString, digitWidth, digitHeight, placement);
                numbersImage.Dispose();
                continue;
            }

            // Fill the number plate area with a solid color before drawing the digits, if configured
            if (!string.IsNullOrWhiteSpace(placement.FillColor))
            {
                try
                {
                    var parsedColour = ParseFillColor(placement.FillColor);
                    var rgba = new Rgba32(parsedColour.R, parsedColour.G, parsedColour.B, parsedColour.A);
                    var fillRect = CalculatePlateFillRectangle(
                        placement.StartX, placement.StartY, placement.PlateWidth, digitHeight, rotation);
                    liveryImage.Mutate(ctx => ctx.Fill(rgba, fillRect));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Invalid FillColor '{placement.FillColor}' for placement, skipping fill: {ex.Message}");
                }
            }

            // Apply the number to this placement
            if (numberString.Length == 1)
            {
                ApplySingleDigitOptimized(liveryImage, numbersImage, numberString[0], digitWidth, digitHeight,
                    placement.StartX, placement.StartY, placement.PlateWidth, rotation);
            }
            else if (numberString.Length == 2)
            {
                ApplyTwoDigitsOptimized(liveryImage, numbersImage, numberString[0], numberString[1],
                    digitWidth, digitHeight, placement.StartX, placement.StartY, placement.PlateWidth, rotation);
            }

            // Dispose numbers image immediately after use
            numbersImage.Dispose();
        }

        RestoreAlpha(liveryImage, materialMask);

        // Encode to DDS with mipmaps
        var encoder = new BcEncoder(BCnEncoder.Shared.CompressionFormat.Bc3)
        {
            OutputOptions = {
                GenerateMipMaps = true,
                Quality = CompressionQuality.Balanced,
                DdsPreferDxt10Header = true,
                MaxMipMapLevel = 0
            },
            Options = {
                TaskCount = EncoderTaskCount
            }
        };

        var ddsFile = encoder.EncodeToDds(liveryImage);

        // Dispose livery image immediately after encoding
        liveryImage.Dispose();

        using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            ddsFile.Write(outputStream);
        }
    }

    private static byte[] ExtractAlphaAndMakeOpaque(Image<Rgba32> image)
    {
        var alpha = new byte[image.Width * image.Height];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    alpha[y * accessor.Width + x] = row[x].A;
                    row[x].A = 255;
                }
            }
        });
        return alpha;
    }

    private static void RestoreAlpha(Image<Rgba32> image, byte[] alpha)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                    row[x].A = alpha[y * accessor.Width + x];
            }
        });
    }

    private static void ApplySingleDigitOptimized(
        Image<Rgba32> liveryImage,
        Image<Rgba32> numbersImage,
        char digit,
        int digitWidth,
        int digitHeight,
        int startX,
        int startY,
        int plateWidth,
        int rotation)
    {
        int digitValue = digit - '0';

        // Extract and process digit
        using var digitImage = numbersImage.Clone(x => x.Crop(new Rectangle(digitValue * digitWidth, 0, digitWidth, digitHeight)));

        // Apply rotation if needed
        if (rotation != 0)
        {
            digitImage.Mutate(x => x.Rotate(rotation));
        }

        // Calculate position
        Point position = CalculateDigitPosition(startX, startY, plateWidth, 0.5f, rotation, digitWidth, digitHeight);

        // Draw directly onto livery
        liveryImage.Mutate(ctx => ctx.DrawImage(digitImage, position, 1f));

        // digitImage disposed here
    }

    private static void ApplyTwoDigitsOptimized(
        Image<Rgba32> liveryImage,
        Image<Rgba32> numbersImage,
        char digit1,
        char digit2,
        int digitWidth,
        int digitHeight,
        int startX,
        int startY,
        int plateWidth,
        int rotation)
    {
        int digit1Value = digit1 - '0';
        int digit2Value = digit2 - '0';

        // Process first digit
        using (var digit1Image = numbersImage.Clone(x => x.Crop(new Rectangle(digit1Value * digitWidth, 0, digitWidth, digitHeight))))
        {
            if (rotation != 0)
            {
                digit1Image.Mutate(x => x.Rotate(rotation));
            }

            Point position1 = CalculateDigitPosition(startX, startY, plateWidth, 0.25f, rotation, digitWidth, digitHeight);
            liveryImage.Mutate(ctx => ctx.DrawImage(digit1Image, position1, 1f));
        } // digit1Image disposed here

        // Process second digit
        using (var digit2Image = numbersImage.Clone(x => x.Crop(new Rectangle(digit2Value * digitWidth, 0, digitWidth, digitHeight))))
        {
            if (rotation != 0)
            {
                digit2Image.Mutate(x => x.Rotate(rotation));
            }

            Point position2 = CalculateDigitPosition(startX, startY, plateWidth, 0.75f, rotation, digitWidth, digitHeight);
            liveryImage.Mutate(ctx => ctx.DrawImage(digit2Image, position2, 1f));
        } // digit2Image disposed here
    }

    /// <summary>
    /// Data class for number placement information
    /// </summary>
    public class NumberPlacementData
    {
        public string NumbersTexture { get; set; }
        public int PlateWidth { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }

        /// <summary>
        /// Degrees clockwise. The plate is PlateWidth long and as tall as the numbers sheet, and
        /// turns around (StartX, StartY), its own top-left corner (above the first digit, with the
        /// number read upright) - for 90 that is its top-right on the texture, for 180 its
        /// bottom-right, for 270 its bottom-left.
        /// </summary>
        public double Rotation { get; set; }
        public string FillColor { get; set; }
    }

    private static bool IsQuarterTurn(double degrees, out int quarterTurn)
    {
        double normalised = ((degrees % 360) + 360) % 360;
        double nearest = Math.Round(normalised / 90) * 90;
        quarterTurn = (int)nearest % 360;
        return Math.Abs(normalised - nearest) < 1e-6;
    }

    /// <summary>
    /// A plate at an arbitrary angle: the same geometry as the quarter turns, written as vectors.
    /// The plate spans start + a*u + b*v with u the reading direction and v "down" for the digits.
    /// Digit centres sit where the quarter-turn path puts them (half way, or at a quarter and three
    /// quarters along the plate), half a sheet height below the plate's top edge.
    /// </summary>
    private static void ApplyAngledPlate(
        Image<Rgba32> liveryImage,
        Image<Rgba32> numbersImage,
        string numberString,
        int digitWidth,
        int digitHeight,
        NumberPlacementData placement)
    {
        double radians = placement.Rotation * Math.PI / 180.0;
        float ux = (float)Math.Cos(radians), uy = (float)Math.Sin(radians);
        float vx = -uy, vy = ux;
        float sx = placement.StartX, sy = placement.StartY;
        float plate = placement.PlateWidth, height = digitHeight;

        if (!string.IsNullOrWhiteSpace(placement.FillColor))
        {
            try
            {
                var parsedColour = ParseFillColor(placement.FillColor);
                var rgba = new Rgba32(parsedColour.R, parsedColour.G, parsedColour.B, parsedColour.A);
                var corners = new[]
                {
                    new PointF(sx, sy),
                    new PointF(sx + plate * ux, sy + plate * uy),
                    new PointF(sx + plate * ux + height * vx, sy + plate * uy + height * vy),
                    new PointF(sx + height * vx, sy + height * vy)
                };
                liveryImage.Mutate(ctx => ctx.FillPolygon(rgba, corners));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Invalid FillColor '{placement.FillColor}' for placement, skipping fill: {ex.Message}");
            }
        }

        float[] along = numberString.Length == 1 ? new[] { 0.5f } : new[] { 0.25f, 0.75f };
        for (int i = 0; i < along.Length && i < numberString.Length; i++)
        {
            int digitValue = numberString[i] - '0';
            using var digitImage = numbersImage.Clone(x => x.Crop(new Rectangle(digitValue * digitWidth, 0, digitWidth, digitHeight)));
            // Rotate grows the canvas around the image centre, so the centre is what gets placed.
            digitImage.Mutate(x => x.Rotate((float)placement.Rotation));

            float centreX = sx + plate * along[i] * ux + height / 2f * vx;
            float centreY = sy + plate * along[i] * uy + height / 2f * vy;
            var position = new Point((int)Math.Round(centreX - digitImage.Width / 2f), (int)Math.Round(centreY - digitImage.Height / 2f));
            liveryImage.Mutate(ctx => ctx.DrawImage(digitImage, position, 1f));
        }
    }

    private static Point CalculateDigitPosition(
        int startX,
        int startY,
        int plateWidth,
        float relativePosition, // 0.5 for center, 0.25 and 0.75 for two digits
        int rotation,
        int digitWidth,
        int digitHeight)
    {
        int offset = (int)(plateWidth * relativePosition);

        // Calculate position based on rotation
        // Note: startX, startY is the top-left corner of the number plate
        // ImageSharp.Rotate() rotates around the center of the image, so positioning is straightforward
        switch (rotation)
        {
            case 0: // Deg0 - horizontal, left to right
                return new Point(startX + offset - digitWidth / 2, startY);

            case 90: // Deg90 - vertical, rotated 90° clockwise
                     // After 90° rotation, the image's width becomes height and vice versa
                return new Point(startX - digitHeight, startY + offset - digitHeight / 2);

            case 180: // Deg180 - horizontal, upside down - offset and height go in reverse direction
                return new Point(startX - offset - digitWidth / 2, startY - digitHeight);

            case 270: // Deg270 - vertical, rotated 90° counter-clockwise
                return new Point(startX, startY - offset - digitWidth / 2);

            default:
                return new Point(startX + offset - digitWidth / 2, startY);
        }
    }

    private static RectangleF CalculatePlateFillRectangle(
        int startX, int startY, int plateWidth, int digitHeight, int rotation)
    {
        switch (rotation)
        {
            case 90:
                return new RectangleF(startX - digitHeight, startY, digitHeight, plateWidth);
            case 180:
                return new RectangleF(startX - plateWidth, startY - digitHeight, plateWidth, digitHeight);
            case 270:
                return new RectangleF(startX, startY - plateWidth, digitHeight, plateWidth);
            default: // 0
                return new RectangleF(startX, startY, plateWidth, digitHeight);
        }
    }

    public static void GenerateSolidColour(
    string colour,
    int width,
    int height,
    string outputPath)
    {
        var parsedColour = System.Drawing.ColorTranslator.FromHtml(colour);
        using var image = new Image<Rgba32>(width, height);
        var rgba = new Rgba32(parsedColour.R, parsedColour.G, parsedColour.B, parsedColour.A);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                row.Fill(rgba);
            }
        });

        SaveAsDds(image, outputPath);
    }

    /// <summary>BC3 with mipmaps, as every texture written for AMS2.</summary>
    public static void SaveAsDds(Image<Rgba32> image, string outputPath)
    {
        var encoder = new BcEncoder(BCnEncoder.Shared.CompressionFormat.Bc3)
        {
            OutputOptions = {
            GenerateMipMaps = true,
            Quality = CompressionQuality.Balanced,
            DdsPreferDxt10Header = true,
            MaxMipMapLevel = 0
        },
            Options = {
                TaskCount = EncoderTaskCount
            }
        };

        var ddsFile = encoder.EncodeToDds(image);

        using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        ddsFile.Write(outputStream);
    }
}