namespace AMS2ChEd.Business.Services.CrashDetection
{
    /// <summary>
    /// Finds serious crashes in a race's telemetry, one frame at a time (crash-incidents-design.md,
    /// section 4). Pure: frames in, incidents out, so recorded sessions can replay through it.
    ///
    /// A sample makes a candidate: a speed drop between two samples steeper than the trigger, or the
    /// car pitched or rolled past the upset angle (for the viewed car also its own g-force, or the
    /// game's rolling state). The candidate is a crash if the car was left stranded: it stopped soon
    /// after and then retired, or was still where it stopped when the confirmation window ended (or
    /// the race did). A car that drove on, or into the pits, is reported only for a Severe crash, as
    /// one it drove on from (it may still have hurt the driver). Only race sessions count, and only
    /// while the game is being played: a pause or a replay measures nothing, and a new start (a
    /// restart, a formation lap, the cars back on the grid) forgets the race so far. A car is
    /// reported once at most: a crash that strands it replaces one it drove on from earlier.
    /// </summary>
    public class CrashDetector
    {
        private const double StandardGravity = 9.80665;

        private class Candidate
        {
            public int CarIndex { get; set; }
            public double StartClock { get; set; }
            public double StartTime { get; set; }
            public int Lap { get; set; }
            public double LapDistance { get; set; }
            public double ImpactSpeed { get; set; }
            public double PeakDecelerationG { get; set; }
            public double MaxUpsetAngle { get; set; }
            public bool HasStopped { get; set; }
            public double StoppedTime { get; set; }
            public double DistanceSinceStop { get; set; }
            public bool Retired { get; set; }
            public bool WasViewedCar { get; set; }
            public double? PeakAccelerationG { get; set; }
            public bool Rolled { get; set; }
            public bool WallContact { get; set; }
            public int WheelsDetached { get; set; }
        }

        private enum Verdict
        {
            Open,
            Stranded,
            DroveOn,
            // gone somewhere else, with no telling why
            Unknown
        }

        private readonly CrashDetectionThresholds _thresholds;
        private readonly Dictionary<string, (TelemetryCarSample Sample, double Time)> _lastSamples = new();
        private readonly Dictionary<string, Candidate> _candidates = new();
        private readonly HashSet<string> _reported = new();
        private readonly Dictionary<string, CrashIncident> _droveOn = new();
        private readonly List<CrashIncident> _incidents = new();

        // seconds of play: stands still while paused, so the windows don't run out behind a menu
        private double _clock;
        private double? _lastPlayingTime;
        private double _settledAt;

        public CrashDetector(CrashDetectionThresholds thresholds = null)
        {
            _thresholds = thresholds ?? new CrashDetectionThresholds();
        }

        public IReadOnlyList<CrashIncident> Incidents => _incidents;

        // a new session: nothing carries over
        public void Reset()
        {
            _lastSamples.Clear();
            _candidates.Clear();
            _reported.Clear();
            _droveOn.Clear();
            _incidents.Clear();
            _clock = 0;
            _lastPlayingTime = null;
            _settledAt = 0;
        }

        /// <summary>
        /// The race is over: the open windows end as they stand, a car still stranded where it
        /// stopped is a crash. Returns the incidents this confirmed.
        /// </summary>
        public IReadOnlyList<CrashIncident> Flush()
        {
            var confirmed = new List<CrashIncident>();
            foreach (var name in _candidates.Keys.ToList())
                Close(name, IsStranded(_candidates[name]) ? Verdict.Stranded : Verdict.DroveOn, confirmed);
            return confirmed;
        }

        /// <summary>The incidents confirmed by this frame.</summary>
        public IReadOnlyList<CrashIncident> Process(TelemetryFrame frame)
        {
            var confirmed = new List<CrashIncident>();
            if (frame == null)
                return confirmed;

            // a new start forgets the race so far: the restart itself, a formation lap, or every car
            // back on the grid (the restart state can be too brief to be sampled)
            if (frame.GameState == TelemetryGameState.Restarting ||
                frame.SessionState == TelemetrySessionState.FormationLap ||
                (frame.SessionState == TelemetrySessionState.Race && frame.Cars.Count > 0 && frame.Cars.All(c => c.RaceState == TelemetryRaceState.NotStarted)))
            {
                Reset();
                return confirmed;
            }

            // paused, in a replay, out of the race: no continuity across it, and the cars need a
            // moment to settle once it's over
            if (frame.GameState != TelemetryGameState.Playing || frame.SessionState != TelemetrySessionState.Race)
            {
                _lastSamples.Clear();
                _lastPlayingTime = null;
                _settledAt = _clock + _thresholds.ResumeSettleTime;
                if (frame.SessionState != TelemetrySessionState.Race)
                    confirmed.AddRange(Flush());
                return confirmed;
            }

            if (_lastPlayingTime != null)
            {
                var step = frame.Time - _lastPlayingTime.Value;
                if (step > 0)
                    _clock += Math.Min(step, _thresholds.MaxSampleGap);
            }
            _lastPlayingTime = frame.Time;
            var settling = _clock < _settledAt;

            var carsInFrame = new HashSet<string>();
            foreach (var car in frame.Cars)
            {
                if (string.IsNullOrEmpty(car.Name) || !carsInFrame.Add(car.Name))
                    continue;

                var hasPrevious = _lastSamples.TryGetValue(car.Name, out var last);
                var previous = hasPrevious ? last.Sample : null;

                // a moving car that hasn't moved: the game hasn't updated it since the last sample
                if (previous != null && IsRepeat(previous, car))
                    continue;
                _lastSamples[car.Name] = (car, frame.Time);

                double moved = 0, dt = 0;
                var teleported = false;
                if (previous != null)
                {
                    var clockGap = frame.Time - last.Time;
                    moved = Distance(previous, car);
                    teleported = moved > Math.Max(previous.Speed, car.Speed) * clockGap + _thresholds.TeleportSlack;

                    // the game can stall and then move the cars on by more than the clock says: the
                    // gap is the longer of the clock's and the time the distance covered takes
                    var meanSpeed = (previous.Speed + car.Speed) / 2;
                    dt = meanSpeed > 1 ? Math.Max(clockGap, moved / meanSpeed) : clockGap;
                }
                var measured = previous != null && !teleported && dt > 0 && dt <= _thresholds.MaxSampleGap;

                if (!_candidates.TryGetValue(car.Name, out var candidate))
                {
                    if (settling || !measured || _reported.Contains(car.Name) || !IsRacing(previous) || !IsRacing(car))
                        continue;

                    var decelerationG = (previous.Speed - car.Speed) / dt / StandardGravity;
                    var upset = Upset(car);
                    var fastEnough = previous.Speed >= _thresholds.MinImpactSpeed;
                    var impact = decelerationG >= _thresholds.TriggerDecelerationG && fastEnough;
                    var launched = upset >= _thresholds.UpsetAngle && Math.Max(previous.Speed, car.Speed) >= _thresholds.MinUpsetSpeed;

                    // the viewed car's own sensors: a hit the averaged speed hides, a roll short of the angle
                    var viewed = car.Index == frame.ViewedCarIndex;
                    var hit = viewed && car.AccelerationG >= _thresholds.ViewedCarTriggerG && fastEnough;
                    var rolling = viewed && car.CrashState == TelemetryCrashState.Rolling && Math.Max(previous.Speed, car.Speed) >= _thresholds.MinUpsetSpeed;

                    if (!impact && !launched && !hit && !rolling)
                        continue;

                    candidate = new Candidate
                    {
                        CarIndex = car.Index,
                        StartClock = _clock,
                        StartTime = frame.Time,
                        Lap = car.CurrentLap,
                        LapDistance = car.LapDistance,
                        ImpactSpeed = previous.Speed,
                        PeakDecelerationG = Math.Max(0, decelerationG)
                    };
                    _candidates[car.Name] = candidate;
                }

                var verdict = Follow(candidate, car, measured ? previous : null, dt, moved, teleported, settling, frame);
                if (verdict != Verdict.Open)
                    Close(car.Name, verdict, confirmed);
            }

            // a car gone from the frame (despawned after retiring) ends its window now
            foreach (var name in _candidates.Keys.Where(n => !carsInFrame.Contains(n)).ToList())
                Close(name, IsStranded(_candidates[name]) ? Verdict.Stranded : Verdict.Unknown, confirmed);

            return confirmed;
        }

        // the candidate's window is over: a crash when it stranded the car, or a Severe one the car
        // drove on from (the first only: a later one changes nothing)
        private void Close(string carName, Verdict verdict, List<CrashIncident> confirmed)
        {
            var candidate = _candidates[carName];
            _candidates.Remove(carName);

            var severity = Severity(candidate);
            if (verdict == Verdict.Stranded)
            {
                if (_droveOn.Remove(carName, out var earlier))
                    _incidents.Remove(earlier);
                _reported.Add(carName);
                confirmed.Add(Confirm(carName, candidate, severity, droveOn: false));
            }
            else if (verdict == Verdict.DroveOn && severity == CrashSeverity.Severe && !_droveOn.ContainsKey(carName))
            {
                var incident = Confirm(carName, candidate, severity, droveOn: true);
                _droveOn[carName] = incident;
                confirmed.Add(incident);
            }
        }

        // the candidate's window, one sample at a time; previous is null when the sample measures
        // nothing (the first after a gap)
        private Verdict Follow(Candidate candidate, TelemetryCarSample car, TelemetryCarSample previous, double dt, double moved,
            bool teleported, bool settling, TelemetryFrame frame)
        {
            if (car.PitState is TelemetryPitState.DrivingIntoPits or TelemetryPitState.InPit)
                return Verdict.DroveOn;

            // retired where it stopped, or sent back to the garage
            if (car.RaceState is TelemetryRaceState.Retired or TelemetryRaceState.Dnf || car.PitState == TelemetryPitState.InGarage)
            {
                candidate.Retired = true;
                return Verdict.Stranded;
            }

            if (teleported)
                return Verdict.Unknown;

            // just back from a pause: the positions and angles aren't to be trusted yet
            if (!settling)
            {
                if (previous != null)
                {
                    var decelerationG = (previous.Speed - car.Speed) / dt / StandardGravity;
                    candidate.PeakDecelerationG = Math.Max(candidate.PeakDecelerationG, decelerationG);

                    // a harder hit just after the first
                    if (decelerationG >= _thresholds.TriggerDecelerationG && previous.Speed > candidate.ImpactSpeed)
                        candidate.ImpactSpeed = previous.Speed;

                    if (candidate.HasStopped)
                        candidate.DistanceSinceStop += moved;
                }

                candidate.MaxUpsetAngle = Math.Max(candidate.MaxUpsetAngle, Upset(car));
            }

            if (car.Speed < _thresholds.StoppedSpeed)
            {
                candidate.HasStopped = true;
                if (previous != null)
                    candidate.StoppedTime += dt;
            }

            if (car.Index == frame.ViewedCarIndex)
            {
                candidate.WasViewedCar = true;
                if (car.AccelerationG != null && !settling)
                    candidate.PeakAccelerationG = Math.Max(candidate.PeakAccelerationG ?? 0, car.AccelerationG.Value);
                candidate.Rolled |= car.CrashState == TelemetryCrashState.Rolling;
                candidate.WallContact |= car.WallContact == true;
                candidate.WheelsDetached = Math.Max(candidate.WheelsDetached, car.WheelsDetached ?? 0);
            }

            if (candidate.DistanceSinceStop > _thresholds.DriveOnDistance)
                return Verdict.DroveOn;

            var elapsed = _clock - candidate.StartClock;
            if (!candidate.HasStopped && elapsed > _thresholds.MaxTimeToStop)
                return Verdict.DroveOn;

            if (elapsed >= _thresholds.ConfirmationWindow)
                return IsStranded(candidate) ? Verdict.Stranded : Verdict.DroveOn;

            return Verdict.Open;
        }

        // stopped (long enough to tell) and still there; or retired
        private bool IsStranded(Candidate candidate) =>
            candidate.Retired ||
            (candidate.HasStopped && candidate.StoppedTime >= _thresholds.MinStoppedTime && candidate.DistanceSinceStop <= _thresholds.DriveOnDistance);

        private CrashIncident Confirm(string carName, Candidate candidate, CrashSeverity severity, bool droveOn)
        {
            var incident = new CrashIncident
            {
                CarName = carName,
                CarIndex = candidate.CarIndex,
                Time = candidate.StartTime,
                Lap = candidate.Lap,
                LapDistance = candidate.LapDistance,
                ImpactSpeed = candidate.ImpactSpeed,
                PeakDecelerationG = candidate.PeakDecelerationG,
                MaxUpsetAngle = candidate.MaxUpsetAngle,
                Retired = candidate.Retired,
                DroveOn = droveOn,
                Severity = severity,
                WasViewedCar = candidate.WasViewedCar,
                PeakAccelerationG = candidate.PeakAccelerationG,
                Rolled = candidate.Rolled,
                WallContact = candidate.WallContact,
                WheelsDetached = candidate.WheelsDetached
            };
            _incidents.Add(incident);
            return incident;
        }

        private CrashSeverity Severity(Candidate candidate)
        {
            var upset = candidate.MaxUpsetAngle >= _thresholds.UpsetAngle || candidate.Rolled;

            if (candidate.ImpactSpeed >= _thresholds.SevereImpactSpeed ||
                candidate.PeakDecelerationG >= _thresholds.SevereDecelerationG ||
                (upset && candidate.ImpactSpeed >= _thresholds.SevereUpsetSpeed) ||
                candidate.PeakAccelerationG >= _thresholds.SevereViewedCarG)
                return CrashSeverity.Severe;

            if (candidate.ImpactSpeed >= _thresholds.ModerateImpactSpeed || upset || candidate.WheelsDetached > 0)
                return CrashSeverity.Moderate;

            return CrashSeverity.Minor;
        }

        // the same speed, and in the same place though moving
        private static bool IsRepeat(TelemetryCarSample previous, TelemetryCarSample car) =>
            previous.Speed > 1 && car.Speed == previous.Speed && car.X == previous.X && car.Y == previous.Y && car.Z == previous.Z;

        private static double Distance(TelemetryCarSample a, TelemetryCarSample b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var dz = b.Z - a.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // racing on track: not in the pits, not before the start or after the chequered flag
        private static bool IsRacing(TelemetryCarSample car) =>
            car.RaceState == TelemetryRaceState.Racing && car.PitState == TelemetryPitState.None;

        private static double Upset(TelemetryCarSample car) =>
            Math.Max(Math.Abs(car.Pitch), Math.Abs(car.Roll));
    }
}
