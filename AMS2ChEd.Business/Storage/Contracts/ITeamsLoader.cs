using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface ITeamsLoader
    {
        Dictionary<string, Team> LoadTeams();
        Team GetTeam(string teamId);

        void SaveTeams(Dictionary<string, Team> teams);
    }
}
