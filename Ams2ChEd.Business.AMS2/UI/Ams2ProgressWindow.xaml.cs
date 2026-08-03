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
        public Ams2ProgressWindow(string message = "Processing...")
        {
            InitializeComponent();
            MessageText.Text = message;
        }
    }
}
