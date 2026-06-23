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
        public string ExitingDriverId { get; set;}

        public bool ExitingDriverWillingToRenew { get; set; }
    }

    public class TeamHiring
    {
        public string TeamId { get; set; }
        public string DriverId { get; set; }

        public DriverRole Role { get; set; }

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


    public interface IOffSeasonMovements
    {
        IEnumerable<TeamHiringBallot> DriversProposeToTeams(IEnumerable<UnemployedDriver> poolOfDrivers, IEnumerable<TeamHiring> currentTeamPickings);

        IEnumerable<DropTeamResult> DropDrivers(IEnumerable<TeamSituation> teams);
        IEnumerable<TeamHiring> FinalBallotResults(IEnumerable<TeamHiringBallot> ballots);

        IEnumerable<TeamHiring> PickPotentialNewDrivers(IEnumerable<TeamJobAd> jobAds, IEnumerable<UnemployedDriver> poolOfDrivers, out List<TeamJobAd> adsWithNoPotentialCandidates);
    }
}
