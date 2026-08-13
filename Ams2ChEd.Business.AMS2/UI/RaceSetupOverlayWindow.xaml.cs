using System.Windows;
using System.Windows.Input;

namespace Ams2ChEd.Business.AMS2.UI
{
    public enum RaceSetupOverlayAction
    {
        Configure,
        Skip
    }

    public partial class RaceSetupOverlayWindow : Window
    {
        private TaskCompletionSource<RaceSetupOverlayAction> _actionTcs;
        private TaskCompletionSource _manualInstructionsTcs;
        private TaskCompletionSource _autoConfigureConfirmedTcs;

        public RaceSetupOverlayWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Resolves the next time the player clicks "set up automatically" or "skip" (from either
        /// the prompt or the error state).
        /// </summary>
        public Task<RaceSetupOverlayAction> WaitForUserActionAsync()
        {
            _actionTcs = new TaskCompletionSource<RaceSetupOverlayAction>();
            return _actionTcs.Task;
        }

        public void ShowWaiting()
        {
            PromptPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            WaitingPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Shows a confirmation that the race was set up automatically, with a reminder that the
        /// player can still tweak the circuit/session durations/etc. themselves before starting.
        /// Awaiting the returned task completes once the player dismisses it with "OK, GOT IT".
        /// </summary>
        public Task WaitForAutoConfigureConfirmedAsync()
        {
            PromptPanel.Visibility = Visibility.Collapsed;
            WaitingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;

            SuccessPanel.Visibility = Visibility.Visible;

            _autoConfigureConfirmedTcs = new TaskCompletionSource();
            return _autoConfigureConfirmedTcs.Task;
        }

        public void ShowError(string message)
        {
            PromptPanel.Visibility = Visibility.Collapsed;
            WaitingPanel.Visibility = Visibility.Collapsed;
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Shows the "set it up yourself" instructions directly on the overlay (instead of a
        /// separate RaceInstructionsWindow popping up over the game). Awaiting the returned task
        /// completes once the player dismisses it with "GOT IT".
        /// </summary>
        public Task WaitForManualInstructionsDismissedAsync(
            string carName, string liveryName, int opponentsNumber, int suggestedDifficulty,
            bool usesPerformanceScalars, bool isPreQuali)
        {
            PromptPanel.Visibility = Visibility.Collapsed;
            WaitingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;

            ManualInstructionsTitle.Text = isPreQuali ? "PRE-QUALIFYING SESSION" : "SET UP MANUALLY";
            ManualStep1.Text = $"1) Ensure you SELECT THE RIGHT CAR ({carName}) AND LIVERY. it will have your driver name on it! (in your case {liveryName})";
            ManualStep2.Text = $"2) Select THE RIGHT NUMBER OF OPPONENTS (in your case {opponentsNumber})";
            ManualStep3.Text = usesPerformanceScalars
                ? "3) SUGGESTED DIFFICULTY: your usual difficulty (teams should have the performance scalar so a slow car is actually going to be slower)"
                : $"3) SUGGESTED DIFFICULTY: +{suggestedDifficulty} POINTS (compared to a difficulty where you fight for wins)";
            ManualStep4.Text = isPreQuali
                ? "4) Run A QUALIFYING SESSION ONLY — do not start the race. Rewind GP will read your qualifying result automatically."
                : "4) it doesn't matter the track or the duration of the race, but have at least A QUALIFYING SESSION and A RACE SESSION.";

            ManualInstructionsPanel.Visibility = Visibility.Visible;

            _manualInstructionsTcs = new TaskCompletionSource();
            return _manualInstructionsTcs.Task;
        }

        private void ConfigureButton_Click(object sender, RoutedEventArgs e) =>
            _actionTcs?.TrySetResult(RaceSetupOverlayAction.Configure);

        private void SkipLink_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
            _actionTcs?.TrySetResult(RaceSetupOverlayAction.Skip);

        private void ContinueManuallyButton_Click(object sender, RoutedEventArgs e) =>
            _actionTcs?.TrySetResult(RaceSetupOverlayAction.Skip);

        private void ManualInstructionsOkButton_Click(object sender, RoutedEventArgs e) =>
            _manualInstructionsTcs?.TrySetResult();

        private void SuccessOkButton_Click(object sender, RoutedEventArgs e) =>
            _autoConfigureConfirmedTcs?.TrySetResult();
    }
}
