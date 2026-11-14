using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Business.Helpers
{
    /// <summary>An open seat as the academy applications see it.</summary>
    public readonly record struct AcademySeat(string TeamId, DriverRole Role, bool IsReserve);

    /// <summary>
    /// The seats an academy member on the market applies to automatically this winter, whatever
    /// his ambition. One table for everyone: the off-season (AI members and the player's locked
    /// applications), the market outlook and the application window all go through it.
    /// </summary>
    public static class AcademyApplications
    {
        /// <summary>
        /// The open seats the driver applies to automatically, given his race seat this season:
        /// - second driver at a sister team: first driver there, first and second driver at the parent teams;
        /// - first driver at a sister team: first and second driver at the parent teams;
        /// - no race seat (reserve or unemployed): second driver at the sister teams, reserve at the parent teams;
        ///   rated YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN or better (IsChampionshipProspect), also first and second driver at the parent teams.
        /// Nobody else gets any: a member racing for a parent team or outside his academy, and a
        /// driver who is no member of an academy in the season being hired for. Only call it for a
        /// driver who is on the market this winter: a running contract means no applications.
        /// "First driver": a FIRST_DRIVER contract, or EQUAL and first-driver material for the team.
        /// </summary>
        public static List<T> PrefilledSeats<T>(ISeason currentSeason, AcademyContext academies, string driverId, DriverReputation reputation, IEnumerable<T> openSeats, Func<T, AcademySeat> describe)
        {
            var result = new List<T>();
            var applies = Rule(currentSeason, academies, driverId, reputation);
            if (applies == null || openSeats == null)
                return result;

            result.AddRange(openSeats.Where(s => applies(describe(s))));
            return result;
        }

        /// <summary>
        /// The table of PrefilledSeats as a test on one seat, or null when the driver applies
        /// nowhere automatically (the market outlook checks seats one at a time).
        /// </summary>
        public static Func<AcademySeat, bool> Rule(ISeason currentSeason, AcademyContext academies, string driverId, DriverReputation reputation)
        {
            var academy = academies?.AcademyOf(driverId);
            return academy == null ? null : RuleFor(currentSeason, academy, driverId, reputation);
        }

        private static Func<AcademySeat, bool> RuleFor(ISeason currentSeason, Academy academy, string driverId, DriverReputation reputation)
        {
            bool IsParent(string teamId) => AcademyMembership.IsParentTeam(academy, teamId);
            bool IsSister(string teamId) => AcademyMembership.IsSisterTeam(academy, teamId);

            var raceTeam = currentSeason?.Teams?.FirstOrDefault(t => t.Driver1Contract?.DriverId == driverId || t.Driver2Contract?.DriverId == driverId);
            if (raceTeam == null)
            {
                // a championship-level prospect skips the sister rung: the parent team's race seats too
                if (IsChampionshipProspect(reputation))
                    return s => s.IsReserve ? IsParent(s.TeamId) : IsParent(s.TeamId) || (s.Role == DriverRole.SECOND_DRIVER && IsSister(s.TeamId));
                return s => s.IsReserve ? IsParent(s.TeamId) : s.Role == DriverRole.SECOND_DRIVER && IsSister(s.TeamId);
            }

            if (!IsSister(raceTeam.TeamId))
                return null;

            var contract = raceTeam.Driver1Contract?.DriverId == driverId ? raceTeam.Driver1Contract : raceTeam.Driver2Contract;
            if (IsFirstDriver(contract, reputation, raceTeam.Reputation))
                return s => !s.IsReserve && IsParent(s.TeamId);

            return s => !s.IsReserve && (IsParent(s.TeamId) || (s.TeamId == raceTeam.TeamId && s.Role == DriverRole.FIRST_DRIVER));
        }

        /// <summary>
        /// A member rated YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN or better (DriverHirer.ReputationScore 5+,
        /// what an outstanding FP1 season earns): without a race seat he also applies to the parent
        /// teams' race seats.
        /// </summary>
        public static bool IsChampionshipProspect(DriverReputation reputation) =>
            DriverHirer.ReputationScore(reputation) >= DriverHirer.ReputationScore(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN);

        public static bool IsFirstDriver(DriverContract contract, DriverReputation reputation, TeamReputation teamReputation) =>
            contract?.Role == ContractRole.FIRST_DRIVER ||
            (contract?.Role == ContractRole.EQUAL && DriverHirer.IsFirstDriverMaterial(reputation, teamReputation));

        /// <summary>The open seats of the ballots: those the market sees (a guaranteed one never opens).</summary>
        public static IEnumerable<Services.Contracts.TeamHiringBallot> OpenBallots(IEnumerable<Services.Contracts.TeamHiringBallot> ballots) =>
            ballots.Where(b => !b.OriginalTeamHiring.Guaranteed);

        public static AcademySeat Describe(Services.Contracts.TeamHiringBallot ballot) =>
            new AcademySeat(ballot.OriginalTeamHiring.TeamId, ballot.OriginalTeamHiring.Role, ballot.OriginalTeamHiring.IsReserve);

        /// <summary>The ballots the driver applies to automatically (see PrefilledSeats).</summary>
        public static List<Services.Contracts.TeamHiringBallot> PrefilledBallots(ISeason currentSeason, AcademyContext academies, string driverId, DriverReputation reputation, IEnumerable<Services.Contracts.TeamHiringBallot> ballots) =>
            PrefilledSeats(currentSeason, academies, driverId, reputation, OpenBallots(ballots), Describe);
    }
}
