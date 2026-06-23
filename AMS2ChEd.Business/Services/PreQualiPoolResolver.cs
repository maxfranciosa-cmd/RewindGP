using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;
using System.Collections.Immutable;

namespace AMS2ChEd.Business.Services
{
    public class PreQualiPoolResolver : IPreQualiPoolResolver
    {
        public PreQualiPool Resolve(
            ISaveGame saveGame,
            int roundIndex)
        {
            var allPotentialTeams = saveGame.CurrentSeason.Teams.ToList();
            int totalDrivers = allPotentialTeams.DriverCount();
            var maxDriversAllowed = (saveGame.CurrentSeason.MaxDriversPerRace ?? 26);

            if (totalDrivers <= maxDriversAllowed)
                return PreQualiPool.NotApplicable();

            int overflow = totalDrivers - maxDriversAllowed;
            int minimumPoolSize = overflow + 2;
           
            var newTeams = IdentifyNewTeams(saveGame, allPotentialTeams);
            var returningTeams = allPotentialTeams.Except(newTeams).ToList();

            List<ITeamEntry> pool;

            if(roundIndex == 0)
            {
                RefreshDefaultPrequalifyingFlagBeginOfSeason(saveGame, allPotentialTeams, minimumPoolSize);
            }
            else if (roundIndex == (saveGame.CurrentSeason.Races.Count() / 2))
            {
                RefreshDefaultPrequalifyingFlagHalfOfSeason(saveGame, allPotentialTeams, minimumPoolSize);
            }

            pool = allPotentialTeams
                .Where(t => t.DefaultPrequalifying)
                .ToList();

            var committed = allPotentialTeams.Except(pool).ToList();

            return new PreQualiPool
            {
                PoolTeams = pool,
                CommittedTeams = committed,
                PassCount = maxDriversAllowed - (totalDrivers - pool.DriverCount())
            };
        }
       
        private void RefreshDefaultPrequalifyingFlagHalfOfSeason(ISaveGame saveGame, List<ITeamEntry> allPotentialTeams, int minimumPoolSize)
        {
            var teamsFromWorstToBest = allPotentialTeams
                .OrderByDescending(t => saveGame.CurrentConstructorStandings.FirstOrDefault(s => s.TeamId == t.TeamId)?.Position ?? int.MaxValue)
                .ToList();

            var poolTeamIds = TakeTeamsUntilDriverCount(teamsFromWorstToBest, minimumPoolSize).Select(t => t.TeamId).ToHashSet();

            foreach (var team in allPotentialTeams)
                team.DefaultPrequalifying = poolTeamIds.Contains(team.TeamId);
        }
        private void RefreshDefaultPrequalifyingFlagBeginOfSeason(ISaveGame saveGame, List<ITeamEntry> allPotentialTeams, int minimumPoolSize)
        {
            var previousSeasonTeamIds = saveGame.HistoricalConstructorStandings
                .Where(s => s.Year == saveGame.CurrentSeason.Year - 1)
                .SelectMany(s => s.Standing.Select(s => s.TeamId))
                .ToHashSet();

            if (!previousSeasonTeamIds.Any()) return;

            var teamsThatShouldPrequalify = new List<ITeamEntry>();

            var newTeams = IdentifyNewTeams(saveGame, allPotentialTeams);
            var returningTeams = allPotentialTeams.Except(newTeams).ToList();

            var poolTeams = newTeams.ToList();
            int currentDriverCount = newTeams.DriverCount();

            if (currentDriverCount < minimumPoolSize)
            {
                var ranked = GetWorstReturningTeams(saveGame, returningTeams, returningTeams.Count);
                poolTeams.AddRange(TakeTeamsUntilDriverCount(ranked, minimumPoolSize - currentDriverCount));
            }

            var poolTeamIds = poolTeams.Select(t => t.TeamId).ToHashSet();
            foreach (var team in allPotentialTeams)
                team.DefaultPrequalifying = poolTeamIds.Contains(team.TeamId);
        }

        private List<ITeamEntry> TakeTeamsUntilDriverCount(IEnumerable<ITeamEntry> rankedTeams, int targetDriverCount)
        {
            var result = new List<ITeamEntry>();
            int count = 0;

            foreach (var team in rankedTeams)
            {
                if (count >= targetDriverCount) break;
                result.Add(team);
                count += (new[] { team }).DriverCount();
            }

            return result;
        }

        private List<ITeamEntry> IdentifyNewTeams(ISaveGame saveGame, List<ITeamEntry> allTeams)
        {
            var previousSeasonTeamIds = saveGame.HistoricalConstructorStandings
                .Where(s => s.Year == saveGame.CurrentSeason.Year - 1)
                .SelectMany(s => s.Standing.Select(s => s.TeamId))
                .ToHashSet();

            if (!previousSeasonTeamIds.Any())
                return new List<ITeamEntry>();

            return allTeams
                .Where(t => !previousSeasonTeamIds.Contains(t.TeamId))
                .ToList();
        }

        private List<ITeamEntry> GetWorstReturningTeams(
            ISaveGame saveGame,
            List<ITeamEntry> returningTeams,
            int count)
        {
            if (count <= 0) return new List<ITeamEntry>();

            if (saveGame.HistoricalConstructorStandings.Any())
            {
                return returningTeams
                    .OrderByDescending(t => saveGame.HistoricalConstructorStandings.Last(s => s.Year == saveGame.CurrentSeason.Year - 1).Standing
                        .FirstOrDefault(s => s.TeamId == t.TeamId)?.Position ?? int.MaxValue)
                    .Take(count)
                    .ToList();
            }

            // Fallback: no standings yet — use DefaultPrequalifying flag.
            return returningTeams
                .Where(t => t.DefaultPrequalifying)
                .Take(count)
                .ToList();
        }
    }
}
