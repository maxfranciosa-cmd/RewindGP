using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public interface IContractNegotiationEngine
    {
        event EventHandler<ContractOfferEventArgs> ContractOfferGenerated;
        ContractOfferResult EvaluateContract(
            string playerDriverId,
            DriverReputation playerReputation,
            string replacedDriverId,
            DriverReputation replacedDriverReputation);

        /// <summary>
        /// Whether the team would take the player as its reserve over the season pack's one, judged
        /// on the reserve policy (DriverHirer.DoesDriverFitReservePolicy): easier than a race seat.
        /// </summary>
        ContractOfferResult EvaluateReserveContract(
            DriverReputation playerReputation,
            DriverReputation replacedReserveReputation,
            TeamReputation teamReputation);
    }
    public class ContractOfferResult
    {
        public bool IsPlayerHired { get; set; }
        public DriverReputation PlayerReputation { get; set; }
        public DriverReputation ReplacedDriverReputation { get; set; }
        public string SuccessMessage { get; set; }
        public string RejectionMessage { get; set; }
    }

    public class ContractOfferEventArgs : EventArgs
    {
        public ContractOfferResult Result { get; set; }
    }
}
