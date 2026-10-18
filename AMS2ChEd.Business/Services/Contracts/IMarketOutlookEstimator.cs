using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.Services.Contracts
{
    public enum MarketChance
    {
        LongShot,
        Possible,
        Likely,
        VeryLikely
    }

    // one seat the player could apply for next season
    public class SeatOutlook
    {
        public string TeamId { get; set; }

        // the contract seat (1 = Driver1Contract, 2 = Driver2Contract)
        public int Slot { get; set; }

        public DriverRole Role { get; set; }

        public TeamReputation TeamReputation { get; set; }

        // the driver leaving the seat, null for a team that's new next season
        public string ExitingDriverId { get; set; }

        // why the driver leaves; NOT_DROPPED when the seat only opens if they retire
        public DriverFirerOutcome ExitingDriverOutcome { get; set; }

        public double OpenChance { get; set; }

        // the player's chance to sign, if the seat opens and they apply for it
        public double WinChanceIfOpen { get; set; }

        public double Chance => OpenChance * WinChanceIfOpen;

        public MarketChance Band { get; set; }

        public DriverHirer.DriverPolicyFit PlayerFit { get; set; }

        // drivers likely still free when the team picks, fitting the seat better than the player
        public List<string> BetterFitRivals { get; set; } = new();

        // drivers likely still free when the team picks, fitting the seat as well as the player
        public List<string> SameFitRivals { get; set; } = new();
    }

    public class MarketOutlook
    {
        public DriverReputation PlayerReputation { get; set; }

        // null when the player has no team this season
        public string PlayerTeamId { get; set; }

        // what the player's team decides about them: NOT_DROPPED means it keeps them
        public DriverFirerOutcome PlayerContractOutcome { get; set; }

        // the seats the player could apply for, most likely first
        public List<SeatOutlook> Seats { get; set; } = new();

        public List<SeatOutlook> BestApplications { get; set; } = new();

        // the chance of being hired somewhere when applying for BestApplications
        public double HiredChance { get; set; }

        public MarketChance HiredBand { get; set; }
    }

    public interface IMarketOutlookEstimator
    {
        /// <summary>
        /// What the driver market would offer the player if the season ended today. An estimate
        /// read from the off-season rules (EndOfSeasonManager, OffSeasonMovements) rather than a
        /// simulation of them: cheap enough to recompute after every race. The save isn't changed.
        /// </summary>
        MarketOutlook Estimate(ISaveGame saveGame, ISeason nextSeason, IReadOnlyDictionary<string, IDriverData> nextSeasonDrivers);
    }
}
