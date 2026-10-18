using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    // one driver at one race, for the team being looked at
    public class HeadToHeadCell
    {
        // null when the driver didn't qualify (or didn't drive)
        public int? Grid { get; set; }
        public int? Finish { get; set; }
        public bool Retired { get; set; }
        public bool NotPrequalified { get; set; }

        // a contracted driver who didn't drive for the team at a race that was run
        public bool Absent { get; set; }

        // drove for the team at this race
        public bool Drove => !Absent && (Finish.HasValue || NotPrequalified);

        // the race counts towards the score, and this driver came out ahead of their teammate
        public bool AheadOfTeammate { get; set; }

        // the race counts towards the score (both contracted drivers drove and someone came out ahead)
        public bool Counted { get; set; }
    }

    public class HeadToHeadRace
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public bool IsRun { get; set; }

        // a contracted driver was replaced at this race
        public bool HasSubstitute { get; set; }
    }

    public class HeadToHeadDriver
    {
        public string DriverId { get; set; }
        public bool IsSubstitute { get; set; }

        // one per calendar race, in calendar order
        public List<HeadToHeadCell> Cells { get; set; } = new();

        // over the races both contracted drivers drove (only filled for them)
        public int QualifyingWins { get; set; }
        public int RaceWins { get; set; }
        public double? AverageGrid { get; set; }
        public double? AverageFinish { get; set; }
        public int? BestFinish { get; set; }
        public int Retirements { get; set; }
    }

    public class TeamHeadToHead
    {
        public string TeamId { get; set; }

        public List<HeadToHeadRace> Races { get; set; } = new();

        // the two contracted drivers first (one for a one-car team), then the substitutes
        public List<HeadToHeadDriver> Drivers { get; set; } = new();

        public bool IsOneCarTeam => Drivers.Count(d => !d.IsSubstitute) < 2;
        public int RacesRun => Races.Count(r => r.IsRun);
        public int RacesTogether { get; set; }
        public int RacesWithSubstitute => Races.Count(r => r.HasSubstitute);
    }

    /// <summary>
    /// Teammates compared over the current season. Who drove for a team at each race is read from
    /// the race results (they record each driver's team), so absences and substitutes show without
    /// needing the absence records; the contracted drivers are this season's contracts. Earlier
    /// seasons aren't covered: their contracts aren't kept.
    /// </summary>
    public static class HeadToHeadCalculator
    {
        public static TeamHeadToHead GetTeamHeadToHead(ISaveGame saveGame, string teamId)
        {
            var season = saveGame.CurrentSeason;
            var races = season.Races.ToList();
            var team = season.Teams.First(t => t.TeamId == teamId);
            var resultsByRace = MatchResultsToRaces(saveGame);

            var result = new TeamHeadToHead { TeamId = teamId };
            var contracted = new[] { team.Driver1Contract?.DriverId, team.Driver2Contract?.DriverId }
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => new HeadToHeadDriver { DriverId = id })
                .ToList();
            var substitutes = new Dictionary<string, HeadToHeadDriver>();

            for (int i = 0; i < races.Count; i++)
            {
                var race = races[i];
                var gp = resultsByRace[i];
                var column = new HeadToHeadRace { Name = race.RaceName, ShortName = race.RaceShortName, IsRun = gp != null };
                result.Races.Add(column);

                var teamResults = gp?.RaceResults?.Where(r => r.TeamId == teamId).ToList() ?? new List<SessionResult>();
                HeadToHeadCell CellFor(string driverId)
                {
                    var raceResult = teamResults.FirstOrDefault(r => r.DriverId == driverId);
                    if (raceResult == null)
                        return new HeadToHeadCell { Absent = column.IsRun && !substitutes.ContainsKey(driverId) };

                    var grid = gp.QualifyingResults?.FirstOrDefault(q => q.DriverId == driverId)?.Position;
                    return new HeadToHeadCell
                    {
                        Grid = raceResult.DidNotPreQualify ? null : grid,
                        Finish = raceResult.DidNotPreQualify ? null : raceResult.Position,
                        Retired = raceResult.DNF && !raceResult.DidNotPreQualify,
                        NotPrequalified = raceResult.DidNotPreQualify
                    };
                }

                foreach (var driver in contracted)
                {
                    driver.Cells.Add(CellFor(driver.DriverId));
                }

                foreach (var substituteResult in teamResults.Where(r => contracted.All(d => d.DriverId != r.DriverId)))
                {
                    if (!substitutes.TryGetValue(substituteResult.DriverId, out var substitute))
                    {
                        substitute = new HeadToHeadDriver
                        {
                            DriverId = substituteResult.DriverId,
                            IsSubstitute = true,
                            // nothing to show for the races before they first stood in
                            Cells = Enumerable.Range(0, i).Select(_ => new HeadToHeadCell()).ToList()
                        };
                        substitutes[substitute.DriverId] = substitute;
                    }
                }

                foreach (var substitute in substitutes.Values)
                {
                    substitute.Cells.Add(CellFor(substitute.DriverId));
                }

                column.HasSubstitute = column.IsRun && contracted.Any(d => d.Cells[i].Absent);
            }

            result.Drivers = contracted.Concat(substitutes.Values).ToList();
            if (contracted.Count == 2)
            {
                Compare(result, contracted[0], contracted[1]);
            }

            return result;
        }

        // GP results only carry the race's name: pair them with the calendar by name, falling back to
        // calendar order. Null for a race not run yet.
        private static GrandPrixResult[] MatchResultsToRaces(ISaveGame saveGame)
        {
            var races = saveGame.CurrentSeason.Races.ToList();
            var results = (saveGame.GrandPrixResults ?? Enumerable.Empty<GrandPrixResult>())
                .Where(g => g.Year == saveGame.CurrentSeason.Year)
                .ToList();
            var matched = new GrandPrixResult[races.Count];

            for (int i = 0; i < results.Count; i++)
            {
                var index = Enumerable.Range(0, races.Count).FirstOrDefault(r => races[r].RaceName == results[i].GrandPrixName && matched[r] == null, -1);
                if (index < 0)
                    index = i < races.Count && matched[i] == null ? i : -1;
                if (index >= 0)
                    matched[index] = results[i];
            }

            return matched;
        }

        private static void Compare(TeamHeadToHead result, HeadToHeadDriver first, HeadToHeadDriver second)
        {
            var together = Enumerable.Range(0, result.Races.Count)
                .Where(i => first.Cells[i].Drove && second.Cells[i].Drove)
                .ToList();
            result.RacesTogether = together.Count;

            foreach (var i in together)
            {
                var a = first.Cells[i];
                var b = second.Cells[i];

                // qualifying: a better grid slot wins; not qualifying loses
                var gridOrder = CompareNullable(a.Grid, b.Grid);
                if (gridOrder < 0) first.QualifyingWins++;
                else if (gridOrder > 0) second.QualifyingWins++;

                // race: a finish beats a retirement, which beats not prequalifying; two retirements
                // (or two non-prequalifications) don't count
                var raceOrder = RaceStanding(a) != RaceStanding(b)
                    ? RaceStanding(b).CompareTo(RaceStanding(a))
                    : RaceStanding(a) == 2 ? a.Finish.Value.CompareTo(b.Finish.Value) : 0;
                if (raceOrder == 0)
                    continue;

                var winner = raceOrder < 0 ? first : second;
                winner.RaceWins++;
                winner.Cells[i].AheadOfTeammate = true;
                a.Counted = b.Counted = true;
            }

            foreach (var driver in new[] { first, second })
            {
                var cells = together.Select(i => driver.Cells[i]).ToList();
                var grids = cells.Where(c => c.Grid.HasValue).Select(c => (double)c.Grid.Value).ToList();
                var finishes = cells.Where(c => RaceStanding(c) == 2).Select(c => c.Finish.Value).ToList();

                driver.AverageGrid = grids.Count > 0 ? grids.Average() : null;
                driver.AverageFinish = finishes.Count > 0 ? finishes.Average() : null;
                driver.BestFinish = finishes.Count > 0 ? finishes.Min() : null;
                driver.Retirements = cells.Count(c => c.Retired);
            }
        }

        // 2 = classified, 1 = retired, 0 = didn't prequalify
        private static int RaceStanding(HeadToHeadCell cell) => cell.NotPrequalified ? 0 : cell.Retired ? 1 : 2;

        // lower is better; a missing value loses
        private static int CompareNullable(int? a, int? b) =>
            a.HasValue && b.HasValue ? a.Value.CompareTo(b.Value) :
            a.HasValue ? -1 :
            b.HasValue ? 1 : 0;
    }
}
