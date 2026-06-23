using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Mocks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace AMS2ChEd.Views
{
    public partial class MockRaceControlWindow : Window
    {
        private MockUserControlledRaceDataService _service;
        private ParticipantData _selectedParticipant;
        private bool _isPrequali;

        public MockRaceControlWindow(MockUserControlledRaceDataService service, bool isPrequali)
        {
            InitializeComponent();
            _service = service;
            _isPrequali = isPrequali;
            // Subscribe to updates
            _service.SessionUpdated += OnSessionUpdated;

            LoadParticipants();
            UpdateSessionDisplay();
        }

        private void OnSessionUpdated(object sender, SessionUpdateEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LoadParticipants();
                UpdateSessionDisplay();
            });
        }

        private void UpdateSessionDisplay()
        {
            var session = _service.CurrentSession;
            TxtCurrentSession.Text = session.SessionType.ToString().ToUpper();

            // Update button states
            BtnStartSession.IsEnabled = !session.IsSessionActive;
            BtnFinishSession.IsEnabled = session.IsSessionActive;
            BtnDNF.IsEnabled = _selectedParticipant != null && session.IsSessionActive;
        }

        private void LoadParticipants()
        {
            ParticipantsItems.ItemsSource = null;
            ParticipantsItems.ItemsSource = _service.CurrentSession.Standings;
            _selectedParticipant = null;
        }

        private void BtnStartSession_Click(object sender, RoutedEventArgs e)
        {
            _service.StartSession();
        }

        private void BtnFinishSession_Click(object sender, RoutedEventArgs e)
        {
            _service.FinishSession();
            MessageBox.Show($"Session finished! Results saved for {_service.CurrentSession.SessionType}",
                "Session Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnNextSession_Click(object sender, RoutedEventArgs e)
        {
            _service.AdvanceToNextSession();
        }

        private void Participant_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.DataContext is ParticipantData participant)
            {
                if (_selectedParticipant == null)
                {
                    // First selection
                    _selectedParticipant = participant;
                    border.BorderBrush = System.Windows.Media.Brushes.Yellow;
                    border.BorderThickness = new Thickness(2);
                    BtnDNF.IsEnabled = _service.CurrentSession.IsSessionActive;
                }
                else if (_selectedParticipant == participant)
                {
                    // Deselect
                    _selectedParticipant = null;
                    border.BorderBrush = System.Windows.Media.Brushes.Gray;
                    border.BorderThickness = new Thickness(1);
                    BtnDNF.IsEnabled = false;
                    LoadParticipants(); // Refresh to remove highlight
                }
                else
                {
                    // Swap positions
                    _service.SwapPositions(_selectedParticipant.Position, participant.Position);
                    _selectedParticipant = null;
                    BtnDNF.IsEnabled = false;
                    LoadParticipants(); // Refresh list
                }
            }
        }

        private void BtnDNF_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedParticipant == null)
                return;

            var result = MessageBox.Show(
                $"Mark {_selectedParticipant.DriverName} as DNF (Did Not Finish)?\n\n" +
                $"This will retire the driver from the session.",
                "Confirm DNF",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _service.MarkDriverAsDNF(_selectedParticipant.Position);
                _selectedParticipant = null;
                BtnDNF.IsEnabled = false;
                LoadParticipants();

                MessageBox.Show(
                    $"Driver marked as DNF and moved to end of results.",
                    "DNF Recorded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _service.SessionUpdated -= OnSessionUpdated;
            base.OnClosed(e);
        }
    }
}