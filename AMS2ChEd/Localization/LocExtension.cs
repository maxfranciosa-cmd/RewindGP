using System.Windows.Markup;

namespace AMS2ChEd.Localization
{
    /// <summary>
    /// XAML markup extension for translatable strings, e.g. Text="{loc:Loc ProgressWindow_Title}".
    /// Resolved once when the XAML is loaded (this app uses restart-based language switching, not
    /// live switching - see CLAUDE.md's language-support notes), so a plain MarkupExtension that
    /// returns a value at ProvideValue time is sufficient; no dynamic binding infrastructure needed.
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
