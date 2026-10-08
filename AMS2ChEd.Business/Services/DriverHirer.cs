using AMS2ChEd.Business.Models;
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
                                                        Tuple.Create(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, DriverPolicyFit.GoodFit),
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
                                                        Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.GoodFit),
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
                                                        Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.GoodFit),
                                                        Tuple.Create(DriverReputation.PAY_DRIVER_SEASON, DriverPolicyFit.PerfectFit),
                                                     }
                    }
                }
            },
        };

        // minimum reputation for a driver to be considered the team leader (FIRST_DRIVER material)
        // when their status inside the team isn't explicit (EQUAL contract role)
        private static Dictionary<TeamReputation, DriverReputation> firstDriverMinReputation = new()
        {
            { TeamReputation.TOP_TEAM, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN },
            { TeamReputation.MIDFIELD_HIGH, DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED },
            { TeamReputation.MIDFIELD, DriverReputation.JUST_ONE_LAST_DANCE },
            { TeamReputation.MINNOW, DriverReputation.PRIME_MIDFIELD },
            { TeamReputation.SUPER_MINNOW, DriverReputation.YOUNG_TALENT },
        };

        public static bool IsFirstDriverMaterial(DriverReputation driverReputation, TeamReputation teamReputation)
        {
            return driverReputation >= firstDriverMinReputation[teamReputation];
        }

        // the role a team looks for when hiring alongside a driver who is staying in the team
        public static DriverRole GetRoleToHireAlongside(ContractRole stayingDriverRole, DriverReputation stayingDriverReputation, TeamReputation teamReputation)
        {
            switch (stayingDriverRole)
            {
                case ContractRole.FIRST_DRIVER:
                    return DriverRole.SECOND_DRIVER;
                case ContractRole.SECOND_DRIVER:
                    return DriverRole.FIRST_DRIVER;
                default:
                    return IsFirstDriverMaterial(stayingDriverReputation, teamReputation) ? DriverRole.SECOND_DRIVER : DriverRole.FIRST_DRIVER;
            }
        }

        // contracts coming from seasons/saves created before roles existed are UNDEFINED:
        // derive their roles from the drivers' reputations
        public static void AssignUndefinedRoles(ISeason season, IEnumerable<IDriverData> drivers)
        {
            if (season?.Teams == null)
                return;

            DriverReputation GetReputation(string driverId) =>
                drivers?.FirstOrDefault(d => d.DriverId == driverId)?.Reputation ?? DriverReputation.PRIME_MIDFIELD;

            foreach (var team in season.Teams)
            {
                if (team.Driver1Contract == null)
                    continue;

                if (string.IsNullOrEmpty(team.Driver2Contract?.DriverId))
                {
                    // one-car team: its only driver leads it
                    if (team.Driver1Contract.Role == ContractRole.UNDEFINED)
                        team.Driver1Contract.Role = ContractRole.FIRST_DRIVER;
                    continue;
                }

                if (team.Driver1Contract.Role != ContractRole.UNDEFINED && team.Driver2Contract.Role != ContractRole.UNDEFINED)
                    continue;

                var driver1Reputation = GetReputation(team.Driver1Contract.DriverId);
                var driver2Reputation = GetReputation(team.Driver2Contract.DriverId);

                if (driver1Reputation == driver2Reputation)
                {
                    team.Driver1Contract.Role = ContractRole.EQUAL;
                    team.Driver2Contract.Role = ContractRole.EQUAL;
                }
                else
                {
                    var driver1Leads = driver1Reputation > driver2Reputation;
                    team.Driver1Contract.Role = driver1Leads ? ContractRole.FIRST_DRIVER : ContractRole.SECOND_DRIVER;
                    team.Driver2Contract.Role = driver1Leads ? ContractRole.SECOND_DRIVER : ContractRole.FIRST_DRIVER;
                }
            }
        }

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
            if (drivers == null)
                return null;

            var rnd = new Random();
            // Group by fit tier (not exact reputation) so every driver within the same tier -
            // e.g. all PerfectFit reputations for this role/team - has an equal chance of being picked,
            // rather than always defaulting to whichever has the highest reputation value.
            var result = drivers
                    .GroupBy(d => GetFitWeight(DoesDriverFitTeamPolicy(d.Reputation, role, teamReputation)))
                    .OrderByDescending(g => g.Key)
                    .SelectMany(g => g.OrderBy(x => rnd.Next()))
                    .FirstOrDefault();

            return result;
        }

        public static int GetFitWeight(DriverPolicyFit fit)
        {
            switch (fit)
            {
                case DriverPolicyFit.OverQualified:
                case DriverPolicyFit.PerfectFit:
                    return 2;
                case DriverPolicyFit.GoodFit:
                    return 1;
                default:
                    return 0;
            }
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

        /// <summary>
        /// Role/team-fit-aware version of <see cref="PickWinner"/>. Raw DriverReputation is an
        /// overall prestige ranking, not a "fits this seat" ranking - e.g. PAY_DRIVER_SEASON sits
        /// near the bottom of that ranking even though teamPolicies rates it a PerfectFit for a
        /// MINNOW/SUPER_MINNOW seat (those teams need the budget more than outright pace). Compare
        /// by DoesDriverFitTeamPolicy tier first, so a driver who fits this specific role/team
        /// better always wins regardless of raw reputation, and only fall back to the raw
        /// comparison (including the pay-driver-season coin toss) to break a tie within the same
        /// fit tier.
        /// </summary>
        public DriverResume PickWinner(DriverResume driverPickedByTeam, DriverResume driverWhoIsProposingToTeam, DriverRole role, TeamReputation teamReputation)
        {
            if (driverPickedByTeam == null)
                return driverWhoIsProposingToTeam;

            var pickedFitWeight = GetFitWeight(DoesDriverFitTeamPolicy(driverPickedByTeam.Reputation, role, teamReputation));
            var proposingFitWeight = GetFitWeight(DoesDriverFitTeamPolicy(driverWhoIsProposingToTeam.Reputation, role, teamReputation));

            if (pickedFitWeight != proposingFitWeight)
                return pickedFitWeight > proposingFitWeight ? driverPickedByTeam : driverWhoIsProposingToTeam;

            return PickWinner(driverPickedByTeam, driverWhoIsProposingToTeam);
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
