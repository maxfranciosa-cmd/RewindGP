using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.CrashDetection;
using AMS2ChEd.Business.Settings;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    // what a race's crash did to the driver, for the race report
    public class CrashNewsItem
    {
        public string DriverId { get; set; }
        public string TeamId { get; set; }
        public bool IsPlayer { get; set; }
        public CrashSeverity Severity { get; set; }
        public CrashOutcomeKind Outcome { get; set; }

        // carried on after the crash: the injury was found afterwards
        public bool DroveOn { get; set; }

        // Injured: the races they miss (0 when the crash was in the season's last race)
        public int RacesOut { get; set; }

        // who takes their seat at the next race, null when they hold none or miss nothing
        public string StandInDriverId { get; set; }
    }

    /// <summary>
    /// Applies a race's crashes to the career (crash-incidents-design.md, section 2b), once the race
    /// is recorded. Each crash's outcome is rolled (CrashOutcomeRoller); a driver hurt or killed is
    /// added to the save's Casualties, their seat goes to a stand-in for every race they miss (an
    /// INJURY or DECEASED absence; for a death, every race left), and any race they were to stand
    /// in for elsewhere gets another stand-in. Stand-ins are picked as for any vacancy: the team's
    /// reserve first (SubstitutePool.PickStandIn); the player applies for these seats as usual.
    /// A crash the driver drove on from rolls lower odds and can't kill (CrashOutcomeRoller).
    /// </summary>
    public static class CrashIncidentManager
    {
        public static List<CrashNewsItem> ApplyRace(ISaveGame saveGame, int raceIndex, IEnumerable<RaceCrash> crashes, CrashOptions options, Random random)
        {
            var news = new List<CrashNewsItem>();
            var races = saveGame.CurrentSeason.Races.ToList();
            if (raceIndex < 0 || raceIndex >= races.Count || crashes == null)
                return news;

            var raceId = races[raceIndex].RaceId;
            var playerId = saveGame.PlayerData?.DriverId;
            var seen = new HashSet<string>();

            foreach (var crash in crashes)
            {
                if (string.IsNullOrEmpty(crash?.DriverId) || !seen.Add(crash.DriverId) || saveGame.IsDeceased(crash.DriverId))
                    continue;

                var isPlayer = crash.DriverId == playerId;
                var racesLeft = races.Count - raceIndex - 1;
                var outcome = CrashOutcomeRoller.Roll(crash.Severity, saveGame.CurrentSeason.Year, isPlayer, options, racesLeft, random, crash.DroveOn);

                var item = new CrashNewsItem
                {
                    DriverId = crash.DriverId,
                    TeamId = crash.TeamId,
                    IsPlayer = isPlayer,
                    Severity = crash.Severity,
                    Outcome = outcome.Kind,
                    DroveOn = crash.DroveOn,
                    RacesOut = outcome.RacesOut
                };

                if (outcome.Kind == CrashOutcomeKind.Unhurt)
                {
                    // walking away from a heavy one is still news; carrying on unhurt isn't
                    if (crash.Severity == CrashSeverity.Severe && !crash.DroveOn)
                        news.Add(item);
                    continue;
                }

                var fatal = outcome.Kind == CrashOutcomeKind.Fatal;
                var missedRaceIds = races.Skip(raceIndex + 1).Take(fatal ? racesLeft : outcome.RacesOut).Select(r => r.RaceId).ToList();

                saveGame.Casualties ??= new List<Casualty>();
                saveGame.Casualties.Add(new Casualty
                {
                    DriverId = crash.DriverId,
                    TeamId = crash.TeamId,
                    Year = saveGame.CurrentSeason.Year,
                    RaceId = raceId,
                    Kind = fatal ? CasualtyKind.DECEASED : CasualtyKind.INJURED,
                    RaceIdsOut = missedRaceIds
                });

                item.StandInDriverId = CoverMissedRaces(saveGame, crash.DriverId, missedRaceIds, fatal ? AbsenceReason.DECEASED : AbsenceReason.INJURY, raceId);
                news.Add(item);
            }

            return news;
        }

        // the driver's seat for each race they miss, and the seats they were to stand in for; the
        // stand-in at the driver's own seat, null when they hold none
        private static string CoverMissedRaces(ISaveGame saveGame, string driverId, List<int> missedRaceIds, AbsenceReason reason, int incidentRaceId)
        {
            if (missedRaceIds.Count == 0)
                return null;

            var absences = (saveGame.CurrentSeason.Absences ?? Enumerable.Empty<Absence>()).ToList();
            var teams = saveGame.CurrentSeason.Teams.ToList();
            var ownTeam = teams.FirstOrDefault(t => t.Driver1Contract?.DriverId == driverId || t.Driver2Contract?.DriverId == driverId);

            // one stand-in for the whole spell, as for a release
            var standIn = ownTeam == null ? null : SubstitutePool.PickStandIn(saveGame, ownTeam, missedRaceIds, absences, driverId);

            foreach (var raceId in missedRaceIds)
            {
                var raceAbsences = absences.Where(a => a.RaceId == raceId).SelectMany(Chain).ToList();

                // already out of that race for another reason (a season pack's absence, a paid ride,
                // a release): the stand-in stays, but the crash is now why, so the news says so. A
                // paid ride on that seat then no longer counts against its buyer's budget
                var alreadyOut = raceAbsences.Where(a => a.DriverOut == driverId).ToList();
                foreach (var absence in alreadyOut)
                {
                    absence.Reason = reason;
                    absence.IncidentRaceId = incidentRaceId;
                }

                if (ownTeam != null && alreadyOut.Count == 0)
                {
                    absences.Add(new Absence
                    {
                        RaceId = raceId,
                        TeamId = ownTeam.TeamId,
                        DriverOut = driverId,
                        DriverIn = standIn,
                        Reason = reason,
                        IncidentRaceId = incidentRaceId
                    });
                }

                foreach (var standingIn in raceAbsences.Where(a => a.DriverIn == driverId))
                {
                    var team = teams.FirstOrDefault(t => t.TeamId == standingIn.TeamId);
                    standingIn.DriverIn = team == null ? "" : SubstitutePool.PickStandIn(saveGame, team, new[] { raceId }, absences, driverId);
                }
            }

            saveGame.CurrentSeason.Absences = absences;
            return standIn;
        }

        private static IEnumerable<Absence> Chain(Absence absence)
        {
            for (var current = absence; current != null; current = current.ChainedAbsence)
                yield return current;
        }
    }
}
