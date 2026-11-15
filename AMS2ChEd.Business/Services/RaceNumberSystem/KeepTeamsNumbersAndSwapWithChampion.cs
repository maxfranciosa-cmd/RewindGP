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
    // NEW TEAMS (not in last season): ignore the season pack's numbers and get the lowest
    // numbers still free once every returning team has kept its own (skipping #13)
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

            // Teams that didn't race last season: their numbers come from the season pack, which was
            // authored against the real previous season, not this save's - drop them so they can't
            // clash with a returning team (or be mistaken for last year's #1/#2 holder).
            var newTeamIds = GetNewTeamIds(saveGame, previousYear);
            foreach (var team in saveGame.CurrentSeason.Teams.Where(t => newTeamIds.Contains(t.TeamId)))
            {
                team.Driver1Contract.DriverNumber = -1;
                team.Driver2Contract.DriverNumber = -1;
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

                    // Champion moved to a team new this season: it has no numbers of its own to hand
                    // over, so the old champion team gives up #1/#2 and gets the lowest free ones below.
                    if (newTeamIds.Contains(newChampionTeam.TeamId) && oldChampionTeam != null && oldChampionTeam.TeamId != newChampionTeam.TeamId)
                    {
                        oldChampionTeam.Driver1Contract.DriverNumber = -1;
                        oldChampionTeam.Driver2Contract.DriverNumber = -1;
                        oldChampionTeam = null;
                    }

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
                KeepOrAssignNumbers(saveGame, newTeamIds, usedNumbers, newChampionTeam?.TeamId, oldChampionTeam?.TeamId);
            }
            else
            {
                // Champion is NOT racing - their old team can use #0 and #2
                var championOldTeam = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => t.TeamId == previousChampion.TeamId);

                // Find whichever team currently holds #1 or #2 (e.g. from that year's season data) -
                // if it's a different team than the champion's old team, it needs to give up #1/#2
                // and receive the champion's old team's previous numbers, mirroring the swap done
                // when the champion is still racing.
                var holderTeam = saveGame.CurrentSeason.Teams
                    .FirstOrDefault(t => !newTeamIds.Contains(t.TeamId) &&
                                        (t.Driver1Contract.DriverNumber == 1 || t.Driver2Contract.DriverNumber == 1 ||
                                         t.Driver1Contract.DriverNumber == 2 || t.Driver2Contract.DriverNumber == 2));

                // #1 stays vacant this season (only #0/#2 substitute for the retired champion) -
                // reserve it so it can't be handed out to an unrelated team below.
                usedNumbers.Add(1);

                if (championOldTeam != null)
                {
                    // Store the numbers the champion's old team currently has (before overwrite)
                    int championTeamOldNumber1 = championOldTeam.Driver1Contract.DriverNumber;
                    int championTeamOldNumber2 = championOldTeam.Driver2Contract.DriverNumber;

                    // Champion's old team gets #0 and #2
                    championOldTeam.Driver1Contract.DriverNumber = 0;
                    championOldTeam.Driver2Contract.DriverNumber = 2;

                    usedNumbers.Add(0);
                    usedNumbers.Add(2);

                    // SWAP: give the team that held #1/#2 the champion's old team's previous numbers
                    if (holderTeam != null && holderTeam.TeamId != championOldTeam.TeamId)
                    {
                        // Only swap if the old numbers were valid
                        if (championTeamOldNumber1 > 0 && championTeamOldNumber1 <= 99 && championTeamOldNumber1 != 1 && championTeamOldNumber1 != 2)
                        {
                            holderTeam.Driver1Contract.DriverNumber = championTeamOldNumber1;
                            usedNumbers.Add(championTeamOldNumber1);
                        }
                        if (championTeamOldNumber2 > 0 && championTeamOldNumber2 <= 99 && championTeamOldNumber2 != 1 && championTeamOldNumber2 != 2)
                        {
                            holderTeam.Driver2Contract.DriverNumber = championTeamOldNumber2;
                            usedNumbers.Add(championTeamOldNumber2);
                        }
                    }
                }

                // All other teams keep their existing numbers
                KeepOrAssignNumbers(saveGame, newTeamIds, usedNumbers, championOldTeam?.TeamId, holderTeam?.TeamId);
            }
        }

        // Every team not handled by the champion rules keeps its valid numbers; those are all
        // reserved before any number is handed out, then returning teams with an invalid number
        // and finally the new teams get the lowest free ones.
        private void KeepOrAssignNumbers(ISaveGame saveGame, HashSet<string> newTeamIds, HashSet<int> usedNumbers, params string?[] handledTeamIds)
        {
            var teams = saveGame.CurrentSeason.Teams
                .Where(t => !handledTeamIds.Contains(t.TeamId))
                .OrderBy(t => newTeamIds.Contains(t.TeamId))
                .ToList();

            var contracts = teams.SelectMany(t => new[] { t.Driver1Contract, t.Driver2Contract }).ToList();

            foreach (var contract in contracts.Where(c => IsValidNumber(c.DriverNumber)))
                usedNumbers.Add(contract.DriverNumber);

            foreach (var contract in contracts.Where(c => !IsValidNumber(c.DriverNumber)))
            {
                contract.DriverNumber = GetNextAvailableNumber(usedNumbers);
                usedNumbers.Add(contract.DriverNumber);
            }
        }

        private static bool IsValidNumber(int number) => number >= 1 && number <= 99;

        // Teams absent from last season's constructor standings. Without those standings no team
        // counts as new, and everyone keeps the numbers they have.
        private static HashSet<string> GetNewTeamIds(ISaveGame saveGame, int previousYear)
        {
            var previousStanding = saveGame.HistoricalConstructorStandings?
                .FirstOrDefault(h => h.Year == previousYear)?.Standing;

            if (previousStanding == null || !previousStanding.Any())
                return new HashSet<string>();

            var previousTeamIds = previousStanding.Select(s => s.TeamId).ToHashSet();
            return saveGame.CurrentSeason.Teams
                .Select(t => t.TeamId)
                .Where(id => !previousTeamIds.Contains(id))
                .ToHashSet();
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