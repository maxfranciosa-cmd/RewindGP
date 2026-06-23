using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;

namespace AMS2ChEd.Business.Services.RaceNumberSystem
{
    /// <summary>
    /// Modern F1 (2014+) permanent number allocation system:
    /// - Each driver chooses a permanent number (2-99) when entering F1
    /// - Drivers keep their number throughout their career
    /// - Reigning champion can choose to use #1 (or keep their permanent number)
    /// - Number 13 is allowed but rarely chosen by drivers
    /// - If a driver's favorite number is taken, assign next preferred or available number
    /// </summary>
    public class DriversChoosingTheirOwnNumbers : IRaceNumberAllocationService
    {
        public void AssignNumbersToCurrentSeason(ISaveGame saveGame)
        {
            // Find the reigning champion
            var previousYear = saveGame.CurrentSeason.Year - 1;
            var previousStandings = saveGame.HistoricalDriverStandings
                .FirstOrDefault(h => h.Year == previousYear)?.Standing;

            // If no previous season, drivers keep their existing numbers
            if (previousStandings == null || !previousStandings.Any())
            {
                return;
            }

            var championDriverId = previousStandings.FirstOrDefault(s => s.Position == 1)?.DriverId;

            // Track which numbers are already in use
            var usedNumbers = new HashSet<int> { 1 }; // Reserve #1 for champion

            // Create a list of all drivers with their previous championship positions
            var driversWithPositions = saveGame.Drivers
                .Select(d => new
                {
                    Driver = d,
                    Position = previousStandings.FirstOrDefault(s => s.DriverId == d.DriverId)?.Position ?? int.MaxValue
                })
                .OrderBy(x => x.Position)
                .ToList();

            // Process drivers in championship order
            foreach (var driverWithPosition in driversWithPositions)
            {
                var driver = driverWithPosition.Driver;

                // Find the team this driver is on
                var team = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => t.Driver1Contract.DriverId == driver.DriverId ||
                                       t.Driver2Contract.DriverId == driver.DriverId);

                if (team == null)
                    continue;

                bool isDriver1 = team.Driver1Contract.DriverId == driver.DriverId;
                var contract = isDriver1 ? team.Driver1Contract : team.Driver2Contract;

                // Champion gets #1
                if (driver.DriverId == championDriverId)
                {
                    contract.DriverNumber = 1;
                }
                else
                {
                    // Other drivers pick their favorite number (subject to availability)
                    int assignedNumber = AssignFavoriteOrAvailableNumber(driver, usedNumbers, saveGame.CurrentSeason.Year);
                    contract.DriverNumber = assignedNumber;
                    usedNumbers.Add(assignedNumber);
                }
            }
        }

        public void AssignNumberAtGameCreation(ISaveGame saveGame)
        {
            // At game creation, assign the favourite (or available number) excluding all the otehrs
            var usedNumbers = new HashSet<int>();

            DriverContract playerDriverContract = null;

            foreach (var team in saveGame.CurrentSeason.Teams)
            {

                if (team.Driver1Contract.DriverId != saveGame.PlayerData.DriverId)
                {
                    usedNumbers.Add(team.Driver1Contract.DriverNumber);
                }
                else
                {
                    playerDriverContract = team.Driver1Contract;
                }

                if (team.Driver2Contract.DriverId != saveGame.PlayerData.DriverId)
                {
                    usedNumbers.Add(team.Driver2Contract.DriverNumber);
                }
                else
                {
                    playerDriverContract = team.Driver2Contract;
                }
            }

            if (playerDriverContract != null)
            {
                var driverData = saveGame.Drivers.FirstOrDefault(d => d.DriverId == saveGame.PlayerData.DriverId);
                playerDriverContract.DriverNumber = AssignFavoriteOrAvailableNumber(driverData, usedNumbers, saveGame.CurrentSeason.Year);
            }
        }

        public int GetNumberForAbsence(ISaveGame saveGame, string driverInId, int numberToSubstitute)
        {
            var usedNumbers = saveGame.CurrentSeason.Teams.SelectMany( t=> new[] { t.Driver1Contract.DriverNumber, t.Driver2Contract.DriverNumber }).ToHashSet();
            var driverData = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverInId);
            return AssignFavoriteOrAvailableNumber(driverData,usedNumbers,saveGame.CurrentSeason.Year);
        }

        private int AssignFavoriteOrAvailableNumber(IDriverData driver, HashSet<int> usedNumbers, int currentYear)
        {
            // Get favorite numbers for current season (or fallback to driver-level favorites)
            var favoriteNumbers = GetFavoriteNumbersForYear(driver, currentYear);

            // Try each favorite number in order
            if (favoriteNumbers != null && favoriteNumbers.Any())
            {
                foreach (var favNumber in favoriteNumbers)
                {
                    if (IsValidNumber(favNumber) && !usedNumbers.Contains(favNumber))
                    {
                        return favNumber;
                    }
                }
            }

            // If no favorites available, find next available number (2-99)
            for (int i = 2; i <= 99; i++)
            {
                if (!usedNumbers.Contains(i))
                {
                    return i;
                }
            }

            // Fallback (shouldn't happen with 98 available numbers for ~20 drivers)
            return 99;
        }

        private IEnumerable<int> GetFavoriteNumbersForYear(IDriverData driver, int year)
        {
            if (driver.FavouriteNumbers != null && driver.FavouriteNumbers.Any())
            {
                return driver.FavouriteNumbers;
            }

            // No favorites defined
            return null;
        }

        private bool IsValidNumber(int number)
        {
            // Valid permanent numbers are 2-99 (1 is reserved for champion)
            return number >= 2 && number <= 99;
        }
    }
}