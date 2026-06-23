using AMS2ChEd.Business.Models.Concrete;
using System.Runtime.CompilerServices;

namespace AMS2ChEd.Business.Services
{
    public enum DriverFirerOutcome
    {
        NOT_DROPPED,
        DROPPED_CONTRACT_EXPIRED,
        DROPPED_UNDERPERFORMING,
        DROPPED_RETIRING,
        DROPPED_TEAM_QUITTING,
        DROPPED_PLAYER_REJECTING
    }

    public static class DriverFirerOutcomeExtension
    {
        public static bool IsDropped(this DriverFirerOutcome driverFirerOutcome)
        {
            return driverFirerOutcome != DriverFirerOutcome.NOT_DROPPED;
        }
    }


    public class DriverFirer
    {

        private DriverReputation[] topTeamDrops = new[]
        {
            DriverReputation.PAY_DRIVER_WILD_CARD,
            DriverReputation.PAY_DRIVER_SEASON,
            DriverReputation.YOUNG_TALENT,
            DriverReputation.PRIME_MIDFIELD,
            DriverReputation.AGEING_MIDFIELD
        };

        private DriverReputation[] midfieldHighDrops = new[]
        {
            DriverReputation.PAY_DRIVER_WILD_CARD,
            DriverReputation.PAY_DRIVER_SEASON,
            DriverReputation.YOUNG_TALENT,
            DriverReputation.AGEING_MIDFIELD
        };

        private DriverReputation[] midfielDrops = new[]
{
            DriverReputation.PAY_DRIVER_WILD_CARD
        };

        public DriverFirerOutcome WillDropDriver(TeamReputation teamReputation, DriverReputation driverNewReputation, int racesLeft, bool isDriverRetiring, bool isTeamQuitting)
        {
            if (isDriverRetiring)
                return DriverFirerOutcome.DROPPED_RETIRING;

            if (isTeamQuitting)
                return DriverFirerOutcome.DROPPED_TEAM_QUITTING;

            if (racesLeft <= 0)
                return DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED;

            switch(teamReputation)
            {
                case TeamReputation.TOP_TEAM:
                    return topTeamDrops.Contains(driverNewReputation) ? DriverFirerOutcome.DROPPED_UNDERPERFORMING : DriverFirerOutcome.NOT_DROPPED ;
                case TeamReputation.MIDFIELD_HIGH:
                    return midfieldHighDrops.Contains(driverNewReputation) ? DriverFirerOutcome.DROPPED_UNDERPERFORMING : DriverFirerOutcome.NOT_DROPPED;
                case TeamReputation.MIDFIELD:
                    return midfielDrops.Contains(driverNewReputation)? DriverFirerOutcome.DROPPED_UNDERPERFORMING : DriverFirerOutcome.NOT_DROPPED;
                default:
                    return DriverFirerOutcome.NOT_DROPPED;
            }
        }
    }
}
