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
