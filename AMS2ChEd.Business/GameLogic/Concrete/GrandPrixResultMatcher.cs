using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Pairs the current season's Grand Prix results with the races of its calendar.
    /// </summary>
    public static class GrandPrixResultMatcher
    {
        /// <summary>
        /// The result of each race of the calendar, in calendar order; null for a race not run yet.
        /// A result is paired by the race id it was recorded with. Results of older saves carry
        /// only the race's name: those are paired by name, falling back to calendar order.
        /// </summary>
        public static GrandPrixResult[] MatchToCalendar(ISaveGame saveGame)
        {
            var races = saveGame.CurrentSeason.Races.ToList();
            var results = (saveGame.GrandPrixResults ?? Enumerable.Empty<GrandPrixResult>())
                .Where(g => g.Year == saveGame.CurrentSeason.Year)
                .ToList();
            var matched = new GrandPrixResult[races.Count];
            var placed = new bool[results.Count];

            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].RaceId == null)
                    continue;

                var index = races.FindIndex(r => r.RaceId == results[i].RaceId);
                if (index >= 0 && matched[index] == null)
                {
                    matched[index] = results[i];
                    placed[i] = true;
                }
            }

            for (int i = 0; i < results.Count; i++)
            {
                if (placed[i])
                    continue;

                var index = Enumerable.Range(0, races.Count).FirstOrDefault(r => races[r].RaceName == results[i].GrandPrixName && matched[r] == null, -1);
                if (index < 0)
                    index = i < races.Count && matched[i] == null ? i : -1;
                if (index >= 0)
                    matched[index] = results[i];
            }

            return matched;
        }

        /// <summary>
        /// Same pairing as <see cref="MatchToCalendar"/>, by race: races not run yet are left out.
        /// </summary>
        public static Dictionary<Race, GrandPrixResult> MatchByRace(ISaveGame saveGame)
        {
            var races = saveGame.CurrentSeason.Races.ToList();
            var matched = MatchToCalendar(saveGame);
            var byRace = new Dictionary<Race, GrandPrixResult>();
            for (int i = 0; i < races.Count; i++)
            {
                if (matched[i] != null)
                    byRace[races[i]] = matched[i];
            }
            return byRace;
        }
    }
}
