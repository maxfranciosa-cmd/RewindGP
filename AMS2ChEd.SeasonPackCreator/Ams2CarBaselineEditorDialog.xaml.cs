using AMS2ChEd.SeasonPackEditor.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AMS2ChEd.SeasonPackEditor
{
    /// <summary>
    /// Collects real-world power/weight for AMS2 car models the author hasn't recorded yet, so the
    /// actual-results malus generator can correct for cars that are already inherently stronger or
    /// weaker than the field average.
    /// </summary>
    public partial class Ams2CarBaselineEditorDialog : Window
    {
        private class CarBaselineRow
        {
            public string CarModel { get; set; }
            public string PowerHpText { get; set; } = "";
            public string WeightKgText { get; set; } = "";
        }

        private readonly List<CarBaselineRow> _rows;

        public Dictionary<string, Ams2CarBaseline> Result { get; private set; }

        public Ams2CarBaselineEditorDialog(IEnumerable<string> unknownCarModels)
        {
            InitializeComponent();
            _rows = unknownCarModels.Select(c => new CarBaselineRow { CarModel = c }).ToList();
            CarsItemsControl.ItemsSource = _rows;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            var result = new Dictionary<string, Ams2CarBaseline>();

            foreach (var row in _rows)
            {
                if (!double.TryParse(row.PowerHpText, out double power) || power <= 0
                    || !double.TryParse(row.WeightKgText, out double weight) || weight <= 0)
                {
                    MessageBox.Show($"Please enter a valid positive power and weight for '{row.CarModel}'.",
                        "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                result[row.CarModel] = new Ams2CarBaseline { PowerHp = power, WeightKg = weight };
            }

            Result = result;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
