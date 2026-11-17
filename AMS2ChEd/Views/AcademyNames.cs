using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Resources;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// How an academy is named on screen, from its parent and sister teams (shared by the academy
    /// letter, the academies window and the agent).
    /// </summary>
    public static class AcademyNames
    {
        // named after its parent teams ("the Ferrari driver academy")
        public static string AcademyName(Academy academy, IEnumerable<ITeamEntry> teams) => AcademyName(academy, TeamNames(teams));

        // "Parent team: Ferrari · Sister team: Sauber"
        public static string AcademyTeams(Academy academy, IEnumerable<ITeamEntry> teams) => AcademyTeams(academy, TeamNames(teams));

        internal static Dictionary<string, string> TeamNames(IEnumerable<ITeamEntry> teams)
        {
            var names = new Dictionary<string, string>();
            foreach (var team in teams ?? Enumerable.Empty<ITeamEntry>())
                names.TryAdd(team.TeamId, team.TeamName);
            return names;
        }

        internal static string AcademyName(Academy academy, Dictionary<string, string> teamNames)
        {
            var parents = Names(academy.ParentTeams, teamNames);
            return string.Format(Strings.AcademyLetterWindow_AcademyName_Format, string.IsNullOrEmpty(parents) ? academy.Id : parents);
        }

        internal static string AcademyTeams(Academy academy, Dictionary<string, string> teamNames)
        {
            var sisters = Names(academy.SisterTeams, teamNames);
            return string.Format(Strings.AcademyLetterWindow_AcademyTeams_Format,
                Names(academy.ParentTeams, teamNames),
                string.IsNullOrEmpty(sisters) ? Strings.AcademyLetterWindow_NoSisterTeam : sisters);
        }

        private static string Names(IEnumerable<string> teamIds, Dictionary<string, string> teamNames) =>
            string.Join(", ", (teamIds ?? Enumerable.Empty<string>()).Select(id => teamNames.GetValueOrDefault(id) ?? id));
    }
}
