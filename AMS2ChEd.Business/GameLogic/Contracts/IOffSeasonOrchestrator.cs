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
    /// <summary>
    /// Outcome of one team-application ballot the player took part in, resolved from the
    /// actual generated new season (never recomputed independently - see
    /// OffSeasonOrchestrator.BuildTeamApplicationResults).
    /// </summary>
    public class TeamApplicationResult
    {
        public string TeamId { get; set; }
        public DriverRole Role { get; set; }

        // an application for the team's reserve seat (Role means nothing then)
        public bool IsReserve { get; set; }
        public bool PlayerHired { get; set; }

        /// <summary>
        /// The driver the player was up against for this seat, or null when the player was hired
        /// and had no real head-to-head opponent (the team had already provisionally picked the
        /// player themselves before they applied, and no other candidate was in the running).
        /// </summary>
        public string OtherDriverId { get; set; }
        public DriverReputation OtherDriverReputation { get; set; }
        public bool OtherDriverWasAtTeamBefore { get; set; }
    }

    /// <summary>
    /// The one letter a player who was a team's reserve gets in the off-season (reserve contracts
    /// last one season, so there's no renewal letter): a promotion to a race seat, why there's none
    /// when a seat opened, and whether the team keeps a reserve next season.
    /// </summary>
    public class ReserveLetter
    {
        public string TeamId { get; set; }

        // the race seat that opened at the team, null when none did. IsOffered: the team promotes them
        public ReservePromotion Promotion { get; set; }

        // the team runs a reserve next season, so it offers the player to stay on
        public bool TeamWantsReserve { get; set; }
    }

    public enum ReserveLetterDecision
    {
        // free to apply anywhere in the application window, other teams' reserve seats included
        FreeAgent,
        AcceptedPromotion,
        StaysReserve
    }

    public interface IOffSeasonUiCallbacks
    {
        Task ShowChampionshipCelebrationAsync(ISaveGame saveGame);

        Task ShowSeasonUnavailableWarningAsync(int nextSeasonYear);

        /// <summary>Shows the player's renewal/termination letter. Returns true if the player accepted.</summary>
        Task<bool> ShowContractLetterAsync(ISaveGame saveGame, IEnumerable<ITeamEntry> nextSeasonTeamEntries, DriverFirerOutcome dropOutcome, DriverReputation playerReputation);

        /// <summary>Shows the reserve's letter from their team. Returns what the player chose.</summary>
        Task<ReserveLetterDecision> ShowReserveLetterAsync(ISaveGame saveGame, IEnumerable<ITeamEntry> nextSeasonTeamEntries, ReserveLetter letter, DriverReputation playerReputation);

        Task ShowRetirementNewsAsync(ISaveGame saveGame, IDriverData retiredDriver, string lastTeamId);

        /// <summary>Lets the player pick from team-application ballots. Returns the (possibly updated) ballots.</summary>
        Task<IEnumerable<TeamHiringBallot>> ShowTeamApplicationAsync(ISaveGame saveGame, IEnumerable<TeamHiringBallot> ballots, List<DropTeamResult> dropResults, DriverReputation newPlayerReputation, IEnumerable<ITeamEntry> nextSeasonTeamEntries);

        /// <summary>Shows one team's off-season application outcome letter (win or lose).</summary>
        Task ShowTeamApplicationResultAsync(ISaveGame saveGame, IEnumerable<ITeamEntry> nextSeasonTeamEntries, TeamApplicationResult result, DriverReputation playerReputation);

        Task ShowNewSeasonRosterAsync(ISaveGame saveGame, ISeason newSeason);
    }

    public interface IOffSeasonOrchestrator
    {
        Task RunAsync(ISaveGame saveGame, IOffSeasonUiCallbacks uiCallbacks);
    }
}
