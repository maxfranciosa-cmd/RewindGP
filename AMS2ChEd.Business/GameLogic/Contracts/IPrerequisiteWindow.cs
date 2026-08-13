namespace AMS2ChEd.Business.GameLogic.Contracts
{
    /// <summary>
    /// Optional per-game seam for a one-time "here's what you need to set up before racing"
    /// prompt (e.g. AMS2's telemetry output / borderless window requirements for the race-launch
    /// overlay). Resolved via GetService (nullable) rather than GetRequiredService - a game with
    /// no such prerequisites simply doesn't register an implementation.
    /// </summary>
    public interface IPrerequisiteWindow
    {
        /// <summary>
        /// Shows the game-specific prerequisite prompt if the player hasn't already dismissed it
        /// with "don't show this again". No-op otherwise. <paramref name="ownerWindow"/> is passed
        /// through as an opaque owner handle (a WPF Window in the AMS2 implementation) so this
        /// contract stays UI-framework-agnostic.
        /// </summary>
        void ShowIfNeeded(object ownerWindow);
    }
}
