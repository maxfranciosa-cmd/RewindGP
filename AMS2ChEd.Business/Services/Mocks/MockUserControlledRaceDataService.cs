using System;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.Business.Services.Mocks
{
    /// <summary>
    /// Mock implementation that allows manual control of race data
    /// </summary>
    public class MockUserControlledRaceDataService : IRaceDataService
    {
        private SessionData _currentSession;
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
        public bool IsRunning { get; private set; }
        public bool IsPreQualiSession { get; set; }

        public void InitializeRaceWeekend(IEnumerable<ParticipantData> participants)
        {
            _currentSession = new SessionData
            {
                SessionType = SessionType.Practice,
                IsSessionActive = false,
                IsSessionFinished = false,
                Standings = participants?.ToList()
            };
        }

        public void Start()
        {
            if (IsRunning)
                return;

            IsRunning = true;
            RaiseSessionUpdate();
        }

        public void Stop()
        {
            IsRunning = false;
        }

        /// <summary>
        /// Starts the current session
        /// </summary>
        public void StartSession()
        {
            lock (_lock)
            {
                _currentSession.IsSessionActive = true;
                _currentSession.IsSessionFinished = false;
            }
            RaiseSessionUpdate();
        }

        /// <summary>
        /// Finishes the current session and stores results
        /// </summary>
        public void FinishSession()
        {
            lock (_lock)
            {
                _currentSession.IsSessionActive = false;
                _currentSession.IsSessionFinished = true;

                // Store results based on session type
                var finalStandings = new List<ParticipantData>(_currentSession.Standings);

                if (_currentSession.SessionType == SessionType.Qualification)
                {
                    QualificationResults = finalStandings;
                    if (IsPreQualiSession)
                    {
                        PreQualiSessionFinished.Invoke(this, new SessionFinishedEventArgs
                        {
                            CompletedSession = _currentSession.SessionType,
                            FinalStandings = finalStandings
                        });
                    }
                }
                else if (_currentSession.SessionType == SessionType.Race)
                {
                    RaceResults = finalStandings;
                }

                // Raise session finished event
                SessionFinished?.Invoke(this, new SessionFinishedEventArgs
                {
                    CompletedSession = _currentSession.SessionType,
                    FinalStandings = finalStandings
                });
            }
            RaiseSessionUpdate();
        }

        /// <summary>
        /// Advances to the next session type
        /// </summary>
        public void AdvanceToNextSession()
        {
            lock (_lock)
            {
                var currentType = _currentSession.SessionType;
                var nextType = currentType switch
                {
                    SessionType.Practice => SessionType.Qualification,
                    SessionType.Qualification => SessionType.Race,
                    SessionType.Race => SessionType.Practice, // Loop back
                    _ => SessionType.Practice
                };

                _currentSession = new SessionData
                {
                    SessionType = nextType,
                    IsSessionActive = false,
                    IsSessionFinished = false,
                    Standings = new List<ParticipantData>(_currentSession.Standings)
                };
            }
            RaiseSessionUpdate();
        }

        public void MarkDriverAsDNF(int position)
        {
            lock (_lock)
            {
                var p1 = _currentSession.Standings.FirstOrDefault(p => p.Position == position);
                if (p1 != null)
                {
                    p1.DNF = true;
                }
            }
        }

        /// <summary>
        /// Swaps two participants' positions
        /// </summary>
        public void SwapPositions(int position1, int position2)
        {
            lock (_lock)
            {
                var p1 = _currentSession.Standings.FirstOrDefault(p => p.Position == position1);
                var p2 = _currentSession.Standings.FirstOrDefault(p => p.Position == position2);

                if (p1 != null && p2 != null)
                {
                    // Swap positions
                    p1.Position = position2;
                    p2.Position = position1;

                    // Re-sort the list
                    _currentSession.Standings = _currentSession.Standings
                        .OrderBy(p => p.Position)
                        .ToList();
                }
            }
            RaiseSessionUpdate();
        }

        /// <summary>
        /// Updates a participant's lap time
        /// </summary>
        public void UpdateLapTime(int position, string lapTime)
        {
            lock (_lock)
            {
                var participant = _currentSession.Standings.FirstOrDefault(p => p.Position == position);
                if (participant != null)
                {
                    participant.BestLapTime = lapTime;

                    // Update session best lap
                    foreach (var p in _currentSession.Standings)
                    {
                        p.IsSessionBestLap = false;
                    }

                    var fastest = _currentSession.Standings
                        .Where(p => !string.IsNullOrEmpty(p.BestLapTime) && p.BestLapTime != "--:--.---")
                        .OrderBy(p => p.BestLapTime)
                        .FirstOrDefault();

                    if (fastest != null)
                    {
                        fastest.IsSessionBestLap = true;
                    }
                }
            }
            RaiseSessionUpdate();
        }

        private void RaiseSessionUpdate()
        {
            SessionData sessionCopy;
            lock (_lock)
            {
                sessionCopy = new SessionData
                {
                    SessionType = _currentSession.SessionType,
                    IsSessionActive = _currentSession.IsSessionActive,
                    IsSessionFinished = _currentSession.IsSessionFinished,
                    Standings = new List<ParticipantData>(_currentSession.Standings)
                };
            }

            SessionUpdated?.Invoke(this, new SessionUpdateEventArgs { SessionData = sessionCopy });
        }
    }
}