namespace AMS2ChEd.Business.Settings.Contracts
{
    public class GameInstallSettings
    {
        public string GameInstallFolder { get; set; }
    }

    public interface IGameInstallSettingsStorage
    {
        GameInstallSettings LoadSettings();

        void SaveSettings(GameInstallSettings settings);

        /// <summary>
        /// Whether the player still needs to provide required per-game setup (e.g. AMS2's in-game
        /// driver name) before starting a race. Games with no extra required setup can always
        /// return false.
        /// </summary>
        bool NeedsPlayerSetup();

        /// <summary>
        /// Shows the game-specific settings editor UI. ownerWindow is passed through as an opaque
        /// owner handle (a WPF Window in the AMS2 implementation) so this contract stays
        /// UI-framework-agnostic. Returns true if the player saved changes.
        /// </summary>
        bool ShowEditor(object ownerWindow);
    }
}
