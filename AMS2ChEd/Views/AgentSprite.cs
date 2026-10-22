using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// The player's agent as a pixel-art sprite, for the retro style of AgentTalkWindow: one frame per
    /// eye position and mouth state, swapped whole rather than animated, like a DOS adventure game.
    /// The frames are drawn at one pixel per sprite pixel; the window scales them up without smoothing.
    /// </summary>
    public static class AgentSprite
    {
        public enum Eyes
        {
            Left,
            Straight,
            Right
        }

        public const int Width = 32;
        public const int Height = 34;

        // the left half of the agent, mirrored into the right; eyes and mouth are drawn on top per frame
        private static readonly string[] LeftHalf =
        {
            "................",
            "..........KKKKKK",
            "........KKHHHHHH",
            ".......KHHHHHHHH",
            "......KHHHHHHHHH",
            "......KHHHHHHHHH",
            ".....KHHHHSSSSSS",
            ".....KHHSSSSSSSS",
            ".....KHSSSSSSSSS",
            ".....KSSKKKKSSSS",
            "....KSSSSSSSSSSS",
            "....KsSSWWWWSSSS",
            "....KsSSWWWWSSSS",
            ".....KSSSSSSSSSs",
            ".....KSSSSSSSSss",
            ".....KSSSSSSSSSS",
            ".....KSSSSHHHHHH",
            ".....KSSSSSSSSSS",
            ".....KsSSSSSSSSS",
            "......KsSSSSSSSS",
            ".......KsSSSSSSS",
            "........KKsSSSSS",
            "..........KKKKKK",
            "...........KsSSS",
            ".......KKKKKKWWW",
            ".....KKJJJJJKWWR",
            "....KJJJJJJJJKWR",
            "...KJJJJJJJJJJKR",
            "..KJJJJJJJJJJJjR",
            ".KJJJJJJJJJJJJjR",
            ".KJJJJJJJJJJJJjR",
            "KJJJJJJJJJJJJJjR",
            "KJJJJJJJJJJJJJjR",
            "KJJJJJJJJJJJJJJJ"
        };

        private static readonly Dictionary<char, uint> Palette = new()
        {
            { 'K', 0xFF000000 }, // outline, eyebrows, pupils
            { 'H', 0xFF552A00 }, // hair, moustache
            { 'S', 0xFFE8A070 }, // skin
            { 's', 0xFFB06840 }, // skin shadow
            { 'W', 0xFFFFFFFF }, // eyes, shirt
            { 'J', 0xFF2A2A6A }, // jacket
            { 'j', 0xFF15153A }, // jacket shadow
            { 'R', 0xFFAA0000 }, // tie
            { 'M', 0xFF550000 }, // lips
            { 'D', 0xFF1A0000 }  // open mouth
        };

        // the left column of each eye's white, and the eyes' rows
        private static readonly int[] EyeColumns = { 8, 20 };
        private const int EyeTop = 11;
        private const int MouthRow = 18;

        private static readonly Dictionary<(Eyes, bool), BitmapSource> Frames = new();

        public static BitmapSource Frame(Eyes eyes, bool mouthOpen)
        {
            if (!Frames.TryGetValue((eyes, mouthOpen), out var frame))
            {
                frame = Draw(eyes, mouthOpen);
                Frames[(eyes, mouthOpen)] = frame;
            }
            return frame;
        }

        private static BitmapSource Draw(Eyes eyes, bool mouthOpen)
        {
            var map = LeftHalf.Select(half =>
            {
                var left = half.PadRight(Width / 2, '.').Substring(0, Width / 2).ToCharArray();
                return left.Concat(left.Reverse()).ToArray();
            }).ToArray();

            // a two-pixel pupil in the four-pixel white, both eyes looking the same way
            var pupil = eyes switch { Eyes.Left => 0, Eyes.Right => 2, _ => 1 };
            foreach (var column in EyeColumns)
                for (int row = EyeTop; row <= EyeTop + 1; row++)
                {
                    map[row][column + pupil] = 'K';
                    map[row][column + pupil + 1] = 'K';
                }

            for (int column = 13; column <= 18; column++)
                map[MouthRow][column] = 'M';
            if (mouthOpen)
            {
                for (int column = 13; column <= 18; column++)
                    map[MouthRow + 1][column] = 'D';
                for (int column = 14; column <= 17; column++)
                    map[MouthRow + 2][column] = 'M';
            }

            var pixels = new uint[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    pixels[y * Width + x] = Palette.GetValueOrDefault(map[y][x], 0u);

            var bitmap = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, null, pixels, Width * 4);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
