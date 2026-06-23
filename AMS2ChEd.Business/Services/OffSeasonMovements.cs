using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;
using System.Reflection;

namespace AMS2ChEd.Business.Services
{
    public class OffSeasonMovements : IOffSeasonMovements
    {
        readonly Dictionary<DriverReputation, Ambition> driverAmbitions = new()
        {
            {  DriverReputation.PAY_DRIVER_WILD_CARD , new() { MaxReputation = TeamReputation.MINNOW , MinReputation = TeamReputation.SUPER_MINNOW } },
            {  DriverReputation.PAY_DRIVER_SEASON , new() { MaxReputation = TeamReputation.MIDFIELD , MinReputation = TeamReputation.SUPER_MINNOW } },
            {  DriverReputation.AGEING_MIDFIELD , new() { MaxReputation = TeamReputation.MIDFIELD , MinReputation = TeamReputation.SUPER_MINNOW } },
            {  DriverReputation.YOUNG_TALENT , new() { MaxReputation = TeamReputation.MIDFIELD , MinReputation = TeamReputation.SUPER_MINNOW } },
            {  DriverReputation.PRIME_MIDFIELD , new() { MaxReputation = TeamReputation.MIDFIELD_HIGH , MinReputation = TeamReputation.MINNOW } },
            {  DriverReputation.AGEING_STRONG_MIDFIELD , new() { MaxReputation = TeamReputation.MIDFIELD_HIGH , MinReputation = TeamReputation.MINNOW } },
            {  DriverReputation.JUST_ONE_LAST_DANCE , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD } },
            {  DriverReputation.PRIME_STRONG_MIDFIELD , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MINNOW } },
            {  DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD } },
            {  DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD } },
            {  DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD } },
            {  DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD } },
            {  DriverReputation.AGEING_CHAMPIONSHIP_LEVEL , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD } },
            {  DriverReputation.PRIME_CHAMPIONSHIP_LEVEL , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD_HIGH } },
            {  DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD_HIGH } },
        };

        DriverFirer _driverFirer;
        DriverHirer _driverHirer;

        public OffSeasonMovements(DriverFirer driverFirer,DriverHirer driverHirer)
        {
            _driverFirer = driverFirer;
            _driverHirer = driverHirer;
        }

        public IEnumerable<DropTeamResult> DropDrivers(IEnumerable<TeamSituation> teams)
        {
            return teams.Select(team => new DropTeamResult
            {
                TeamId = team.TeamId,
                DropDriver1 = _driverFirer.WillDropDriver(team.Reputation, team.Driver1.Reputation, team.Driver1.RacesLeftInContract, team.Driver1.DriverRetiring, team.TeamQuitting),
                DropDriver2 = _driverFirer.WillDropDriver(team.Reputation, team.Driver2.Reputation, team.Driver2.RacesLeftInContract, team.Driver2.DriverRetiring, team.TeamQuitting),
            });
        }



        public IEnumerable<TeamHiring> PickPotentialNewDrivers(IEnumerable<TeamJobAd> jobAds,  IEnumerable<UnemployedDriver> poolOfDrivers, out List<TeamJobAd> adsWithNoPotentialCandidates)
        { 
            adsWithNoPotentialCandidates = new List<TeamJobAd>();
            var result = new List<TeamHiring>();
            var rnd = new Random();
            var orderJobAdOrderedByReputationAndRandomly = jobAds
                                                    .GroupBy(x => x.TeamReputation)
                                                    .OrderByDescending(g => g.Key)
                                                    .SelectMany(g => g.OrderBy(x => rnd.Next()))
                                                    .ToList();

            var availableDrivers = poolOfDrivers
                                        .GroupBy(x => x.Reputation)
                                        .OrderByDescending(g => g.Key)
                                        .SelectMany(g => g.OrderBy(x => rnd.Next()))
                                        .Select(g => new DriverResume()
                                        {
                                            Id = g.DriverId,
                                            Reputation = g.Reputation
                                        })
                                        .ToList();

            foreach (var teamJobAd in orderJobAdOrderedByReputationAndRandomly)
            {
                var driverIdsToExclude = new HashSet<string>();
                // if the ExitingDriverid is not null (so it's an ad for an exiting driver and not for a new team with no drivers before)
                // AND is not willing to renew, exclude from contention
                if (!string.IsNullOrEmpty(teamJobAd.ExitingDriverId) && !teamJobAd.ExitingDriverWillingToRenew)
                {
                    driverIdsToExclude.Add(teamJobAd.ExitingDriverId);
                }

                var availableDriversForThatTeam = availableDrivers
                                                    .Where(d => !driverIdsToExclude.Contains(d.Id) && driverAmbitions[d.Reputation].MinReputation <= teamJobAd.TeamReputation)
                                                    .OrderByDescending(d => d.Reputation)
                                                    .ToList();

                if(availableDriversForThatTeam.Any())
                {
                    var winner = _driverHirer.PickBestCandidate(availableDriversForThatTeam, teamJobAd.Role, teamJobAd.TeamReputation);

                    result.Add(new TeamHiring()
                    {
                        DriverId = winner.Id,
                        Role = teamJobAd.Role,
                        TeamId = teamJobAd.TeamId,
                        DriverReputation = winner.Reputation,
                        TeamReputation = teamJobAd.TeamReputation,
                        ExcludeDriverIds = driverIdsToExclude,
                        OtherPotentialCandidates = availableDriversForThatTeam
                    });

                    availableDrivers.Remove(winner);
                } 
                else
                {
                    adsWithNoPotentialCandidates.Add(teamJobAd);
                }
            }

            return result;

        }


        public IEnumerable<TeamHiringBallot> DriversProposeToTeams(IEnumerable<UnemployedDriver> poolOfDrivers,IEnumerable<TeamHiring> currentTeamPickings)
        {
            var result = currentTeamPickings.Select(h => new Tuple<TeamHiring,List<TeamHiringBallotCandidate>>(h,new List<TeamHiringBallotCandidate>())).ToList();

            foreach(var unenployedDriver in poolOfDrivers)
            {
                var maxAmbition = driverAmbitions[unenployedDriver.Reputation].MaxReputation;
                var minAmbition = driverAmbitions[unenployedDriver.Reputation].MinReputation;

                var potentialTeams = result.Where(r => r.Item1.TeamReputation <= maxAmbition && r.Item1.TeamReputation >= minAmbition);

                foreach (var potentialTeam in potentialTeams)
                {
                    potentialTeam.Item2.Add(new TeamHiringBallotCandidate { DriverId = unenployedDriver.DriverId, DriverReputation = unenployedDriver.Reputation });
                }
            }

            return result.Select(t => new TeamHiringBallot { OriginalTeamHiring = t.Item1, Candidates = t.Item2 });

        }

        public IEnumerable<TeamHiring> FinalBallotResults(IEnumerable<TeamHiringBallot> ballots)
        {
            var result = new List<TeamHiring>();
            var rnd = new Random();
            var hiredDriversId = new HashSet<string>();

            foreach(var ballot in ballots.OrderByDescending(b => b.OriginalTeamHiring.TeamReputation))
            {
                var availableCandidates = ballot.Candidates.Where(d => !hiredDriversId.Contains(d.DriverId));

                if (!availableCandidates.Any())
                {
                    if (!hiredDriversId.Contains(ballot.OriginalTeamHiring.DriverId))
                    {
                        result.Add(ballot.OriginalTeamHiring);
                        hiredDriversId.Add(ballot.OriginalTeamHiring.DriverId);
                    } 
                    else
                    {
                        var firstAvailableDriverOfTheSameReputationOrLower = ballot.OriginalTeamHiring.OtherPotentialCandidates
                                                                                                        .Where(d => !hiredDriversId.Contains(d.Id) && 
                                                                                                         d.Reputation <= ballot.OriginalTeamHiring.DriverReputation)
                                                                                                        .OrderByDescending(d => d.Reputation).First();
                        result.Add(new TeamHiring
                        {
                            Role = ballot.OriginalTeamHiring.Role,
                            TeamId = ballot.OriginalTeamHiring.TeamId,
                            TeamReputation = ballot.OriginalTeamHiring.TeamReputation,
                            DriverId = firstAvailableDriverOfTheSameReputationOrLower.Id,
                            DriverReputation = firstAvailableDriverOfTheSameReputationOrLower.Reputation
                        });
                        hiredDriversId.Add(firstAvailableDriverOfTheSameReputationOrLower.Id);
                    }
                    continue;
                }

                var bestCandidateResume = availableCandidates
                                            .GroupBy(x => x.DriverReputation)
                                            .OrderByDescending(g => g.Key)
                                            .SelectMany(g => g.OrderBy(x => rnd.Next()))
                                            .Select(g => new DriverResume { Id = g.DriverId, Reputation = g.DriverReputation })
                                            .OrderBy(d => ballot.OriginalTeamHiring.ExcludeDriverIds?.Contains(d.Id))
                                            .First();

                var originalHireResume = ballot.OriginalTeamHiring == null || ballot.OriginalTeamHiring.DriverId == null ? null : new DriverResume { Id = ballot.OriginalTeamHiring.DriverId, Reputation = ballot.OriginalTeamHiring.DriverReputation };

                var winner = _driverHirer.PickWinner(originalHireResume, bestCandidateResume);

                if (winner == originalHireResume && !hiredDriversId.Contains(winner.Id))
                {
                    result.Add(ballot.OriginalTeamHiring);
                    hiredDriversId.Add(originalHireResume.Id);
                }
                else
                {
                    result.Add(new TeamHiring
                    {
                        Role = ballot.OriginalTeamHiring.Role,
                        TeamId = ballot.OriginalTeamHiring.TeamId,
                        TeamReputation = ballot.OriginalTeamHiring.TeamReputation,
                        DriverId = bestCandidateResume.Id,
                        DriverReputation = bestCandidateResume.Reputation
                    });
                    hiredDriversId.Add(bestCandidateResume.Id);
                }
            }

            return result;
        }
    }
}
