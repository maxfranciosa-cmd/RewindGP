using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Settings.Storage.Contracts;
using AMS2ChEd.Business.DependencyInjection;
using AMS2ChEd.Business.Services;
using AMS2SharedMemoryNet;
using AMS2SharedMemoryNet.Enums;
using AMS2SharedMemoryNet.Structs;
using System.Text;

namespace Ams2ChEd.Business.AMS2.Services
{
    /// <summary>
    /// Real implementation that polls AMS2 shared memory
    /// </summary>
    public class Ams2RaceDataService : IRaceDataService
    {
        private CancellationTokenSource _cts;
        private MemoryParser _parser;
        private SessionData _currentSession;
        private Dictionary<string, string> _driverNameDriverIdLookup;
        private Dictionary<string, string> _driverNameTeamIdlookup;
        private Dictionary<string, string> _driverNameTeamNamelookup;
        private Dictionary<string, int> _driverNameDriverNumber;
        private bool _sessionHasStarted = false;
        private AMS2Page _previousPage;

        // Player's career driver information
        private string _playerInGameNameInLowerCase;
        private string _playerDriverName;
        private string _playerDriverId;

        private Task _pollingTask;
        private readonly object _lock = new object();

        public event EventHandler<SessionUpdateEventArgs> SessionUpdated;
        public event EventHandler<SessionFinishedEventArgs> SessionFinished;
        public event EventHandler<SessionFinishedEventArgs> PreQualiSessionFinished;

        public SessionData CurrentSession
        {
            get
            {
                lock (_lock)
                {
                    return _currentSession;
                }
            }
        }

        public List<ParticipantData> QualificationResults { get; private set; }
        public List<ParticipantData> RaceResults { get; private set; }
        public IAms2AppSettingsStorage SettingsStorage { get; private set;  }
        public bool IsRunning { get; private set; }

        public bool IsPreQualiSession { get; set; }

        private SessionType? _previousSessionType;
        private bool _sessionFinishedTriggered;

        public Ams2RaceDataService(Ams2StorageFactory storageFactory)
        {
            SettingsStorage = storageFactory.Ams2AppSettingsStorage;
            _playerInGameNameInLowerCase = SettingsStorage.LoadSettings().Ams2InGameName.ToLower();
            _currentSession = new SessionData
            {
                SessionType = SessionType.Practice,
                IsSessionActive = false,
                IsSessionFinished = false
            };
        }

        public void Start()
        {
            if (IsRunning)
                return;

            try
            {
                _parser = new MemoryParser("$pcars2$");
                _cts = new CancellationTokenSource();
                IsRunning = true;
                _pollingTask = Task.Run(() => PollLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                IsRunning = false;
                throw new InvalidOperationException("Failed to connect to AMS2 shared memory", ex);
            }
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            _cts?.Cancel();
            IsRunning = false;
            _pollingTask?.Wait(TimeSpan.FromSeconds(2));
        }

        private void PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var page = _parser.GetPage();
                    ProcessPage(page);
                    Thread.Sleep(500); // Poll every 500ms
                }
                catch (Exception ex)
                {
                    // Log error but continue polling
                    System.Diagnostics.Debug.WriteLine($"Polling error: {ex.Message}");
                }
            }
        }

        private void ProcessPage(AMS2Page page)
        {
            SessionType sessionType;
            bool isFinished;
            List<ParticipantData> standings;

            lock (_lock)
            {
                sessionType = MapSessionType(page.mSessionState);

                // Detect session change
                if (_previousSessionType.HasValue && _previousSessionType != sessionType)
                {
                    _sessionFinishedTriggered = false;
                    _sessionHasStarted = false;
                } 
                else if (!_sessionHasStarted && page.mGameState == 2)
                {
                    _sessionHasStarted = true;
                    // Capture player index at session start
                }
 
                isFinished = IsSessionFinished(page);
                standings = GetStandings(page);

                _currentSession = new SessionData
                {
                    SessionType = sessionType,
                    IsSessionActive = page.mGameState == 2, // In game
                    IsSessionFinished = isFinished,
                    Standings = standings
                };

                _previousSessionType = sessionType;
            }

            // Raise events
            SessionUpdated?.Invoke(this, new SessionUpdateEventArgs { SessionData = _currentSession });

            // Trigger session finished event
            if (isFinished && !_sessionFinishedTriggered && _sessionHasStarted)
            {
                _sessionFinishedTriggered = true;
                
                OnSessionFinished(sessionType, standings);
            } 
        }

        private void OnSessionFinished(SessionType sessionType, List<ParticipantData> standings)
        {
            if (IsPreQualiSession)
            {
                if (sessionType == SessionType.Qualification)
                {
                    QualificationResults = new List<ParticipantData>(standings);
                    PreQualiSessionFinished?.Invoke(this, new SessionFinishedEventArgs
                    {
                        CompletedSession = sessionType,
                        FinalStandings = standings
                    });
                }
                return; // do not flow into championship handling
            }

            // Store results
            if (sessionType == SessionType.Qualification)
            {
                QualificationResults = new List<ParticipantData>(standings);
            }
            else if (sessionType == SessionType.Race)
            {
                RaceResults = new List<ParticipantData>(standings);
            }

            SessionFinished?.Invoke(this, new SessionFinishedEventArgs
            {
                CompletedSession = sessionType,
                FinalStandings = standings
            });
        }

        private SessionType MapSessionType(uint sessionState)
        {
            // AMS2 SessionState enum values:
            // 0 = Invalid
            // 1 = Practice
            // 2 = Test
            // 3 = Qualify
            // 4 = Formation Lap
            // 5 = Race
            // 6 = Time Trial

            // We need to track which practice session we're in separately
            // or use additional game state information
            // For now, map the available states:

            return sessionState switch
            {
                1 => SessionType.Practice, // Practice
                2 => SessionType.Practice, // Test - treat as practice
                3 => SessionType.Qualification, // Qualify
                4 => SessionType.Race, // Formation Lap - treat as race starting
                5 => SessionType.Race, // Race
                6 => SessionType.Practice, // Time Trial - treat as Practice
                _ => SessionType.Practice
            };
        }

        private bool IsSessionFinished(AMS2Page page)
        {
            // Check race state (3 = finished)
            if (page.mRaceState == 3)
                return true;

            // Check if all participants are finished or retired
            bool anyActive = false;
            foreach (var raceState in page.mRaceStates)
            {
                if (raceState > 0 && raceState < 3) // Racing or other active state
                {
                    anyActive = true;
                    break;
                }
            }

            return !anyActive && page.mRaceState >= 2;
        }

        private List<ParticipantData> GetStandings(AMS2Page page)
        {
            var participants = new List<ParticipantData>();

            // Validate that we have lap time data
            bool hasLapTimes = page.mFastestLapTimes != null &&
                               page.mFastestLapTimes.Length >= page.mParticipantInfo.Length;

            // Find fastest lap for highlighting (only if we have lap time data)
            float fastestLap = float.MaxValue;
            if (hasLapTimes)
            {
                foreach (var lapTime in page.mFastestLapTimes)
                {
                    if (lapTime > 0 && lapTime < fastestLap)
                    {
                        fastestLap = lapTime;
                    }
                }
            }

            for (int i = 0; i < page.mParticipantInfo.Length; i++)
            {
                var p = page.mParticipantInfo[i];

                if (p.mRacePosition == 0)
                    continue;

                var decodedDriverName = DecodeAms2String(p.mName);
                if (decodedDriverName.ToLower() == "safety car")
                {
                    continue;
                }

                // Determine if this is the player
                bool isPlayer = decodedDriverName.ToLower() == _playerInGameNameInLowerCase;

                // For the player, use their career driver data; for AI, use name-based lookup
                string driverName;
                string driverId;
                string teamId;
                string teamName;
                int number;

                if (isPlayer)
                {
                    // Player: Use stored career driver information (regardless of in-game name)
                    driverName = _playerDriverName;
                    driverId = _playerDriverId;
                }
                else
                {
                    // AI drivers: Use name-based lookup
                    driverName = decodedDriverName;
                    driverId = _driverNameDriverIdLookup.GetValueOrDefault(decodedDriverName) ?? "driver_id";
                }

                teamId = _driverNameTeamIdlookup.GetValueOrDefault(driverName) ?? "team_id";
                teamName = _driverNameTeamNamelookup.GetValueOrDefault(driverName) ?? "TEAM";
                number = _driverNameDriverNumber.GetValueOrDefault(driverName);

                // Get lap time from page-level array (if available)
                // The arrays should be parallel: mFastestLapTimes[i] = lap time for mParticipantInfo[i]
                float lapTime = 0;
                if (hasLapTimes && i < page.mFastestLapTimes.Length)
                {
                    lapTime = page.mFastestLapTimes[i];
                }

                var raceState = page.mRaceStates[i];

                participants.Add(new ParticipantData
                {
                    Position = (int)p.mRacePosition,
                    DNF = raceState == (int)RaceState.RACESTATE_DNF || raceState == (int)RaceState.RACESTATE_RETIRED || raceState == (int)RaceState.RACESTATE_DISQUALIFIED,
                    Number = number,
                    DriverId = driverId,
                    DriverName = driverName,
                    TeamId = teamId,
                    TeamName = teamName,
                    BestLapTime = FormatLapTime(lapTime),
                    IsSessionBestLap = hasLapTimes && Math.Abs(lapTime - fastestLap) < 0.001f && lapTime > 0,
                    IsPlayer = isPlayer
                });
            }

            return participants.OrderBy(p => p.Position).ToList();
        }

        private string FormatLapTime(float seconds)
        {
            if (seconds <= 0)
                return "--:--.---";

            var ts = TimeSpan.FromSeconds(seconds);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }

        private string DecodeAms2String(char[] raw)
        {
            if (raw == null || raw.Length == 0)
                return string.Empty;

            byte[] bytes = raw.Select(c => (byte)c).ToArray();
            int end = Array.IndexOf(bytes, (byte)0);
            if (end < 0) end = bytes.Length;

            return Encoding.UTF8.GetString(bytes, 0, end);
        }

        public void InitializeRaceWeekend(IEnumerable<ParticipantData> participants)
        {
            _driverNameDriverIdLookup = participants.ToDictionary(p => p.DriverName, p => p.DriverId);
            _driverNameTeamIdlookup = participants.ToDictionary(p => p.DriverName, p => p.TeamId);
            _driverNameTeamNamelookup = participants.ToDictionary(p => p.DriverName, p => p.TeamName);
            _driverNameDriverNumber = participants.ToDictionary(p => p.DriverName, p => p.Number);
            
            // Reset session state for the new race weekend.
            _sessionFinishedTriggered = false;
            _sessionHasStarted = false;
            _previousSessionType = null;

            // Store player's career driver information
            var playerData = participants.FirstOrDefault(p => p.IsPlayer);
            if (playerData != null)
            {
                _playerDriverName = playerData.DriverName;
                _playerDriverId = playerData.DriverId;
            }
        }
    }
}