using System.Collections.Generic;
using System.Windows;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class DriverSelectionDialog : Window
    {
        public int SelectedIndex { get; private set; } = -1;

        public DriverSelectionDialog(string driverName, List<string> choices)
        {
            InitializeComponent();
            
            MessageTextBlock.Text = $"Multiple entries found for driver '{driverName}'. Please select which one to import:";
            ChoicesListBox.ItemsSource = choices;
            
            if (choices.Count > 0)
            {
                ChoicesListBox.SelectedIndex = 0;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (ChoicesListBox.SelectedIndex >= 0)
            {
                SelectedIndex = ChoicesListBox.SelectedIndex;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please select an option.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
