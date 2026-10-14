using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Improvement packages: each team can install up to three per season, at most one before
    /// each race, on the instruction of its first driver (or either of two equal drivers). A
    /// package randomly varies the car's performance values, for better or worse, and applies
    /// to both cars of the team.
    /// </summary>
    public class ImprovementPackageManager : IImprovementPackageManager
    {
        public const int PACKAGES_PER_SEASON = 3;

        // AI teams don't touch the car before this round (0-based race index)
        public const int FIRST_RACE_INDEX_FOR_AI = 2;
        // chance for a team that is where its reputation lets it be content
        public const double CONTENT_TEAM_CHANCE = 0.08;
        public const double CHANCE_PER_POSITION_BELOW_EXPECTATION = 0.10;
        // further down than this adds nothing: the order among the teams yet to score says little
        public const int MAX_POSITIONS_BELOW_EXPECTATION = 3;
        // the team right behind in the standings is within a race win's worth of points
        // (only once both have scored: before that every gap is that small)
        public const double THREAT_CHANCE_BONUS = 0.05;
        // "use it or lose it" in the last rounds of the season
        public const int LATE_SEASON_RACES = 3;
        public const double LATE_SEASON_CHANCE_BONUS = 0.10;
        public const double INSTALLED_LAST_RACE_MULTIPLIER = 0.25;
        public const double INSTALLED_TWO_RACES_AGO_MULTIPLIER = 0.6;

        private readonly IImprovementPackageProvider _provider;
        private readonly Random _random;

        public ImprovementPackageManager(IImprovementPackageProvider provider, Random random = null)
        {
            _provider = provider;
            _random = random ?? new Random();
        }

        public IReadOnlyList<ImprovementStat> Stats => _provider.Stats;

        public int GetRemainingPackages(ITeamEntry team)
        {
            return Math.Max(0, PACKAGES_PER_SEASON - (team.ImprovementPackages?.Count ?? 0));
        }

        public bool CanDriverInstall(ISaveGame saveGame, string driverId)
        {
            var race = GetNextRace(saveGame);
            var team = GetContractedTeam(saveGame, driverId);
            if (race == null || team == null)
                return false;

            return CanTeamInstall(team, race.RaceId) &&
                   GetDeciders(saveGame, team, race.RaceId).Any(c => c.DriverId == driverId);
        }

        public bool IsSecondDriverCoveringForAbsentLeader(ISaveGame saveGame, string driverId)
        {
            var race = GetNextRace(saveGame);
            var team = GetContractedTeam(saveGame, driverId);
            if (race == null || team == null)
                return false;

            return GetDeciders(saveGame, team, race.RaceId)
                .Any(c => c.DriverId == driverId && c.Role == ContractRole.SECOND_DRIVER);
        }

        public ImprovementPackage Install(ISaveGame saveGame, string teamId, string driverId)
        {
            var race = GetNextRace(saveGame);
            var team = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == teamId);
            if (race == null || team == null || !CanTeamInstall(team, race.RaceId))
                return null;

            var package = new ImprovementPackage
            {
                DriverId = driverId,
                RaceId = race.RaceId,
                Values = _provider.GenerateValues(saveGame.CurrentSeason, _random)
            };

            team.ImprovementPackages ??= new List<ImprovementPackage>();
            team.ImprovementPackages.Add(package);
            return package;
        }

        public IReadOnlyList<InstalledImprovementPackage> ProcessAiDecisions(ISaveGame saveGame)
        {
            var installed = new List<InstalledImprovementPackage>();
            var race = GetNextRace(saveGame);
            if (race == null)
                return installed;

            var playerId = saveGame.PlayerData?.DriverId;

            foreach (var team in saveGame.CurrentSeason.Teams)
            {
                if (!CanTeamInstall(team, race.RaceId))
                    continue;

                var deciders = GetDeciders(saveGame, team, race.RaceId)
                    .Where(c => c.DriverId != playerId)
                    .ToList();
                if (!deciders.Any())
                    continue;

                if (_random.NextDouble() >= GetInstallChance(saveGame, team))
                    continue;

                var decider = deciders[_random.Next(deciders.Count)];
                var package = Install(saveGame, team.TeamId, decider.DriverId);
                if (package != null)
                    installed.Add(new InstalledImprovementPackage { TeamId = team.TeamId, Package = package });
            }

            return installed;
        }

        public IReadOnlyList<InstalledImprovementPackage> GetPackagesForRace(ISaveGame saveGame, int raceId)
        {
            return saveGame.CurrentSeason.Teams
                .SelectMany(t => (t.ImprovementPackages ?? new List<ImprovementPackage>())
                    .Where(p => p.RaceId == raceId)
                    .Select(p => new InstalledImprovementPackage { TeamId = t.TeamId, Package = p }))
                .ToList();
        }

        /// <summary>
        /// How likely an AI-led team is to install a package before the next race: the further
        /// it sits below the worst constructors' position a team of its reputation can accept,
        /// the likelier.
        /// </summary>
        public double GetInstallChance(ISaveGame saveGame, ITeamEntry team)
        {
            var races = saveGame.CurrentSeason.Races.ToList();
            var raceIndex = saveGame.NextGpIndex;
            if (raceIndex < FIRST_RACE_INDEX_FOR_AI || raceIndex >= races.Count)
                return 0;

            var standings = (saveGame.CurrentConstructorStandings ?? Enumerable.Empty<ConstructorStandingEntry>()).ToList();
            var standing = standings.FirstOrDefault(s => s.TeamId == team.TeamId);
            if (standing == null)
                return 0;

            var worstAcceptablePosition = GetWorstAcceptablePosition(saveGame, team);
            var positionsBelowExpectation = worstAcceptablePosition == null
                ? 0
                : Math.Clamp(standing.Position - worstAcceptablePosition.Value, 0, MAX_POSITIONS_BELOW_EXPECTATION);

            var chance = CONTENT_TEAM_CHANCE + CHANCE_PER_POSITION_BELOW_EXPECTATION * positionsBelowExpectation;

            var teamBehind = standings.FirstOrDefault(s => s.Position == standing.Position + 1);
            if (teamBehind != null && teamBehind.Points > 0 &&
                standing.Points - teamBehind.Points <= GetPointsForWin(saveGame.CurrentSeason))
                chance += THREAT_CHANCE_BONUS;

            if (races.Count - raceIndex <= LATE_SEASON_RACES)
                chance += LATE_SEASON_CHANCE_BONUS;

            // teams rarely bring two packages in a row
            if (HasInstalledForRace(team, races[raceIndex - 1].RaceId))
                chance *= INSTALLED_LAST_RACE_MULTIPLIER;
            else if (HasInstalledForRace(team, races[raceIndex - 2].RaceId))
                chance *= INSTALLED_TWO_RACES_AGO_MULTIPLIER;

            return chance;
        }

        // points systems change over the years, so "within reach" is measured in race wins
        private static double GetPointsForWin(ISeason season)
        {
            return season.PointsSystem != null && season.PointsSystem.Any() ? season.PointsSystem.Values.Max() : 0;
        }

        // What a team can live with depends on its reputation: for a top team anything but first
        // is a failure, a midfield team is fine as one of the best two of its peers, a minnow as
        // long as it isn't the last of the minnows. Null for a super minnow, which has nothing to
        // prove. Positions are counted behind the teams of a better reputation, so being beaten
        // by a lesser team is a failure too.
        private static int? GetWorstAcceptablePosition(ISaveGame saveGame, ITeamEntry team)
        {
            var teams = saveGame.CurrentSeason.Teams.ToList();
            var betterTeams = teams.Count(t => t.Reputation > team.Reputation);
            var sameReputationTeams = teams.Count(t => t.Reputation == team.Reputation);

            switch (team.Reputation)
            {
                case TeamReputation.TOP_TEAM:
                    return 1;
                case TeamReputation.SUPER_MINNOW:
                    return null;
                case TeamReputation.MINNOW:
                    return betterTeams + Math.Max(1, sameReputationTeams - 1);
                default:
                    return betterTeams + Math.Min(2, sameReputationTeams);
            }
        }

        // the contracted drivers who may instruct the team for the given race: whoever is absent
        // (and their substitute) has no say, and a second driver only decides when no first or
        // equal driver is around
        private static List<DriverContract> GetDeciders(ISaveGame saveGame, ITeamEntry team, int raceId)
        {
            var absentDriverIds = (saveGame.CurrentSeason.Absences ?? Enumerable.Empty<Absence>())
                .Where(a => a.RaceId == raceId && a.TeamId == team.TeamId)
                .Select(a => a.DriverOut)
                .ToHashSet();

            var presentDrivers = new[] { team.Driver1Contract, team.Driver2Contract }
                .Where(c => c != null && !string.IsNullOrEmpty(c.DriverId) && !absentDriverIds.Contains(c.DriverId))
                .ToList();

            var leaders = presentDrivers.Where(c => c.Role != ContractRole.SECOND_DRIVER).ToList();

            return leaders.Any() ? leaders : presentDrivers;
        }

        private bool CanTeamInstall(ITeamEntry team, int raceId)
        {
            return GetRemainingPackages(team) > 0 && !HasInstalledForRace(team, raceId);
        }

        private static bool HasInstalledForRace(ITeamEntry team, int raceId)
        {
            return team.ImprovementPackages != null && team.ImprovementPackages.Any(p => p.RaceId == raceId);
        }

        private static Race GetNextRace(ISaveGame saveGame)
        {
            return saveGame.CurrentSeason.Races.ElementAtOrDefault(saveGame.NextGpIndex);
        }

        private static ITeamEntry GetContractedTeam(ISaveGame saveGame, string driverId)
        {
            if (string.IsNullOrEmpty(driverId))
                return null;

            return saveGame.CurrentSeason.Teams.FirstOrDefault(t =>
                t.Driver1Contract?.DriverId == driverId || t.Driver2Contract?.DriverId == driverId);
        }
    }
}
