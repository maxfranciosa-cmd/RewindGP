using System.Collections.Generic;
using System.Windows;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class LiverySelectionDialog : Window
    {
        public int SelectedIndex { get; private set; } = -1;

        public LiverySelectionDialog(List<string> liveryNames)
        {
            InitializeComponent();
            
            LiveryListBox.ItemsSource = liveryNames;
            
            if (liveryNames.Count > 0)
            {
                LiveryListBox.SelectedIndex = 0;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (LiveryListBox.SelectedIndex >= 0)
            {
                SelectedIndex = LiveryListBox.SelectedIndex;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please select a livery.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
