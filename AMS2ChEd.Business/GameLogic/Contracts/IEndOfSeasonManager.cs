using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public interface IEndOfSeasonManager
    {
        void UpdateDriversPoolForNextSeason(int nextSeasonYear, ISaveGame saveGame, Dictionary<string, IDriverData> driversNewSeasonDictionary);

        Dictionary<string, DriverReputation> ProjectNewReputations(ISaveGame saveGame, int nextSeasonYear);

        IEnumerable<DropTeamResult> ExecuteTeamDrops(
                      ISaveGame saveGame,
                      ISeason newSeason);

        /// <summary>
        /// The race seat (if any) opening at a reserve's team next season, and whether the team
        /// promotes the reserve to it (first refusal: only when they're a PerfectFit).
        /// </summary>
        ReservePromotion GetReservePromotion(ISaveGame saveGame, IEnumerable<ITeamEntry> newSeasonTeamEntries, IEnumerable<DropTeamResult> dropTeamResults, string teamId, DriverReputation reserveReputation);

        IEnumerable<TeamHiringBallot> TeamPicksPotentialReplacementsDrivers(int newSeasonYear, ISaveGame saveGame, IEnumerable<ITeamEntry> newSeasonTeamEntries, IEnumerable<DropTeamResult> dropTeamResults, ReservePlan reservePlan = null);

        ISeason GenerateNewSeasonWithNewHirings(ISaveGame saveGame, ISeason newSeason, IEnumerable<TeamHiringBallot> ballots);

        void StartNewSeason(ISaveGame saveGame, ISeason newSeason);
    }
}
