using AMS2ChEd.Business.Models.Concrete;
using System.Linq;

namespace AMS2ChEd.Business.Services
{
    public enum DriverRole
    {
        FIRST_DRIVER,
        SECOND_DRIVER
    }

    public class DriverResume
    {
        public string Id { get; set; }
        public DriverReputation Reputation { get; set; }
    }

    public class DriverHirer
    {

        public static Dictionary<TeamReputation, DriverReputation> teamAbsenceSubstitutionMaxReputation = new()
        {
            { TeamReputation.TOP_TEAM, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL },

            { TeamReputation.MIDFIELD_HIGH, DriverReputation.AGEING_CHAMPIONSHIP_LEVEL },

            { TeamReputation.MIDFIELD, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED },

            { TeamReputation.MINNOW, DriverReputation.PRIME_STRONG_MIDFIELD },

            { TeamReputation.SUPER_MINNOW, DriverReputation.AGEING_STRONG_MIDFIELD }
        };

        public static Dictionary<TeamReputation, Dictionary<DriverRole, DriverReputation[]>> teamPolicies = new()
        {
            { 
                TeamReputation.TOP_TEAM, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] { 
                                                        DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL,
                                                        DriverReputation.PRIME_CHAMPIONSHIP_LEVEL,
                                                        DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN,
                                                     } 
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL,
                                                        DriverReputation.PRIME_CHAMPIONSHIP_LEVEL,
                                                        DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN,
                                                        DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN,
                                                        DriverReputation.AGEING_CHAMPIONSHIP_LEVEL,
                                                        DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED,
                                                        DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED,
                                                        DriverReputation.JUST_ONE_LAST_DANCE,
                                                        DriverReputation.PRIME_STRONG_MIDFIELD
                                                     }
                    }
                }
            },
            {
                TeamReputation.MIDFIELD_HIGH, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] {
                                                        DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED,
                                                        DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN,
                                                        DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN,
                                                        DriverReputation.PRIME_STRONG_MIDFIELD,
                                                        DriverReputation.JUST_ONE_LAST_DANCE,
                                                        DriverReputation.AGEING_STRONG_MIDFIELD
                                                     }
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED,
                                                        DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED,
                                                        DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN,
                                                        DriverReputation.PRIME_STRONG_MIDFIELD,
                                                        DriverReputation.JUST_ONE_LAST_DANCE,
                                                        DriverReputation.AGEING_STRONG_MIDFIELD,
                                                        DriverReputation.PRIME_MIDFIELD
                                                     }
                    }
                }
            },
            {
                TeamReputation.MIDFIELD, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] {
                                                        DriverReputation.PRIME_STRONG_MIDFIELD,
                                                        DriverReputation.JUST_ONE_LAST_DANCE,
                                                        DriverReputation.AGEING_STRONG_MIDFIELD,
                                                        DriverReputation.PRIME_MIDFIELD,
                                                        DriverReputation.AGEING_MIDFIELD,
                                                     }
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        DriverReputation.AGEING_STRONG_MIDFIELD,
                                                        DriverReputation.PRIME_MIDFIELD,
                                                        DriverReputation.YOUNG_TALENT,
                                                        DriverReputation.AGEING_MIDFIELD,
                                                     }
                    }
                }
            },
            {
                TeamReputation.MINNOW, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] {
                                                        DriverReputation.PRIME_MIDFIELD,
                                                        DriverReputation.AGEING_MIDFIELD,
                                                        DriverReputation.YOUNG_TALENT,
                                                        DriverReputation.PAY_DRIVER_SEASON,
                                                     }
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        DriverReputation.YOUNG_TALENT,
                                                        DriverReputation.PAY_DRIVER_SEASON,
                                                     }
                    }
                }
            },
            {
                TeamReputation.SUPER_MINNOW, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] {
                                                        DriverReputation.YOUNG_TALENT,
                                                        DriverReputation.PAY_DRIVER_SEASON,
                                                     }
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        DriverReputation.YOUNG_TALENT,
                                                        DriverReputation.PAY_DRIVER_SEASON,
                                                     }
                    }
                }
            },
        };
        public DriverResume? PickBestCandidate(IEnumerable<DriverResume> drivers, DriverRole role, TeamReputation teamReputation)
        {
            var result = drivers?
                    .OrderByDescending(d => teamPolicies[teamReputation][role].Contains(d.Reputation))
                    .ThenByDescending(d => d.Reputation)
                    .FirstOrDefault();

            return result;
        }

        public DriverResume PickWinner(DriverResume driverPickedByTeam, DriverResume driverWhoIsProposingToTeam)
        {
            if (driverPickedByTeam == null)
                return driverWhoIsProposingToTeam;

            // if they're both pay driver season, coin toss between them
            if (driverPickedByTeam.Reputation == DriverReputation.PAY_DRIVER_SEASON && 
                driverWhoIsProposingToTeam.Reputation == DriverReputation.PAY_DRIVER_SEASON)
            {
                var random = new Random();
                var result = (random.Next(2) == 1) ? driverPickedByTeam : driverWhoIsProposingToTeam;
                return result;
            }

            return driverPickedByTeam.Reputation >= driverWhoIsProposingToTeam.Reputation ? driverPickedByTeam : driverWhoIsProposingToTeam;
        }

        public DriverResume PickWinnerForAbsence(DriverResume driverPickedByTeam, DriverResume driverWhoIsProposingToTeam)
        {
            if (driverPickedByTeam == null)
                return driverWhoIsProposingToTeam;

            // if both are pay drivers (season or wild card), coin toss between them
            var payDriversReputations = new[] { DriverReputation.PAY_DRIVER_SEASON, DriverReputation.PAY_DRIVER_WILD_CARD };
            if (payDriversReputations.Contains(driverWhoIsProposingToTeam.Reputation) &&
                payDriversReputations.Contains(driverPickedByTeam.Reputation))
            {
                var random = new Random();
                var result = (random.Next(2) == 1) ? driverPickedByTeam : driverWhoIsProposingToTeam;
                return result;
            }

            return driverPickedByTeam.Reputation >= driverWhoIsProposingToTeam.Reputation ? driverPickedByTeam : driverWhoIsProposingToTeam;
        }
    }
}
