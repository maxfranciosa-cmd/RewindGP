using Ams2Interop;
using System.Threading.Tasks;
using System.Windows;

namespace Ams2ChEd.Business.AMS2.UI
{
    public partial class RaceReturnOverlayWindow : Window
    {
        private readonly Window _ownerToActivate;
        private readonly TaskCompletionSource _dismissedTcs = new TaskCompletionSource();

        public RaceReturnOverlayWindow(Window ownerToActivate)
        {
            InitializeComponent();
            _ownerToActivate = ownerToActivate;
        }

        /// <summary>Resolves once the overlay has been dismissed, one way or another.</summary>
        public Task WaitForDismissedAsync() => _dismissedTcs.Task;

        /// <summary>
        /// Dismisses the overlay without activating the owner or trying to close AMS2 - used when
        /// AMS2's process disappears (crash/manual close) while the overlay is still showing, since
        /// there's no game left to return from at that point.
        /// </summary>
        public void DismissWithoutReturning()
        {
            Close();
            _dismissedTcs.TrySetResult();
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            _ownerToActivate?.Activate();
            Close();
            _dismissedTcs.TrySetResult();

            // Fire-and-forget: best-effort close of AMS2 itself, now that the player is done with
            // it for this session - CloseAsync never throws, so nothing to await/handle here.
            _ = Ams2Launcher.CloseAsync();
        }
    }
}
