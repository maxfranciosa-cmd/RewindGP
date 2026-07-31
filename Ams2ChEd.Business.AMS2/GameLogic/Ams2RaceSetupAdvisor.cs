using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.AMS2.GameLogic
{
    public class Ams2RaceSetupAdvisor : IRaceSetupAdvisor
    {
        public int GetSuggestedAiDifficulty(ISeason season, string teamId, int driverSlot, IEnumerable<EntryListEntry>? poolEntries = null)
        {
            var team = season.Teams.OfType<Ams2TeamEntry>().FirstOrDefault(t => t.TeamId == teamId);
            if (team == null) return 0;

            if (poolEntries == null)
            {
                return GetReputationBasedDifficulty(team.Reputation);
            }

            var normalisedSeason = NormalisePreQualiPool(season, poolEntries);
            var normalisedTeam = normalisedSeason.Teams.OfType<Ams2TeamEntry>().FirstOrDefault(t => t.TeamId == teamId);

            double playerMalus = normalisedTeam?
                .GetAms2CarPerformanceMalus(driverSlot)?
                .GetValueOrDefault("qualifying_skill", 0.0) ?? 0.0;

            // +5 difficulty points per 0.1 malus gap from the fastest car (which is now at 0).
            return (int)Math.Round(playerMalus / 0.1) * 5;
        }

        public string GetCarDisplayName(ISeason season, string teamId, int driverSlot)
        {
            var team = season.Teams.OfType<Ams2TeamEntry>().FirstOrDefault(t => t.TeamId == teamId);
            return team?.GetAms2Car(driverSlot) ?? "";
        }

        public bool SeasonUsesPerformanceScalars(ISeason season)
        {
            return season.Teams.OfType<Ams2TeamEntry>().Any(t => t.HasPerformanceScalarMalus);
        }

        private static int GetReputationBasedDifficulty(TeamReputation reputation)
        {
            switch (reputation)
            {
                case TeamReputation.SUPER_MINNOW:
                    return 15;
                case TeamReputation.MINNOW:
                    return 10;
                case TeamReputation.MIDFIELD:
                    return 7;
                case TeamReputation.MIDFIELD_HIGH:
                    return 5;
                case TeamReputation.TOP_TEAM:
                    return 0;
                default:
                    return 0;
            }
        }

        public ISeason NormalisePreQualiPool(ISeason season, IEnumerable<EntryListEntry> poolEntries)
        {
            var normalisedSeason = ((Ams2Season)season).DeepClone();

            var relevantMalusDicts = new List<Dictionary<string, double>>();

            foreach (var entry in poolEntries)
            {
                var team = normalisedSeason.Teams
                    .OfType<Ams2TeamEntry>()
                    .FirstOrDefault(t => t.TeamId == entry.TeamId);
                if (team == null) continue;

                if (!string.IsNullOrEmpty(entry.Driver1Id))
                {
                    var malus1 = team.GetAms2CarPerformanceMalus(1);
                    if (malus1 != null) relevantMalusDicts.Add(malus1);
                }

                if (!string.IsNullOrEmpty(entry.Driver2Id))
                {
                    var malus2 = team.GetAms2CarPerformanceMalus(2);
                    if (malus2 != null) relevantMalusDicts.Add(malus2);
                }
            }

            var dictsWithQualifyingSkill = relevantMalusDicts
                .Where(m => m.ContainsKey("qualifying_skill"))
                .ToList();

            if (!dictsWithQualifyingSkill.Any())
            {
                return normalisedSeason;
            }

            double minQualifyingSkill = dictsWithQualifyingSkill.Min(m => m["qualifying_skill"]);

            var shiftedDicts = new HashSet<Dictionary<string, double>>();
            foreach (var malus in dictsWithQualifyingSkill)
            {
                if (!shiftedDicts.Add(malus)) continue;

                malus["qualifying_skill"] -= minQualifyingSkill;
            }

            return normalisedSeason;
        }
    }
}
