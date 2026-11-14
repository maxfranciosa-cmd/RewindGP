using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Business.Helpers
{
    /// <summary>
    /// Who belongs to which junior academy, and the fit a member gets at his academy's teams.
    /// Nothing is stored: membership comes from the season's academies and the driver's age.
    /// </summary>
    public static class AcademyMembership
    {
        // "young": younger than this in the season being played or hired for
        public const int AgeLimit = ReputationUpdater.YOUNG_DRIVER_AGE;

        public static bool IsYoung(int seasonYear, int yearOfBirth) => seasonYear - yearOfBirth < AgeLimit;

        /// <summary>
        /// The academy the driver belongs to in that season, or null: an AI driver must be listed
        /// in its drivers, the player must have its id as his academy_id, and either must be young
        /// in that season. A player whose academy the season lacks is a member of none (dormant).
        /// An AI driver whose last signing (lastTeamId, see LastTeams) was with a team outside his
        /// academy is out of it too, until he signs for one of its teams again while still listed.
        /// </summary>
        public static Academy AcademyOf(ISeason season, string driverId, int yearOfBirth, string playerDriverId, string playerAcademyId, string lastTeamId = null)
        {
            if (season == null)
                return null;
            return AcademyOf(season.Academies, season.Year, driverId, yearOfBirth, playerDriverId, playerAcademyId, lastTeamId);
        }

        public static Academy AcademyOf(IEnumerable<Academy> academies, int seasonYear, string driverId, int yearOfBirth, string playerDriverId, string playerAcademyId, string lastTeamId = null)
        {
            if (academies == null || string.IsNullOrEmpty(driverId) || !IsYoung(seasonYear, yearOfBirth))
                return null;

            if (driverId == playerDriverId)
                return string.IsNullOrEmpty(playerAcademyId) ? null : academies.FirstOrDefault(a => a.Id == playerAcademyId);

            var academy = academies.FirstOrDefault(a => a.Drivers?.Contains(driverId) == true);
            if (academy != null && !string.IsNullOrEmpty(lastTeamId) && !IsParentTeam(academy, lastTeamId) && !IsSisterTeam(academy, lastTeamId))
                return null;
            return academy;
        }

        /// <summary>
        /// The team each driver signed for last, as the save knows it: the team holding his
        /// contract (race seat or reserve) in the current season, else the team he last raced for
        /// in an earlier season of the save. A driver with neither is missing. Nothing new is
        /// stored: an AI junior who signed outside his academy is out of it (AcademyOf) until he
        /// signs for one of its teams again.
        /// </summary>
        public static Dictionary<string, string> LastTeams(ISaveGame saveGame)
        {
            var lastTeams = new Dictionary<string, string>();
            if (saveGame?.CurrentSeason == null)
                return lastTeams;

            foreach (var team in saveGame.CurrentSeason.Teams ?? Enumerable.Empty<ITeamEntry>())
            {
                foreach (var contract in new[] { team.Driver1Contract, team.Driver2Contract, team.ReserveContract })
                {
                    if (!string.IsNullOrEmpty(contract?.DriverId))
                        lastTeams.TryAdd(contract.DriverId, team.TeamId);
                }
            }

            // earlier seasons, latest first; the current season's standings (once it's archived)
            // are left out: a driver without a contract now only stood in for someone this year
            var earlierSeasons = (saveGame.HistoricalDriverStandings ?? Enumerable.Empty<HistoricalDriverStanding>())
                .Where(s => s.Year < saveGame.CurrentSeason.Year)
                .OrderByDescending(s => s.Year);
            foreach (var season in earlierSeasons)
            {
                foreach (var entry in season.Standing ?? Enumerable.Empty<HisoricalDriverStandingEntry>())
                {
                    if (!string.IsNullOrEmpty(entry.DriverId) && !string.IsNullOrEmpty(entry.TeamId))
                        lastTeams.TryAdd(entry.DriverId, entry.TeamId);
                }
            }

            return lastTeams;
        }

        public static bool IsParentTeam(Academy academy, string teamId) =>
            academy?.ParentTeams?.Contains(teamId) == true;

        public static bool IsSisterTeam(Academy academy, string teamId) =>
            academy?.SisterTeams?.Contains(teamId) == true;

        // the academy listing the team as parent or sister, or null
        public static Academy AcademyOfTeam(ISeason season, string teamId) =>
            season?.Academies?.FirstOrDefault(a => IsParentTeam(a, teamId) || IsSisterTeam(a, teamId));

        /// <summary>
        /// The fit a member gets at a team of his academy, whatever the seat (first, second,
        /// reserve), or null at any other team. A sister team takes YOUNG_TALENT and
        /// YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN as a PerfectFit, a parent team only the latter;
        /// every other member is a GoodFit.
        /// </summary>
        public static DriverHirer.DriverPolicyFit? AcademyFit(Academy academy, string teamId, DriverReputation reputation)
        {
            var isParent = IsParentTeam(academy, teamId);
            var isSister = IsSisterTeam(academy, teamId);
            if (!isParent && !isSister)
                return null;

            if (reputation == DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN)
                return DriverHirer.DriverPolicyFit.PerfectFit;
            if (reputation == DriverReputation.YOUNG_TALENT && isSister)
                return DriverHirer.DriverPolicyFit.PerfectFit;
            return DriverHirer.DriverPolicyFit.GoodFit;
        }

        // the better (by DriverHirer.GetFitWeight) of the normal fit and the academy fit; the normal
        // one on a tie, so an OverQualified star stays OverQualified
        public static DriverHirer.DriverPolicyFit Better(DriverHirer.DriverPolicyFit normalFit, DriverHirer.DriverPolicyFit? academyFit)
        {
            if (academyFit == null)
                return normalFit;
            return DriverHirer.GetFitWeight(academyFit.Value) > DriverHirer.GetFitWeight(normalFit) ? academyFit.Value : normalFit;
        }
    }

    /// <summary>
    /// The academies of the season being hired for, with what's needed to tell who belongs to
    /// them: that season's year, each driver's year of birth, and the player's academy_id. Built
    /// once per off-season (OffSeasonOrchestrator) or per outlook, and handed to the hiring fit
    /// checks (DriverHirer.DoesDriverFitSeat). Null means no academies: the reputation-only fit.
    /// </summary>
    public class AcademyContext
    {
        public IReadOnlyList<Academy> Academies { get; }
        public int SeasonYear { get; }
        public IReadOnlyDictionary<string, int> YearsOfBirth { get; }
        public string PlayerDriverId { get; }
        public string PlayerAcademyId { get; }
        // the team each driver signed for last (AcademyMembership.LastTeams)
        public IReadOnlyDictionary<string, string> LastTeams { get; }

        public AcademyContext(IEnumerable<Academy> academies, int seasonYear, IReadOnlyDictionary<string, int> yearsOfBirth, string playerDriverId, string playerAcademyId, IReadOnlyDictionary<string, string> lastTeams = null)
        {
            Academies = academies?.ToList() ?? new List<Academy>();
            SeasonYear = seasonYear;
            YearsOfBirth = yearsOfBirth ?? new Dictionary<string, int>();
            PlayerDriverId = playerDriverId;
            PlayerAcademyId = playerAcademyId;
            LastTeams = lastTeams ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// The context for hiring into that season, or null when it has no academies. The years of
        /// birth come from every driver given (the save's drivers, plus next season's rookies when
        /// they aren't in the save yet); the first one listed for a driver wins. The last teams
        /// (AcademyMembership.LastTeams of the save) drop the juniors who signed outside.
        /// </summary>
        public static AcademyContext For(ISeason season, IEnumerable<IDriverData> drivers, string playerDriverId, string playerAcademyId, IReadOnlyDictionary<string, string> lastTeams = null)
        {
            if (season?.Academies == null || season.Academies.Count == 0)
                return null;

            var yearsOfBirth = new Dictionary<string, int>();
            foreach (var driver in drivers ?? Enumerable.Empty<IDriverData>())
            {
                if (driver?.DriverId != null)
                    yearsOfBirth.TryAdd(driver.DriverId, driver.YearOfBirth);
            }

            return new AcademyContext(season.Academies, season.Year, yearsOfBirth, playerDriverId, playerAcademyId, lastTeams);
        }

        // the driver's academy in the season being hired for, or in atYear when given (a judgement
        // about a later season: ReserveAdvice)
        public Academy AcademyOf(string driverId, int? atYear = null)
        {
            if (string.IsNullOrEmpty(driverId) || !YearsOfBirth.TryGetValue(driverId, out var yearOfBirth))
                return null;
            LastTeams.TryGetValue(driverId, out var lastTeamId);
            return AcademyMembership.AcademyOf(Academies, atYear ?? SeasonYear, driverId, yearOfBirth, PlayerDriverId, PlayerAcademyId, lastTeamId);
        }

        // whether the driver is a member of an academy the team is a parent or sister team of: he
        // wins a tie on fit there (DriverHirer)
        public bool IsMemberAt(string driverId, string teamId)
        {
            var academy = AcademyOf(driverId);
            return AcademyMembership.IsParentTeam(academy, teamId) || AcademyMembership.IsSisterTeam(academy, teamId);
        }

        // the normal fit, raised to the academy fit at a team of the driver's academy
        public DriverHirer.DriverPolicyFit Apply(string driverId, DriverReputation reputation, string teamId, DriverHirer.DriverPolicyFit normalFit, int? atYear = null)
        {
            var academy = AcademyOf(driverId, atYear);
            return academy == null ? normalFit : AcademyMembership.Better(normalFit, AcademyMembership.AcademyFit(academy, teamId, reputation));
        }
    }
}
