using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public class CosmeticsOption
    {
        public string Id { get; set; }

        public string PreviewImagePath { get; set; }
    }

    /// <summary>
    /// Optional per-game seam for player cosmetics (e.g. helmet design). Resolved via
    /// GetService (nullable) rather than GetRequiredService - a game with no cosmetics
    /// concept simply doesn't register an implementation.
    /// </summary>
    public interface IPlayerCosmeticsEditor
    {
        bool HasCosmeticsSupport { get; }

        IEnumerable<CosmeticsOption> GetDefaultCosmeticsOptions(int seasonYear);

        void ApplySelectedCosmetics(IDriverData playerDriverData, string selectedOptionId, int seasonYear);

        /// <summary>
        /// Shows the game-specific cosmetics editor. <paramref name="ownerWindow"/> is passed through
        /// as an opaque owner handle (a WPF Window in the AMS2 implementation) so this contract stays
        /// UI-framework-agnostic. Returns true if the player saved changes.
        /// </summary>
        bool ShowEditor(IPlayerData playerData, ISaveGame saveGame, object ownerWindow);
    }
}
