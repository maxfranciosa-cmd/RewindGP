using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;
using System.Net;

namespace AMS2ChEd.Business.Services
{
    public class ReputationUpdater : IReputationUpdater
    {
        public static IEnumerable<DriverReputation> AvailableReputationForAge(int age)
        {
            yield return DriverReputation.PAY_DRIVER_WILD_CARD;
            yield return DriverReputation.PAY_DRIVER_SEASON;

            if (age < YOUNG_DRIVER_AGE)
            {
                yield return DriverReputation.YOUNG_TALENT;
                if (age >= MINIMUM_AGE)
                {
                    yield return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    yield return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL;
                }
            }
            else if (age < OLD_DRIVER_AGE)
            {
                yield return DriverReputation.PRIME_MIDFIELD;
                yield return DriverReputation.PRIME_STRONG_MIDFIELD;
                yield return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                yield return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL;
                yield return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED;
            }
            else
            {
                yield return DriverReputation.AGEING_MIDFIELD;
                yield return DriverReputation.AGEING_STRONG_MIDFIELD;
                yield return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                yield return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;

                if (age > 40)
                {
                    yield return DriverReputation.JUST_ONE_LAST_DANCE;
                }
            }

        }

        public const int MINIMUM_AGE = 18;
        public const int YOUNG_DRIVER_AGE = 25;
        public const int OLD_DRIVER_AGE = 31;

        private DriverReputation EvaluateWildCard(int age, int podiums, int races, int dnfs)
        {
            if (dnfs > 0 || races < 3) return DriverReputation.PAY_DRIVER_WILD_CARD;

            if (age < YOUNG_DRIVER_AGE)
            {
                if (podiums > 0)
                {
                    return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                }
                else
                {
                    return DriverReputation.YOUNG_TALENT;
                }
            }
            else if (age < OLD_DRIVER_AGE)
            {
                if (podiums > 0)
                {
                    return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                }
                else
                {
                    return DriverReputation.PRIME_MIDFIELD;
                }
            }
            else
            {
                if (podiums > 0)
                {
                    return DriverReputation.AGEING_STRONG_MIDFIELD;
                }
                else
                {
                    return DriverReputation.PAY_DRIVER_WILD_CARD;
                }
            }
        }

        public DriverReputation GetNewReputationForInactiveDriver(DriverReputation currentReputation, int age)
        {
            if (age < YOUNG_DRIVER_AGE)
            {
                return GetRelatedYoungReputation(currentReputation);
            }
            else if (age < OLD_DRIVER_AGE)
            {
                return GetRelatedPrimeReputation(currentReputation);
            }
            else
            {
                return GetRelatedAgeingReputation(currentReputation);
            }

        }
        private DriverReputation GetRelatedYoungReputation(DriverReputation currentReputation)
        {
            switch (currentReputation)
            {
                case DriverReputation.PAY_DRIVER_WILD_CARD: return DriverReputation.PAY_DRIVER_WILD_CARD;
                case DriverReputation.PAY_DRIVER_SEASON: return DriverReputation.PAY_DRIVER_SEASON;
                case DriverReputation.AGEING_MIDFIELD: return DriverReputation.YOUNG_TALENT;
                case DriverReputation.YOUNG_TALENT: return DriverReputation.YOUNG_TALENT;
                case DriverReputation.PRIME_MIDFIELD: return DriverReputation.YOUNG_TALENT;
                case DriverReputation.AGEING_STRONG_MIDFIELD: return DriverReputation.YOUNG_TALENT;
                case DriverReputation.JUST_ONE_LAST_DANCE: return DriverReputation.YOUNG_TALENT;
                case DriverReputation.PRIME_STRONG_MIDFIELD: return DriverReputation.YOUNG_TALENT;
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED: return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED: return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN: return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN: return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL: return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL: return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL;
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL: return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL;
                default: throw new Exception($"reputation not valid: {currentReputation}");
            }
        }

        private DriverReputation GetRelatedPrimeReputation(DriverReputation currentReputation)
        {
            switch (currentReputation)
            {
                case DriverReputation.PAY_DRIVER_WILD_CARD: return DriverReputation.PAY_DRIVER_WILD_CARD;
                case DriverReputation.PAY_DRIVER_SEASON: return DriverReputation.PAY_DRIVER_SEASON;
                case DriverReputation.AGEING_MIDFIELD: return DriverReputation.PRIME_MIDFIELD;
                case DriverReputation.YOUNG_TALENT: return DriverReputation.PRIME_MIDFIELD;
                case DriverReputation.PRIME_MIDFIELD: return DriverReputation.PRIME_MIDFIELD;
                case DriverReputation.AGEING_STRONG_MIDFIELD: return DriverReputation.PRIME_STRONG_MIDFIELD;
                case DriverReputation.JUST_ONE_LAST_DANCE: return DriverReputation.PRIME_STRONG_MIDFIELD;
                case DriverReputation.PRIME_STRONG_MIDFIELD: return DriverReputation.PRIME_STRONG_MIDFIELD;
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED: return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED: return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN: return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN: return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL: return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL: return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL;
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL: return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL;
                default: throw new Exception($"reputation not valid: {currentReputation}");
            }
        }

        private DriverReputation GetRelatedAgeingReputation(DriverReputation currentReputation)
        {
            switch (currentReputation)
            {
                case DriverReputation.PAY_DRIVER_WILD_CARD: return DriverReputation.PAY_DRIVER_WILD_CARD;
                case DriverReputation.PAY_DRIVER_SEASON: return DriverReputation.PAY_DRIVER_SEASON;
                case DriverReputation.AGEING_MIDFIELD: return DriverReputation.AGEING_MIDFIELD;
                case DriverReputation.YOUNG_TALENT: return DriverReputation.AGEING_MIDFIELD;
                case DriverReputation.PRIME_MIDFIELD: return DriverReputation.AGEING_MIDFIELD;
                case DriverReputation.AGEING_STRONG_MIDFIELD: return DriverReputation.AGEING_STRONG_MIDFIELD;
                case DriverReputation.JUST_ONE_LAST_DANCE: return DriverReputation.JUST_ONE_LAST_DANCE;
                case DriverReputation.PRIME_STRONG_MIDFIELD: return DriverReputation.AGEING_STRONG_MIDFIELD;
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED: return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED: return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN: return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN: return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL: return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL: return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL: return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                default: throw new Exception($"reputation not valid: {currentReputation}");
            }
        }

        public DriverReputation GetNewReputation(DriverReputation currentReputation, int age, int standings, int podiums, int dnfs, int races)
        {
            if (currentReputation == DriverReputation.PAY_DRIVER_WILD_CARD)
            {
                return EvaluateWildCard(age, podiums, dnfs, races);
            }

            if (age < YOUNG_DRIVER_AGE)
            {
                return GetNewYoungReputation(currentReputation, age, standings, podiums, dnfs);
            }
            else if (age < OLD_DRIVER_AGE)
            {
                return GetNewPrimeReputation(currentReputation, age, standings, podiums, dnfs);
            }
            else
            {
                return GetNewAgeingReputation(currentReputation, age, standings, podiums, dnfs);
            }
        }

        private DriverReputation GetNewAgeingReputation(DriverReputation currentReputation, int age, int standings, int podiums, int dnfs)
        {
            if (standings == 1)
                return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;

            switch (currentReputation)
            {
                case DriverReputation.PAY_DRIVER_SEASON:
                    if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else if (standings <= 16)
                        return DriverReputation.AGEING_MIDFIELD;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.YOUNG_TALENT:
                case DriverReputation.PRIME_MIDFIELD:
                    if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.AGEING_MIDFIELD;

                case DriverReputation.PRIME_STRONG_MIDFIELD:
                    if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.AGEING_MIDFIELD;

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.PRIME_MIDFIELD;

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                    if (standings <= 3)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 6)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;
                    else if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.AGEING_MIDFIELD;

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED:
                    if (standings <= 3)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 6)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;
                    else if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.AGEING_MIDFIELD;

                case DriverReputation.AGEING_MIDFIELD:
                    if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.AGEING_MIDFIELD;

                case DriverReputation.AGEING_STRONG_MIDFIELD:
                    if (standings <= 3)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.AGEING_MIDFIELD;

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                    if (age > 40)
                        return DriverReputation.JUST_ONE_LAST_DANCE;
                    if (standings <= 3)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 6)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;
                    else if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.AGEING_MIDFIELD;

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                    if (age > 40)
                        return DriverReputation.JUST_ONE_LAST_DANCE;
                    if (standings <= 3)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 6)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;
                    else if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.AGEING_MIDFIELD;

                case DriverReputation.JUST_ONE_LAST_DANCE:
                    return DriverReputation.JUST_ONE_LAST_DANCE;

                default:
                    throw new Exception($"reputation not valid for a {age} year old: {currentReputation}");
            }
        }

        private DriverReputation GetNewPrimeReputation(DriverReputation currentReputation, int age, int standings, int podiums, int dnfs)
        {
            if (standings == 1)
                return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL;

            switch (currentReputation)
            {
                case DriverReputation.PAY_DRIVER_SEASON:
                    if (standings <= 8)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 10)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else if (standings <= 16)
                        return DriverReputation.PRIME_MIDFIELD;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.YOUNG_TALENT:
                    if (standings <= 8)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 10)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else if (dnfs <= 2)
                        return DriverReputation.PRIME_MIDFIELD;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    if (standings <= 6)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 12)
                        return DriverReputation.PRIME_MIDFIELD;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    if (standings <= 3)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 6)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED;
                    else if (standings <= 8)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else
                        return DriverReputation.PRIME_MIDFIELD;

                case DriverReputation.AGEING_MIDFIELD:
                case DriverReputation.PRIME_MIDFIELD:
                    if (standings <= 6)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 8)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else if (standings <= 15)
                        return DriverReputation.PRIME_MIDFIELD;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.AGEING_STRONG_MIDFIELD:
                case DriverReputation.PRIME_STRONG_MIDFIELD:
                    if (standings <= 5)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 8)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else
                        return DriverReputation.PRIME_MIDFIELD;

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    if (standings <= 6)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 8)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else
                        return DriverReputation.PRIME_MIDFIELD;

                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                    if (standings <= 3)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 6)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED;
                    else if (standings <= 8)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else
                        return DriverReputation.PRIME_MIDFIELD;

                case DriverReputation.JUST_ONE_LAST_DANCE:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED:
                    if (standings <= 3)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 6)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED;
                    else if (standings <= 8)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else
                        return DriverReputation.PRIME_MIDFIELD;

                default:
                    throw new Exception($"reputation not valid for a {age} year old: {currentReputation}");
            }
        }

        private DriverReputation GetNewYoungReputation(DriverReputation currentReputation, int age, int standings, int podiums, int dnfs)
        {
            if (standings == 1)
                return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL;

            switch (currentReputation)
            {
                case DriverReputation.PAY_DRIVER_SEASON:
                    if (standings <= 8)
                        return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 12)
                        return DriverReputation.YOUNG_TALENT;
                    else return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.PRIME_MIDFIELD:
                case DriverReputation.PRIME_STRONG_MIDFIELD:
                case DriverReputation.AGEING_STRONG_MIDFIELD:
                case DriverReputation.AGEING_MIDFIELD:
                case DriverReputation.YOUNG_TALENT:
                    if (standings <= 8)
                        return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 12)
                        return DriverReputation.YOUNG_TALENT;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    if (standings <= 3)
                        return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 8)
                        return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 12)
                        return DriverReputation.YOUNG_TALENT;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.JUST_ONE_LAST_DANCE:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    if (standings <= 5)
                        return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 10)
                        return DriverReputation.YOUNG_TALENT;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                default:
                    throw new Exception($"reputation not valid for a {age} year old: {currentReputation}");
            }
        }


    }
}

