using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    /// <summary>
    /// Optional per-game seam that launches the game and auto-configures a race/pre-qualifying
    /// session on top of it via an in-game overlay. Resolved via GetService (nullable) rather
    /// than GetRequiredService - a game with no live-process configuration story simply doesn't
    /// register an implementation, and callers fall back to their existing manual-instructions flow.
    /// </summary>
    public interface IRaceLaunchAssistant
    {
        /// <summary>
        /// Launches the game if needed and shows a "click here to set up the race automatically"
        /// overlay on top of it. Returns once the overlay is dismissed - either because the
        /// settings were applied successfully, or because the player chose to skip/setup failed.
        /// <paramref name="ownerWindow"/> is passed through as an opaque owner handle. Returns true
        /// if the race was auto-configured (callers should skip their own manual-instructions
        /// fallback in that case), false otherwise.
        /// </summary>
        Task<bool> ShowSetupOverlayAsync(RaceLaunchRequest request, object ownerWindow, CancellationToken ct = default);

        /// <summary>
        /// Shows a transient "click here to return to Rewind GP" overlay on top of the game.
        /// Dismissing it brings <paramref name="ownerWindow"/> to the foreground. Returns once the
        /// overlay is dismissed (or immediately, without showing anything, if the game isn't
        /// actually running) - callers should await this before showing their own follow-up
        /// windows/dialogs, otherwise those appear behind the still-foregrounded game and any modal
        /// ones block the UI thread invisibly until the player alt-tabs.
        /// </summary>
        Task ShowReturnOverlayAsync(object ownerWindow);
    }
}
