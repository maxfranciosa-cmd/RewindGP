using System.Windows;

namespace Ams2ChEd.Business.AMS2.UI
{
    public partial class PrerequisiteSetupWindow : Window
    {
        public bool DontShowAgain { get; private set; }

        public PrerequisiteSetupWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DontShowAgain = DontShowAgainCheckBox.IsChecked == true;
            DialogResult = true;
            Close();
        }
    }
}
