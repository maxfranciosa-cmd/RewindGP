using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    /// <summary>
    /// Optional per-game seam for a picture of a team's car (e.g. the paddock opportunities
    /// window's seat tiles). Resolved via GetService (nullable) rather than GetRequiredService - a
    /// game with no car pictures simply doesn't register an implementation, and callers fall back
    /// to the team's colour.
    /// </summary>
    public interface ITeamPreviewProvider
    {
        /// <summary>
        /// The team's car as it runs at that race, encoded as PNG so this contract stays
        /// UI-framework-agnostic, scaled down to <paramref name="maxWidth"/> pixels when wider (a
        /// game's car pictures can be full textures, far bigger than any tile showing them); null
        /// when the team has no picture. Slow enough to be called off the UI thread.
        /// </summary>
        byte[] LoadTeamPreviewPng(ISeason season, string teamId, int raceId, int maxWidth);
    }
}
