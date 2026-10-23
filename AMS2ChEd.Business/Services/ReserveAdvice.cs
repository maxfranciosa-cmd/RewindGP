using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;

namespace AMS2ChEd.Business.Services
{
    // the race seats at a team that open the winter after next, when a reserve could be promoted
    public enum FutureSeats
    {
        None,
        FirstDriver,
        SecondDriver,
        Both
    }

    // whether a reserve could get races next season, read from the team's contracted pair
    public enum ReserveRaceTime
    {
        // both seats under contract next season, the two drivers a level or less apart: they may clash
        PossibleClash,
        // both seats under contract next season, with a clear gap between the two drivers
        UnlikelyClash,
        // a seat isn't settled for next season yet: the lineup isn't known
        Unknown
    }

    /// <summary>
    /// The agent's rough reading of a reserve role at a team: no replay, only the market outlook,
    /// the contracts and the projected reputations. A reserve's first refusal comes the winter after
    /// next. A seat opens then for sure when its contract runs through next season and ends there; it
    /// may open then when it changes hands this winter, as a new driver usually signs for one season
    /// (EndOfSeasonManager gives a season's races, one more only to a PerfectFit). The team's
    /// reputation is assumed not to change.
    /// </summary>
    public class ReserveAdvice
    {
        // the market outlook gives the player a seat at the team this winter as likely to open for him
        // as not: a seat the outlook rates this likely or better is worth applying for instead
        public const MarketChance OfficialSeatBand = MarketChance.Likely;

        // a seat the outlook expects to open this winter at least this often (expiry, drop, retirement)
        // isn't counted as under contract next season
        public const double OpensNowChance = 0.5;

        // the market outlook rates one of the team's race seats this winter Likely or better for the player
        public bool OfficialSeatNow { get; init; }

        public FutureSeats FutureSeats { get; init; }

        // at least one of FutureSeats only may open: it changes hands this winter, and the new
        // driver's contract isn't known yet
        public bool FutureSeatsUncertain { get; init; }

        // a PerfectFit for one of the future seats: the team would promote them (first refusal)
        public bool WouldBePromoted { get; init; }

        public ReserveRaceTime RaceTime { get; init; }

        /// <param name="team">the team this season, with its contracts</param>
        /// <param name="teamReputation">the team's reputation next season, assumed to stay the same</param>
        /// <param name="racesThisSeason">this season's races, which the contracts run through first</param>
        /// <param name="racesNextSeason">next season's races</param>
        /// <param name="outlook">the market outlook, with every driver's projected reputation</param>
        /// <param name="playerReputationWhenPromoted">the player's reputation when the team would decide,
        /// the winter after next</param>
        public static ReserveAdvice For(ITeamEntry team, TeamReputation teamReputation, int racesThisSeason, int racesNextSeason,
            MarketOutlook outlook, DriverReputation playerReputationWhenPromoted)
        {
            var raceSeats = outlook.Seats.Where(s => s.TeamId == team.TeamId && !s.IsReserve).ToList();
            var officialSeatNow = raceSeats.Any(s => s.Band >= OfficialSeatBand);

            var contracts = new[] { team.Driver1Contract, team.Driver2Contract };

            // this winter: the contract ends, or the outlook expects the seat to open (a retirement, a drop)
            var opensNow = contracts.Select((c, i) =>
                !IsContracted(c) || c.Races - racesThisSeason <= 0 ||
                raceSeats.Any(s => s.Slot == i + 1 && s.OpenChance >= OpensNowChance)).ToArray();
            var contractedNextSeason = contracts.Select((c, i) => IsContracted(c) && !opensNow[i]).ToArray();

            // the winter after next: for sure when the contract ends after next season, maybe when the
            // seat changes hands this winter
            var opensLater = contracts.Select((c, i) => contractedNextSeason[i] && c.Races - racesThisSeason - racesNextSeason <= 0).ToArray();
            var mayOpenLater = contracts.Select((c, i) => opensNow[i] && IsContracted(c)).ToArray();
            var future = contracts.Select((c, i) => opensLater[i] || mayOpenLater[i]).ToArray();

            DriverReputation ProjectedReputation(DriverContract contract) =>
                outlook.ProjectedReputations.TryGetValue(contract.DriverId, out var reputation) ? reputation : DriverReputation.PRIME_MIDFIELD;

            var futureRoles = new List<DriverRole>();
            var futureSeats = FutureSeats.None;
            if (future[0] && future[1])
            {
                futureRoles.Add(DriverRole.FIRST_DRIVER);
                futureRoles.Add(DriverRole.SECOND_DRIVER);
                futureSeats = FutureSeats.Both;
            }
            else if (future[0] || future[1])
            {
                var opening = future[0] ? 0 : 1;
                var role = FutureRole(contracts[opening], contracts[1 - opening], contractedNextSeason[1 - opening], teamReputation, ProjectedReputation);
                futureRoles.Add(role);
                futureSeats = role == DriverRole.FIRST_DRIVER ? FutureSeats.FirstDriver : FutureSeats.SecondDriver;
            }

            var driverHirer = new DriverHirer();
            var wouldBePromoted = futureRoles.Any(role =>
                driverHirer.DoesDriverFitTeamPolicy(playerReputationWhenPromoted, role, teamReputation) == DriverHirer.DriverPolicyFit.PerfectFit);

            var raceTime = ReserveRaceTime.Unknown;
            if (contractedNextSeason[0] && contractedNextSeason[1])
            {
                var gap = Math.Abs(DriverHirer.ReputationScore(ProjectedReputation(contracts[0])) - DriverHirer.ReputationScore(ProjectedReputation(contracts[1])));
                raceTime = gap <= 1 ? ReserveRaceTime.PossibleClash : ReserveRaceTime.UnlikelyClash;
            }

            return new ReserveAdvice
            {
                OfficialSeatNow = officialSeatNow,
                FutureSeats = futureSeats,
                FutureSeatsUncertain = futureSeats != FutureSeats.None && Enumerable.Range(0, 2).Any(i => future[i] && !opensLater[i]),
                WouldBePromoted = wouldBePromoted,
                RaceTime = raceTime
            };
        }

        // the role of the one seat opening: hired alongside a teammate who is there next season, as the
        // off-season does (EndOfSeasonManager.GetJobAdRoles); otherwise the seat's own contract role, or
        // for drivers racing as equals, what its driver's standing made it
        private static DriverRole FutureRole(DriverContract opening, DriverContract teammate, bool teammateContractedNextSeason,
            TeamReputation teamReputation, Func<DriverContract, DriverReputation> projectedReputation)
        {
            if (teammateContractedNextSeason)
                return DriverHirer.GetRoleToHireAlongside(teammate.Role, projectedReputation(teammate), teamReputation);

            if (opening.Role == ContractRole.FIRST_DRIVER)
                return DriverRole.FIRST_DRIVER;
            if (opening.Role == ContractRole.SECOND_DRIVER)
                return DriverRole.SECOND_DRIVER;

            return DriverHirer.IsFirstDriverMaterial(projectedReputation(opening), teamReputation) ? DriverRole.FIRST_DRIVER : DriverRole.SECOND_DRIVER;
        }

        private static bool IsContracted(DriverContract contract) => !string.IsNullOrEmpty(contract?.DriverId);
    }
}
