using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Handles contract negotiation logic between player and teams
    /// </summary>
    public class ContractNegotiationEngine : IContractNegotiationEngine
    {
        private readonly DriverHirer _driverHirer;

        public event EventHandler<ContractOfferEventArgs> ContractOfferGenerated;

        public ContractNegotiationEngine()
        {
            _driverHirer = new DriverHirer();
        }

        /// <summary>
        /// Evaluate if the team would hire the player over the current driver
        /// </summary>
        public ContractOfferResult EvaluateContract(
            string playerDriverId,
            DriverReputation playerReputation,
            string replacedDriverId,
            DriverReputation replacedDriverReputation)
        {
            var playerResume = new DriverResume
            {
                Id = playerDriverId,
                Reputation = playerReputation
            };

            var replacedDriverResume = new DriverResume
            {
                Id = replacedDriverId,
                Reputation = replacedDriverReputation
            };

            var winner = _driverHirer.PickWinner(replacedDriverResume, playerResume);
            bool playerHired = (winner.Id == playerDriverId);

            var result = new ContractOfferResult
            {
                IsPlayerHired = playerHired,
                PlayerReputation = playerReputation,
                ReplacedDriverReputation = replacedDriverReputation,
                SuccessMessage = playerHired ? GenerateSuccessMessage(playerReputation) : null,
                RejectionMessage = !playerHired ? GenerateRejectionMessage(playerReputation, replacedDriverReputation) : null
            };

            ContractOfferGenerated?.Invoke(this, new ContractOfferEventArgs { Result = result });
            return result;
        }

        private string GenerateSuccessMessage(DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD =>
                    "While we recognize that your budget brings valuable resources to our team, we also see potential in your raw talent. This opportunity allows you to demonstrate your abilities when it matters most.",

                DriverReputation.PAY_DRIVER_SEASON =>
                    "Your financial backing has secured you this seat, but we expect you to prove that you deserve it through your performance on track. Show us what you're capable of.",

                DriverReputation.YOUNG_TALENT =>
                    "Your recent performances have caught our attention. While you're still developing as a driver, we see tremendous potential in you. We're willing to take a chance on your raw talent and give you the opportunity to learn and grow with us.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    "You've shown flashes of brilliance that suggest you could be championship material. However, you haven't yet proven you can sustain that level consistently. We believe our team can provide the environment for you to take that final step.",

                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL =>
                    "Your accomplishments speak for themselves. You've proven you have the talent to fight for championships, and we want that winning mentality in our garage. We believe together we can achieve great success.",

                DriverReputation.PRIME_MIDFIELD =>
                    "Your consistency and reliability are exactly what our team needs. You may not grab the headlines, but you deliver solid results race after race, and that's invaluable to us.",

                DriverReputation.PRIME_STRONG_MIDFIELD =>
                    "You've consistently punched above your weight in the midfield, and we believe you're ready for the next challenge. Your ability to maximize every opportunity makes you an ideal candidate for our team.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN =>
                    "You've demonstrated race-winning pace, but the championship has eluded you so far. We believe our team can provide the platform for you to finally challenge for the title you deserve.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL =>
                    "As a proven champion, you bring the winning experience and mentality we need. Your track record speaks for itself, and we're honored to have you join our team.",

                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED =>
                    "While some might say your best days are behind you, we see a champion hungry to prove the doubters wrong. Your experience and determination make you the perfect fit for our team.",

                DriverReputation.AGEING_MIDFIELD =>
                    "Your years of experience make you a safe pair of hands. In our position, reliability and consistency are exactly what we need, and you deliver that every weekend.",

                DriverReputation.AGEING_STRONG_MIDFIELD =>
                    "Your veteran experience combined with your continued strong performances make you an ideal addition to our lineup. You know how to extract the maximum from the car.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL =>
                    "Age is just a number, and you continue to prove you can compete at the highest level. Your experience and racecraft are invaluable assets to our team.",

                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED =>
                    "You may have fallen from your peak, but we believe you still have the fire within. We're giving you the chance to recapture past glory and silence your critics.",

                DriverReputation.JUST_ONE_LAST_DANCE =>
                    "Your legendary status still carries weight in this sport. While we acknowledge you're past your absolute prime, your experience, racecraft, and sheer determination make this partnership intriguing. This could be one final glorious chapter - for both of us.",

                _ => "Your dedication and passion for racing have impressed us, and we believe you deserve this opportunity."
            };
        }

        private string GenerateRejectionMessage(DriverReputation playerReputation, DriverReputation replacedReputation)
        {
            // Logic for rejection based on comparison
            if (replacedReputation > playerReputation)
            {
                return "After careful consideration, we believe our current driver lineup better suits our team's objectives at this time. " +
                       "We appreciate your interest and wish you the best in finding the right opportunity for your talents.";
            }
            else
            {
                return "While we were impressed by your credentials, we've decided to proceed with our current driver. " +
                       "The decision was not easy, but we feel our existing lineup aligns better with our strategic goals for this season. " +
                       "We encourage you to continue pursuing opportunities that match your abilities.";
            }
        }
    }
}