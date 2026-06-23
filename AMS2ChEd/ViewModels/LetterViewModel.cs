using AMS2ChEd.Commands;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AMS2ChEd.ViewModels
{
    public class LetterViewModel : BaseViewModel
    {
        public string LetterHeader { get; }
        public string LetterBody { get; }
        public ICommand AcceptCommand { get; }
        public ICommand BackCommand { get; }

        public LetterViewModel(SeatSelectionModel seat, string playerName)
        {
            LetterHeader = $"{seat.Team.Name} - Recruitment";
            LetterBody = GenerateLetter(seat, playerName);

            AcceptCommand = new RelayCommand(_ => OnAccept());
            BackCommand = new RelayCommand(_ => OnBack());
        }

        private string GenerateLetter(SeatSelectionModel seat, string playerName)
        {
            var seatLabel = seat.SeatIndex == 1 ? "first" : "second";
            return
$@"Dear {playerName},

We are pleased to offer you the {seatLabel} seat at {seat.Team.Name}. We look forward to discussing terms.

Best regards,
{seat.Team.Name} Racing Division";
        }

        private void OnAccept()
        {
            // implement accept logic (close window, update model, etc.)
        }

        private void OnBack()
        {
            // implement back logic
        }
    }
}
