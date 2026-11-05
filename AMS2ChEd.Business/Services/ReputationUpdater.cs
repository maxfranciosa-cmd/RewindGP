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

        // Deepest floor for a driver who was already PRIME_MIDFIELD or below going into the
        // season and finished outside this position - PAY_DRIVER_SEASON alone was absorbing
        // every truly poor finish regardless of grid size (a 30+ car historical grid piles
        // everyone from P21 down into the same bucket as someone who narrowly missed the points),
        // so a genuine backmarker now drops one tier further instead of crowding out the seats
        // PAY_DRIVER_SEASON drivers who merely missed the points are competing for. Only applied
        // where the branch's current-season floor was already PAY_DRIVER_SEASON for a
        // pre-season reputation of PRIME_MIDFIELD or below (see ReputationUpdaterTests, whose
        // golden 1994/1995 data never goes past P25 and stays PAY_DRIVER_SEASON there).
        public const int WILD_CARD_FLOOR_STANDINGS_THRESHOLD = 20;

        // A top-3 finish in a dominant car says more about the car than the driver when the
        // teammate beat them clearly (Bottas 2017/19/20, Patrese 1992, Barrichello 2002/04): such a
        // season is judged as a P4. One who out-won the teammate (Senna 1989) or held their own in
        // the head-to-head keeps the top-3 verdict. A title is never touched.
        public const double DOMINANT_CAR_WIN_SHARE = 0.5;
        public const double DOMINANT_CAR_POINTS_RATIO = 1.5;
        public const double CLEARLY_BEATEN_HEAD_TO_HEAD_SHARE = 0.4;
        public const int MINIMUM_HEAD_TO_HEAD_RACES = 3;
        public const int BEATEN_IN_DOMINANT_CAR_JUDGED_AS = 4;
        // also beaten: lost the head-to-head and took under a third of the team's wins (Bottas 2017,
        // Hill 1993: close in the head-to-head, far behind on wins). Prost 1988 (7 of 15) is not.
        public const double LOST_HEAD_TO_HEAD_SHARE = 0.5;
        public const double MINOR_SHARE_OF_TEAM_WINS = 1.0 / 3.0;

        /// <summary>The standings position a season is judged by: the real one, except as above.</summary>
        public static int StandingsJudgedBy(int standings, TeammateComparison teammate)
        {
            if (standings < 2 || standings > 3 || teammate == null || !teammate.DominantCar)
                return standings;
            if (teammate.HeadToHeadRaces < MINIMUM_HEAD_TO_HEAD_RACES)
                return standings;

            var teamWins = teammate.Wins + teammate.TeammateWins;
            var shareOfTeamWins = teamWins == 0 ? 0 : (double)teammate.Wins / teamWins;

            var clearlyBeaten = teammate.HeadToHeadShare < CLEARLY_BEATEN_HEAD_TO_HEAD_SHARE
                                && teammate.Wins <= teammate.TeammateWins;
            var outWonByFar = teammate.HeadToHeadShare < LOST_HEAD_TO_HEAD_SHARE
                              && shareOfTeamWins < MINOR_SHARE_OF_TEAM_WINS;
            return clearlyBeaten || outWonByFar ? BEATEN_IN_DOMINANT_CAR_JUDGED_AS : standings;
        }

        private static DriverReputation SeasonFloorOrWildCard(int standings)
        {
            return standings > WILD_CARD_FLOOR_STANDINGS_THRESHOLD
                ? DriverReputation.PAY_DRIVER_WILD_CARD
                : DriverReputation.PAY_DRIVER_SEASON;
        }

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

        // the reputation moved onto the driver's age band; with decays, a season spent without racing
        // also costs one tier, down to PAY_DRIVER_WILD_CARD (reserves and imported drivers don't decay)
        public DriverReputation GetNewReputationForInactiveDriver(DriverReputation currentReputation, int age, bool decays)
        {
            DriverReputation related;
            if (age < YOUNG_DRIVER_AGE)
            {
                related = GetRelatedYoungReputation(currentReputation);
            }
            else if (age < OLD_DRIVER_AGE)
            {
                related = GetRelatedPrimeReputation(currentReputation);
            }
            else
            {
                related = GetRelatedAgeingReputation(currentReputation);
            }

            return decays ? DecayOneTier(related) : related;
        }

        // one step down the reputation's own age band
        private DriverReputation DecayOneTier(DriverReputation reputation)
        {
            switch (reputation)
            {
                case DriverReputation.PAY_DRIVER_WILD_CARD: return DriverReputation.PAY_DRIVER_WILD_CARD;
                case DriverReputation.PAY_DRIVER_SEASON: return DriverReputation.PAY_DRIVER_WILD_CARD;

                case DriverReputation.YOUNG_TALENT: return DriverReputation.PAY_DRIVER_SEASON;
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN: return DriverReputation.YOUNG_TALENT;
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL: return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;

                case DriverReputation.PRIME_MIDFIELD: return DriverReputation.PAY_DRIVER_SEASON;
                case DriverReputation.PRIME_STRONG_MIDFIELD: return DriverReputation.PRIME_MIDFIELD;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN: return DriverReputation.PRIME_STRONG_MIDFIELD;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED: return DriverReputation.PRIME_STRONG_MIDFIELD;
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL: return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED;

                case DriverReputation.AGEING_MIDFIELD: return DriverReputation.PAY_DRIVER_SEASON;
                case DriverReputation.AGEING_STRONG_MIDFIELD: return DriverReputation.AGEING_MIDFIELD;
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED: return DriverReputation.AGEING_STRONG_MIDFIELD;
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL: return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;
                case DriverReputation.JUST_ONE_LAST_DANCE: return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED;

                default: throw new Exception($"reputation not valid: {reputation}");
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

        public DriverReputation GetNewReputation(DriverReputation currentReputation, int age, int standings, int podiums, int dnfs, int races, int totalSeasonRaces, double averageRacePosition)
        {
            if (currentReputation == DriverReputation.PAY_DRIVER_WILD_CARD)
            {
                return EvaluateWildCard(age, podiums, races, dnfs);
            }

            // a part-timer is judged on where they finished; one who never got past pre-qualifying has
            // no average (0), and is judged on the standings instead
            var effectiveStandings = (races < totalSeasonRaces / 2.0 && averageRacePosition > 0)
                ? (int)Math.Round(averageRacePosition, MidpointRounding.AwayFromZero)
                : standings;

            if (age < YOUNG_DRIVER_AGE)
            {
                return GetNewYoungReputation(currentReputation, age, effectiveStandings, podiums, dnfs);
            }
            else if (age < OLD_DRIVER_AGE)
            {
                return GetNewPrimeReputation(currentReputation, age, effectiveStandings, podiums, dnfs);
            }
            else
            {
                return GetNewAgeingReputation(currentReputation, age, effectiveStandings, podiums, dnfs);
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
                        return SeasonFloorOrWildCard(standings);

                case DriverReputation.YOUNG_TALENT:
                case DriverReputation.PRIME_MIDFIELD:
                    if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else if (standings <= 16)
                        return DriverReputation.AGEING_MIDFIELD;
                    else
                        return SeasonFloorOrWildCard(standings);

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else
                        return DriverReputation.AGEING_MIDFIELD;

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
                    else if (standings <= 16)
                        return DriverReputation.AGEING_MIDFIELD;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.AGEING_MIDFIELD:
                    if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else if (standings <= 16)
                        return DriverReputation.AGEING_MIDFIELD;
                    else
                        return SeasonFloorOrWildCard(standings);

                // a strong midfielder turning 31 is judged as one already past it: a top-3 season makes
                // them championship level either way
                case DriverReputation.PRIME_STRONG_MIDFIELD:
                case DriverReputation.AGEING_STRONG_MIDFIELD:
                    if (standings <= 3)
                        return DriverReputation.AGEING_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 8)
                        return DriverReputation.AGEING_STRONG_MIDFIELD;
                    else if (standings <= 16)
                        return DriverReputation.AGEING_MIDFIELD;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

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
                        return SeasonFloorOrWildCard(standings);

                case DriverReputation.YOUNG_TALENT:
                    if (standings <= 8)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 10)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else if (dnfs <= 2 && standings <= 18)
                        return DriverReputation.PRIME_MIDFIELD;
                    else
                        return SeasonFloorOrWildCard(standings);

                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                    if (standings <= 6)
                        return DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 8)
                        return DriverReputation.PRIME_STRONG_MIDFIELD;
                    else if (standings <= 15)
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
                    else if (standings <= 15)
                        return DriverReputation.PRIME_MIDFIELD;
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
                        return SeasonFloorOrWildCard(standings);

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
                    else if (standings <= 15)
                        return DriverReputation.YOUNG_TALENT;
                    else return SeasonFloorOrWildCard(standings);

                // PRIME_MIDFIELD/AGEING_MIDFIELD/YOUNG_TALENT are PRIME_MIDFIELD-or-below, so a
                // truly poor finish here can fall through to PAY_DRIVER_WILD_CARD.
                case DriverReputation.PRIME_MIDFIELD:
                case DriverReputation.AGEING_MIDFIELD:
                case DriverReputation.YOUNG_TALENT:
                    if (standings <= 8)
                        return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 15)
                        return DriverReputation.YOUNG_TALENT;
                    else
                        return SeasonFloorOrWildCard(standings);

                // PRIME_STRONG_MIDFIELD/AGEING_STRONG_MIDFIELD sit above PRIME_MIDFIELD, so they
                // keep the plain PAY_DRIVER_SEASON floor regardless of how poorly they finish.
                case DriverReputation.PRIME_STRONG_MIDFIELD:
                case DriverReputation.AGEING_STRONG_MIDFIELD:
                    if (standings <= 8)
                        return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN;
                    else if (standings <= 15)
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
                    else if (standings <= 15)
                        return DriverReputation.YOUNG_TALENT;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                case DriverReputation.JUST_ONE_LAST_DANCE:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                case DriverReputation.PRIME_CHAMPIONSHIP_LEVEL:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    if (standings <= 6)
                        return DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL;
                    else if (standings <= 15)
                        return DriverReputation.YOUNG_TALENT;
                    else
                        return DriverReputation.PAY_DRIVER_SEASON;

                default:
                    throw new Exception($"reputation not valid for a {age} year old: {currentReputation}");
            }
        }


    }
}

