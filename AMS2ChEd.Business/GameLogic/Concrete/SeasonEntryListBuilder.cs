using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// The entry list of one race of a season pack as it really was: every team's contracted
    /// drivers, with that race's declared absences applied (the substitute takes the absent
    /// driver's car and number). For tools that work on a pack without a save game in play, such
    /// as the developer season export; a career builds its entry lists through EntryListGenerator
    /// and AbsenceManager instead, where the player can step into an absence.
    /// </summary>
    public static class SeasonEntryListBuilder
    {
        public static List<EntryListEntry> ForRace(ISeason season, int raceId)
        {
            var entryList = season.Teams.Select(t => new EntryListEntry
            {
                TeamId = t.TeamId,
                Driver1Id = t.Driver1Contract?.DriverId,
                Driver1Number = t.Driver1Contract?.DriverNumber ?? 0,
                Driver2Id = t.Driver2Contract?.DriverId,
                Driver2Number = t.Driver2Contract?.DriverNumber ?? 0
            }).ToList();

            foreach (var absence in (season.Absences ?? Enumerable.Empty<Absence>()).Where(a => a.RaceId == raceId))
            {
                // a substitute who leaves his own team for this race leaves an absence there too
                for (var current = absence; current != null; current = current.ChainedAbsence)
                {
                    Apply(entryList, current);
                }
            }

            return entryList;
        }

        private static void Apply(List<EntryListEntry> entryList, Absence absence)
        {
            var entry = entryList.FirstOrDefault(e => e.TeamId == absence.TeamId);
            if (entry == null || string.IsNullOrEmpty(absence.DriverOut)) return;

            // an empty DriverIn means the car isn't raced at all: the generators skip an empty driver id
            if (entry.Driver1Id == absence.DriverOut)
                entry.Driver1Id = absence.DriverIn;
            else if (entry.Driver2Id == absence.DriverOut)
                entry.Driver2Id = absence.DriverIn;
        }
    }
}
