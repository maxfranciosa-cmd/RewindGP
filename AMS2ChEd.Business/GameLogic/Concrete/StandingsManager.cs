using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Manages driver and constructor championship standings
    /// </summary>
    public class StandingsManager : IStandingsManager
    {
        public event EventHandler<StandingsUpdatedEventArgs> StandingsUpdated;

        /// <summary>
        /// Update standings after a Grand Prix
        /// </summary>
        public void UpdateStandings(ISaveGame saveGame, GrandPrixResult result)
        {
            var currentRace = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex);
            var pointsSystem = currentRace.PointsSystem?.ToDictionary(x => int.Parse(x.Key), x => Convert.ToDouble(x.Value)) ?? saveGame.CurrentSeason.PointsSystem.ToDictionary(x => int.Parse(x.Key), x => Convert.ToDouble(x.Value));
            var pointsForFastestLap = currentRace.PointsForFastestLap ?? saveGame.CurrentSeason.PointsForFastestLap ?? 0;

            // assign points
            AssignPoints(result, pointsSystem, pointsForFastestLap);

            // Update driver standings
            UpdateDriverStandings(saveGame, result, currentRace.IgnoreForPositionsTally);

            // Update constructor standings
            UpdateConstructorStandings(saveGame, result, currentRace.IgnoreForPositionsTally);

            StandingsUpdated?.Invoke(this, new StandingsUpdatedEventArgs
            {
                DriverStandings = saveGame.CurrentDriverStandings,
                ConstructorStandings = saveGame.CurrentConstructorStandings
            });
        }

        private void UpdateDriverStandings(ISaveGame saveGame, GrandPrixResult result, bool ignoreForPositionsTally)
        {
            var driverStandingsDictionary = saveGame.CurrentDriverStandings.ToDictionary(s => s.DriverId, s => s);
            foreach (var position in result.RaceResults)
            {   
                var driverStanding = driverStandingsDictionary.GetValueOrDefault(position.DriverId);

                if (driverStanding != null)
                {
                    driverStanding.Points += position.Points;
                    driverStanding.PositionsTally = driverStanding.PositionsTally ?? new PositionsTally();
                    if (!ignoreForPositionsTally && !position.DidNotPreQualify) driverStanding.PositionsTally.AddPosition(position.Position); 
                }
                else
                {
                    // Driver were never in the standings (because it's not in the official roster and doesn't have a default team)
                    var newDriverInStandings = new HistoricalDriverStandingEntry
                    {
                        DriverId = position.DriverId,
                        Points = position.Points,
                        PositionsTally = new PositionsTally(),
                        TeamId = null
                    };
                    if (!ignoreForPositionsTally && !position.DidNotPreQualify) newDriverInStandings.PositionsTally.AddPosition(position.Position);
                    saveGame.CurrentDriverStandings = saveGame.CurrentDriverStandings.Append(newDriverInStandings);
                }
                
            }

            // Re-sort standings
            saveGame.CurrentDriverStandings = saveGame.CurrentDriverStandings
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.PositionsTally)
                .Select((s, index) =>
                {
                    s.Position = index + 1;
                    return s;
                })
                .ToList();
        }

        private void UpdateConstructorStandings(ISaveGame saveGame, GrandPrixResult result, bool ignoreForPositionsTally)
        {
            // Group points by team
            var teamPoints = result.RaceResults
                .GroupBy(r => r.TeamId)
                .Select(g => new
                {
                    TeamId = g.Key,
                    Points = g.Sum(r => r.Points),
                    Positions = g.Where(r => !r.DidNotPreQualify).Select(r => r.Position).ToArray()
                });

            foreach (var teamResult in teamPoints)
            {
                var constructorStanding = saveGame.CurrentConstructorStandings
                    .FirstOrDefault(s => s.TeamId == teamResult.TeamId);

                if (constructorStanding != null)
                {
                    constructorStanding.Points += teamResult.Points;
                    constructorStanding.PositionsTally = constructorStanding.PositionsTally ?? new PositionsTally();
                    if (!ignoreForPositionsTally)
                    {
                        foreach (var position in teamResult.Positions)
                        {
                            constructorStanding.PositionsTally.AddPosition(position);
                        }
                    }
                }
            }

            // Re-sort standings
            saveGame.CurrentConstructorStandings = saveGame.CurrentConstructorStandings
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.PositionsTally)
                .Select((s, index) =>
                {
                    s.Position = index + 1;
                    return s;
                })
                .ToList();
        }

        /// <summary>
        /// Get driver standing display data
        /// </summary>
        public IEnumerable<DriverStandingDisplayData> GetDriverStandingsDisplay(ISaveGame saveGame)
        {
            return saveGame.CurrentDriverStandings
                .OrderBy(s => s.Position)
                .Select(s =>
                {
                    var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == s.DriverId);
                    return new DriverStandingDisplayData
                    {
                        Position = s.Position,
                        DriverId = s.DriverId,
                        DriverName = driver?.Name ?? "Unknown Driver",
                        TeamId = s.TeamId,
                        Points = s.Points,
                        IsPlayer = s.DriverId == saveGame.PlayerData.DriverId
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Get constructor standing display data
        /// </summary>
        public IEnumerable<ConstructorStandingDisplayData> GetConstructorStandingsDisplay(ISaveGame saveGame, Dictionary<string, string> teamNames)
        {
            return saveGame.CurrentConstructorStandings
                .OrderBy(s => s.Position)
                .Select(s =>
                {
                    teamNames.TryGetValue(s.TeamId, out string teamName);
                    return new ConstructorStandingDisplayData
                    {
                        Position = s.Position,
                        TeamId = s.TeamId,
                        TeamName = teamName ?? "Unknown Team",
                        Points = s.Points,
                        IsPlayerTeam = s.TeamId == saveGame.PlayerData.TeamId
                    };
                })
                .ToList();
        }

        private void AssignPoints(GrandPrixResult result, Dictionary<int, double> pointsSystem, double pointsForFastestLap)
        {
            foreach (var position in result.RaceResults)
            {
                if (pointsSystem.TryGetValue(position.Position, out double points))
                {
                    position.Points = points;
                }

                if (position.Position <= 10 && position.FastestLap)
                {
                    position.Points += pointsForFastestLap;
                }
            }
        }
    }
}