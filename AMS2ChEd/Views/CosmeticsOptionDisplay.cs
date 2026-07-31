using System.ComponentModel;

namespace AMS2ChEd.Views
{
    public class CosmeticsOptionDisplay : INotifyPropertyChanged
    {
        public string Id { get; set; }

        public string PreviewImagePath { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
