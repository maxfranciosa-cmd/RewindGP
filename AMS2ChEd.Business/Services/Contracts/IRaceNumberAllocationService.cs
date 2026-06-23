using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Services.Contracts
{
    public interface IRaceNumberAllocationService
    {
        void AssignNumbersToCurrentSeason(ISaveGame saveGame);

        void AssignNumberAtGameCreation(ISaveGame saveGame);

        int GetNumberForAbsence(ISaveGame saveGame, string driverInId, int numberToSubstitute);

    }
}
