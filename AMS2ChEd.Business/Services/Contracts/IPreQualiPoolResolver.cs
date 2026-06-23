using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Services.Contracts
{
    public interface IPreQualiPoolResolver
    {
        public PreQualiPool Resolve(ISaveGame saveGame, int roundIndex);
    }

    public class PreQualiPool
    {
        public List<ITeamEntry> PoolTeams { get; set; }
        public List<ITeamEntry> CommittedTeams { get; set; }
        public int PassCount { get; set; }
        public bool IsApplicable => PoolTeams?.Any() == true;

        public static PreQualiPool NotApplicable() => new PreQualiPool
        {
            PoolTeams = new List<ITeamEntry>(),
            CommittedTeams = new List<ITeamEntry>(),
            PassCount = 0
        };
    }
}
