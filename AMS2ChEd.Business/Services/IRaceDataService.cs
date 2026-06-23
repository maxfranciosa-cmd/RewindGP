namespace AMS2ChEd.Business.Services
{
    /// <summary>
    /// Represents a participant in the race weekend
    /// </summary>
    public class ParticipantData
    {
        public int Position { get; set; }
        public int Number { get; set; }

        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public string BestLapTime { get; set; }
        public bool IsSessionBestLap { get; set; }
        public bool DNF { get; set; }
        public bool IsPlayer { get; set; }
        public bool DidNotPreQualify { get; set; }
    }

    /// <summary>
    /// Represents the current session state
    /// </summary>
    public enum SessionType
    {
        Practice,
        Qualification,
        Race
    }

    /// <summary>
    /// Session state information
    /// </summary>
    public class SessionData
    {
        public SessionType SessionType { get; set; }
        public bool IsSessionActive { get; set; }
        public bool IsSessionFinished { get; set; }
        public List<ParticipantData> Standings { get; set; } = new List<ParticipantData>();
    }

    /// <summary>
    /// Event arguments for session updates
    /// </summary>
    public class SessionUpdateEventArgs : EventArgs
    {
        public SessionData SessionData { get; set; }
    }

    /// <summary>
    /// Event arguments for session finished
    /// </summary>
    public class SessionFinishedEventArgs : EventArgs
    {
        public SessionType CompletedSession { get; set; }
        public List<ParticipantData> FinalStandings { get; set; }
    }

    /// <summary>
    /// Interface for race data service implementations
    /// </summary>
    public interface IRaceDataService
    {
        /// <summary>
        /// Raised when session data is updated
        /// </summary>
        event EventHandler<SessionUpdateEventArgs> SessionUpdated;

        /// <summary>
        /// Raised when a session is finished
        /// </summary>
        event EventHandler<SessionFinishedEventArgs> SessionFinished;

        event EventHandler<SessionFinishedEventArgs> PreQualiSessionFinished;

        /// <summary>
        /// provides a look-up so that we can identify participants and teams within the session.
        /// </summary>
        /// <param name="participants">the participants of the session</param>
        void InitializeRaceWeekend(IEnumerable<ParticipantData> participants);
        /// <summary>
        /// Gets the current session data
        /// </summary>
        SessionData CurrentSession { get; }

        bool IsPreQualiSession { get; set; }

        /// <summary>
        /// Gets the qualification results (null if not completed)
        /// </summary>
        List<ParticipantData> QualificationResults { get; }

        /// <summary>
        /// Gets the race results (null if not completed)
        /// </summary>
        List<ParticipantData> RaceResults { get; }

        /// <summary>
        /// Starts the data service
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the data service
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets whether the service is currently running
        /// </summary>
        bool IsRunning { get; }
    }
}
