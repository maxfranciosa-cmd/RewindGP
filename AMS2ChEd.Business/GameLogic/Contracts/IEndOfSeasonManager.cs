using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Services.Contracts;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public interface IEndOfSeasonManager
    {
        void UpdateDriversPoolForNextSeason(int nextSeasonYear, ISaveGame saveGame, Dictionary<string, IDriverData> driversNewSeasonDictionary);

        IEnumerable<DropTeamResult> ExecuteTeamDrops(
                      ISaveGame saveGame,
                      ISeason newSeason);

        IEnumerable<TeamHiringBallot> TeamPicksPotentialReplacementsDrivers(int newSeasonYear, ISaveGame saveGame, IEnumerable<ITeamEntry> newSeasonTeamEntries, IEnumerable<DropTeamResult> dropTeamResults);

        ISeason GenerateNewSeasonWithNewHirings(ISaveGame saveGame, ISeason newSeason, IEnumerable<TeamHiringBallot> ballots);

        void StartNewSeason(ISaveGame saveGame, ISeason newSeason);
    }
}
