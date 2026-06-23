using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Storage.Contracts;

namespace AMS2ChEd.Business.Services
{
    /// <summary>
    /// Simulates race weekend results based on driver and team reputation
    /// </summary>
    public class SimulatedRaceDataService : IRaceDataService
    {
        private SessionData _currentSession;
        private List<ParticipantData> _participants;
        private readonly ISaveGame _saveGame;
        private readonly Random _random;

        public event EventHandler<SessionUpdateEventArgs> SessionUpdated;
        public event EventHandler<SessionFinishedEventArgs> SessionFinished;
        public event EventHandler<SessionFinishedEventArgs> PreQualiSessionFinished;

        public SessionData CurrentSession => _currentSession;
        public List<ParticipantData> QualificationResults { get; private set; }
        public List<ParticipantData> RaceResults { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsPreQualiSession { get; set; }

        public SimulatedRaceDataService(ISaveGame saveGame)
        {
            _saveGame = saveGame;
            _random = new Random();
            _currentSession = new SessionData
            {
                SessionType = SessionType.Practice,
                IsSessionActive = false,
                IsSessionFinished = false
            };
        }

        public void InitializeRaceWeekend(IEnumerable<ParticipantData> participants)
        {
            _participants = participants.ToList();
        }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public List<ParticipantData> SimulateQualifyingOnly()
        {
            if (_participants == null || !_participants.Any())
                throw new InvalidOperationException("Must call InitializeRaceWeekend first");

            QualificationResults = SimulateQualifying();

            _currentSession = new SessionData
            {
                SessionType = SessionType.Qualification,
                IsSessionActive = false,
                IsSessionFinished = true,
                Standings = QualificationResults
            };

            SessionFinished?.Invoke(this, new SessionFinishedEventArgs
            {
                CompletedSession = SessionType.Qualification,
                FinalStandings = QualificationResults
            });

            return QualificationResults;
        }

        public void SimulateRaceWeekend()
        {
            SimulateQualifyingOnly();
            Thread.Sleep(100);
            SimulateRaceOnly(QualificationResults);
        }

        private void SimulateRaceOnly(List<ParticipantData> qualificationResults)
        {
            RaceResults = SimulateRace(QualificationResults);
            _currentSession = new SessionData
            {
                SessionType = SessionType.Race,
                IsSessionActive = false,
                IsSessionFinished = true,
                Standings = RaceResults
            };
            SessionUpdated?.Invoke(this, new SessionUpdateEventArgs { SessionData = _currentSession });
            SessionFinished?.Invoke(this, new SessionFinishedEventArgs
            {
                CompletedSession = SessionType.Race,
                FinalStandings = RaceResults
            });
        }

        private List<ParticipantData> SimulateQualifying()
        {
            var results = new List<ParticipantData>();

            foreach (var participant in _participants)
            {
                var performance = CalculatePerformance(participant);

                results.Add(new ParticipantData
                {
                    DriverId = participant.DriverId,
                    DriverName = participant.DriverName.ToUpper(),
                    TeamId = participant.TeamId,
                    TeamName = participant.TeamName.ToUpper(),
                    Number = participant.Number,
                    Position = 0, // Will be set after sorting
                    BestLapTime = GenerateLapTime(performance),
                    IsSessionBestLap = false,
                    DNF = false
                });
            }

            // Sort by lap time and assign positions
            results = results.OrderBy(r => ParseLapTime(r.BestLapTime)).ToList();
            for (int i = 0; i < results.Count; i++)
            {
                results[i].Position = i + 1;
            }

            // Mark fastest lap
            if (results.Any())
            {
                results[0].IsSessionBestLap = true;
            }

            return results;
        }

        private List<ParticipantData> SimulateRace(List<ParticipantData> qualifyingResults)
        {
            var results = new List<ParticipantData>();

            foreach (var qualifier in qualifyingResults)
            {
                var performance = CalculatePerformance(qualifier);

                // Add randomness for race incidents, strategy, overtakes, etc.
                var racePerformance = performance + (_random.NextDouble() * 24 - 12); // +/- 12 points

                // Determine DNF (higher reputation = lower DNF chance)
                var dnfChance = CalculateDNFChance(performance);
                var isDnf = _random.NextDouble() < dnfChance;

                results.Add(new ParticipantData
                {
                    DriverId = qualifier.DriverId,
                    DriverName = qualifier.DriverName,
                    TeamId = qualifier.TeamId,
                    TeamName = qualifier.TeamName,
                    Number = qualifier.Number,
                    Position = 0, // Will be set after sorting
                    BestLapTime = qualifier.BestLapTime,
                    IsSessionBestLap = qualifier.IsSessionBestLap,
                    DNF = isDnf
                });
            }

            // Sort: DNF goes to back, then by performance with race variance
            results = results
                .OrderBy(r => r.DNF)
                .ThenByDescending(r => CalculatePerformance(r) + (_random.NextDouble() * 20 - 10)) // ±10 for balance
                .ToList();

            // Assign positions
            for (int i = 0; i < results.Count; i++)
            {
                results[i].Position = i + 1;
            }

            return results;
        }

        private double CalculatePerformance(ParticipantData participant)
        {
            // Get driver reputation
            var driver = _saveGame.Drivers.FirstOrDefault(d => d.DriverId == participant.DriverId);
            var driverScore = 50.0; // default

            if (driver != null)
            {
                driverScore = GetDriverReputationScore(driver.Reputation);
            }

            // Get team reputation
            var team = _saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == participant.TeamId);
            var teamScore = team != null ? GetTeamReputationScore(team.Reputation) : 50.0;

            // Weighted combination: 50% driver skill, 50% car performance
            // This ensures elite drivers (Schumacher-level) are competitive even in weaker cars
            return (driverScore * 0.50) + (teamScore * 0.50);
        }

        private double GetDriverReputationScore(DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD => 35,
                DriverReputation.PAY_DRIVER_SEASON => 45,
                DriverReputation.AGEING_MIDFIELD => 60,
                DriverReputation.YOUNG_TALENT => 65,
                DriverReputation.PRIME_MIDFIELD => 70,
                DriverReputation.AGEING_STRONG_MIDFIELD => 75,
                DriverReputation.JUST_ONE_LAST_DANCE => 77,
                DriverReputation.PRIME_STRONG_MIDFIELD => 80,
                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED => 78,
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED => 82,
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN => 85,
                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN => 87,
                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL => 88,
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL => 93,
                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL => 95,
                _ => 50
            };
        }

        private double GetTeamReputationScore(TeamReputation reputation)
        {
            return reputation switch
            {
                TeamReputation.SUPER_MINNOW => 40,
                TeamReputation.MINNOW => 55,
                TeamReputation.MIDFIELD => 70,
                TeamReputation.MIDFIELD_HIGH => 82,
                TeamReputation.TOP_TEAM => 95,
                _ => 50
            };
        }

        private double CalculateDNFChance(double performance)
        {
            // Better performance = lower DNF chance
            // Range: 3% (best) to 18% (worst)
            var baseChance = 0.18 - (performance / 100.0 * 0.15);
            return Math.Max(0.03, Math.Min(0.18, baseChance));
        }

        private string GenerateLapTime(double performance)
        {
            // Base time: 90 seconds (1:30.000)
            var baseTime = 90.0;

            // Better performance = faster lap
            // Performance range: ~35-95, reduce time by up to 8 seconds
            var performanceBonus = ((performance - 35) / 60.0) * 8.0;

            // Add randomness: +/- 1.2 seconds (qualifying variance - increased from 0.3)
            // This allows midfield teams to occasionally outqualify top teams
            var randomness = (_random.NextDouble() * 2.4) - 1.2;

            var totalSeconds = baseTime - performanceBonus + randomness;
            var ts = TimeSpan.FromSeconds(totalSeconds);

            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }

        private double ParseLapTime(string lapTime)
        {
            if (lapTime == "--:--.---") return double.MaxValue;

            var parts = lapTime.Split(':');
            if (parts.Length != 2) return double.MaxValue;

            var minutes = int.Parse(parts[0]);
            var secondsParts = parts[1].Split('.');
            var seconds = int.Parse(secondsParts[0]);
            var milliseconds = int.Parse(secondsParts[1]);

            return (minutes * 60.0) + seconds + (milliseconds / 1000.0);
        }
    }
}