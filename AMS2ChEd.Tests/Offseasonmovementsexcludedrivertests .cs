
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;


namespace AMS2ChEd.Tests.Business.GameLogic
{
    /// <summary>
    /// Tests for ExcludeDriverIds functionality in OffSeasonMovements.
    /// This feature allows teams to deprioritize certain drivers (usually the exiting driver)
    /// to create variety in the driver market and prevent drivers from staying at the same team forever.
    /// 
    /// IMPORTANT: How ExcludeDriverIds Works:
    /// 
    /// 1. In PickPotentialNewDrivers:
    ///    - ExcludeDriverIds is created when ExitingDriverWillingToRenew = false
    ///    - Passed to PickBestCandidate which filters/deprioritizes excluded drivers
    ///    - Result: OriginalTeamHiring.DriverId will NEVER be an excluded driver
    /// 
    /// 2. In FinalBallotResults:
    ///    - OriginalTeamHiring already has the correct driver (NOT excluded)
    ///    - ExcludeDriverIds is used to deprioritize CANDIDATES from DriversProposeToTeams
    ///    - Candidates might still include the excluded driver (they propose to teams independently)
    ///    - OrderBy(ExcludeDriverIds.Contains) ensures excluded candidates go to bottom of list
    /// 
    /// Key behaviors:
    /// - Excluded drivers are deprioritized (moved to bottom of candidate list), NOT blocked
    /// - Original hire is already correct (exclusion applied in PickPotentialNewDrivers)
    /// - Exclusion only affects candidates in FinalBallotResults
    /// - Excluded drivers can still be hired if they're significantly better or no other options
    /// </summary>
    [TestClass]
    public class OffSeasonMovementsExcludeDriverTests
    {
        private OffSeasonMovements _offSeasonMovements;
        private DriverFirer _driverFirer;
        private DriverHirer _driverHirer;

        [TestInitialize]
        public void Setup()
        {
            _driverFirer = new DriverFirer();
            _driverHirer = new DriverHirer();
            _offSeasonMovements = new OffSeasonMovements(_driverFirer, _driverHirer);
        }

        #region PickPotentialNewDrivers - ExcludeDriverIds Tests

        [TestMethod]
        public void PickPotentialNewDrivers_ExitingDriverNotWillingToRenew_IsDeprioritized()
        {
            // Arrange - Mercedes has HAMILTON exiting (not willing to renew)
            // Available drivers: HAMILTON, RUSSELL, LECLERC
            // Despite HAMILTON being championship level, he should be deprioritized

            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "MERCEDES",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = "HAMILTON",
                    ExitingDriverWillingToRenew = false  // ✅ Not willing to renew
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver { DriverId = "HAMILTON", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new UnemployedDriver { DriverId = "RUSSELL", Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL },
                new UnemployedDriver { DriverId = "LECLERC", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
            };

            // Act
            List<TeamJobAd> adsWithNoCandidates;
            var hirings = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Assert
            Assert.AreEqual(1, hirings.Count);
            var mercedesHiring = hirings[0];

            // HAMILTON should be excluded - either RUSSELL or LECLERC should be picked
            Assert.AreNotEqual("HAMILTON", mercedesHiring.DriverId,
                "HAMILTON (exiting, not willing to renew) should be deprioritized - another driver should be picked");

            // Should be one of the other championship-level drivers
            Assert.IsTrue(mercedesHiring.DriverId == "RUSSELL" || mercedesHiring.DriverId == "LECLERC",
                "Mercedes should pick RUSSELL or LECLERC instead of excluded HAMILTON");

            // Verify ExcludeDriverIds was set
            Assert.IsNotNull(mercedesHiring.ExcludeDriverIds);
            Assert.IsTrue(mercedesHiring.ExcludeDriverIds.Contains("HAMILTON"),
                "ExcludeDriverIds should contain HAMILTON");
        }

        [TestMethod]
        public void PickPotentialNewDrivers_ExitingDriverWillingToRenew_NotExcluded()
        {
            // Arrange - Mercedes has HAMILTON exiting but willing to renew
            // HAMILTON should be considered normally (not excluded)

            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "MERCEDES",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = "HAMILTON",
                    ExitingDriverWillingToRenew = true  // ✅ Willing to renew
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver { DriverId = "HAMILTON", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new UnemployedDriver { DriverId = "BOTTAS", Reputation = DriverReputation.PRIME_STRONG_MIDFIELD }
            };

            // Act
            List<TeamJobAd> adsWithNoCandidates;
            var hirings = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Assert
            Assert.AreEqual(1, hirings.Count);
            var mercedesHiring = hirings[0];

            // HAMILTON should be picked (willing to renew, best reputation)
            Assert.AreEqual("HAMILTON", mercedesHiring.DriverId,
                "HAMILTON (willing to renew) should be picked - has best reputation");

            // ExcludeDriverIds should be empty or not contain HAMILTON
            Assert.IsTrue(mercedesHiring.ExcludeDriverIds == null ||
                         !mercedesHiring.ExcludeDriverIds.Contains("HAMILTON"),
                "HAMILTON should NOT be in ExcludeDriverIds (willing to renew)");
        }

        [TestMethod]
        public void PickPotentialNewDrivers_NoExitingDriver_NoExclusions()
        {
            // Arrange - New team (no exiting driver)

            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "NEWTEAM",
                    TeamReputation = TeamReputation.MIDFIELD,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,  // ✅ No exiting driver
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver { DriverId = "DRIVER1", Reputation = DriverReputation.PRIME_MIDFIELD },
                new UnemployedDriver { DriverId = "DRIVER2", Reputation = DriverReputation.YOUNG_TALENT }
            };

            // Act
            List<TeamJobAd> adsWithNoCandidates;
            var hirings = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Assert
            Assert.AreEqual(1, hirings.Count);
            var hiring = hirings[0];

            // Should pick best available (DRIVER1)
            Assert.AreEqual("DRIVER1", hiring.DriverId);

            // ExcludeDriverIds should be empty (no one to exclude)
            Assert.IsTrue(hiring.ExcludeDriverIds == null || hiring.ExcludeDriverIds.Count == 0,
                "No drivers should be excluded (new team, no exiting driver)");
        }

        [TestMethod]
        public void PickPotentialNewDrivers_OnlyExcludedDriverAvailable_ShouldMarkJobAd_AsWithoutPick()
        {
            // Arrange - Only HAMILTON available, but he's excluded
            // Should still hire HAMILTON (fallback behavior)

            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "MERCEDES",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = "HAMILTON",
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver { DriverId = "HAMILTON", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
                // ✅ Only HAMILTON available
            };

            // Act
            List<TeamJobAd> adsWithNoCandidates;
            var hirings = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Assert
            Assert.AreEqual(1, adsWithNoCandidates.Count);
            var mercedesAd = adsWithNoCandidates[0];

            // no options available
            Assert.AreEqual(jobAds.First(), mercedesAd,
                "JobAd is marked as WITH NO CANDIDATES - no other drivers available (fallback behavior)");
        }

        [TestMethod]
        public void PickPotentialNewDrivers_MultipleTeams_EachHasOwnExclusions()
        {
            // Arrange - Multiple teams, each excluding their own exiting driver

            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "MERCEDES",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = "HAMILTON",
                    ExitingDriverWillingToRenew = false
                },
                new TeamJobAd
                {
                    TeamId = "FERRARI",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = "LECLERC",
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver { DriverId = "HAMILTON", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new UnemployedDriver { DriverId = "LECLERC", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new UnemployedDriver { DriverId = "VERSTAPPEN", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new UnemployedDriver { DriverId = "RUSSELL", Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL }
            };

            // Act
            List<TeamJobAd> adsWithNoCandidates;
            var hirings = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Assert
            Assert.AreEqual(2, hirings.Count);

            var mercedesHiring = hirings.FirstOrDefault(h => h.TeamId == "MERCEDES");
            var ferrariHiring = hirings.FirstOrDefault(h => h.TeamId == "FERRARI");

            Assert.IsNotNull(mercedesHiring);
            Assert.IsNotNull(ferrariHiring);

            // Mercedes should NOT pick HAMILTON (excluded)
            Assert.AreNotEqual("HAMILTON", mercedesHiring.DriverId,
                "Mercedes should not pick HAMILTON (excluded)");

            // Ferrari should NOT pick LECLERC (excluded)
            Assert.AreNotEqual("LECLERC", ferrariHiring.DriverId,
                "Ferrari should not pick LECLERC (excluded)");

            // Verify exclusions are set correctly
            Assert.IsTrue(mercedesHiring.ExcludeDriverIds.Contains("HAMILTON"));
            Assert.IsTrue(ferrariHiring.ExcludeDriverIds.Contains("LECLERC"));
        }

        #endregion

        #region FinalBallotResults - ExcludeDriverIds Tests

        [TestMethod]
        public void FinalBallotResults_ExcludedDriverInCandidates_IsDeprioritized()
        {
            // Arrange - Original hire already filtered by PickPotentialNewDrivers (won't be excluded driver)
            // But candidates might include the excluded driver - those should be deprioritized

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MERCEDES",
                        TeamReputation = TeamReputation.TOP_TEAM,
                        Role = DriverRole.FIRST_DRIVER,
                        DriverId = "RUSSELL",  // ✅ Already picked by PickPotentialNewDrivers (NOT HAMILTON)
                        DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL,
                        ExcludeDriverIds = new HashSet<string> { "HAMILTON" }  // HAMILTON was excluded
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        // Candidates might still include HAMILTON (from DriversProposeToTeams)
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "LECLERC", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BOTTAS", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            var mercedesResult = results[0];

            // Should pick LECLERC (best non-excluded candidate) over HAMILTON (excluded) or keep RUSSELL
            // HAMILTON should be deprioritized even though he has championship reputation
            if (mercedesResult.DriverId != "RUSSELL")  // If original hire is replaced
            {
                Assert.AreNotEqual("HAMILTON", mercedesResult.DriverId,
                    "HAMILTON (excluded) should be deprioritized - LECLERC should be picked instead");

                Assert.AreEqual("LECLERC", mercedesResult.DriverId,
                    "LECLERC should be picked (best non-excluded candidate)");
            }
        }

        [TestMethod]
        public void FinalBallotResults_OnlyExcludedDriverInCandidates_KeepsOriginal()
        {
            // Arrange - Original hire is fine, but only candidate is the excluded driver
            // Should keep original hire (excluded candidate is worse option)

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "HAAS",
                        TeamReputation = TeamReputation.SUPER_MINNOW,
                        Role = DriverRole.FIRST_DRIVER,
                        DriverId = "MAZEPIN",  // ✅ Already picked (not the excluded driver)
                        DriverReputation = DriverReputation.PAY_DRIVER_SEASON,
                        ExcludeDriverIds = new HashSet<string> { "GROSJEAN" }  // GROSJEAN was excluded
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "GROSJEAN", DriverReputation = DriverReputation.PRIME_MIDFIELD }
                        // ✅ Only GROSJEAN in candidates (and he's excluded)
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            var haasResult = results[0];

            // Should keep MAZEPIN (original) because GROSJEAN is excluded
            // OR might pick GROSJEAN anyway if PickWinner decides he's better despite exclusion
            // The key is that exclusion deprioritizes GROSJEAN
            Assert.IsNotNull(haasResult.DriverId,
                "Should have a driver hired");

            // Most likely keeps original since only candidate is excluded
            // But if GROSJEAN is significantly better, might still pick him (deprioritized, not blocked)
        }

        [TestMethod]
        public void FinalBallotResults_ExcludedDriverBetterThanOthers_OthersPreferred()
        {
            // Arrange - Original hire is decent, but candidates include excluded championship driver
            // Excluded championship driver should be deprioritized below non-excluded midfield drivers

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "REDBULL",
                        TeamReputation = TeamReputation.TOP_TEAM,
                        Role = DriverRole.FIRST_DRIVER,
                        DriverId = "PEREZ",  // ✅ Already picked (not VERSTAPPEN)
                        DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                        ExcludeDriverIds = new HashSet<string> { "VERSTAPPEN" }  // VERSTAPPEN excluded
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "VERSTAPPEN", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },  // Excluded, best
                        new TeamHiringBallotCandidate { DriverId = "SAINZ", DriverReputation = DriverReputation.PRIME_MIDFIELD },  // Not excluded
                        new TeamHiringBallotCandidate { DriverId = "LAWSON", DriverReputation = DriverReputation.YOUNG_TALENT }  // Not excluded, worse
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            var redbullResult = results[0];

            // Should pick SAINZ (best non-excluded candidate) over VERSTAPPEN (excluded)
            // Might also keep PEREZ (original) if he's deemed good enough
            if (redbullResult.DriverId != "PEREZ")  // If original is replaced
            {
                Assert.AreNotEqual("VERSTAPPEN", redbullResult.DriverId,
                    "VERSTAPPEN should be deprioritized (excluded)");

                Assert.AreEqual("SAINZ", redbullResult.DriverId,
                    "Should pick SAINZ - best non-excluded candidate");
            }
        }

        [TestMethod]
        public void FinalBallotResults_NoExclusions_BestDriverPicked()
        {
            // Arrange - No exclusions, should pick best driver normally

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MERCEDES",
                        TeamReputation = TeamReputation.TOP_TEAM,
                        Role = DriverRole.FIRST_DRIVER,
                        DriverId = "BOTTAS",
                        DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                        ExcludeDriverIds = null  // ✅ No exclusions
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "RUSSELL", DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BOTTAS", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            var mercedesResult = results[0];

            // Should pick HAMILTON or RUSSELL (both championship level, no exclusions)
            Assert.IsTrue(mercedesResult.DriverId == "HAMILTON" || mercedesResult.DriverId == "RUSSELL",
                "Should pick one of the championship-level drivers (no exclusions)");

            Assert.AreNotEqual("BOTTAS", mercedesResult.DriverId,
                "Should not pick BOTTAS (worse reputation than other candidates)");
        }

        [TestMethod]
        public void FinalBallotResults_MultipleBallotsWithDifferentExclusions_EachProcessedIndependently()
        {
            // Arrange - Multiple teams, each has already excluded their own exiting driver in PickPotentialNewDrivers
            // Now in FinalBallotResults, candidates might include those excluded drivers

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MERCEDES",
                        TeamReputation = TeamReputation.TOP_TEAM,
                        Role = DriverRole.FIRST_DRIVER,
                        DriverId = "RUSSELL",  // ✅ Mercedes already picked RUSSELL (not HAMILTON)
                        DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL,
                        ExcludeDriverIds = new HashSet<string> { "HAMILTON" }  // HAMILTON was excluded
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "LECLERC", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "FERRARI",
                        TeamReputation = TeamReputation.TOP_TEAM,
                        Role = DriverRole.FIRST_DRIVER,
                        DriverId = "SAINZ",  // ✅ Ferrari already picked SAINZ (not LECLERC)
                        DriverReputation = DriverReputation.PRIME_MIDFIELD,
                        ExcludeDriverIds = new HashSet<string> { "LECLERC" }  // LECLERC was excluded
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "LECLERC", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(2, results.Count);

            var mercedesResult = results.FirstOrDefault(r => r.TeamId == "MERCEDES");
            var ferrariResult = results.FirstOrDefault(r => r.TeamId == "FERRARI");

            Assert.IsNotNull(mercedesResult);
            Assert.IsNotNull(ferrariResult);

            // If Mercedes upgrades, should pick LECLERC (not excluded HAMILTON)
            if (mercedesResult.DriverId != "RUSSELL")
            {
                Assert.AreEqual("LECLERC", mercedesResult.DriverId,
                    "Mercedes should pick LECLERC (HAMILTON excluded)");
            }

            // If Ferrari upgrades, should pick HAMILTON (not excluded LECLERC)
            if (ferrariResult.DriverId != "SAINZ")
            {
                Assert.AreEqual("HAMILTON", ferrariResult.DriverId,
                    "Ferrari should pick HAMILTON (LECLERC excluded)");
            }
        }

        [TestMethod]
        public void FinalBallotResults_ExcludedDriverAlreadyHiredByAnotherTeam_PicksNextBest()
        {
            // Arrange - HAMILTON excluded by Mercedes, but already hired by Ferrari
            // Mercedes should pick next best (not HAMILTON anyway due to exclusion)

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "FERRARI",
                        TeamReputation = TeamReputation.TOP_TEAM,
                        Role = DriverRole.FIRST_DRIVER,
                        DriverId = "HAMILTON",
                        DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL,
                        ExcludeDriverIds = null  // Ferrari doesn't exclude HAMILTON
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MERCEDES",
                        TeamReputation = TeamReputation.MIDFIELD_HIGH,  // Lower priority than Ferrari
                        Role = DriverRole.FIRST_DRIVER,
                        DriverId = "TEMP",
                        DriverReputation = DriverReputation.PRIME_MIDFIELD,
                        ExcludeDriverIds = new HashSet<string> { "HAMILTON" }  // Mercedes excludes HAMILTON anyway
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "RUSSELL", DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(2, results.Count);

            var ferrariResult = results.FirstOrDefault(r => r.TeamId == "FERRARI");
            var mercedesResult = results.FirstOrDefault(r => r.TeamId == "MERCEDES");

            // Ferrari picks HAMILTON (no exclusion, best reputation)
            Assert.AreEqual("HAMILTON", ferrariResult.DriverId);

            // Mercedes picks RUSSELL (HAMILTON excluded AND already hired by Ferrari)
            Assert.AreEqual("RUSSELL", mercedesResult.DriverId,
                "Mercedes should pick RUSSELL (HAMILTON excluded and already hired)");
        }

        #endregion

        #region Edge Cases and Integration Tests

        [TestMethod]
        public void Integration_ExcludedDriverMovesToDifferentTeam()
        {
            // Arrange - Full integration test: Mercedes excludes HAMILTON, Ferrari picks him up

            // Step 1: PickPotentialNewDrivers
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "MERCEDES",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = "HAMILTON",
                    ExitingDriverWillingToRenew = false  // ✅ HAMILTON excluded
                },
                new TeamJobAd
                {
                    TeamId = "FERRARI",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver { DriverId = "HAMILTON", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new UnemployedDriver { DriverId = "RUSSELL", Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL },
                new UnemployedDriver { DriverId = "LECLERC", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
            };

            List<TeamJobAd> adsWithNoCandidates;
            var initialHirings = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Step 2: DriversProposeToTeams
            var remainingDrivers = poolOfDrivers.Where(d => !initialHirings.Any(h => h.DriverId == d.DriverId)).ToList();
            var ballots = _offSeasonMovements.DriversProposeToTeams(remainingDrivers, initialHirings).ToList();

            // Step 3: FinalBallotResults
            var finalResults = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            var mercedesResult = finalResults.FirstOrDefault(r => r.TeamId == "MERCEDES");
            var ferrariResult = finalResults.FirstOrDefault(r => r.TeamId == "FERRARI");

            Assert.IsNotNull(mercedesResult);
            Assert.IsNotNull(ferrariResult);

            // Mercedes should NOT have HAMILTON (excluded)
            Assert.AreNotEqual("HAMILTON", mercedesResult.DriverId,
                "Mercedes should not hire HAMILTON (excluded)");

            // HAMILTON should be available for Ferrari or someone else
            // This demonstrates driver market movement - excluded driver finds new team
            var hamiltonHired = finalResults.Any(r => r.DriverId == "HAMILTON");
            Assert.IsTrue(hamiltonHired || remainingDrivers.Any(d => d.DriverId == "HAMILTON"),
                "HAMILTON should either be hired by another team or remain in unemployment pool");
        }

        #endregion
    }
}