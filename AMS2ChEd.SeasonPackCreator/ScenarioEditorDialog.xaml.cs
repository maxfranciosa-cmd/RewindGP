using Microsoft.Win32;
using System.Windows;

namespace AMS2ChEd.SeasonPackEditor
{
    public partial class ScenarioEditorDialog : Window
    {
        public MainWindow.ScenarioEntry Scenario { get; private set; }

        public ScenarioEditorDialog(MainWindow.ScenarioEntry existing = null)
        {
            InitializeComponent();

            if (existing != null)
            {
                NameTextBox.Text = existing.Name;
                DescriptionTextBox.Text = existing.Description;
                PicturePathTextBox.Text = existing.PictureFullPath;
                GameFilePathTextBox.Text = existing.GameFileFullPath;
            }
        }

        private void BrowsePicture_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Scenario Picture",
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                PicturePathTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseGameFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Game Save / Scenario JSON File",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                GameFilePathTextBox.Text = dialog.FileName;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Please enter a name for the scenario.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Scenario = new MainWindow.ScenarioEntry
            {
                Name            = NameTextBox.Text.Trim(),
                Description     = DescriptionTextBox.Text.Trim(),
                PictureFullPath = PicturePathTextBox.Text.Trim(),
                GameFileFullPath= GameFilePathTextBox.Text.Trim()
            };

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
