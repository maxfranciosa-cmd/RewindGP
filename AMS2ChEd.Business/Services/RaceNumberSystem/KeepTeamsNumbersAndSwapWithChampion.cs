using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Services.Contracts;
using System.Linq;
using System.Collections.Generic;

namespace AMS2ChEd.Business.Services.RaceNumberSystem
{
    /// <summary>
    // Pre-1996 F1 Rule: Teams KEEP their numbers from the previous season
    // EXCEPTION: The champion's team gets #1 and #2
    // SWAP RULE: The previous champion's team gets the numbers that the new champion's team had
    // SPECIAL CASE: If champion doesn't compete, their old team can use #0 and #2
    // NEW TEAMS: Get assigned the next available numbers (skipping #13)
    /// </summary>
    public class KeepTeamsNumbersAndSwapWithChampion : IRaceNumberAllocationService
    {
        public void AssignNumbersToCurrentSeason(ISaveGame saveGame)
        {
            var previousYear = saveGame.CurrentSeason.Year - 1;

            // Find the previous champion driver and their team
            var previousDriverStandings = saveGame.HistoricalDriverStandings
                .FirstOrDefault(h => h.Year == previousYear)?.Standing;

            if (previousDriverStandings == null || !previousDriverStandings.Any())
            {
                // No previous standings, drivers keep their current numbers
                return;
            }

            var previousChampion = previousDriverStandings.FirstOrDefault(s => s.Position == 1);
            if (previousChampion == null)
            {
                // No champion found, keep existing numbers
                return;
            }

            // Check if the previous champion is still racing this year
            bool championStillRacing = saveGame.CurrentSeason.Teams
                .Any(t => t.Driver1Contract.DriverId == previousChampion.DriverId ||
                         t.Driver2Contract.DriverId == previousChampion.DriverId);

            // Track which numbers are already in use
            var usedNumbers = new HashSet<int>();

            if (championStillRacing)
            {
                // Champion is racing - their team gets #1 and #2
                var newChampionTeam = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => t.Driver1Contract.DriverId == previousChampion.DriverId ||
                                       t.Driver2Contract.DriverId == previousChampion.DriverId);

                // Find the previous year's champion's team (the team that had #1 and #2 last year)
                var oldChampionTeam = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => t.Driver1Contract.DriverNumber == 1 || t.Driver2Contract.DriverNumber == 1 ||
                                       t.Driver1Contract.DriverNumber == 2 || t.Driver2Contract.DriverNumber == 2);

                if (newChampionTeam != null)
                {
                    // Store the numbers the new champion's team currently has (before swap)
                    int championTeamOldNumber1 = newChampionTeam.Driver1Contract.DriverNumber;
                    int championTeamOldNumber2 = newChampionTeam.Driver2Contract.DriverNumber;

                    // Assign 1 to champion, 2 to teammate
                    if (newChampionTeam.Driver1Contract.DriverId == previousChampion.DriverId)
                    {
                        newChampionTeam.Driver1Contract.DriverNumber = 1;
                        newChampionTeam.Driver2Contract.DriverNumber = 2;
                    }
                    else
                    {
                        newChampionTeam.Driver2Contract.DriverNumber = 1;
                        newChampionTeam.Driver1Contract.DriverNumber = 2;
                    }

                    usedNumbers.Add(1);
                    usedNumbers.Add(2);

                    // SWAP: Give the old champion's team the numbers that the new champion's team had
                    if (oldChampionTeam != null && oldChampionTeam.TeamId != newChampionTeam.TeamId)
                    {
                        // Only swap if the old numbers were valid
                        if (championTeamOldNumber1 > 0 && championTeamOldNumber1 <= 99 && championTeamOldNumber1 != 1 && championTeamOldNumber1 != 2)
                        {
                            oldChampionTeam.Driver1Contract.DriverNumber = championTeamOldNumber1;
                            usedNumbers.Add(championTeamOldNumber1);
                        }
                        if (championTeamOldNumber2 > 0 && championTeamOldNumber2 <= 99 && championTeamOldNumber2 != 1 && championTeamOldNumber2 != 2)
                        {
                            oldChampionTeam.Driver2Contract.DriverNumber = championTeamOldNumber2;
                            usedNumbers.Add(championTeamOldNumber2);
                        }
                    }
                }

                // All other teams keep their existing numbers
                foreach (var team in saveGame.CurrentSeason.Teams)
                {
                    if (team.TeamId != newChampionTeam?.TeamId && team.TeamId != oldChampionTeam?.TeamId)
                    {
                        // Check if team has valid numbers
                        if (team.Driver1Contract.DriverNumber < 1 || team.Driver1Contract.DriverNumber > 99)
                            team.Driver1Contract.DriverNumber = GetNextAvailableNumber(usedNumbers);

                        usedNumbers.Add(team.Driver1Contract.DriverNumber);

                        if (team.Driver2Contract.DriverNumber < 1 || team.Driver2Contract.DriverNumber > 99)
                            team.Driver2Contract.DriverNumber = GetNextAvailableNumber(usedNumbers);

                        usedNumbers.Add(team.Driver2Contract.DriverNumber);
                    }
                }
            }
            else
            {
                // Champion is NOT racing - their old team can use #0 and #2
                var championOldTeam = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => t.TeamId == previousChampion.TeamId);

                if (championOldTeam != null)
                {
                    // Champion's old team gets #0 and #2
                    championOldTeam.Driver1Contract.DriverNumber = 0;
                    championOldTeam.Driver2Contract.DriverNumber = 2;

                    usedNumbers.Add(0);
                    usedNumbers.Add(2);
                }

                // All other teams keep their existing numbers
                foreach (var team in saveGame.CurrentSeason.Teams)
                {
                    if (team.TeamId != championOldTeam?.TeamId)
                    {
                        // Check if team has valid numbers
                        if (team.Driver1Contract.DriverNumber < 1 || team.Driver1Contract.DriverNumber > 99)
                            team.Driver1Contract.DriverNumber = GetNextAvailableNumber(usedNumbers);

                        usedNumbers.Add(team.Driver1Contract.DriverNumber);

                        if (team.Driver2Contract.DriverNumber < 1 || team.Driver2Contract.DriverNumber > 99)
                            team.Driver2Contract.DriverNumber = GetNextAvailableNumber(usedNumbers);

                        usedNumbers.Add(team.Driver2Contract.DriverNumber);
                    }
                }
            }
        }

        private int GetNextAvailableNumber(HashSet<int> usedNumbers)
        {
            // Find next available number, skipping 13
            for (int i = 1; i <= 99; i++)
            {
                if (i == 13) continue; // Skip 13
                if (!usedNumbers.Contains(i))
                    return i;
            }
            // Fallback (shouldn't happen with ~20 teams)
            return 99;
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
        public int GetNumberForAbsence(ISaveGame saveGame, string driverInId, int numberToSubstitute)
        {
            return numberToSubstitute == 1 ? 0 : numberToSubstitute;
        }
    }
}