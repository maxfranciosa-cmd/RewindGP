using System.Windows.Markup;

namespace Ams2ChEd.Business.AMS2.Localization
{
    /// <summary>
    /// XAML markup extension for translatable strings, e.g. Content="{loc:Loc OptionsWindow_SaveButton}".
    /// Resolved once when the XAML is loaded (restart-based language switching - see the main app's
    /// AMS2ChEd.Localization.LocExtension for the equivalent in that project).
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Loc.GetString(Key);
        }
    }
}
