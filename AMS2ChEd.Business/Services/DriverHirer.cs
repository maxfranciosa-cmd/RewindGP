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

        private static Dictionary<TeamReputation, Dictionary<DriverRole, Tuple<DriverReputation,DriverPolicyFit>[]>> teamPolicies = new()
        {
            { 
                TeamReputation.TOP_TEAM, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] { 
                                                        Tuple.Create(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverPolicyFit.GoodFit),
                                                     } 
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        Tuple.Create(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.JUST_ONE_LAST_DANCE, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.PRIME_STRONG_MIDFIELD, DriverPolicyFit.GoodFit)
                                                     }
                    }
                }
            },
            {
                TeamReputation.MIDFIELD_HIGH, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] {
                                                        Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PRIME_STRONG_MIDFIELD, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.JUST_ONE_LAST_DANCE, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.AGEING_STRONG_MIDFIELD, DriverPolicyFit.GoodFit)
                                                     }
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PRIME_STRONG_MIDFIELD, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.JUST_ONE_LAST_DANCE, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.AGEING_STRONG_MIDFIELD, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.PRIME_MIDFIELD, DriverPolicyFit.GoodFit)
                                                     }
                    }
                }
            },
            {
                TeamReputation.MIDFIELD, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] {
                                                        Tuple.Create(DriverReputation.PRIME_STRONG_MIDFIELD, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.JUST_ONE_LAST_DANCE, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.AGEING_STRONG_MIDFIELD, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.PRIME_MIDFIELD, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.AGEING_MIDFIELD, DriverPolicyFit.GoodFit),
                                                     }
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        Tuple.Create(DriverReputation.AGEING_STRONG_MIDFIELD, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PRIME_MIDFIELD, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.AGEING_MIDFIELD, DriverPolicyFit.GoodFit),
                                                     }
                    }
                }
            },
            {
                TeamReputation.MINNOW, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] {
                                                        Tuple.Create(DriverReputation.PRIME_MIDFIELD, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.AGEING_MIDFIELD, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.PAY_DRIVER_SEASON, DriverPolicyFit.GoodFit),
                                                     }
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PAY_DRIVER_SEASON, DriverPolicyFit.PerfectFit),
                                                     }
                    }
                }
            },
            {
                TeamReputation.SUPER_MINNOW, new()
                {
                    { DriverRole.FIRST_DRIVER, new[] {
                                                        Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PAY_DRIVER_SEASON, DriverPolicyFit.PerfectFit),
                                                     }
                    },
                    { DriverRole.SECOND_DRIVER, new[] {
                                                        Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.PerfectFit),
                                                        Tuple.Create(DriverReputation.PAY_DRIVER_SEASON, DriverPolicyFit.PerfectFit),
                                                     }
                    }
                }
            },
        };

        public enum DriverPolicyFit
        {
            UnderQualified,
            GoodFit,
            PerfectFit,
            OverQualified
        }

        public DriverPolicyFit DoesDriverFitTeamPolicy(DriverReputation driverReputation, DriverRole role, TeamReputation teamReputation)
        {
            if (!teamPolicies[teamReputation][role].Select(p => p.Item1).Contains(driverReputation))
                return driverReputation > teamPolicies[teamReputation][role].Select(p => p.Item1).Max() ? DriverPolicyFit.OverQualified : DriverPolicyFit.UnderQualified;
            return teamPolicies[teamReputation][role].FirstOrDefault(p => p.Item1 == driverReputation)?.Item2 ?? DriverPolicyFit.GoodFit;
        }

        public DriverResume? PickBestCandidate(IEnumerable<DriverResume> drivers, DriverRole role, TeamReputation teamReputation)
        {
            var result = drivers?
                    .OrderByDescending(d => teamPolicies[teamReputation][role].Select(p => p.Item1).Contains(d.Reputation))
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
