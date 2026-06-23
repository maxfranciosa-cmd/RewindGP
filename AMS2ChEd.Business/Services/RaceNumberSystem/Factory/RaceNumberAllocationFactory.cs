using AMS2ChEd.Business.Services.Contracts;

namespace AMS2ChEd.Business.Services.RaceNumberSystem.Factory
{
    public static class RaceNumberAllocationFactory
    {
        public static IRaceNumberAllocationService GetRaceNumberAllocationService(int year)
        {
            if (year < 1996)
            {
                return new KeepTeamsNumbersAndSwapWithChampion();
            } 
            else if (year < 2014)
            {
                return new AssignNumbersToTeamsChampionshipTally();
            }
            else
            {
                return new DriversChoosingTheirOwnNumbers();
            }
        }

    }
}
