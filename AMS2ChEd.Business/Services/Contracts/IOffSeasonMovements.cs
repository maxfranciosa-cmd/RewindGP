using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AMS2ChEd.Business.Services.OffSeasonMovements;

namespace AMS2ChEd.Business.Services.Contracts
{

    public class DriverSituation
    {
        public string DriverId { get; set; }
        public DriverReputation Reputation { get; set; }

        public int RacesLeftInContract { get; set; }
        public bool DriverRetiring { get; set; }
    }

    public class TeamSituation
    {
        public string TeamId { get; set; }
        public TeamReputation Reputation { get; set; }
        public DriverSituation Driver1 { get; set; }
        public DriverSituation Driver2 { get; set; }
        public bool TeamQuitting { get; set; }

    }

    public class DropTeamResult
    {
        public string TeamId { get; set; }
        public DriverFirerOutcome DropDriver1 { get; set; }

        public DriverFirerOutcome DropDriver2 { get; set; }
    }

    public class TeamJobAd
    {
        public string TeamId { get; set; }

        public TeamReputation TeamReputation { get; set; }

        public DriverRole Role { get; set; }

        // the contract seat (1 = Driver1Contract, 2 = Driver2Contract, ReserveSlot = ReserveContract)
        // being filled - independent from Role, which is only the kind of driver the team is looking for
        public int Slot { get; set; }
        public string ExitingDriverId { get; set;}

        public bool ExitingDriverWillingToRenew { get; set; }

        public bool IsReserve => Slot == TeamHiring.ReserveSlot;

        // reserve seats only: the driver the next season's pack names as the team's reserve, the
        // team's pick when they're free (history tends to repeat without being forced)
        public string PreferredDriverId { get; set; }
    }

    public class TeamHiring
    {
        // the slot of a team's reserve seat (ReserveContract). Reserve ballots run after every race
        // seat is settled, among the drivers still without one
        public const int ReserveSlot = 3;

        public string TeamId { get; set; }
        public string DriverId { get; set; }

        // for a reserve seat it means nothing: reserves are judged on DriverHirer.reservePolicies
        public DriverRole Role { get; set; }

        // the contract seat (1 = Driver1Contract, 2 = Driver2Contract, ReserveSlot = ReserveContract) this hiring fills
        public int Slot { get; set; }

        // a hiring with no explicit slot fills the seat historically tied to its role
        public int GetSlot() => Slot != 0 ? Slot : (Role == DriverRole.FIRST_DRIVER ? 1 : 2);

        public bool IsReserve => Slot == ReserveSlot;

        // already agreed before the market opens: a reserve promoted to a race seat, or the player
        // staying on as a team's reserve. The driver signs unless they've been hired before this
        // ballot is reached; the market never sees the seat (TeamApplicationWindow hides it)
        public bool Guaranteed { get; set; }

        public DriverReputation DriverReputation { get; set; }

        public TeamReputation TeamReputation { get; set; }

        public HashSet<string> ExcludeDriverIds { get; set; }

        public IEnumerable<DriverResume> OtherPotentialCandidates { get; set; }
    }

    public class UnemployedDriver
    {
        public string DriverId { get; set; }

        public DriverReputation Reputation { get; set; }
    }


    public class Ambition
    {
        public TeamReputation MinReputation { get; set; }
        public TeamReputation MaxReputation { get; set; }
    }

    public class TeamHiringBallot
    {
        public TeamHiring OriginalTeamHiring { get; set; }

        public IEnumerable<TeamHiringBallotCandidate> Candidates { get; set; }

    }

    public class TeamHiringBallotCandidate
    {
        public string DriverId { get; set; }

        public DriverReputation DriverReputation { get; set; }
    }

    /// <summary>
    /// The off-season's reserve decisions, made before the teams' provisional picks
    /// (EndOfSeasonManager.TeamPicksPotentialReplacementsDrivers).
    /// </summary>
    public class ReservePlan
    {
        // the teams that run a reserve next season (EndOfSeasonManager.RollReserveWishes)
        public HashSet<string> TeamsWantingReserve { get; set; } = new();

        // the player, as a team's reserve, accepted the promotion to this team's race seat
        public ReservePromotion PlayerPromotion { get; set; }

        // the player accepted to stay on as this team's reserve
        public string PlayerStaysReserveAtTeamId { get; set; }
    }

    /// <summary>
    /// A race seat opening at the team a reserve drives for, and how the reserve fits it
    /// (first refusal: the team promotes them only when they're a PerfectFit).
    /// </summary>
    public class ReservePromotion
    {
        public string TeamId { get; set; }
        public int Slot { get; set; }
        public DriverRole Role { get; set; }
        public DriverHirer.DriverPolicyFit Fit { get; set; }
        public bool IsOffered => Fit == DriverHirer.DriverPolicyFit.PerfectFit;
    }


    public interface IOffSeasonMovements
    {
        IEnumerable<TeamHiringBallot> DriversProposeToTeams(IEnumerable<UnemployedDriver> poolOfDrivers, IEnumerable<TeamHiring> currentTeamPickings);

        IEnumerable<DropTeamResult> DropDrivers(IEnumerable<TeamSituation> teams);
        IEnumerable<TeamHiring> FinalBallotResults(IEnumerable<TeamHiringBallot> ballots);

        IEnumerable<TeamHiring> PickPotentialNewDrivers(IEnumerable<TeamJobAd> jobAds, IEnumerable<UnemployedDriver> poolOfDrivers, out List<TeamJobAd> adsWithNoPotentialCandidates);
    }
}
