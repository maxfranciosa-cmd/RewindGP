using System.Windows;

namespace Ams2ChEd.Business.AMS2.UI
{
    /// <summary>
    /// Duplicate of AMS2ChEd.Views.ProgressWindow, kept in this project because
    /// Ams2ChEd.Business.AMS2 only references AMS2ChEd.Business.csproj (not the AMS2ChEd app
    /// project itself), so it can't reference that window directly - see OptionsWindow.xaml.cs,
    /// the only current caller.
    /// </summary>
    public partial class Ams2ProgressWindow : Window
    {
        public Ams2ProgressWindow(string message = null)
        {
            InitializeComponent();
            // Null keeps the localized default message already set by the XAML's {loc:Loc}
            // binding; callers passing a custom message still supply it hardcoded in English,
            // except OptionsWindow's own call site, which now uses a resx key (see OptionsWindow.xaml.cs).
            if (message != null)
            {
                MessageText.Text = message;
            }
        }
    }
}
