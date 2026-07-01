using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models.Concrete;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public class DdsTextureComposer
{
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

    public static void Compose(string basePath, string sponsorPath, string outputPath)
    {

        Image<Rgba32> baseImage = LoadTexture(basePath);

        if (!string.IsNullOrEmpty(sponsorPath))
        {
            // Load the sponsor overlay DDS
            Image<Rgba32> sponsorImage = LoadTexture(sponsorPath);

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
            }
        };

        var ddsFile = encoder.EncodeToDds(baseImage);

        // Dispose base image after encoding
        baseImage.Dispose();

        // Force GC to release memory
        GC.Collect();
        GC.WaitForPendingFinalizers();

        using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            ddsFile.Write(outputStream);
        }
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
        Image<Rgba32> liveryImage = LoadTexture(baseLiveryPath);

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

            // Fill the number plate area with a solid color before drawing the digits, if configured
            if (!string.IsNullOrWhiteSpace(placement.FillColor))
            {
                try
                {
                    var parsedColour = System.Drawing.ColorTranslator.FromHtml(placement.FillColor);
                    var rgba = new Rgba32(parsedColour.R, parsedColour.G, parsedColour.B, parsedColour.A);
                    var fillRect = CalculatePlateFillRectangle(
                        placement.StartX, placement.StartY, placement.PlateWidth, digitHeight, placement.Rotation);
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
                    placement.StartX, placement.StartY, placement.PlateWidth, placement.Rotation);
            }
            else if (numberString.Length == 2)
            {
                ApplyTwoDigitsOptimized(liveryImage, numbersImage, numberString[0], numberString[1],
                    digitWidth, digitHeight, placement.StartX, placement.StartY, placement.PlateWidth, placement.Rotation);
            }

            // Dispose numbers image immediately after use
            numbersImage.Dispose();
        }

        // Encode to DDS with mipmaps
        var encoder = new BcEncoder(BCnEncoder.Shared.CompressionFormat.Bc3)
        {
            OutputOptions = {
                GenerateMipMaps = true,
                Quality = CompressionQuality.Balanced,
                DdsPreferDxt10Header = true,
                MaxMipMapLevel = 0
            }
        };

        var ddsFile = encoder.EncodeToDds(liveryImage);

        // Dispose livery image immediately after encoding
        liveryImage.Dispose();

        // Force GC to release large image buffers
        GC.Collect();
        GC.WaitForPendingFinalizers();

        using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            ddsFile.Write(outputStream);
        }
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
        public int Rotation { get; set; }
        public string FillColor { get; set; }
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

        var encoder = new BcEncoder(BCnEncoder.Shared.CompressionFormat.Bc3)
        {
            OutputOptions = {
            GenerateMipMaps = true,
            Quality = CompressionQuality.Balanced,
            DdsPreferDxt10Header = true,
            MaxMipMapLevel = 0
        }
        };

        var ddsFile = encoder.EncodeToDds(image);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        ddsFile.Write(outputStream);
    }
}