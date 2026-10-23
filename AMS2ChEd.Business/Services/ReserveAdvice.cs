using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;

namespace AMS2ChEd.Business.Services
{
    // the race seats at a team whose contracts end after next season
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
        // a seat isn't under contract next season yet: the lineup isn't known
        Unknown
    }

    /// <summary>
    /// The agent's rough reading of a reserve role at a team: no replay, only the market outlook,
    /// the contracts and the projected reputations. A reserve's first refusal comes the winter after
    /// next, so a seat counts as opening in the future when its contract runs through next season and
    /// ends then. The team's reputation is assumed not to change.
    /// </summary>
    public class ReserveAdvice
    {
        // the market outlook rates one of the team's race seats this winter Likely or better for the player
        public bool OfficialSeatNow { get; init; }

        public FutureSeats FutureSeats { get; init; }

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
            var officialSeatNow = outlook.Seats.Any(s => s.TeamId == team.TeamId && !s.IsReserve && s.Band >= MarketChance.Likely);

            var contracts = new[] { team.Driver1Contract, team.Driver2Contract };
            var contractedNextSeason = contracts.Select(c => IsContracted(c) && c.Races - racesThisSeason > 0).ToArray();
            var opensInTheFuture = contracts.Select((c, i) => contractedNextSeason[i] && c.Races - racesThisSeason - racesNextSeason <= 0).ToArray();

            DriverReputation ProjectedReputation(DriverContract contract) =>
                outlook.ProjectedReputations.TryGetValue(contract.DriverId, out var reputation) ? reputation : DriverReputation.PRIME_MIDFIELD;

            // the roles the team would hire for, as the off-season does (EndOfSeasonManager.GetJobAdRoles)
            var futureRoles = new List<DriverRole>();
            var futureSeats = FutureSeats.None;
            if (opensInTheFuture[0] && opensInTheFuture[1])
            {
                futureRoles.Add(DriverRole.FIRST_DRIVER);
                futureRoles.Add(DriverRole.SECOND_DRIVER);
                futureSeats = FutureSeats.Both;
            }
            else if (opensInTheFuture[0] || opensInTheFuture[1])
            {
                var staying = opensInTheFuture[0] ? contracts[1] : contracts[0];
                var role = IsContracted(staying)
                    ? DriverHirer.GetRoleToHireAlongside(staying.Role, ProjectedReputation(staying), teamReputation)
                    : DriverRole.FIRST_DRIVER;
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
                WouldBePromoted = wouldBePromoted,
                RaceTime = raceTime
            };
        }

        private static bool IsContracted(DriverContract contract) => !string.IsNullOrEmpty(contract?.DriverId);
    }
}
