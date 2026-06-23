using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class TeamsLoader : ITeamsLoader
    {
        private static Dictionary<string, Team> teamsCache;

        public Dictionary<string, Team> LoadTeams()
        {
            if (teamsCache != null)
                return teamsCache;

            try
            {
                if (!File.Exists(StoragePaths.TeamsFilePath))
                {
                    throw new FileNotFoundException($"Teams database not found at: {StoragePaths.TeamsFilePath}");
                }

                string json = File.ReadAllText(StoragePaths.TeamsFilePath);
                var teamsDb = JsonSerializer.Deserialize<TeamsDatabase>(json, DefaultJsonSerializerOptions.Instance);

                teamsCache = teamsDb.Teams
                                    .ToDictionary(x => x.TeamId);
                return teamsCache;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading teams database: {ex.Message}", ex);
            }
        }

        public Team GetTeam(string teamId)
        {
            var teams = LoadTeams();
            return teams.ContainsKey(teamId) ? teams[teamId] : null;
        }

        public void SaveTeams(Dictionary<string, Team> teams)
        {
            try
            {
                var teamsDb = new TeamsDatabase
                {
                    Teams = teams.Values.ToList()
                };

                var options = new JsonSerializerOptions(DefaultJsonSerializerOptions.Instance)
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(teamsDb, options);

                // Ensure directory exists
                string directory = Path.GetDirectoryName(StoragePaths.TeamsFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(StoragePaths.TeamsFilePath, json);

                // Update cache
                teamsCache = teams;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving teams database: {ex.Message}", ex);
            }
        }
    }
}
