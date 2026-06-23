using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using System.Collections.ObjectModel;

namespace AMS2ChEd.ViewModels
{
    public class PlayerCreationViewModel : BaseViewModel
    {

        public string DriverName { get; set; }
        public string Nationality { get; set; }

        private int _age;
        public int Age
        {
            get => _age;
            set
            {
                _age = value;
                Notify();
                RefreshFilteredReputations();
            }
        }

        public ObservableCollection<DriverReputation> FilteredReputations { get; } = new();

        private DriverReputation? _selectedReputation;
        public DriverReputation? SelectedReputation
        {
            get => _selectedReputation;
            set { _selectedReputation = value; Notify(); }
        }

        public PlayerCreationViewModel(ReputationUpdater reputationUpdater)
        {
            RefreshFilteredReputations();
        }

        private void RefreshFilteredReputations()
        {
            FilteredReputations.Clear();

            if (Age <= 0)
                return; // age not yet entered

            foreach (var rep in ReputationUpdater.AvailableReputationForAge(Age))
                FilteredReputations.Add(rep);
        }
    }
}
