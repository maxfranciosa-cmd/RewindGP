using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;
using System.Reflection;

namespace AMS2ChEd.Business.Services
{
    public class OffSeasonMovements : IOffSeasonMovements
    {
        internal static readonly Dictionary<DriverReputation, Ambition> driverAmbitions = new()
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
            {  DriverReputation.AGEING_CHAMPIONSHIP_LEVEL , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD_HIGH } },
            {  DriverReputation.PRIME_CHAMPIONSHIP_LEVEL , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD_HIGH } },
            {  DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD_HIGH } },
        };

        // the teams at which each reputation would accept a reserve role: a step up from where they
        // would race, as standing by at a better team is worth it (a YOUNG_TALENT aims at top teams)
        internal static readonly Dictionary<DriverReputation, Ambition> reserveAmbitions = new()
        {
            {  DriverReputation.PAY_DRIVER_WILD_CARD , new() { MaxReputation = TeamReputation.MIDFIELD , MinReputation = TeamReputation.SUPER_MINNOW } },
            {  DriverReputation.PAY_DRIVER_SEASON , new() { MaxReputation = TeamReputation.MIDFIELD_HIGH , MinReputation = TeamReputation.MINNOW } },
            {  DriverReputation.AGEING_MIDFIELD , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD } },
            {  DriverReputation.YOUNG_TALENT , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD } },
            {  DriverReputation.PRIME_MIDFIELD , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD_HIGH } },
            {  DriverReputation.AGEING_STRONG_MIDFIELD , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.MIDFIELD_HIGH } },
            {  DriverReputation.JUST_ONE_LAST_DANCE , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.TOP_TEAM } },
            {  DriverReputation.PRIME_STRONG_MIDFIELD , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.TOP_TEAM } },
            {  DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.TOP_TEAM } },
            {  DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.TOP_TEAM } },
            {  DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.TOP_TEAM } },
            {  DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.TOP_TEAM } },
            {  DriverReputation.AGEING_CHAMPIONSHIP_LEVEL , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.TOP_TEAM } },
            {  DriverReputation.PRIME_CHAMPIONSHIP_LEVEL , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.TOP_TEAM } },
            {  DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL , new() { MaxReputation = TeamReputation.TOP_TEAM , MinReputation = TeamReputation.TOP_TEAM } },
        };

        public static Ambition AmbitionFor(DriverReputation reputation, bool isReserve) =>
            isReserve ? reserveAmbitions[reputation] : driverAmbitions[reputation];

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
                // a team with no second car this season has a null Driver2 situation - there's
                // no driver to evaluate for dropping, so it's simply never dropped.
                DropDriver2 = team.Driver2 == null
                    ? DriverFirerOutcome.NOT_DROPPED
                    : _driverFirer.WillDropDriver(team.Reputation, team.Driver2.Reputation, team.Driver2.RacesLeftInContract, team.Driver2.DriverRetiring, team.TeamQuitting),
            });
        }



        public IEnumerable<TeamHiring> PickPotentialNewDrivers(IEnumerable<TeamJobAd> jobAds,  IEnumerable<UnemployedDriver> poolOfDrivers, out List<TeamJobAd> adsWithNoPotentialCandidates)
        { 
            adsWithNoPotentialCandidates = new List<TeamJobAd>();
            var result = new List<TeamHiring>();
            var rnd = new Random();
            // race seats first, then reserve seats, so a reserve pick never takes a race seat's driver
            var orderJobAdOrderedByReputationAndRandomly = jobAds
                                                    .GroupBy(x => (x.IsReserve, x.TeamReputation))
                                                    .OrderBy(g => g.Key.IsReserve)
                                                    .ThenByDescending(g => g.Key.TeamReputation)
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
                                                    .Where(d => !driverIdsToExclude.Contains(d.Id) && AmbitionFor(d.Reputation, teamJobAd.IsReserve).MinReputation <= teamJobAd.TeamReputation)
                                                    .OrderByDescending(d => d.Reputation)
                                                    .ToList();

                if(availableDriversForThatTeam.Any())
                {
                    // a reserve seat goes to the driver the season pack names, when they're free
                    var winner = availableDrivers.FirstOrDefault(d => teamJobAd.PreferredDriverId != null && d.Id == teamJobAd.PreferredDriverId)
                                 ?? _driverHirer.PickBestCandidate(availableDriversForThatTeam, teamJobAd.Role, teamJobAd.TeamReputation, teamJobAd.IsReserve);

                    result.Add(new TeamHiring()
                    {
                        DriverId = winner.Id,
                        Role = teamJobAd.Role,
                        Slot = teamJobAd.Slot,
                        TeamId = teamJobAd.TeamId,
                        DriverReputation = winner.Reputation,
                        TeamReputation = teamJobAd.TeamReputation,
                        ExcludeDriverIds = driverIdsToExclude,
                        OtherPotentialCandidates = availableDriversForThatTeam
                    });

                    availableDrivers.Remove(winner);
                }
                else if (!teamJobAd.IsReserve)
                {
                    // nobody for a race seat: a driver gets generated for it. A reserve seat
                    // nobody wants simply stays empty
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
                bool WithinAmbition(TeamHiring hiring)
                {
                    var ambition = AmbitionFor(unenployedDriver.Reputation, hiring.IsReserve);
                    return hiring.TeamReputation <= ambition.MaxReputation && hiring.TeamReputation >= ambition.MinReputation;
                }

                var potentialTeams = result.Where(r => WithinAmbition(r.Item1));

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

            // every race seat is settled before the reserve seats, so nobody ends up holding both
            foreach(var ballot in ballots.OrderBy(b => b.OriginalTeamHiring.IsReserve).ThenByDescending(b => b.OriginalTeamHiring.TeamReputation))
            {
                var isReserve = ballot.OriginalTeamHiring.IsReserve;
                var availableCandidates = ballot.Candidates.Where(d => !hiredDriversId.Contains(d.DriverId));

                if (ballot.OriginalTeamHiring.Guaranteed &&
                    !string.IsNullOrEmpty(ballot.OriginalTeamHiring.DriverId) &&
                    !hiredDriversId.Contains(ballot.OriginalTeamHiring.DriverId))
                {
                    result.Add(ballot.OriginalTeamHiring);
                    hiredDriversId.Add(ballot.OriginalTeamHiring.DriverId);
                    continue;
                }

                if (!availableCandidates.Any())
                {
                    var originalHireAvailable = isReserve
                        ? !string.IsNullOrEmpty(ballot.OriginalTeamHiring.DriverId) && !hiredDriversId.Contains(ballot.OriginalTeamHiring.DriverId)
                        : !hiredDriversId.Contains(ballot.OriginalTeamHiring.DriverId);

                    if (originalHireAvailable)
                    {
                        result.Add(ballot.OriginalTeamHiring);
                        hiredDriversId.Add(ballot.OriginalTeamHiring.DriverId);
                    }
                    else if (isReserve)
                    {
                        // the team's pick signed elsewhere: the next one down, or no reserve at all
                        var nextPick = (ballot.OriginalTeamHiring.OtherPotentialCandidates ?? Enumerable.Empty<DriverResume>())
                                                .Where(d => !hiredDriversId.Contains(d.Id))
                                                .OrderByDescending(d => d.Reputation)
                                                .FirstOrDefault();
                        if (nextPick != null)
                        {
                            result.Add(new TeamHiring
                            {
                                Role = ballot.OriginalTeamHiring.Role,
                                Slot = ballot.OriginalTeamHiring.Slot,
                                TeamId = ballot.OriginalTeamHiring.TeamId,
                                TeamReputation = ballot.OriginalTeamHiring.TeamReputation,
                                DriverId = nextPick.Id,
                                DriverReputation = nextPick.Reputation
                            });
                            hiredDriversId.Add(nextPick.Id);
                        }
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
                            Slot = ballot.OriginalTeamHiring.Slot,
                            TeamId = ballot.OriginalTeamHiring.TeamId,
                            TeamReputation = ballot.OriginalTeamHiring.TeamReputation,
                            DriverId = firstAvailableDriverOfTheSameReputationOrLower.Id,
                            DriverReputation = firstAvailableDriverOfTheSameReputationOrLower.Reputation
                        });
                        hiredDriversId.Add(firstAvailableDriverOfTheSameReputationOrLower.Id);
                    }
                    continue;
                }

                var weightFit = (DriverReputation driverReputation) =>
                {
                    var fit = _driverHirer.DoesDriverFitSeat(driverReputation, ballot.OriginalTeamHiring.Role, ballot.OriginalTeamHiring.TeamReputation, isReserve);
                    return DriverHirer.GetFitWeight(fit);
                };

                // Drivers the team excluded (an exiting driver not willing to renew) only come into
                // play when nobody else applied. Among the rest the best fit tier wins, and inside
                // it the pick is weighted by reputation (DriverHirer.PickWeightedByReputation).
                var bestTier = availableCandidates
                                            .GroupBy(x => ballot.OriginalTeamHiring.ExcludeDriverIds?.Contains(x.DriverId) == true)
                                            .OrderBy(g => g.Key)
                                            .First()
                                            .GroupBy(x => weightFit(x.DriverReputation))
                                            .OrderByDescending(g => g.Key)
                                            .First();
                var bestCandidate = DriverHirer.PickWeightedByReputation(bestTier, x => x.DriverReputation, rnd);
                var bestCandidateResume = new DriverResume { Id = bestCandidate.DriverId, Reputation = bestCandidate.DriverReputation };

                var originalHireResume = ballot.OriginalTeamHiring == null || ballot.OriginalTeamHiring.DriverId == null ? null : new DriverResume { Id = ballot.OriginalTeamHiring.DriverId, Reputation = ballot.OriginalTeamHiring.DriverReputation };

                var winner = _driverHirer.PickWinner(originalHireResume, bestCandidateResume, ballot.OriginalTeamHiring.Role, ballot.OriginalTeamHiring.TeamReputation, isReserve);

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
                        Slot = ballot.OriginalTeamHiring.Slot,
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
