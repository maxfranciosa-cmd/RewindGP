using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Business.Services.RaceNumberSystem.Factory;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    public class EndOfSeasonManager : IEndOfSeasonManager
    {

        private class SeasonRecap
        {
            public int Position { get; set; }
            public int Podiums { get; set; }
            public int DNFs { get; set; }

            public int Races { get; set; }
            public double AverageRacePosition { get; set; }
        }


        private IReputationUpdater _reputationUpdater;
        private IOffSeasonMovements _offSeasonMovements;
        private IRandomDriverGenerator _randomDriverGenerator;
        private DriverHirer _driverHirer;

        public EndOfSeasonManager(IReputationUpdater reputationUpdater, IOffSeasonMovements offSeasonMovements, IRandomDriverGenerator randomDriverGenerator, DriverHirer driverHirer)
        {
            _reputationUpdater = reputationUpdater;
            _offSeasonMovements = offSeasonMovements;
            _randomDriverGenerator = randomDriverGenerator;
            _driverHirer = driverHirer;
        }

        public IEnumerable<DropTeamResult> ExecuteTeamDrops(
               ISaveGame saveGame,
               ISeason newSeason)
        {

            var currentTeamsSituation = GetTeamSituationBeforeDriverMarket(newSeason, saveGame);

            var dropResults = _offSeasonMovements.DropDrivers(currentTeamsSituation).ToList();

            ApplyDisciplinaryDrops(saveGame, dropResults);

            return dropResults;
        }

        // drivers who collected too many team-orders reprimands are let go by their team,
        // unless they're the champion. Retirements and teams quitting still take precedence.
        private void ApplyDisciplinaryDrops(ISaveGame saveGame, List<DropTeamResult> dropResults)
        {
            var reprimands = saveGame.Reprimands ?? new List<Reprimand>();
            if (!reprimands.Any())
                return;

            var championId = saveGame.CurrentDriverStandings?.FirstOrDefault(s => s.Position == 1)?.DriverId;
            var teamsDictionary = saveGame.CurrentSeason.Teams.ToDictionary(t => t.TeamId, t => t);

            DriverFirerOutcome GetOutcome(DriverFirerOutcome current, string teamId, string driverId)
            {
                if (string.IsNullOrEmpty(driverId) || driverId == championId)
                    return current;

                if (current == DriverFirerOutcome.DROPPED_RETIRING || current == DriverFirerOutcome.DROPPED_TEAM_QUITTING)
                    return current;

                var count = reprimands.Count(r => r.DriverId == driverId && r.TeamId == teamId);
                return count >= ReprimandManager.REPRIMANDS_FOR_SEASON_END_DROP ? DriverFirerOutcome.DROPPED_DISCIPLINARY : current;
            }

            foreach (var dropResult in dropResults)
            {
                if (!teamsDictionary.TryGetValue(dropResult.TeamId, out var team))
                    continue;

                dropResult.DropDriver1 = GetOutcome(dropResult.DropDriver1, team.TeamId, team.Driver1Contract?.DriverId);
                dropResult.DropDriver2 = GetOutcome(dropResult.DropDriver2, team.TeamId, team.Driver2Contract?.DriverId);
            }
        }

        public void UpdateDriversPoolForNextSeason(
               int nextSeasonYear,
               ISaveGame saveGame,
               Dictionary<string, IDriverData> driversNewSeasonDictionary
            )
        {
            var result = new List<IDriverData>();
            var currentSeasonYear = saveGame.CurrentSeason.Year;
            var seasonRecapsByDriverId = GenerateSeasonRecaps(saveGame);

            var driversInOldSeasonWithNewSeasonVersion = new List<IDriverData>();
            var newSeasonVersionOfOldDrivers = new List<IDriverData>();

            foreach (var currentDriver in saveGame.Drivers)
            {
                var driverAge = nextSeasonYear - currentDriver.YearOfBirth;
                var currentReputation = currentDriver.Reputation;
                var driverSeasonRecap = seasonRecapsByDriverId.ContainsKey(currentDriver.DriverId) ? seasonRecapsByDriverId[currentDriver.DriverId] : null;

                var newReputation = (driverSeasonRecap == null || driverSeasonRecap.Races == 0) ?
                    _reputationUpdater.GetNewReputationForInactiveDriver(currentReputation, driverAge) :
                    _reputationUpdater.GetNewReputation(currentReputation, driverAge, driverSeasonRecap.Position, driverSeasonRecap.Podiums, driverSeasonRecap.DNFs, driverSeasonRecap.Races, saveGame.CurrentSeason.Races.Count(), driverSeasonRecap.AverageRacePosition);

                if (driversNewSeasonDictionary.TryGetValue(currentDriver.DriverId, out var sameDriverWithNewSeasonRatings))
                {
                    // exists both in current list and new list. clone the driver and just set the new reputation.
                    // the old version of the driver will be replaced.
                    var cloneOfSameDriverWithNewSeasonRating = sameDriverWithNewSeasonRatings.DeepClone();
                    driversInOldSeasonWithNewSeasonVersion.Add(currentDriver);
                    cloneOfSameDriverWithNewSeasonRating.Reputation = newReputation;

                    newSeasonVersionOfOldDrivers.Add(cloneOfSameDriverWithNewSeasonRating);

                    if (sameDriverWithNewSeasonRatings.DriverId == saveGame.PlayerData.DriverId)
                    {
                        saveGame.PlayerData.Name = sameDriverWithNewSeasonRatings.Name;
                        saveGame.PlayerData.Nationality = sameDriverWithNewSeasonRatings.Nationality;
                    }

                    // Remove from dictionary so we don't process it again
                    driversNewSeasonDictionary.Remove(currentDriver.DriverId);

                }
                else
                {
                    // Driver only in current list - update the reputation.
                    currentDriver.Reputation = newReputation;
                }
            }

            // Process the rest of the drivers NOT RETIRED IN THE SAVE FILE (the ones already processed should've been removed)
            var retiredDriverIds = saveGame.RetiredDrivers?.Select(d => d.DriverId).ToHashSet() ?? new HashSet<string>();

            var rookiesList = driversNewSeasonDictionary
                .Values
                .Where(d => !retiredDriverIds.Contains(d.DriverId))
                .Select(d =>
                {
                    // clone the driver
                    var clonedDriver = d.DeepClone();
                    return clonedDriver;
                }) ?? new List<IDriverData>();

            saveGame.Drivers = saveGame.Drivers
                                        .Except(driversInOldSeasonWithNewSeasonVersion)
                                        .Concat(newSeasonVersionOfOldDrivers)
                                        .Concat(rookiesList);

        }

        private Dictionary<string, SeasonRecap> GenerateSeasonRecaps(ISaveGame saveGame)
        {
            var driverStats = saveGame.GrandPrixResults
                        .Where(r => r.Year == saveGame.CurrentSeason.Year)
                        .SelectMany(gp => gp.RaceResults)
                        .GroupBy(result => result.DriverId)
                        .ToDictionary(
                            g => g.Key,
                            g => new
                            {
                                Podiums = g.Count(r => !r.DidNotPreQualify && r.Position <= 3),
                                DNFs = g.Count(r => r.DNF),
                                Races = g.Count(),
                                AverageRacePosition = g.Where(r => !r.DidNotPreQualify)
                                                       .Select(r => (double)r.Position)
                                                       .DefaultIfEmpty(0)
                                                       .Average()
                            }
                        );

            var seasonRecapsByDriverId = saveGame.CurrentDriverStandings.ToDictionary(
                standing => standing.DriverId,
                standing => new SeasonRecap
                {
                    Position = standing.Position,
                    Podiums = driverStats.TryGetValue(standing.DriverId, out var stats) ? stats.Podiums : 0,
                    DNFs = driverStats.TryGetValue(standing.DriverId, out var stats2) ? stats2.DNFs : 0,
                    Races = driverStats.TryGetValue(standing.DriverId, out var stats3) ? stats3.Races : 0,
                    AverageRacePosition = driverStats.TryGetValue(standing.DriverId, out var stats4) ? stats4.AverageRacePosition : 0,
                }
            );

            return seasonRecapsByDriverId;
        }

        private IEnumerable<TeamSituation> GetTeamSituationBeforeDriverMarket(ISeason nextSeason, ISaveGame saveGame)
        {
            var driversDictionary = saveGame.Drivers.ToDictionary(d => d.DriverId, d => d);
            var nextSeasonTeamsDictionary = nextSeason.Teams.ToDictionary(t => t.TeamId, t => t);

            var result = saveGame.CurrentSeason.Teams.Select(e =>
            {
                var driver1 = driversDictionary[e.Driver1Contract.DriverId];
                var hasDriver2 = !string.IsNullOrEmpty(e.Driver2Contract?.DriverId);
                var driver2 = hasDriver2 ? driversDictionary[e.Driver2Contract.DriverId] : null;

                var nextSeasonTeamReputation = nextSeasonTeamsDictionary.GetValueOrDefault(e.TeamId)?.Reputation;

                return new TeamSituation
                {
                    TeamId = e.TeamId,
                    TeamQuitting = (nextSeasonTeamReputation == null),
                    Reputation = nextSeasonTeamReputation ?? e.Reputation,
                    Driver1 = new DriverSituation
                    {
                        DriverId = driver1.DriverId,
                        DriverRetiring = driver1.DriverId != saveGame.PlayerData.DriverId && IsDriverRetiring(nextSeason.Year - driver1.YearOfBirth),
                        RacesLeftInContract = e.Driver1Contract.Races - saveGame.CurrentSeason.Races.Count(),
                        Reputation = driver1.Reputation
                    },
                    Driver2 = !hasDriver2 ? null : new DriverSituation
                    {
                        DriverId = driver2.DriverId,
                        DriverRetiring = driver2.DriverId != saveGame.PlayerData.DriverId && IsDriverRetiring(nextSeason.Year - driver2.YearOfBirth),
                        RacesLeftInContract = e.Driver2Contract.Races - saveGame.CurrentSeason.Races.Count(),
                        Reputation = driver2.Reputation
                    }
                };
            });
            return result;
        }

        // after a team has hired, the driver with the better reputation leads the team.
        // On equal reputations the newly hired driver takes the role the team advertised for.
        private void AssignRolesAfterHiring(ISaveGame saveGame, ITeamEntry teamEntry, TeamHiring driver1Hiring, TeamHiring driver2Hiring)
        {
            if (string.IsNullOrEmpty(teamEntry.Driver2Contract?.DriverId))
            {
                // one-car team: no teammate to give orders to
                teamEntry.Driver1Contract.Role = ContractRole.EQUAL;
                return;
            }

            DriverReputation GetReputation(string driverId, TeamHiring hiring) =>
                saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId)?.Reputation
                ?? hiring?.DriverReputation
                ?? DriverReputation.PRIME_MIDFIELD;

            var driver1Reputation = GetReputation(teamEntry.Driver1Contract.DriverId, driver1Hiring);
            var driver2Reputation = GetReputation(teamEntry.Driver2Contract.DriverId, driver2Hiring);

            bool driver1Leads;
            if (driver1Reputation != driver2Reputation)
                driver1Leads = driver1Reputation > driver2Reputation;
            else if (driver1Hiring != null)
                driver1Leads = driver1Hiring.Role == DriverRole.FIRST_DRIVER;
            else
                driver1Leads = driver2Hiring.Role == DriverRole.SECOND_DRIVER;

            teamEntry.Driver1Contract.Role = driver1Leads ? ContractRole.FIRST_DRIVER : ContractRole.SECOND_DRIVER;
            teamEntry.Driver2Contract.Role = driver1Leads ? ContractRole.SECOND_DRIVER : ContractRole.FIRST_DRIVER;
        }

        private DriverRole GetRoleToHireAlongside(ISaveGame saveGame, DriverContract stayingDriverContract, TeamReputation teamReputation)
        {
            var stayingDriver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == stayingDriverContract.DriverId);
            var stayingDriverReputation = stayingDriver?.Reputation ?? DriverReputation.PRIME_MIDFIELD;
            return DriverHirer.GetRoleToHireAlongside(stayingDriverContract.Role, stayingDriverReputation, teamReputation);
        }

        private const int MIN_RETIRING_AGE = 34;
        private const int MAX_RETIRING_AGE = 42;
        private bool IsDriverRetiring(int age)
        {
            if (age < MIN_RETIRING_AGE) return false;

            if (age > MAX_RETIRING_AGE) return true;

            var possibleRetiringAge = (new Random()).Next(MIN_RETIRING_AGE, MAX_RETIRING_AGE + 1);

            return age >= possibleRetiringAge;
        }

        public IEnumerable<TeamHiringBallot> TeamPicksPotentialReplacementsDrivers(int newSeasonYear, ISaveGame saveGame, IEnumerable<ITeamEntry> newSeasonTeamEntries, IEnumerable<DropTeamResult> dropTeamResults)
        {
            var idsOfRetiringDrivers = new HashSet<string>();
            var dropTeamResltsDictionary = dropTeamResults.ToDictionary(t => t.TeamId, t => t);
            var currentSeasonTeamEntriesDictionary = saveGame.CurrentSeason.Teams.ToDictionary(t => t.TeamId, t => t);
            var driversDictionary = saveGame.Drivers.ToDictionary(d => d.DriverId, d => d);
            var teamJobAds = new List<TeamJobAd>();
            var unemployedDrivers = new List<UnemployedDriver>();

            foreach (var teamEntry in newSeasonTeamEntries)
            {
                // assuming both drivers are needed if the team is not in the dropTeamsResult
                // (because it would be a new team)
                var hiringDriver1 = true;
                var hiringDriver2 = true;

                var driver1Retiring = false;
                var driver2Retiring = false;

                var exitingDriver1WillingToRenew = false;
                var exitingDriver2WillingToRenew = false;

                string driver1Id = null;
                string driver2Id = null;

                if (dropTeamResltsDictionary.ContainsKey(teamEntry.TeamId))
                {
                    var driver1DropOutcome = dropTeamResltsDictionary[teamEntry.TeamId].DropDriver1;
                    var driver2DropOutcome = dropTeamResltsDictionary[teamEntry.TeamId].DropDriver2;

                    driver1Id = currentSeasonTeamEntriesDictionary[teamEntry.TeamId].Driver1Contract.DriverId;
                    driver2Id = currentSeasonTeamEntriesDictionary[teamEntry.TeamId].Driver2Contract?.DriverId;

                    if (driver1DropOutcome == DriverFirerOutcome.DROPPED_RETIRING)
                    {
                        idsOfRetiringDrivers.Add(driver1Id);
                        // if the driver is retiring we are marking them as processed as it won't go to the unemployment list
                        driversDictionary.Remove(driver1Id);
                    }
                    else if (driver1DropOutcome == DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED)
                    {
                        // if the driver contract's expired, it's a coin toss to decide
                        // if team and driver are willing to renew
                        exitingDriver1WillingToRenew = (new Random().Next(2)) == 1;
                    }
                    else if (!driver1DropOutcome.IsDropped())
                    {
                        // if the driver stays in the team we are marking them as processed as it won't go to the unenployment list
                        driversDictionary.Remove(driver1Id);
                    }

                    // a team with no second car this season has an empty driver2Id - there's no
                    // driver to retire/renew/remove from the pool, and it never gets a SECOND_DRIVER
                    // job ad below (hiringDriver2 stays false, since NOT_DROPPED.IsDropped() == false).
                    if (!string.IsNullOrEmpty(driver2Id))
                    {
                        if (driver2DropOutcome == DriverFirerOutcome.DROPPED_RETIRING)
                        {
                            idsOfRetiringDrivers.Add(driver2Id);
                            // if the driver is retiring we are marking them as processed as it won't go to the unemployment list
                            driversDictionary.Remove(driver2Id);
                        }
                        else if (driver2DropOutcome == DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED)
                        {
                            // if the driver contract's expired, it's a coin toss to decide
                            // if team and driver are willing to renew
                            exitingDriver2WillingToRenew = (new Random().Next(2)) == 1;
                        }
                        else if (!driver2DropOutcome.IsDropped())
                        {
                            // if the driver stays in the team we are marking them as processed as it won't go to the unenployment list
                            driversDictionary.Remove(driver2Id);
                        }
                    }

                    hiringDriver1 = driver1DropOutcome.IsDropped();
                    hiringDriver2 = !string.IsNullOrEmpty(driver2Id) && driver2DropOutcome.IsDropped();
                }

                // with both seats open the team looks for a leader and a wingman; with only one seat
                // open it looks for whatever complements the driver who's staying
                var driver1Role = DriverRole.FIRST_DRIVER;
                var driver2Role = DriverRole.SECOND_DRIVER;
                var currentTeamEntry = currentSeasonTeamEntriesDictionary.GetValueOrDefault(teamEntry.TeamId);

                if (currentTeamEntry != null && hiringDriver1 && !hiringDriver2 && !string.IsNullOrEmpty(driver2Id))
                {
                    driver1Role = GetRoleToHireAlongside(saveGame, currentTeamEntry.Driver2Contract, teamEntry.Reputation);
                }
                else if (currentTeamEntry != null && hiringDriver2 && !hiringDriver1)
                {
                    driver2Role = GetRoleToHireAlongside(saveGame, currentTeamEntry.Driver1Contract, teamEntry.Reputation);
                }

                if (hiringDriver1)
                {
                    teamJobAds.Add(new TeamJobAd
                    {
                        TeamId = teamEntry.TeamId,
                        TeamReputation = teamEntry.Reputation,
                        Role = driver1Role,
                        Slot = 1,
                        ExitingDriverId = driver1Id,
                        ExitingDriverWillingToRenew = exitingDriver1WillingToRenew
                    });
                }

                if (hiringDriver2)
                {
                    teamJobAds.Add(new TeamJobAd
                    {
                        TeamId = teamEntry.TeamId,
                        TeamReputation = teamEntry.Reputation,
                        Role = driver2Role,
                        Slot = 2,
                        ExitingDriverId = driver2Id,
                        ExitingDriverWillingToRenew = exitingDriver2WillingToRenew
                    });
                }
            }

            // mark the rest of the drivers in the dictionary (those not hired) into the unemployment list
            var unprocessedDrivers = driversDictionary.Values.Select(d => new UnemployedDriver
            {
                DriverId = d.DriverId,
                Reputation = d.Reputation
            });
            unemployedDrivers.AddRange(unprocessedDrivers);

            // remove retiring drivers from the drivers pool and add them to the retired drivers' list
            var retiredDrivers = saveGame.Drivers.Where(d => idsOfRetiringDrivers.Contains(d.DriverId)).Cast<DriverData>().ToList();
            saveGame.Drivers = saveGame.Drivers.Except(retiredDrivers);
            saveGame.RetiredDrivers = saveGame.RetiredDrivers == null ? retiredDrivers : saveGame.RetiredDrivers.Concat(retiredDrivers);

            var teamProvisionalHirings = _offSeasonMovements.PickPotentialNewDrivers(teamJobAds, unemployedDrivers, out var jobAdsUnfulfilled);

            var provisionalHiringsWithGeneratedDrivers = new List<TeamHiring>();
            var newDrivers = new List<IDriverData>();
            foreach (var job in jobAdsUnfulfilled)
            {
                var newDriver = _randomDriverGenerator.GenerateDriver(saveGame.Drivers, newSeasonYear);
                newDrivers.Add(newDriver);
                var teamProvisionalHiring = new TeamHiring()
                {
                    DriverId = newDriver.DriverId,
                    DriverReputation = newDriver.Reputation,
                    ExcludeDriverIds = (!string.IsNullOrEmpty(job.ExitingDriverId) && !job.ExitingDriverWillingToRenew) ? new HashSet<string> { job.ExitingDriverId } : new HashSet<string> { },
                    Role = job.Role,
                    Slot = job.Slot,
                    TeamId = job.TeamId,
                    TeamReputation = job.TeamReputation
                };
                provisionalHiringsWithGeneratedDrivers.Add(teamProvisionalHiring);
            }
            saveGame.Drivers = saveGame.Drivers.Concat(newDrivers);

            // exclude the player from the unemployed drivers the player (they will choose on a specific screen)
            unemployedDrivers.RemoveAll(d => d.DriverId == saveGame.PlayerData.DriverId);

            return _offSeasonMovements.DriversProposeToTeams(unemployedDrivers, teamProvisionalHirings.Concat(provisionalHiringsWithGeneratedDrivers));

        }

        public ISeason GenerateNewSeasonWithNewHirings(ISaveGame saveGame, ISeason newSeason, IEnumerable<TeamHiringBallot> ballots)
        {
            var oldTeamEntries = saveGame.CurrentSeason.Teams.ToDictionary(t => t.TeamId, t => t);
            var newSeasonResult = newSeason.DeepClone();

            var teamHiringResultDictionary = _offSeasonMovements
                                                .FinalBallotResults(ballots)
                                                .GroupBy(b => b.TeamId)
                                                .ToDictionary(g => g.Key, g => g.AsEnumerable());

            var teamEntriesDictionary = newSeasonResult.Teams.ToDictionary(t => t.TeamId, t => t);

            var employedDriversIds = new Dictionary<string, string>();
            
            foreach (var teamEntry in newSeasonResult.Teams)
            {
                var currentSeasonTeam = oldTeamEntries.GetValueOrDefault(teamEntry.TeamId);
                var hirings = teamHiringResultDictionary.GetValueOrDefault(teamEntry.TeamId) ?? new List<TeamHiring>();
                
                // DRIVER 1 SEAT
                var driver1Hiring = hirings.FirstOrDefault(h => h.GetSlot() == 1);

                if (driver1Hiring != null)
                {
                    // Hired - use ballot result
                    teamEntry.Driver1Contract.DriverId = driver1Hiring.DriverId;
                    var firstDriverFit = _driverHirer.DoesDriverFitTeamPolicy(driver1Hiring.DriverReputation, driver1Hiring.Role, teamEntry.Reputation);
                    teamEntry.Driver1Contract.Races = firstDriverFit >= DriverHirer.DriverPolicyFit.PerfectFit ? newSeason.Races.Count() + 1 : newSeason.Races.Count();
                }
                else if (currentSeasonTeam != null)
                {
                    teamEntry.Driver1Contract.DriverId = currentSeasonTeam.Driver1Contract.DriverId;
                    teamEntry.Driver1Contract.Races = currentSeasonTeam.Driver1Contract.Races - saveGame.CurrentSeason.Races.Count();
                    teamEntry.Driver1Contract.Role = currentSeasonTeam.Driver1Contract.Role;
                }

                // DRIVER 2 SEAT
                var driver2Hiring = hirings.FirstOrDefault(h => h.GetSlot() == 2);

                if (driver2Hiring != null)
                {
                    // Hired - use ballot result
                    teamEntry.Driver2Contract.DriverId = driver2Hiring.DriverId;
                    var secondDriverFit = _driverHirer.DoesDriverFitTeamPolicy(driver2Hiring.DriverReputation, driver2Hiring.Role, teamEntry.Reputation);
                    teamEntry.Driver2Contract.Races = secondDriverFit >= DriverHirer.DriverPolicyFit.PerfectFit ? newSeason.Races.Count() + 1 : newSeason.Races.Count();
                }
                else if (currentSeasonTeam != null)
                {
                    teamEntry.Driver2Contract.DriverId = currentSeasonTeam.Driver2Contract.DriverId;
                    teamEntry.Driver2Contract.Races = currentSeasonTeam.Driver2Contract.Races - saveGame.CurrentSeason.Races.Count();
                    teamEntry.Driver2Contract.Role = currentSeasonTeam.Driver2Contract.Role;
                }

                if (driver1Hiring != null || driver2Hiring != null)
                {
                    AssignRolesAfterHiring(saveGame, teamEntry, driver1Hiring, driver2Hiring);
                }

                employedDriversIds.Add(teamEntry.Driver1Contract.DriverId, teamEntry.TeamId);
                // teams with no second car this season have an empty Driver2Contract.DriverId;
                // skip so multiple one-car teams don't collide on the same "" dictionary key
                if (!string.IsNullOrEmpty(teamEntry.Driver2Contract?.DriverId))
                {
                    employedDriversIds.Add(teamEntry.Driver2Contract.DriverId, teamEntry.TeamId);
                }
            }

            // the season pack may not define roles: make sure none is left undefined
            DriverHirer.AssignUndefinedRoles(newSeasonResult, saveGame.Drivers);

            saveGame.PlayerData.TeamId = employedDriversIds.ContainsKey(saveGame.PlayerData.DriverId) ? employedDriversIds[saveGame.PlayerData.DriverId] : null;

            var unemployedDrivers = saveGame
                                        .Drivers
                                        .Where(d => !employedDriversIds.ContainsKey(d.DriverId))
                                        .Select(d => new { Driver = d, d.Reputation })
                                        .OrderByDescending(d => d.Reputation);

            var unemployedDriversReputations = unemployedDrivers.ToDictionary(d => d.Driver.DriverId, d => d.Reputation);

            var absencesToKeep = new List<Absence>();
            // check for the absences
            foreach (var absencesForRace in newSeasonResult.Absences.GroupBy(a => a.RaceId))
            {
                var availableUnemployedDriversForThisRace = unemployedDrivers.Select(d => d.Driver.DriverId).ToHashSet();
                var raceId = absencesForRace.Key;
                foreach (var absence in absencesForRace)
                {
                    Absence parentAbsence = null;
                    var recursiveAbsence = absence;
                    var keepAbsence = true;
                    while (recursiveAbsence != null && keepAbsence)
                    {
                        if (teamEntriesDictionary[recursiveAbsence.TeamId].Driver1Contract.DriverId == recursiveAbsence.DriverOut ||
                            teamEntriesDictionary[recursiveAbsence.TeamId].Driver2Contract.DriverId == recursiveAbsence.DriverOut)

                        {
                            var isEmployed = employedDriversIds.ContainsKey(recursiveAbsence.DriverIn);
                            var wasPreviouslyEmployed = recursiveAbsence.ChainedAbsence != null;

                            if (isEmployed && wasPreviouslyEmployed)
                            {
                                // if is already employed, no problem. absence can stay,
                                // but we need to check the chained absence
                                // (as the driver is leaving a team)
                                parentAbsence = recursiveAbsence;
                                recursiveAbsence = recursiveAbsence.ChainedAbsence;
                                continue;
                            }

                            var isUnemployed = unemployedDriversReputations.ContainsKey(recursiveAbsence.DriverIn);

                            if (isUnemployed)
                            {
                                var driverIn = recursiveAbsence.DriverIn;
                                // there's no need for a chained absence
                                // (as the driver potentially is not leaving any teams)
                                recursiveAbsence = null;
                                // if its unemployed AND not yet taken by other absences for that race, the absence can stay
                                if (availableUnemployedDriversForThisRace.Contains(driverIn))
                                {
                                    availableUnemployedDriversForThisRace.Remove(driverIn);

                                    continue;
                                }
                            }

                            // we're going to be here ONLY if
                            // 1) the driver is NOT AVAILABLE (not in employed AND not in unemployed lists)
                            // OR
                            // 2) the driver is unemployed but already picked up by someone else
                            //
                            // this means we need to try and find another substitute from the unemployment pool.
                            var teamReputation = teamEntriesDictionary[recursiveAbsence.TeamId].Reputation;
                            var potentialPick = unemployedDrivers.FirstOrDefault(d => d.Reputation <= DriverHirer.teamAbsenceSubstitutionMaxReputation[teamReputation] && availableUnemployedDriversForThisRace.Contains(d.Driver.DriverId));

                            // if we can't find it, we will need to remove the whole chain of absences
                            if (potentialPick == null)
                            {
                                keepAbsence = false;
                            }
                            else
                            {
                                parentAbsence = recursiveAbsence;
                                recursiveAbsence.DriverIn = potentialPick.Driver.DriverId;
                                recursiveAbsence = recursiveAbsence.ChainedAbsence;
                                availableUnemployedDriversForThisRace.Remove(potentialPick.Driver.DriverId);
                            }
                        }
                        else
                        {
                            // the driver out doesn't run for the team anymore
                            //
                            // if DOES HAVE A PARENT ABSCENCE
                            //  change the parent absence and assign an unemployed driver (and remove the chained absence)
                            // else
                            //  we should remove the absence
                            if (parentAbsence != null)
                            {
                                var teamReputation = teamEntriesDictionary[parentAbsence.TeamId].Reputation;
                                var potentialPick = unemployedDrivers.FirstOrDefault(d => d.Reputation <= DriverHirer.teamAbsenceSubstitutionMaxReputation[teamReputation] && availableUnemployedDriversForThisRace.Contains(d.Driver.DriverId));

                                // if we can't find it, we will need to remove the whole chain of absences
                                if (potentialPick == null)
                                {
                                    keepAbsence = false;
                                }
                                else
                                {
                                    parentAbsence.DriverIn = potentialPick.Driver.DriverId;
                                    parentAbsence.ChainedAbsence = null;
                                    parentAbsence = recursiveAbsence;
                                    recursiveAbsence = recursiveAbsence.ChainedAbsence;
                                    availableUnemployedDriversForThisRace.Remove(potentialPick.Driver.DriverId);
                                }
                            }
                            else
                            {
                                keepAbsence = false;
                                parentAbsence = recursiveAbsence;
                                recursiveAbsence = recursiveAbsence.ChainedAbsence;
                            }
                        }
                    }

                    if (keepAbsence) absencesToKeep.Add(absence);
                }

            }

            newSeasonResult.Absences = absencesToKeep;

            return newSeasonResult;
        }

        public void StartNewSeason(ISaveGame saveGame, ISeason newSeason)
        {
            var driverNamesDictionary = saveGame.Drivers.Union(saveGame.RetiredDrivers ?? new List<IDriverData>()).ToDictionary(d => d.DriverId, d => d.Name);
            var teamNamesDictionary = saveGame.CurrentSeason.Teams.ToDictionary(t => t.TeamId, t => t.TeamName);

            // Carry forward the race numbers teams actually raced with this season, so a team the
            // race-number allocator leaves untouched (e.g. no champion swap involves it) keeps its
            // real number instead of reverting to the season pack's authored default for next year.
            var previousTeamNumbers = saveGame.CurrentSeason.Teams
                .ToDictionary(t => t.TeamId, t => (t.Driver1Contract.DriverNumber, t.Driver2Contract.DriverNumber));

            foreach (var team in newSeason.Teams)
            {
                if (previousTeamNumbers.TryGetValue(team.TeamId, out var numbers))
                {
                    team.Driver1Contract.DriverNumber = numbers.Item1;
                    team.Driver2Contract.DriverNumber = numbers.Item2;
                }
            }

            var historicalDriverStanding = saveGame.CurrentDriverStandings.Select(e =>
                                            new HisoricalDriverStandingEntry
                                            {
                                                Points = e.Points,
                                                Position = e.Position,
                                                PositionsTally = e.PositionsTally,
                                                TeamId = e.TeamId,
                                                DriverId = e.DriverId,
                                                DriverName = driverNamesDictionary[e.DriverId],
                                                TeamName = e.TeamId == null ? "No Team" : teamNamesDictionary[e.TeamId]
                                            }).ToList();

            var historicalTeamStanding = saveGame.CurrentConstructorStandings.Select(e =>
                                new HistoricalConstructorStandingEntry
                                {
                                    Points = e.Points,
                                    Position = e.Position,
                                    PositionsTally = e.PositionsTally,
                                    TeamId = e.TeamId,
                                    TeamName = teamNamesDictionary[e.TeamId]
                                }).ToList();

            saveGame.HistoricalDriverStandings = saveGame.HistoricalDriverStandings.Concat(new[] { new HistoricalDriverStanding { Year = saveGame.CurrentSeason.Year, Standing = historicalDriverStanding } });
            saveGame.HistoricalConstructorStandings = saveGame.HistoricalConstructorStandings.Concat(new[] { new HistoricalConstructorStanding { Year = saveGame.CurrentSeason.Year, Standing = historicalTeamStanding } });
            saveGame.CurrentDriverStandings = InitializeDriverStandings(newSeason);
            saveGame.CurrentConstructorStandings = InitializeConstructorStandings(newSeason);
            saveGame.NextGpIndex = 0;
            saveGame.CurrentSeason = newSeason;
            saveGame.Reprimands = new List<Reprimand>();

            // every team starts the new season with its full allowance of improvement packages
            foreach (var team in newSeason.Teams)
            {
                team.ImprovementPackages = new List<ImprovementPackage>();
            }

            // reassign the race numbers
            var raceNumberSystem = RaceNumberAllocationFactory.GetRaceNumberAllocationService(newSeason.Year);
            raceNumberSystem.AssignNumbersToCurrentSeason(saveGame);

        }

        private List<HistoricalDriverStandingEntry> InitializeDriverStandings(ISeason season)
        {
            var standings = new List<HistoricalDriverStandingEntry>();
            int position = 1;

            foreach (var team in season.Teams)
            {
                standings.Add(new HistoricalDriverStandingEntry
                {
                    Position = position++,
                    DriverId = team.Driver1Contract.DriverId,
                    TeamId = team.TeamId,
                    Points = 0,
                    PositionsTally = new PositionsTally()
                });

                if (!string.IsNullOrEmpty(team.Driver2Contract?.DriverId))
                {
                    standings.Add(new HistoricalDriverStandingEntry
                    {
                        Position = position++,
                        DriverId = team.Driver2Contract.DriverId,
                        TeamId = team.TeamId,
                        Points = 0,
                        PositionsTally = new PositionsTally()
                    });
                }
            }

            return standings;
        }

        private List<ConstructorStandingEntry> InitializeConstructorStandings(ISeason season)
        {
            var standings = new List<ConstructorStandingEntry>();
            int position = 1;

            foreach (var team in season.Teams)
            {
                standings.Add(new ConstructorStandingEntry
                {
                    Position = position++,
                    TeamId = team.TeamId,
                    Points = 0,
                    PositionsTally = new PositionsTally()
                });
            }

            return standings;
        }
    }
}