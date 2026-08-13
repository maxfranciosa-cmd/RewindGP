using System.Resources;

namespace AMS2ChEd.Resources
{
    /// <summary>
    /// Hand-written accessor over Strings.resx (+ Strings.&lt;culture&gt;.resx satellites, e.g.
    /// Strings.it.resx). Not designer-generated: `dotnet build` alone never invokes the ResX
    /// single-file generator (that's a Visual Studio design-time-only feature), so a checked-in
    /// Strings.Designer.cs would silently go stale in CI/CLI builds. Keep the property list below
    /// and the resx &lt;data name="..."&gt; keys in sync by hand - see AMS2ChEd/Localization/Loc.cs
    /// for the lookup/fallback logic that actually reads these at runtime.
    /// </summary>
    public static class Strings
    {
        public static ResourceManager ResourceManager { get; } =
            new ResourceManager("AMS2ChEd.Resources.Strings", typeof(Strings).Assembly);

        public static string ProgressWindow_Title => ResourceManager.GetString(nameof(ProgressWindow_Title))!;
        public static string ProgressWindow_DefaultMessage => ResourceManager.GetString(nameof(ProgressWindow_DefaultMessage))!;
    }
}
