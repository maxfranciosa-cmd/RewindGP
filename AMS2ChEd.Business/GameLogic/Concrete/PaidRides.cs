using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    // a seat the player can pay for at one race: its driver stands down for that race
    public class PaidRideOffer
    {
        public string TeamId { get; set; }

        // the contract seat (1 = Driver1Contract, 2 = Driver2Contract)
        public int Slot { get; set; }

        public string DriverOutId { get; set; }
    }

    /// <summary>
    /// One-race seats a pay driver without a team buys with their sponsors' money. Only for a player
    /// who brings money (PAY_DRIVER_WILD_CARD or PAY_DRIVER_SEASON) and has neither a race seat nor
    /// a reserve contract. The team can't refuse: any super minnow, and minnows depending on their
    /// line-up. Nothing is stored but the bought rides themselves (absences with reason PAID_RIDE),
    /// so the budget left can't drift, and it resets on its own with the new season.
    /// </summary>
    public static class PaidRides
    {
        // a minnow running one pay driver sells the other driver's seat this often
        private const double SeatOnSaleChance = 0.5;

        // paid rides per season: a season pay driver brings season-level money
        public static int BudgetFor(DriverReputation reputation) => reputation switch
        {
            DriverReputation.PAY_DRIVER_WILD_CARD => 3,
            DriverReputation.PAY_DRIVER_SEASON => 6,
            _ => 0
        };

        public static bool IsPayDriver(DriverReputation reputation) =>
            reputation == DriverReputation.PAY_DRIVER_WILD_CARD || reputation == DriverReputation.PAY_DRIVER_SEASON;

        // the player's budget this season: none with a race seat or a reserve contract
        public static int Budget(ISaveGame saveGame)
        {
            var playerId = saveGame.PlayerData.DriverId;
            if (!string.IsNullOrEmpty(saveGame.PlayerData.TeamId) || saveGame.CurrentSeason.ReserveTeamOf(playerId) != null)
                return 0;

            var player = saveGame.Drivers.FirstOrDefault(d => d.DriverId == playerId);
            return player == null ? 0 : BudgetFor(player.Reputation);
        }

        // the rides bought this season
        public static int Used(ISaveGame saveGame)
        {
            var playerId = saveGame.PlayerData.DriverId;
            return (saveGame.CurrentSeason.Absences ?? Enumerable.Empty<Absence>())
                .Count(a => a.Reason == AbsenceReason.PAID_RIDE && a.DriverIn == playerId);
        }

        public static int Remaining(ISaveGame saveGame) => Math.Max(0, Budget(saveGame) - Used(saveGame));

        /// <summary>
        /// The seats the player can buy at this race: at a super minnow the seat of a driver who
        /// brings no money, then of the lowest reputation; at a minnow with no pay driver either seat,
        /// with one pay driver the other driver's seat half the time (decided once per team and race),
        /// with two none. Empty when the budget is spent, or the player already races there.
        /// </summary>
        public static List<PaidRideOffer> Offers(ISaveGame saveGame, int raceIndex)
        {
            var result = new List<PaidRideOffer>();
            var races = saveGame.CurrentSeason.Races.ToList();
            if (raceIndex < 0 || raceIndex >= races.Count || Remaining(saveGame) == 0)
                return result;

            var playerId = saveGame.PlayerData.DriverId;
            var raceId = races[raceIndex].RaceId;
            var absences = (saveGame.CurrentSeason.Absences ?? Enumerable.Empty<Absence>()).Where(a => a.RaceId == raceId).ToList();
            if (SubstitutePool.DriversIn(absences).Contains(playerId))
                return result;

            var reputations = saveGame.Drivers.ToDictionary(d => d.DriverId, d => d.Reputation);
            bool BringsMoney(string driverId) => reputations.TryGetValue(driverId, out var r) && IsPayDriver(r);

            foreach (var team in saveGame.CurrentSeason.Teams.Where(t => t.Reputation <= TeamReputation.MINNOW))
            {
                var lineUp = new[] { (Slot: 1, DriverId: team.Driver1Contract?.DriverId), (Slot: 2, DriverId: team.Driver2Contract?.DriverId) }
                    .Where(s => !string.IsNullOrEmpty(s.DriverId))
                    .ToList();

                // a driver already out for this race has no seat to sell
                var seats = lineUp
                    .Where(s => s.DriverId != playerId && !absences.Any(a => a.TeamId == team.TeamId && a.DriverOut == s.DriverId))
                    .ToList();
                if (seats.Count == 0)
                    continue;

                IEnumerable<(int Slot, string DriverId)> onSale;
                if (team.Reputation == TeamReputation.SUPER_MINNOW)
                {
                    onSale = seats
                        .OrderBy(s => BringsMoney(s.DriverId))
                        .ThenBy(s => reputations.GetValueOrDefault(s.DriverId, DriverReputation.PRIME_MIDFIELD))
                        .Take(1);
                }
                else
                {
                    var payDrivers = lineUp.Count(s => BringsMoney(s.DriverId));
                    onSale = payDrivers == 0
                        ? seats
                        : payDrivers == 1 && lineUp.Count == 2 && IsSeatOnSale(saveGame.CurrentSeason.Year, raceId, team.TeamId)
                            ? seats.Where(s => !BringsMoney(s.DriverId))
                            : Enumerable.Empty<(int, string)>();
                }

                result.AddRange(onSale.Select(s => new PaidRideOffer { TeamId = team.TeamId, Slot = s.Slot, DriverOutId = s.DriverId }));
            }

            return result;
        }

        // the player pays for the seat: its driver stands down for that race
        public static Absence Buy(ISaveGame saveGame, int raceIndex, PaidRideOffer offer)
        {
            var absence = new Absence
            {
                RaceId = saveGame.CurrentSeason.Races.ElementAt(raceIndex).RaceId,
                TeamId = offer.TeamId,
                DriverOut = offer.DriverOutId,
                DriverIn = saveGame.PlayerData.DriverId,
                Reason = AbsenceReason.PAID_RIDE
            };

            saveGame.CurrentSeason.Absences = (saveGame.CurrentSeason.Absences ?? Enumerable.Empty<Absence>()).Append(absence).ToList();
            return absence;
        }

        // the coin toss of a minnow with one pay driver: the same for a team at a race however often
        // it's asked (string.GetHashCode changes with every run, hence the hash of our own)
        public static bool IsSeatOnSale(int seasonYear, int raceId, string teamId)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in $"{seasonYear}|{raceId}|{teamId}")
                {
                    hash = (hash ^ c) * 16777619;
                }
                return new Random((int)hash).NextDouble() < SeatOnSaleChance;
            }
        }
    }

    /// <summary>
    /// What the paddock offers a player without a race seat for one race: the real absences they
    /// can apply for (on race day, as always), the races they already stand in for (a reserve's
    /// call-up, a bought ride), and the rides they can buy. Built from the save, never stored.
    /// </summary>
    public class PaddockOpportunities
    {
        public int RaceIndex { get; set; }
        public Race Race { get; set; }

        // absences at that race the player can apply for
        public List<Absence> OpenAbsences { get; set; } = new();

        // absences the player already stands in for
        public List<Absence> CallUps { get; set; } = new();

        public List<PaidRideOffer> PaidRideOffers { get; set; } = new();

        public int PaidRidesBudget { get; set; }
        public int PaidRidesRemaining { get; set; }

        // the team the player is the reserve of, null when they're nobody's reserve
        public string ReserveTeamId { get; set; }

        // a ride the player can buy (only a pay driver without any contract, not racing there yet)
        public bool HasChoices => PaidRideOffers.Count > 0;

        public static PaddockOpportunities For(ISaveGame saveGame, int raceIndex)
        {
            var races = saveGame.CurrentSeason.Races.ToList();
            var result = new PaddockOpportunities { RaceIndex = raceIndex };
            if (raceIndex < 0 || raceIndex >= races.Count)
                return result;

            var playerId = saveGame.PlayerData.DriverId;
            var race = races[raceIndex];
            var absences = (saveGame.CurrentSeason.Absences ?? Enumerable.Empty<Absence>()).Where(a => a.RaceId == race.RaceId).ToList();

            result.Race = race;
            result.ReserveTeamId = saveGame.CurrentSeason.ReserveTeamOf(playerId)?.TeamId;
            result.CallUps = absences.Where(a => SubstitutePool.DriversIn(new[] { a }).Contains(playerId)).ToList();
            result.OpenAbsences = absences
                .Where(a => a.DriverOut != playerId && a.DriverIn != playerId && a.TeamId != saveGame.PlayerData.TeamId)
                .ToList();
            result.PaidRideOffers = PaidRides.Offers(saveGame, raceIndex);
            result.PaidRidesBudget = PaidRides.Budget(saveGame);
            result.PaidRidesRemaining = PaidRides.Remaining(saveGame);
            return result;
        }
    }
}
