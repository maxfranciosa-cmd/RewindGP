namespace AMS2ChEd.Business.Services.CrashDetection
{
    public enum TelemetryGameState
    {
        Other,
        Playing,
        Paused,
        Replay,
        Restarting
    }

    public enum TelemetrySessionState
    {
        Other,
        FormationLap,
        Race
    }

    public enum TelemetryRaceState
    {
        Invalid,
        NotStarted,
        Racing,
        Finished,
        Disqualified,
        Retired,
        Dnf
    }

    public enum TelemetryPitState
    {
        None,
        DrivingIntoPits,
        InPit,
        DrivingOutOfPits,
        InGarage,
        DrivingOutOfGarage
    }

    // the flag a car is being shown, for the area it's in: never the car that caused it
    public enum TelemetryFlagReason
    {
        None,
        SoloCrash,
        VehicleCrash,
        Obstruction
    }

    public enum TelemetryCrashState
    {
        None,
        Offtrack,
        LargeProp,
        Spinning,
        Rolling
    }

    /// <summary>
    /// One read of the game's telemetry: the session, and every car on track at that moment. Game
    /// agnostic: a game module maps its own data onto it (Ams2TelemetryFrameReader), the telemetry
    /// recorder writes it to CSV (TelemetryCsv) and the crash detector reads it.
    /// </summary>
    public class TelemetryFrame
    {
        // seconds on a steady clock, from wherever the reader started counting
        public double Time { get; set; }

        public TelemetryGameState GameState { get; set; }
        public TelemetrySessionState SessionState { get; set; }

        // the car the camera follows, -1 when none
        public int ViewedCarIndex { get; set; } = -1;

        // the recorder's marker number on this frame, 0 for none
        public int Marker { get; set; }

        public List<TelemetryCarSample> Cars { get; set; } = new();
    }

    /// <summary>
    /// One car in a frame. The fields after Flag are only known for the viewed car (normally the
    /// player's): null for the others.
    /// </summary>
    public class TelemetryCarSample
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public TelemetryRaceState RaceState { get; set; }
        public TelemetryPitState PitState { get; set; }

        // metres per second
        public double Speed { get; set; }

        // degrees, in the order the game gives them; the yaw is the heading
        public double Pitch { get; set; }
        public double Yaw { get; set; }
        public double Roll { get; set; }

        // metres
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double LapDistance { get; set; }
        public int CurrentLap { get; set; }

        public bool YellowFlag { get; set; }
        public TelemetryFlagReason FlagReason { get; set; }

        // viewed car only: the car's own acceleration, in g
        public double? AccelerationG { get; set; }
        public TelemetryCrashState? CrashState { get; set; }

        // a tyre touching a tyre wall, a concrete wall or a guard rail
        public bool? WallContact { get; set; }
        public int? WheelsDetached { get; set; }

        // 0..1
        public double? AeroDamage { get; set; }
        public double? EngineDamage { get; set; }
        public double? SuspensionDamage { get; set; }
    }
}
