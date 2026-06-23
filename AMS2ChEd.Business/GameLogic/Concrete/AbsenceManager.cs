using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.RaceNumberSystem.Factory;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Manages driver absences and substitution logic
    /// </summary>
    public class AbsenceManager : IAbsenceManager
    {
        private readonly DriverHirer _driverHirer;

        public event EventHandler<AbsenceOpportunityEventArgs> AbsenceOpportunityAvailable;
        public event EventHandler<AbsenceDecisionEventArgs> AbsenceDecisionMade;

        public AbsenceManager()
        {
            _driverHirer = new DriverHirer();
        }

        /// <summary>
        /// Process all absences for a given Grand Prix
        /// </summary>
        public void ProcessAbsences(
            List<EntryListEntry> entryList,
            List<Absence> absences,
            ISaveGame saveGame,
            IAbsenceDecisionProvider decisionProvider)
        {
            int i = 0;
            Absence chainedAbsence = null;
            bool playerHasSteppedIn = IsDriverInAnyAbsence(saveGame.PlayerData.DriverId, absences);

            while (i < absences.Count)
            {
                var currentAbsence = chainedAbsence ?? absences[i];
                bool playerCanApply = (currentAbsence.DriverOut != saveGame.PlayerData.DriverId && saveGame.PlayerData.TeamId != currentAbsence.TeamId);

                if (playerCanApply)
                {
                    // Check if player wants to apply
                    var opportunity = new AbsenceOpportunity
                    {
                        DriverOut = currentAbsence.DriverOut,
                        TeamId = currentAbsence.TeamId,
                        RaceId = currentAbsence.RaceId,
                        DriverIn = currentAbsence.DriverIn
                    };

                    AbsenceOpportunityAvailable?.Invoke(this, new AbsenceOpportunityEventArgs { Opportunity = opportunity });

                    bool playerWantsToApply = decisionProvider.DoesPlayerWantToApply(opportunity, playerHasSteppedIn);

                    if (playerWantsToApply && !playerHasSteppedIn)
                    {
                        // Check if player's current team allows them to go
                        var playerTeam = entryList.FirstOrDefault(e =>
                            e.Driver1Id == saveGame.PlayerData.DriverId || e.Driver2Id == saveGame.PlayerData.DriverId);

                        bool playerTeamAllows = decisionProvider.DoesPlayerTeamAllowLeave(playerTeam?.TeamId, currentAbsence);

                        if (!playerTeamAllows && playerTeam != null)
                        {
                            // Team refuses - use declared absence
                            var decision = new AbsenceDecision
                            {
                                DecisionType = AbsenceDecisionType.TeamRefused,
                                Absence = currentAbsence
                            };
                            AbsenceDecisionMade?.Invoke(this, new AbsenceDecisionEventArgs { Decision = decision });
                            ExecuteDeclaredAbsence(entryList, currentAbsence, saveGame);

                            chainedAbsence = currentAbsence.ChainedAbsence;
                            if (currentAbsence.ChainedAbsence == null) i++;
                            continue;
                        }

                        // Check if team prefers player over declared driver
                        var playerReputation = GetDriverReputation(saveGame.PlayerData.DriverId, saveGame);
                        var declaredDriverReputation = GetDriverReputation(currentAbsence.DriverIn, saveGame);

                        var playerResume = new DriverResume { Id = saveGame.PlayerData.DriverId, Reputation = playerReputation };
                        var declaredDriverResume = new DriverResume { Id = currentAbsence.DriverIn, Reputation = declaredDriverReputation };

                        var winner = _driverHirer.PickWinnerForAbsence(declaredDriverResume, playerResume);

                        if (winner.Id != saveGame.PlayerData.DriverId)
                        {
                            // Team prefers other driver
                            var decision = new AbsenceDecision
                            {
                                DecisionType = AbsenceDecisionType.PlayerRefused,
                                Absence = currentAbsence
                            };
                            AbsenceDecisionMade?.Invoke(this, new AbsenceDecisionEventArgs { Decision = decision });
                            ExecuteDeclaredAbsence(entryList, currentAbsence, saveGame);

                            chainedAbsence = currentAbsence.ChainedAbsence;
                            if (currentAbsence.ChainedAbsence == null) i++;
                            continue;
                        }
                        else
                        {
                            // Create chained absence if player has a team
                            if (saveGame.PlayerData.TeamId != null)
                            {
                                CreateChainedAbsenceForPlayer(entryList, currentAbsence, saveGame);
                            }

                            // Player successfully steps in
                            StepInAbsence(entryList, currentAbsence, saveGame);
                            playerHasSteppedIn = true;

                            var decision = new AbsenceDecision
                            {
                                DecisionType = AbsenceDecisionType.PlayerAccepted,
                                Absence = currentAbsence
                            };
                            AbsenceDecisionMade?.Invoke(this, new AbsenceDecisionEventArgs { Decision = decision });

                            chainedAbsence = currentAbsence.ChainedAbsence;
                            if (currentAbsence.ChainedAbsence == null) i++;
                            continue;
                        }
                    }
                    else
                    {
                        // Player declined
                        ExecuteDeclaredAbsence(entryList, currentAbsence, saveGame);

                        var decision = new AbsenceDecision
                        {
                            DecisionType = AbsenceDecisionType.PlayerDeclined,
                            Absence = currentAbsence
                        };
                        AbsenceDecisionMade?.Invoke(this, new AbsenceDecisionEventArgs { Decision = decision });

                        chainedAbsence = currentAbsence.ChainedAbsence;
                        if (currentAbsence.ChainedAbsence == null) i++;
                        continue;
                    }
                }
                else
                {
                    // No player interaction needed
                    ExecuteDeclaredAbsence(entryList, currentAbsence, saveGame);

                    var decision = new AbsenceDecision
                    {
                        DecisionType = AbsenceDecisionType.AutoExecuted,
                        Absence = currentAbsence
                    };
                    AbsenceDecisionMade?.Invoke(this, new AbsenceDecisionEventArgs { Decision = decision });

                    chainedAbsence = currentAbsence.ChainedAbsence;
                    if (currentAbsence.ChainedAbsence == null) i++;
                    continue;
                }
            }
        }

        private static bool IsPlayerAssignedInChain(Absence absence, string playerId)
        {
            if (absence == null) return false;
            if (absence.DriverIn == playerId) return true;
            return IsPlayerAssignedInChain(absence.ChainedAbsence, playerId);
        }

        private void CreateChainedAbsenceForPlayer(List<EntryListEntry> entryList, Absence currentAbsence, ISaveGame saveGame)
        {
            var employedDriverIds = entryList
                .SelectMany(e => new[] { e.Driver1Id, e.Driver2Id })
                .Distinct()
                .ToHashSet();

            var playerReputation = GetDriverReputation(saveGame.PlayerData.DriverId, saveGame);

            var substituteDriver = saveGame.Drivers
                .Where(d => !employedDriverIds.Contains(d.DriverId) && d.DriverId != saveGame.PlayerData.DriverId)
                .Select(d => new
                {
                    DriverId = d.DriverId,
                    Reputation = GetDriverReputation(d.DriverId, saveGame)
                })
                .Where(d => d.Reputation <= playerReputation)
                .OrderByDescending(d => d.Reputation)
                .FirstOrDefault();

            currentAbsence.ChainedAbsence = new Absence
            {
                DriverOut = saveGame.PlayerData.DriverId,
                RaceId = currentAbsence.RaceId,
                TeamId = saveGame.PlayerData.TeamId,
                DriverIn = substituteDriver?.DriverId ?? ""
            };
        }

        private void ExecuteDeclaredAbsence(List<EntryListEntry> entryList, Absence absence, ISaveGame saveGame)
        {
            var teamEntry = entryList.FirstOrDefault(e => e.TeamId == absence.TeamId);
            if (teamEntry == null) return;
            var numberAllocationService = RaceNumberAllocationFactory.GetRaceNumberAllocationService(saveGame.CurrentSeason.Year);

            var driverInReputation = GetDriverReputation(absence.DriverIn, saveGame);

            if (teamEntry.Driver1Id == absence.DriverOut)
            {
                teamEntry.Driver1Id = absence.DriverIn;
                teamEntry.Driver1Reputation = driverInReputation;
                teamEntry.Driver1Number = numberAllocationService.GetNumberForAbsence(saveGame, absence.DriverIn, teamEntry.Driver1Number);
            }
            else if (teamEntry.Driver2Id == absence.DriverOut)
            {
                teamEntry.Driver2Id = absence.DriverIn;
                teamEntry.Driver2Reputation = driverInReputation;
                teamEntry.Driver2Number = numberAllocationService.GetNumberForAbsence(saveGame, absence.DriverIn, teamEntry.Driver2Number);
            }
        }

        private void StepInAbsence(List<EntryListEntry> entryList, Absence absence, ISaveGame saveGame)
        {
            var teamEntry = entryList.FirstOrDefault(e => e.TeamId == absence.TeamId);
            if (teamEntry == null) return;

            var playerReputation = GetDriverReputation(saveGame.PlayerData.DriverId, saveGame);
            var numberAllocationService = RaceNumberAllocationFactory.GetRaceNumberAllocationService(saveGame.CurrentSeason.Year);

            if (teamEntry.Driver1Id == absence.DriverOut)
            {
                teamEntry.Driver1Id = saveGame.PlayerData.DriverId;
                teamEntry.Driver1Reputation = playerReputation;
                teamEntry.Driver1Number = numberAllocationService.GetNumberForAbsence(saveGame, saveGame.PlayerData.DriverId, teamEntry.Driver1Number);
            }
            else if (teamEntry.Driver2Id == absence.DriverOut)
            {
                teamEntry.Driver2Id = saveGame.PlayerData.DriverId;
                teamEntry.Driver2Reputation = playerReputation;
                teamEntry.Driver2Number = numberAllocationService.GetNumberForAbsence(saveGame, saveGame.PlayerData.DriverId, teamEntry.Driver2Number);
            }
        }

        private DriverReputation GetDriverReputation(string driverId, ISaveGame saveGame)
        {
            var season = saveGame.CurrentSeason.Year;
            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);

            if (driver != null)
            {
                return driver.Reputation;
            }

            return DriverReputation.PRIME_MIDFIELD;
        }

        public bool IsDriverInAnyAbsence(string driverId, List<Absence> absences)
        {
            return absences.Any(a => IsPlayerAssignedInChain(a, driverId));
        }
    }
}