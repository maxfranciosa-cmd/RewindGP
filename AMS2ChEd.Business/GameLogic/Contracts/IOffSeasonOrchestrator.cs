using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using static AMS2ChEd.Business.Services.OffSeasonMovements;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    /// <summary>
    /// Player-facing decision points in the off-season saga. A game/UI-specific implementation
    /// translates each step into an actual dialog/window, letting the sequencing logic in
    /// IOffSeasonOrchestrator stay UI-framework-agnostic.
    /// </summary>
    public interface IOffSeasonUiCallbacks
    {
        Task ShowChampionshipCelebrationAsync(ISaveGame saveGame);

        Task ShowSeasonUnavailableWarningAsync(int nextSeasonYear);

        /// <summary>Shows the player's renewal/termination letter. Returns true if the player accepted.</summary>
        Task<bool> ShowContractLetterAsync(ISaveGame saveGame, IEnumerable<ITeamEntry> nextSeasonTeamEntries, DriverFirerOutcome dropOutcome, DriverReputation playerReputation);

        Task ShowRetirementNewsAsync(ISaveGame saveGame, IDriverData retiredDriver, string lastTeamId);

        /// <summary>Lets the player pick from team-application ballots. Returns the (possibly updated) ballots.</summary>
        Task<IEnumerable<TeamHiringBallot>> ShowTeamApplicationAsync(ISaveGame saveGame, IEnumerable<TeamHiringBallot> ballots, List<DropTeamResult> dropResults, DriverReputation newPlayerReputation, IEnumerable<ITeamEntry> nextSeasonTeamEntries);

        Task ShowNewSeasonRosterAsync(ISaveGame saveGame, ISeason newSeason);

        /// <summary>Asks whether to generate a fictional absence for a player still without a team. Returns true if requested.</summary>
        Task<bool> AskCreateFictionalAbsenceAsync();
    }

    public interface IOffSeasonOrchestrator
    {
        Task RunAsync(ISaveGame saveGame, IOffSeasonUiCallbacks uiCallbacks);
    }
}
