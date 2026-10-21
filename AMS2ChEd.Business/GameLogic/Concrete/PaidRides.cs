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

        // the AI pay driver who already bought this seat at this race, null when nobody did: the
        // team then picks one of the two
        public string RivalBidderId { get; set; }

        // that AI driver's ride, which the player takes over if the team picks them
        public Absence RivalRide { get; set; }
    }

    /// <summary>
    /// One-race seats a pay driver without a team buys with their sponsors' money: the player when
    /// they bring money (PAY_DRIVER_WILD_CARD or PAY_DRIVER_SEASON) and have neither a race seat nor
    /// a reserve contract, and AI pay drivers without any contract, who buy first. The team can't
    /// refuse: any super minnow, and minnows depending on their line-up, but only drivers below
    /// PRIME_STRONG_MIDFIELD and not already out of 4 races are stood down. Nothing is stored but the
    /// bought rides themselves (absences with reason PAID_RIDE), so the budget left can't drift, and
    /// it resets on its own with the new season.
    /// </summary>
    public static class PaidRides
    {
        // a minnow running one pay driver sells the other driver's seat this often
        private const double SeatOnSaleChance = 0.5;

        // the player bidding for a seat an AI driver already bought gets it this often
        private const double PlayerWinsContestChance = 0.5;

        // a driver of this reputation or above is never stood down for a paid ride
        public const DriverReputation ProtectedReputation = DriverReputation.PRIME_STRONG_MIDFIELD;

        // no paid ride stands down a driver already out of MaxAbsencesPerDriver races in the season:
        // this many, or a quarter of the season's races when that's fewer
        public const int MaxAbsencesCapPerSeason = 4;

        public static int MaxAbsencesPerDriver(ISeason season) =>
            Math.Min(MaxAbsencesCapPerSeason, (season.Races?.Count() ?? 0) / 4);

        // paid rides per season: a season pay driver brings season-level money
        public static int BudgetFor(DriverReputation reputation) => reputation switch
        {
            DriverReputation.PAY_DRIVER_WILD_CARD => 3,
            DriverReputation.PAY_DRIVER_SEASON => 6,
            _ => 0
        };

        // how often an AI pay driver with budget left buys a ride at a race
        public static double AiBuyChance(DriverReputation reputation) => reputation switch
        {
            DriverReputation.PAY_DRIVER_WILD_CARD => 0.2,
            DriverReputation.PAY_DRIVER_SEASON => 0.35,
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
        public static int Used(ISaveGame saveGame) => Used(saveGame, saveGame.PlayerData.DriverId);

        public static int Used(ISaveGame saveGame, string driverId)
        {
            return (saveGame.CurrentSeason.Absences ?? Enumerable.Empty<Absence>())
                .Count(a => a.Reason == AbsenceReason.PAID_RIDE && a.DriverIn == driverId);
        }

        public static int Remaining(ISaveGame saveGame) => Math.Max(0, Budget(saveGame) - Used(saveGame));

        // an AI driver's rides left this season (whether they may buy at all is up to BuyAiRides)
        public static int RemainingFor(ISaveGame saveGame, string driverId)
        {
            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);
            return driver == null ? 0 : Math.Max(0, BudgetFor(driver.Reputation) - Used(saveGame, driverId));
        }

        // the races this season the driver is out of, past or to come, whatever the reason (chained
        // absences included), the ride in ignoring left out
        public static int AbsencesOf(ISeason season, string driverId, Absence ignoring = null)
        {
            return (season.Absences ?? Enumerable.Empty<Absence>())
                .SelectMany(a => Chain(a))
                .Count(a => a != ignoring && a.DriverOut == driverId);
        }

        /// <summary>
        /// Whether the driver's seat can be sold at all: never a driver of PRIME_STRONG_MIDFIELD
        /// reputation or above, and never one already out of MaxAbsencesPerDriver races (4, or a
        /// quarter of the season's races, rounded down, when that's fewer). The cap only stops new
        /// rides: a season pack's absences can take a driver past it.
        /// </summary>
        public static bool CanBeBoughtOut(ISeason season, string driverId, DriverReputation reputation, Absence ignoring = null)
        {
            return reputation < ProtectedReputation && AbsencesOf(season, driverId, ignoring) < MaxAbsencesPerDriver(season);
        }

        /// <summary>
        /// The seats the player can buy at this race: at a super minnow the seat of a driver who
        /// brings no money, then of the lowest reputation; at a minnow with no pay driver either seat,
        /// with one pay driver the other driver's seat half the time (decided once per team and race),
        /// with two none; never the seat of a driver who can't be bought out (CanBeBoughtOut). A seat
        /// an AI pay driver already bought at this race is still offered, with that driver as rival
        /// bidder. Empty when the budget is spent, or the player already races there.
        /// </summary>
        public static List<PaidRideOffer> Offers(ISaveGame saveGame, int raceIndex) =>
            Offers(saveGame, raceIndex, saveGame.PlayerData.DriverId, Remaining(saveGame), contestAiRides: true);

        private static List<PaidRideOffer> Offers(ISaveGame saveGame, int raceIndex, string buyerId, int remaining, bool contestAiRides)
        {
            var result = new List<PaidRideOffer>();
            var races = saveGame.CurrentSeason.Races.ToList();
            if (raceIndex < 0 || raceIndex >= races.Count || remaining == 0)
                return result;

            var raceId = races[raceIndex].RaceId;
            var absences = (saveGame.CurrentSeason.Absences ?? Enumerable.Empty<Absence>()).Where(a => a.RaceId == raceId).ToList();
            if (SubstitutePool.DriversIn(absences).Contains(buyerId))
                return result;

            var reputations = saveGame.Drivers.ToDictionary(d => d.DriverId, d => d.Reputation);
            bool BringsMoney(string driverId) => reputations.TryGetValue(driverId, out var r) && IsPayDriver(r);

            // the stand-ins of chained absences too: a driver who left their seat to stand in
            // elsewhere has it covered by the chain filed under the other team's absence
            var raceAbsences = absences
                .SelectMany(a => Chain(a))
                .ToList();

            foreach (var team in saveGame.CurrentSeason.Teams.Where(t => t.Reputation <= TeamReputation.MINNOW))
            {
                var lineUp = new[] { (Slot: 1, DriverId: team.Driver1Contract?.DriverId), (Slot: 2, DriverId: team.Driver2Contract?.DriverId) }
                    .Where(s => !string.IsNullOrEmpty(s.DriverId))
                    .ToList();

                // a driver already out for this race has no seat to sell, unless an AI driver bought
                // it and the player bids against them
                var seats = new List<(int Slot, string DriverId, Absence Rival)>();
                foreach (var s in lineUp.Where(s => s.DriverId != buyerId))
                {
                    var takenBy = raceAbsences.FirstOrDefault(a => a.TeamId == team.TeamId && a.DriverOut == s.DriverId);
                    if (takenBy != null && !(contestAiRides && takenBy.Reason == AbsenceReason.PAID_RIDE))
                        continue;

                    // a protected driver's seat isn't for sale, so the team sells the other one if it can
                    if (CanBeBoughtOut(saveGame.CurrentSeason, s.DriverId, reputations.GetValueOrDefault(s.DriverId, DriverReputation.PRIME_MIDFIELD), takenBy))
                        seats.Add((s.Slot, s.DriverId, takenBy));
                }
                if (seats.Count == 0)
                    continue;

                IEnumerable<(int Slot, string DriverId, Absence Rival)> onSale;
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
                            : Enumerable.Empty<(int, string, Absence)>();
                }

                result.AddRange(onSale
                    .Select(s => new PaidRideOffer
                    {
                        TeamId = team.TeamId,
                        Slot = s.Slot,
                        DriverOutId = s.DriverId,
                        RivalBidderId = s.Rival?.DriverIn,
                        RivalRide = s.Rival
                    }));
            }

            return result;
        }

        /// <summary>
        /// The player pays for the seat: its driver stands down for that race. When an AI driver
        /// bought it first, the team picks one of the two: the player takes over the AI driver's ride
        /// (their budget is spent, the AI driver's isn't), or loses and nothing changes (null).
        /// </summary>
        public static Absence Buy(ISaveGame saveGame, int raceIndex, PaidRideOffer offer, Random random = null)
        {
            if (offer.RivalRide != null)
            {
                if ((random ?? new Random()).NextDouble() >= PlayerWinsContestChance)
                    return null;

                offer.RivalRide.DriverIn = saveGame.PlayerData.DriverId;
                return offer.RivalRide;
            }

            return AddRide(saveGame, saveGame.CurrentSeason.Races.ElementAt(raceIndex).RaceId, offer, saveGame.PlayerData.DriverId);
        }

        /// <summary>
        /// AI pay drivers without any contract buy their rides at this race, before the player is
        /// offered theirs. Each with budget left buys with AiBuyChance, one of the seats on sale to
        /// them picked at random; they go in a random order, every ride bought shrinking what's left
        /// for the next. Returns the rides bought, already added to the season's absences.
        /// </summary>
        public static List<Absence> BuyAiRides(ISaveGame saveGame, int raceIndex, Random random)
        {
            var bought = new List<Absence>();
            var races = saveGame.CurrentSeason.Races.ToList();
            if (raceIndex < 0 || raceIndex >= races.Count)
                return bought;

            var unavailable = SubstitutePool.UnavailableDriverIds(saveGame.CurrentSeason);
            unavailable.UnionWith((saveGame.RetiredDrivers ?? Enumerable.Empty<IDriverData>()).Select(d => d.DriverId));
            unavailable.Add(saveGame.PlayerData.DriverId);

            var buyers = saveGame.Drivers
                .Where(d => IsPayDriver(d.Reputation) && !unavailable.Contains(d.DriverId))
                .OrderBy(_ => random.Next())
                .ToList();

            foreach (var buyer in buyers)
            {
                if (random.NextDouble() >= AiBuyChance(buyer.Reputation))
                    continue;

                var offers = Offers(saveGame, raceIndex, buyer.DriverId, RemainingFor(saveGame, buyer.DriverId), contestAiRides: false);
                if (offers.Count == 0)
                    continue;

                bought.Add(AddRide(saveGame, races[raceIndex].RaceId, offers[random.Next(offers.Count)], buyer.DriverId));
            }

            return bought;
        }

        private static IEnumerable<Absence> Chain(Absence absence)
        {
            for (var current = absence; current != null; current = current.ChainedAbsence)
            {
                yield return current;
            }
        }

        private static Absence AddRide(ISaveGame saveGame, int raceId, PaidRideOffer offer, string buyerId)
        {
            var absence = new Absence
            {
                RaceId = raceId,
                TeamId = offer.TeamId,
                DriverOut = offer.DriverOutId,
                DriverIn = buyerId,
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

        // absences at that race the player can apply for (a paid ride is bid for, not applied for)
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
                .Where(a => a.DriverOut != playerId && a.DriverIn != playerId && a.TeamId != saveGame.PlayerData.TeamId && a.Reason != AbsenceReason.PAID_RIDE)
                .ToList();
            result.PaidRideOffers = PaidRides.Offers(saveGame, raceIndex);
            result.PaidRidesBudget = PaidRides.Budget(saveGame);
            result.PaidRidesRemaining = PaidRides.Remaining(saveGame);
            return result;
        }
    }
}
