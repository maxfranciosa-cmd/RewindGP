namespace AMS2ChEd.Extensions
{
    /// <summary>
    /// The one shape every driver portrait is shown in: 2:3 (width:height), the shape of the
    /// driver photos (120x180). Generic avatars (4:5) are filled into it and lose a little at the
    /// sides. The XAML frames are sized to it by hand (inside any border); keep them in step.
    /// </summary>
    public static class PortraitFrame
    {
        public const double AspectRatio = 2.0 / 3.0;

        public static double WidthFor(double height) => height * AspectRatio;
    }
}
