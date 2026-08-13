using System.Windows;

namespace AMS2ChEd.Views
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow(string message = null)
        {
            InitializeComponent();
            // Null keeps the localized default message already set by the XAML's {loc:Loc}
            // binding; callers passing a custom message still supply it hardcoded in English
            // (those call sites aren't localized yet - see the plan's Wave 3).
            if (message != null)
            {
                MessageText.Text = message;
            }
        }
    }
}