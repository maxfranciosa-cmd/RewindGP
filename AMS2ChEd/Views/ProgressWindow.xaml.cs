using System.Windows;

namespace AMS2ChEd.Views
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow(string message = "Processing...")
        {
            InitializeComponent();
            MessageText.Text = message;
        }
    }
}