using Ams2ChEd.Business.AMS2.Resources;
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
            WaitingText.Text = Strings.RaceSetupOverlayWindow_WaitingText;
            WaitingPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Shows the overlay's initial state, before the player can interact with anything -
        /// reuses the "waiting" panel (with different text) to cover the span where AMS2 itself is
        /// still starting up, so the overlay has something to show the moment it's created instead
        /// of staying hidden/nonexistent until the process is confirmed running.
        /// </summary>
        public void ShowLaunching()
        {
            PromptPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            WaitingText.Text = Strings.RaceSetupOverlayWindow_LaunchingText;
            WaitingPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Switches from the launching state to the interactive Configure/Skip prompt, once AMS2's
        /// window has been found and given a moment to settle.
        /// </summary>
        public void ShowPrompt()
        {
            WaitingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            PromptPanel.Visibility = Visibility.Visible;
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

            ManualInstructionsTitle.Text = isPreQuali ? Strings.RaceSetupOverlayWindow_PreQualiSessionTitle : Strings.RaceSetupOverlayWindow_ManualSetupTitle;
            ManualStep1.Text = string.Format(Strings.RaceSetupOverlayWindow_ManualStep1_Format, carName, liveryName);
            ManualStep2.Text = string.Format(Strings.RaceSetupOverlayWindow_ManualStep2_Format, opponentsNumber);
            ManualStep3.Text = usesPerformanceScalars
                ? Strings.RaceSetupOverlayWindow_ManualStep3_UsesScalars
                : string.Format(Strings.RaceSetupOverlayWindow_ManualStep3_Format, suggestedDifficulty);
            ManualStep4.Text = isPreQuali
                ? Strings.RaceSetupOverlayWindow_ManualStep4_PreQuali
                : Strings.RaceSetupOverlayWindow_ManualStep4_Normal;

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
