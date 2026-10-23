using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;

namespace AMS2ChEd.Business.Services
{
    /// <summary>
    /// Estimates the player's chances on the driver market with a light replay of the off-season.
    /// Reputations, contracts and drops are worked out once; only the random parts are replayed many
    /// times, in memory: retirements, renewals, the order teams of the same standing pick in and the
    /// weighted draws of the provisional picks and final ballots (EndOfSeasonManager,
    /// OffSeasonMovements). At each seat the player's chance is computed exactly from what's left, as
    /// if they applied there alone, so a few hundred replays give steady numbers in milliseconds.
    /// Leaves out the drivers generated for seats nobody applies to.
    /// </summary>
    public class MarketOutlookEstimator : IMarketOutlookEstimator
    {
        // the applications the player can send (TeamApplicationWindow)
        public const int MaxApplications = 4;

        private const int Replays = 400;

        // the same save always shows the same outlook
        private const int Seed = 1990;

        // a rival free at least this often when the team picks is named as competition
        private const double RivalShownChance = 0.25;

        private class SeatPlan
        {
            public int Driver { get; set; }
            public ContractRole ContractRole { get; set; }
            public DriverFirerOutcome Outcome { get; set; }
            public double RetirementChance { get; set; }
            public bool IsPlayer { get; set; }
        }

        private class TeamPlan
        {
            public string TeamId { get; set; }
            public TeamReputation Reputation { get; set; }

            // index 0 = Driver1Contract, 1 = Driver2Contract; null for a seat with no driver,
            // and both null for a team new next season
            public SeatPlan[] Seats { get; set; } = new SeatPlan[2];
            public bool IsNewTeam { get; set; }
            public bool IsQuitting { get; set; }

            // this season's reserve, who has first refusal on an opening race seat
            public int Reserve { get; set; } = NoDriver;
            public bool IsPlayerReserve { get; set; }

            // the outlook of each seat, gathered over the replays: index 2 is the reserve seat
            public SeatTally[] Tallies { get; } = new SeatTally[3];
        }

        // a seat open in one replay
        private class OpenSeat
        {
            public TeamPlan Team { get; set; }
            public int Slot { get; set; }
            public DriverRole Role { get; set; }
            public int ExcludedDriver { get; set; } = NoDriver;
            public int ProvisionalPick { get; set; } = NoDriver;

            // the drivers the team made its provisional pick from, the player aside
            public List<int> PickPool { get; set; } = new();
            public bool IsReserve => Slot == TeamHiring.ReserveSlot;
        }

        private class SeatTally
        {
            public int Opened { get; set; }
            public double PlayerWins { get; set; }
            public int[] RoleCounts { get; } = new int[2];
            public int[] FreeCounts { get; set; }
        }

        private const int NoDriver = -1;

        private readonly IEndOfSeasonManager _endOfSeasonManager;
        private readonly DriverFirer _driverFirer;
        private readonly DriverHirer _driverHirer;

        // drivers by index, for the replays
        private List<string> _driverIds;
        private DriverReputation[] _reputations;
        private double[] _tickets;
        private TeamReputation[] _minAmbition;
        private TeamReputation[] _maxAmbition;
        private TeamReputation[] _minReserveAmbition;
        private TeamReputation[] _maxReserveAmbition;
        private int _player;

        // DriverHirer's fit weight of each [driver reputation, role, team reputation]
        private int[,,] _fitWeights;

        // whether each [driver reputation, role, team reputation] is a PerfectFit (a reserve's promotion)
        private bool[,,] _perfectFits;

        // DriverHirer's reserve fit weight of each [driver reputation, team reputation]
        private int[,] _reserveFitWeights;

        public MarketOutlookEstimator(IEndOfSeasonManager endOfSeasonManager, DriverFirer driverFirer, DriverHirer driverHirer)
        {
            _endOfSeasonManager = endOfSeasonManager;
            _driverFirer = driverFirer;
            _driverHirer = driverHirer;
        }

        public static MarketChance ToBand(double chance) =>
            chance >= 0.75 ? MarketChance.VeryLikely :
            chance >= 0.5 ? MarketChance.Likely :
            chance >= 0.2 ? MarketChance.Possible :
            MarketChance.LongShot;

        public MarketOutlook Estimate(ISaveGame saveGame, ISeason nextSeason, IReadOnlyDictionary<string, IDriverData> nextSeasonDrivers)
        {
            var reputations = _endOfSeasonManager.ProjectNewReputations(saveGame, nextSeason.Year);
            var retiredDriverIds = saveGame.RetiredDrivers?.Select(d => d.DriverId).ToHashSet() ?? new HashSet<string>();
            foreach (var rookie in nextSeasonDrivers.Values.Where(d => !reputations.ContainsKey(d.DriverId) && !retiredDriverIds.Contains(d.DriverId)))
            {
                reputations[rookie.DriverId] = rookie.Reputation;
            }

            _driverIds = reputations.Keys.ToList();
            _reputations = _driverIds.Select(id => reputations[id]).ToArray();
            _tickets = _reputations.Select(r => Math.Pow(2, DriverHirer.ReputationScore(r))).ToArray();
            _minAmbition = _reputations.Select(r => OffSeasonMovements.driverAmbitions[r].MinReputation).ToArray();
            _maxAmbition = _reputations.Select(r => OffSeasonMovements.driverAmbitions[r].MaxReputation).ToArray();
            _minReserveAmbition = _reputations.Select(r => OffSeasonMovements.reserveAmbitions[r].MinReputation).ToArray();
            _maxReserveAmbition = _reputations.Select(r => OffSeasonMovements.reserveAmbitions[r].MaxReputation).ToArray();
            _player = _driverIds.IndexOf(saveGame.PlayerData.DriverId);

            var driverReputations = Enum.GetValues<DriverReputation>();
            var roles = Enum.GetValues<DriverRole>();
            var teamReputations = Enum.GetValues<TeamReputation>();
            _fitWeights = new int[driverReputations.Length, roles.Length, teamReputations.Length];
            _perfectFits = new bool[driverReputations.Length, roles.Length, teamReputations.Length];
            _reserveFitWeights = new int[driverReputations.Length, teamReputations.Length];
            foreach (var driverReputation in driverReputations)
                foreach (var teamReputation in teamReputations)
                {
                    _reserveFitWeights[(int)driverReputation, (int)teamReputation] = DriverHirer.GetFitWeight(_driverHirer.DoesDriverFitReservePolicy(driverReputation, teamReputation));
                    foreach (var role in roles)
                    {
                        var fit = _driverHirer.DoesDriverFitTeamPolicy(driverReputation, role, teamReputation);
                        _fitWeights[(int)driverReputation, (int)role, (int)teamReputation] = DriverHirer.GetFitWeight(fit);
                        _perfectFits[(int)driverReputation, (int)role, (int)teamReputation] = fit == DriverHirer.DriverPolicyFit.PerfectFit;
                    }
                }

            var outlook = new MarketOutlook { PlayerReputation = _reputations[_player], ProjectedReputations = reputations };
            var teams = PlanTeams(saveGame, nextSeason, outlook);

            var employed = teams.SelectMany(t => t.Seats).Where(s => s != null).Select(s => s.Driver).ToHashSet();
            var unemployed = Enumerable.Range(0, _driverIds.Count).Where(d => !employed.Contains(d) && d != _player).ToList();

            var random = new Random(Seed);
            for (int replay = 0; replay < Replays; replay++)
            {
                Replay(teams, unemployed, random);
            }

            outlook.Seats = teams
                .SelectMany(t => Enumerable.Range(0, 3).Where(i => t.Tallies[i]?.Opened > 0).Select(i => ToSeatOutlook(t, i)))
                .OrderByDescending(s => s.Chance)
                .ThenByDescending(s => s.TeamReputation)
                .ToList();

            outlook.BestApplications = outlook.Seats.Take(MaxApplications).ToList();
            // applications are treated as independent of each other
            outlook.HiredChance = 1 - outlook.BestApplications.Aggregate(1.0, (missed, s) => missed * (1 - s.Chance));
            outlook.HiredBand = ToBand(outlook.HiredChance);

            return outlook;
        }

        // what's certain about each team's drivers next season: the drops, and each driver's chance to retire
        private List<TeamPlan> PlanTeams(ISaveGame saveGame, ISeason nextSeason, MarketOutlook outlook)
        {
            var racesInSeason = saveGame.CurrentSeason.Races.Count();
            var nextSeasonTeams = nextSeason.Teams.ToDictionary(t => t.TeamId, t => t);
            var birthYears = saveGame.Drivers.ToDictionary(d => d.DriverId, d => d.YearOfBirth);
            var championId = saveGame.CurrentDriverStandings?.FirstOrDefault(s => s.Position == 1)?.DriverId;
            var reprimands = saveGame.Reprimands ?? new List<Reprimand>();
            var driverIndexes = Enumerable.Range(0, _driverIds.Count).ToDictionary(i => _driverIds[i], i => i);
            var result = new List<TeamPlan>();

            foreach (var team in saveGame.CurrentSeason.Teams)
            {
                var nextSeasonTeam = nextSeasonTeams.GetValueOrDefault(team.TeamId);
                var plan = new TeamPlan { TeamId = team.TeamId, Reputation = nextSeasonTeam?.Reputation ?? team.Reputation, IsQuitting = nextSeasonTeam == null };
                var contracts = new[] { team.Driver1Contract, team.Driver2Contract };

                for (int i = 0; i < 2; i++)
                {
                    var driverId = contracts[i]?.DriverId;
                    if (string.IsNullOrEmpty(driverId) || !driverIndexes.TryGetValue(driverId, out var driver))
                        continue;

                    // retiring is replayed: here only what's certain
                    var outcome = _driverFirer.WillDropDriver(plan.Reputation, _reputations[driver], contracts[i].Races - racesInSeason, false, plan.IsQuitting);
                    if (outcome != DriverFirerOutcome.DROPPED_TEAM_QUITTING && driverId != championId &&
                        reprimands.Count(r => r.DriverId == driverId && r.TeamId == team.TeamId) >= ReprimandManager.REPRIMANDS_FOR_SEASON_END_DROP)
                    {
                        outcome = DriverFirerOutcome.DROPPED_DISCIPLINARY;
                    }

                    var isPlayer = driver == _player;
                    if (isPlayer)
                    {
                        outlook.PlayerTeamId = team.TeamId;
                        outlook.PlayerContractOutcome = outcome;
                    }

                    plan.Seats[i] = new SeatPlan
                    {
                        Driver = driver,
                        ContractRole = contracts[i].Role,
                        Outcome = outcome,
                        // the player never retires (EndOfSeasonManager.GetTeamSituationBeforeDriverMarket)
                        RetirementChance = isPlayer ? 0 : EndOfSeasonManager.RetirementChance(nextSeason.Year - birthYears.GetValueOrDefault(driverId, nextSeason.Year)),
                        IsPlayer = isPlayer
                    };
                }

                var reserveId = team.ReserveDriverId();
                if (reserveId != null && driverIndexes.TryGetValue(reserveId, out var reserve))
                {
                    plan.Reserve = reserve;
                    plan.IsPlayerReserve = reserve == _player;
                }

                result.Add(plan);
            }

            var currentTeamIds = saveGame.CurrentSeason.Teams.Select(t => t.TeamId).ToHashSet();
            result.AddRange(nextSeason.Teams
                .Where(t => !currentTeamIds.Contains(t.TeamId))
                .Select(t => new TeamPlan { TeamId = t.TeamId, Reputation = t.Reputation, IsNewTeam = true }));

            return result;
        }

        private void Replay(List<TeamPlan> teams, List<int> unemployed, Random random)
        {
            var free = new List<int>(unemployed);
            var openSeats = new List<OpenSeat>();
            var reserveSeats = new List<OpenSeat>();

            foreach (var team in teams)
            {
                // whether the team runs a reserve next season (EndOfSeasonManager.RollReserveWishes)
                var wantsReserve = !team.IsQuitting && random.NextDouble() < EndOfSeasonManager.ReserveChance(team.Reputation);

                if (team.IsNewTeam)
                {
                    openSeats.Add(new OpenSeat { Team = team, Slot = 1, Role = DriverRole.FIRST_DRIVER });
                    openSeats.Add(new OpenSeat { Team = team, Slot = 2, Role = DriverRole.SECOND_DRIVER });
                    if (wantsReserve)
                        reserveSeats.Add(new OpenSeat { Team = team, Slot = TeamHiring.ReserveSlot, Role = DriverRole.SECOND_DRIVER });
                    continue;
                }

                var open = new bool[2];
                var excluded = new[] { NoDriver, NoDriver };
                for (int i = 0; i < 2; i++)
                {
                    var seat = team.Seats[i];
                    if (seat == null)
                        continue;

                    if (seat.IsPlayer)
                    {
                        // the outlook is what the player finds if they leave their team
                        open[i] = true;
                    }
                    else if (random.NextDouble() < seat.RetirementChance)
                    {
                        open[i] = true;
                    }
                    else if (seat.Outcome.IsDropped())
                    {
                        open[i] = true;
                        free.Add(seat.Driver);
                        // out of contract, team and driver renew on a coin toss; any other drop is final
                        var willingToRenew = seat.Outcome == DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED && random.Next(2) == 1;
                        excluded[i] = willingToRenew ? NoDriver : seat.Driver;
                    }
                }

                // a quitting team's drivers go on the market, but it has no seats
                if (team.IsQuitting)
                    continue;

                // with both seats open the team looks for a leader and a wingman; with only one, for
                // whatever complements the driver who's staying
                var playerPromoted = false;
                var reservePromoted = false;
                for (int i = 0; i < 2; i++)
                {
                    if (!open[i])
                        continue;

                    var other = team.Seats[1 - i];
                    var role = other == null || open[1 - i]
                        ? (i == 0 ? DriverRole.FIRST_DRIVER : DriverRole.SECOND_DRIVER)
                        : DriverHirer.GetRoleToHireAlongside(other.ContractRole, _reputations[other.Driver], team.Reputation);
                    var openSeat = new OpenSeat { Team = team, Slot = i + 1, Role = role, ExcludedDriver = excluded[i] };

                    // first refusal: the team's reserve takes the seat when they're a PerfectFit for it
                    // (EndOfSeasonManager.TeamPicksPotentialReplacementsDrivers); the player as reserve
                    // would be offered it
                    if (!reservePromoted && team.Reserve != NoDriver && IsPerfectFit(team.Reserve, role, team.Reputation))
                    {
                        reservePromoted = true;
                        if (team.IsPlayerReserve)
                        {
                            playerPromoted = true;
                            TallyCertain(openSeat);
                        }
                        else
                        {
                            free.Remove(team.Reserve);
                        }
                        continue;
                    }

                    openSeats.Add(openSeat);
                }

                if (wantsReserve)
                {
                    var reserveSeat = new OpenSeat { Team = team, Slot = TeamHiring.ReserveSlot, Role = DriverRole.SECOND_DRIVER };
                    // a player who is the team's reserve is offered to stay on
                    if (team.IsPlayerReserve && !playerPromoted)
                        TallyCertain(reserveSeat);
                    else
                        reserveSeats.Add(reserveSeat);
                }
            }

            // best teams first, teams of the same standing in random order (OffSeasonMovements.PickPotentialNewDrivers);
            // the final ballots keep that order. The reserve seats come after every race seat
            var order = InPickingOrder(openSeats, random);
            var reserveOrder = InPickingOrder(reserveSeats, random);

            // provisional picks: a player leaving their team is free too, so a team may pick them
            var pool = new List<int>(free) { _player };
            foreach (var seat in order.Concat(reserveOrder))
            {
                var candidates = pool.Where(d => d != seat.ExcludedDriver && MinAmbition(d, seat) <= seat.Team.Reputation).ToList();
                if (candidates.Count == 0)
                    continue;

                seat.PickPool = candidates.Where(d => d != _player).ToList();
                seat.ProvisionalPick = PickWeighted(BestFitTier(candidates, seat), random);
                pool.Remove(seat.ProvisionalPick);
            }

            // final ballots: every free driver applies to the teams within their ambition. The player
            // is weighed at each seat as if they applied there alone, then left out of it. Reserve
            // ballots run among the drivers still without a race seat
            var hired = new bool[_driverIds.Count];
            foreach (var seat in order.Concat(reserveOrder))
            {
                var candidates = free.Where(d => !hired[d] && WithinAmbition(d, seat)).ToList();
                Tally(seat, candidates, hired);

                // the player not applying here: a seat that picked them has no pick
                var pick = seat.ProvisionalPick == _player ? NoDriver : seat.ProvisionalPick;
                var winner = ResolveBallot(seat, candidates, pick, hired, random);
                if (winner != NoDriver)
                    hired[winner] = true;
            }
        }

        private static List<OpenSeat> InPickingOrder(List<OpenSeat> seats, Random random) => seats
            .GroupBy(s => s.Team.Reputation)
            .OrderByDescending(g => g.Key)
            .SelectMany(g => g.OrderBy(_ => random.Next()))
            .ToList();

        private TeamReputation MinAmbition(int driver, OpenSeat seat) =>
            seat.IsReserve ? _minReserveAmbition[driver] : _minAmbition[driver];

        private bool WithinAmbition(int driver, OpenSeat seat) => seat.IsReserve
            ? _minReserveAmbition[driver] <= seat.Team.Reputation && seat.Team.Reputation <= _maxReserveAmbition[driver]
            : _minAmbition[driver] <= seat.Team.Reputation && seat.Team.Reputation <= _maxAmbition[driver];

        private bool IsPerfectFit(int driver, DriverRole role, TeamReputation teamReputation) =>
            _perfectFits[(int)_reputations[driver], (int)role, (int)teamReputation];

        private int FitWeight(int driver, DriverRole role, TeamReputation teamReputation) =>
            _fitWeights[(int)_reputations[driver], (int)role, (int)teamReputation];

        private int ReserveFitWeight(int driver, TeamReputation teamReputation) =>
            _reserveFitWeights[(int)_reputations[driver], (int)teamReputation];

        private int FitWeight(int driver, OpenSeat seat) => seat.IsReserve
            ? ReserveFitWeight(driver, seat.Team.Reputation)
            : FitWeight(driver, seat.Role, seat.Team.Reputation);

        // a seat already the player's in this replay: a reserve's promotion, or staying on as reserve
        private void TallyCertain(OpenSeat seat)
        {
            var tally = seat.Team.Tallies[seat.Slot - 1] ??= new SeatTally { FreeCounts = new int[_driverIds.Count] };
            tally.Opened++;
            tally.RoleCounts[(int)seat.Role]++;
            tally.PlayerWins += 1;
        }

        private List<int> BestFitTier(List<int> candidates, OpenSeat seat)
        {
            var best = int.MinValue;
            var tier = new List<int>();
            foreach (var driver in candidates)
            {
                var fit = FitWeight(driver, seat);
                if (fit > best)
                {
                    best = fit;
                    tier.Clear();
                }
                if (fit == best)
                    tier.Add(driver);
            }
            return tier;
        }

        // DriverHirer.PickWeightedByReputation, on the tickets worked out once
        private int PickWeighted(List<int> candidates, Random random)
        {
            double ticket = random.NextDouble() * candidates.Sum(d => _tickets[d]);
            foreach (var driver in candidates)
            {
                ticket -= _tickets[driver];
                if (ticket < 0)
                    return driver;
            }
            return candidates[^1];
        }

        // the chance the team's pick beats the drawn candidate (DriverHirer.PickWinner)
        private double PickBeats(int pick, int candidate, OpenSeat seat)
        {
            var pickFit = FitWeight(pick, seat);
            var candidateFit = FitWeight(candidate, seat);
            if (pickFit != candidateFit)
                return pickFit > candidateFit ? 1 : 0;

            if (_reputations[pick] == DriverReputation.PAY_DRIVER_SEASON && _reputations[candidate] == DriverReputation.PAY_DRIVER_SEASON)
                return 0.5;

            return _reputations[pick] >= _reputations[candidate] ? 1 : 0;
        }

        // who signs (OffSeasonMovements.FinalBallotResults): an excluded driver only counts when
        // nobody else applied; the best fit tier is drawn from by reputation, and the drawn candidate
        // faces the team's provisional pick if that's still free
        private int ResolveBallot(OpenSeat seat, List<int> candidates, int pick, bool[] hired, Random random)
        {
            if (candidates.Count == 0)
                return pick != NoDriver && !hired[pick] ? pick : NoDriver;

            var eligible = candidates.Any(d => d != seat.ExcludedDriver) ? candidates.Where(d => d != seat.ExcludedDriver).ToList() : candidates;
            var drawn = PickWeighted(BestFitTier(eligible, seat), random);

            if (pick == NoDriver || hired[pick])
                return drawn;

            return random.NextDouble() < PickBeats(pick, drawn, seat) ? pick : drawn;
        }

        // the player's exact chance at this seat if they applied here alone, given who's left
        private void Tally(OpenSeat seat, List<int> candidates, bool[] hired)
        {
            var index = seat.Slot - 1;
            if (index < seat.Team.Seats.Length && seat.Team.Seats[index]?.IsPlayer == true)
                return;

            var tally = seat.Team.Tallies[index] ??= new SeatTally { FreeCounts = new int[_driverIds.Count] };
            tally.Opened++;
            tally.RoleCounts[(int)seat.Role]++;
            foreach (var driver in candidates)
            {
                tally.FreeCounts[driver]++;
            }

            // whoever the team could have picked competes too, even when the seat is beyond their
            // ambition and they don't apply: the pick is drawn by reputation, so the bigger names are
            // the likelier ones
            foreach (var driver in seat.PickPool)
            {
                if (!hired[driver] && !candidates.Contains(driver))
                    tally.FreeCounts[driver]++;
            }

            var pick = seat.ProvisionalPick;

            // the player is never excluded, so excluded drivers drop out of the draw
            var playerFit = FitWeight(_player, seat);
            double tickets = _tickets[_player];
            foreach (var driver in candidates)
            {
                if (driver == seat.ExcludedDriver)
                    continue;

                var fit = FitWeight(driver, seat);
                if (fit > playerFit)
                    return;
                if (fit == playerFit)
                    tickets += _tickets[driver];
            }

            if (pick == _player)
            {
                // the team's own pick: the player signs unless the drawn candidate beats them
                var chance = _tickets[_player] / tickets;
                foreach (var driver in candidates)
                {
                    if (driver != seat.ExcludedDriver && FitWeight(driver, seat) == playerFit)
                        chance += _tickets[driver] / tickets * PickBeats(_player, driver, seat);
                }
                tally.PlayerWins += chance;
                return;
            }

            var drawnChance = _tickets[_player] / tickets;
            tally.PlayerWins += pick == NoDriver || hired[pick] ? drawnChance : drawnChance * (1 - PickBeats(pick, _player, seat));
        }

        private SeatOutlook ToSeatOutlook(TeamPlan team, int index)
        {
            var tally = team.Tallies[index];
            var isReserve = index + 1 == TeamHiring.ReserveSlot;
            var role = tally.RoleCounts[(int)DriverRole.FIRST_DRIVER] >= tally.RoleCounts[(int)DriverRole.SECOND_DRIVER] ? DriverRole.FIRST_DRIVER : DriverRole.SECOND_DRIVER;
            int Fit(int driver) => isReserve ? ReserveFitWeight(driver, team.Reputation) : FitWeight(driver, role, team.Reputation);
            var playerFit = Fit(_player);
            var playerScore = DriverHirer.ReputationScore(_reputations[_player]);

            List<string> Named(Func<int, bool> rival) => Enumerable.Range(0, _driverIds.Count)
                .Where(d => tally.FreeCounts[d] >= RivalShownChance * tally.Opened && rival(d))
                .OrderByDescending(d => tally.FreeCounts[d] * _tickets[d])
                .Select(d => _driverIds[d])
                .ToList();

            // a reserve seat's "exiting" driver is this season's reserve, if any
            var exiting = isReserve ? null : team.Seats[index];
            var exitingDriver = isReserve ? team.Reserve : exiting?.Driver ?? NoDriver;
            var outlook = new SeatOutlook
            {
                TeamId = team.TeamId,
                Slot = index + 1,
                Role = role,
                TeamReputation = team.Reputation,
                ExitingDriverId = exitingDriver == NoDriver ? null : _driverIds[exitingDriver],
                ExitingDriverOutcome = exiting?.Outcome ?? DriverFirerOutcome.NOT_DROPPED,
                OpenChance = tally.Opened / (double)Replays,
                WinChanceIfOpen = tally.PlayerWins / tally.Opened,
                PlayerFit = _driverHirer.DoesDriverFitSeat(_reputations[_player], role, team.Reputation, isReserve),
                BetterFitRivals = Named(d => Fit(d) > playerFit),
                SameFitRivals = Named(d => Fit(d) == playerFit),
                StrongerSameFitRivals = Named(d => Fit(d) == playerFit && DriverHirer.ReputationScore(_reputations[d]) > playerScore),
                EqualSameFitRivals = Named(d => Fit(d) == playerFit && DriverHirer.ReputationScore(_reputations[d]) == playerScore)
            };
            outlook.Band = ToBand(outlook.Chance);
            return outlook;
        }
    }
}
