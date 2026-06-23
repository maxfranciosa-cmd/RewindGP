using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMS2ChEd.ViewModels
{
    public class TeamModel
    {
        public string Name { get; set; }
        public string TeamId { get; set; }
    }

    public class DriverModel
    {
        public string Name { get; set; }
        public string DriverId { get; set; }
    }

    public class SeatSelectionModel
    {
        public TeamModel Team { get; set; }
        public DriverModel Driver { get; set; }

        public int SeatIndex { get; set; }

        public bool IsSelectable { get; set; }
    }

    public class TeamSelectionViewModel : BaseViewModel
    {
        public ObservableCollection<TeamModel> Teams { get; set; }
        public ObservableCollection<SeatSelectionModel> Seats { get; set; }

        public TeamSelectionViewModel(List<TeamModel> teams)
        {
            Teams = new ObservableCollection<TeamModel>(teams);
            Seats = new ObservableCollection<SeatSelectionModel>();

            foreach (var t in teams)
            {
                Seats.Add(new SeatSelectionModel { Team = t, SeatIndex = 1, IsSelectable = true });
                Seats.Add(new SeatSelectionModel { Team = t, SeatIndex = 2, IsSelectable = true });
            }
        }
    }

}
