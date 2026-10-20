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
                                                        // a champion past 31 (e.g. a title won at 39) still leads a top team; left out,
                                                        // they fell below the list's best entry and counted as UnderQualified
                                                        Tuple.Create(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, DriverPolicyFit.PerfectFit),
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

        // Reserve seats, per team reputation: easier to get than a race seat (a YOUNG_TALENT can be a
        // top team's reserve). Read through DoesDriverFitReservePolicy, with the same rules as
        // teamPolicies: above the list's best entry is OverQualified, anything else not listed is
        // UnderQualified. Minnows never hire a reserve (EndOfSeasonManager.ReserveChance), but their
        // entries keep the table complete.
        private static Dictionary<TeamReputation, Tuple<DriverReputation, DriverPolicyFit>[]> reservePolicies = new()
        {
            {
                TeamReputation.TOP_TEAM, new[] {
                                                Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.PRIME_MIDFIELD, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.AGEING_MIDFIELD, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.AGEING_STRONG_MIDFIELD, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.JUST_ONE_LAST_DANCE, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.PRIME_STRONG_MIDFIELD, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, DriverPolicyFit.GoodFit),
                                             }
            },
            {
                TeamReputation.MIDFIELD_HIGH, new[] {
                                                Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.PRIME_MIDFIELD, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.AGEING_MIDFIELD, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.AGEING_STRONG_MIDFIELD, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.JUST_ONE_LAST_DANCE, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.PRIME_STRONG_MIDFIELD, DriverPolicyFit.GoodFit),
                                             }
            },
            {
                TeamReputation.MIDFIELD, new[] {
                                                Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.PAY_DRIVER_SEASON, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.PAY_DRIVER_WILD_CARD, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.AGEING_MIDFIELD, DriverPolicyFit.GoodFit),
                                                Tuple.Create(DriverReputation.PRIME_MIDFIELD, DriverPolicyFit.GoodFit),
                                             }
            },
            {
                TeamReputation.MINNOW, new[] {
                                                Tuple.Create(DriverReputation.PAY_DRIVER_WILD_CARD, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.PAY_DRIVER_SEASON, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.YOUNG_TALENT, DriverPolicyFit.GoodFit),
                                             }
            },
            {
                TeamReputation.SUPER_MINNOW, new[] {
                                                Tuple.Create(DriverReputation.PAY_DRIVER_WILD_CARD, DriverPolicyFit.PerfectFit),
                                                Tuple.Create(DriverReputation.PAY_DRIVER_SEASON, DriverPolicyFit.PerfectFit),
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

        // DoesDriverFitTeamPolicy's counterpart for a reserve seat (reservePolicies)
        public DriverPolicyFit DoesDriverFitReservePolicy(DriverReputation driverReputation, TeamReputation teamReputation)
        {
            var policy = reservePolicies[teamReputation];
            if (!policy.Select(p => p.Item1).Contains(driverReputation))
                return driverReputation > policy.Select(p => p.Item1).Max() ? DriverPolicyFit.OverQualified : DriverPolicyFit.UnderQualified;
            return policy.First(p => p.Item1 == driverReputation).Item2;
        }

        // the fit for a race seat in this role, or for the team's reserve seat
        public DriverPolicyFit DoesDriverFitSeat(DriverReputation driverReputation, DriverRole role, TeamReputation teamReputation, bool isReserve)
        {
            return isReserve
                ? DoesDriverFitReservePolicy(driverReputation, teamReputation)
                : DoesDriverFitTeamPolicy(driverReputation, role, teamReputation);
        }

        public DriverResume? PickBestCandidate(IEnumerable<DriverResume> drivers, DriverRole role, TeamReputation teamReputation, bool isReserve = false)
        {
            if (drivers == null)
                return null;

            // The best fit tier for this role/team wins; inside it every driver has a chance,
            // weighted by reputation (see PickWeightedByReputation).
            var bestTier = drivers
                    .GroupBy(d => GetFitWeight(DoesDriverFitSeat(d.Reputation, role, teamReputation, isReserve)))
                    .OrderByDescending(g => g.Key)
                    .FirstOrDefault();

            return bestTier == null ? null : PickWeightedByReputation(bestTier, d => d.Reputation, new Random());
        }

        // A driver's standing for the weighted draw below. DriverReputation's enum order is a
        // prestige ranking with uneven steps (AGEING_STRONG_MIDFIELD and JUST_ONE_LAST_DANCE sit
        // between PRIME_MIDFIELD and PRIME_STRONG_MIDFIELD), so the draw uses these levels instead.
        public static int ReputationScore(DriverReputation reputation) => reputation switch
        {
            DriverReputation.PAY_DRIVER_WILD_CARD => 1,
            DriverReputation.PAY_DRIVER_SEASON => 2,
            DriverReputation.AGEING_MIDFIELD or DriverReputation.YOUNG_TALENT or DriverReputation.PRIME_MIDFIELD => 3,
            DriverReputation.AGEING_STRONG_MIDFIELD or DriverReputation.PRIME_STRONG_MIDFIELD or DriverReputation.JUST_ONE_LAST_DANCE => 4,
            DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED or DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED
                or DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN or DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN => 5,
            _ => 6 // AGEING_, PRIME_ and YOUNG_CHAMPIONSHIP_LEVEL
        };

        /// <summary>
        /// Picks one driver from candidates that fit the seat equally well, at random but weighted
        /// by reputation: each has 2^score tickets (see ReputationScore), so one level of standing
        /// doubles a driver's chance. A slightly weaker driver can still land the seat (one level
        /// down: 1 in 3 against a single rival), a much weaker one rarely does, and drivers of the
        /// same standing are equally likely.
        /// </summary>
        public static T PickWeightedByReputation<T>(IEnumerable<T> candidates, Func<T, DriverReputation> reputationOf, Random random)
        {
            var list = candidates.ToList();
            if (list.Count == 0)
                return default;

            var weights = list.Select(c => Math.Pow(2, ReputationScore(reputationOf(c)))).ToList();
            double ticket = random.NextDouble() * weights.Sum();
            for (int i = 0; i < list.Count; i++)
            {
                ticket -= weights[i];
                if (ticket < 0)
                    return list[i];
            }
            return list[^1];
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
        public DriverResume PickWinner(DriverResume driverPickedByTeam, DriverResume driverWhoIsProposingToTeam, DriverRole role, TeamReputation teamReputation, bool isReserve = false)
        {
            if (driverPickedByTeam == null)
                return driverWhoIsProposingToTeam;

            var pickedFitWeight = GetFitWeight(DoesDriverFitSeat(driverPickedByTeam.Reputation, role, teamReputation, isReserve));
            var proposingFitWeight = GetFitWeight(DoesDriverFitSeat(driverWhoIsProposingToTeam.Reputation, role, teamReputation, isReserve));

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
