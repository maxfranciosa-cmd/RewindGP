using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Services.Contracts;
using System.Linq;
using System.Collections.Generic;

namespace AMS2ChEd.Business.Services.RaceNumberSystem
{
    /// <summary>
    /// 1996-2013 F1 number allocation system:
    /// - Champion gets #1, their teammate gets #2
    /// - Numbers assigned based on previous season's CONSTRUCTORS championship order
    /// - Number 13 is skipped (superstition)
    /// - New teams get high numbers
    /// </summary>
    public class AssignNumbersToTeamsChampionshipTally : IRaceNumberAllocationService
    {
        public void AssignNumbersToCurrentSeason(ISaveGame saveGame)
        {
            // Get previous season's driver and constructor standings
            var previousYear = saveGame.CurrentSeason.Year - 1;
            var previousDriverStandings = saveGame.HistoricalDriverStandings
                .FirstOrDefault(h => h.Year == previousYear)?.Standing;

            var previousConstructorStandings = saveGame.HistoricalConstructorStandings
                .FirstOrDefault(h => h.Year == previousYear)?.Standing;

            if (previousDriverStandings == null || !previousDriverStandings.Any() ||
                previousConstructorStandings == null || !previousConstructorStandings.Any())
            {
                // No previous standings, assign sequentially
                AssignSequentialNumbers(saveGame);
                return;
            }

            // Find the driver champion
            var champion = previousDriverStandings.FirstOrDefault(s => s.Position == 1);

            // Check if the previous champion is still racing this year
            bool championStillRacing = champion != null && saveGame.CurrentSeason.Teams
                .Any(t => t.Driver1Contract.DriverId == champion.DriverId ||
                         t.Driver2Contract.DriverId == champion.DriverId);

            // Track which teams have been assigned numbers
            var assignedTeams = new HashSet<string>();

            // Assign champion's team #1 and #2
            if (champion != null && championStillRacing)
            {
                var championTeam = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => t.Driver1Contract.DriverId == champion.DriverId ||
                                       t.Driver2Contract.DriverId == champion.DriverId);

                if (championTeam != null)
                {
                    // Champion gets #1, teammate gets #2
                    if (championTeam.Driver1Contract.DriverId == champion.DriverId)
                    {
                        championTeam.Driver1Contract.DriverNumber = 1;
                        championTeam.Driver2Contract.DriverNumber = 2;
                    }
                    else
                    {
                        championTeam.Driver2Contract.DriverNumber = 1;
                        championTeam.Driver1Contract.DriverNumber = 2;
                    }

                    assignedTeams.Add(championTeam.TeamId);
                }
            }
            else if (champion != null && !championStillRacing)
            {
                // Champion retired - their old team gets #0 and #1 (if team still exists)
                var championOldTeam = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => t.TeamId == champion.TeamId);

                if (championOldTeam != null)
                {
                    championOldTeam.Driver1Contract.DriverNumber = 0;
                    championOldTeam.Driver2Contract.DriverNumber = 2;
                    assignedTeams.Add(championOldTeam.TeamId);
                }
            }

            // Create a map of team positions from constructors championship
            var teamPositions = previousConstructorStandings
                .OrderBy(s => s.Position)
                .ToDictionary(s => s.TeamId, s => s.Position);

            // Get remaining teams sorted by constructor position
            var remainingTeams = saveGame.CurrentSeason.Teams
                .Where(t => !assignedTeams.Contains(t.TeamId))
                .OrderBy(t => teamPositions.ContainsKey(t.TeamId) ? teamPositions[t.TeamId] : int.MaxValue)
                .ThenByDescending(t => t.Reputation) // For new teams, sort by reputation
                .ToList();

            int nextNumber = 3;

            foreach (var team in remainingTeams)
            {
                // Each team gets two consecutive numbers
                var number1 = GetNextValidNumber(ref nextNumber);
                var number2 = GetNextValidNumber(ref nextNumber);

                team.Driver1Contract.DriverNumber = number1;
                team.Driver2Contract.DriverNumber = number2;
            }
        }

        public void AssignNumberAtGameCreation(ISaveGame saveGame)
        {
            // At game creation, assign #0 to the player ONLY if they're replacing the driver who has #1
            var playerTeam = saveGame.CurrentSeason.Teams
                .FirstOrDefault(t => t.Driver1Contract.DriverId == saveGame.PlayerData.DriverId ||
                                   t.Driver2Contract.DriverId == saveGame.PlayerData.DriverId);

            if (playerTeam != null)
            {
                if (playerTeam.Driver1Contract.DriverNumber == 1 && playerTeam.Driver1Contract.DriverId == saveGame.PlayerData.DriverId)
                {
                    playerTeam.Driver1Contract.DriverNumber = 0;
                }
                if (playerTeam.Driver2Contract.DriverNumber == 1 && playerTeam.Driver2Contract.DriverId == saveGame.PlayerData.DriverId)
                {
                    playerTeam.Driver2Contract.DriverNumber = 0;
                }
            }
        }

        private int GetNextValidNumber(ref int currentNumber)
        {
            // Skip 13
            if (currentNumber == 13)
                currentNumber++;

            return currentNumber++;
        }

        private void AssignSequentialNumbers(ISaveGame saveGame)
        {
            int nextNumber = 1;

            foreach (var team in saveGame.CurrentSeason.Teams)
            {
                var number1 = GetNextValidNumber(ref nextNumber);
                var number2 = GetNextValidNumber(ref nextNumber);

                team.Driver1Contract.DriverNumber = number1;
                team.Driver2Contract.DriverNumber = number2;
            }
        }

        public int GetNumberForAbsence(ISaveGame saveGame, string driverInId, int numberToSubstitute)
        {
            return numberToSubstitute == 1 ? 0 : numberToSubstitute;
        }
    }
}