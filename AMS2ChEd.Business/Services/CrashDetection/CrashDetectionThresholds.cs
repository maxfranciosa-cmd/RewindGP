namespace AMS2ChEd.Business.Services.CrashDetection
{
    /// <summary>
    /// The crash detector's tuning, set from the telemetry recorder's sessions (crash-incidents-design.md,
    /// section 6): clean races must give no incidents at all, and every crash that left a car stranded
    /// must be found. Speeds in m/s, times in seconds of play, distances in metres.
    /// </summary>
    public class CrashDetectionThresholds
    {
        // TRIGGER: one sample makes a candidate

        // the speed lost between two samples, averaged over the gap: at 20 Hz a real impact's peak
        // is spread over 50 ms, so this reads far lower than the impact's actual g. Recorded: hard
        // braking up to ~6.5 g, crashes that stranded the car 13 to 41 g
        public double TriggerDecelerationG { get; set; } = 10;

        // the speed before the drop: slower impacts never count
        public double MinImpactSpeed { get; set; } = 100 / 3.6;

        // pitch or roll beyond this is an upset (a launch, a flip); the heading is ignored, spins are normal
        public double UpsetAngle { get; set; } = 65;
        public double MinUpsetSpeed { get; set; } = 15;

        // the viewed car only: its own g-force at this or more, or the game's rolling state, is an
        // impact too. Recorded: kerbs, gravel and spins up to ~7.5 g; a roll at 229 km/h that cost
        // only ~9 g of averaged speed read 27 g
        public double ViewedCarTriggerG { get; set; } = 20;

        // a longer gap between two samples (a pause, a stutter) measures nothing
        public double MaxSampleGap { get; set; } = 0.25;

        // how far a car may move between two samples beyond what its speed explains: more is a
        // teleport (back to the garage, a reset, a despawn), never an impact. Generous, as the game
        // can stall for half a second and then move every car on at once
        public double TeleportSlack { get; set; } = 50;

        // back from a pause or a replay the cars' positions and angles take a moment to settle:
        // nothing triggers until then
        public double ResumeSettleTime { get; set; } = 2;

        // CONFIRMATION: the seconds after the trigger

        // the car must stop within MaxTimeToStop and not drive on before the window ends; retiring
        // ends the window at once. An AI car stuck in a crash is marked retired some 45 to 55 s
        // later, while cars caught in a pile-up can sit for half a minute before driving on
        public double ConfirmationWindow { get; set; } = 60;
        public double MaxTimeToStop { get; set; } = 15;
        public double StoppedSpeed { get; set; } = 3;
        public double MinStoppedTime { get; set; } = 1.5;

        // travelled since it first stopped: more is driving on, the crash wasn't serious. A
        // stranded car still shuffles a few metres, trying to get free
        public double DriveOnDistance { get; set; } = 50;

        // SEVERITY: a stranded car's crash counts at any severity; one the car drove on from is
        // reported only when Severe (CrashIncident.DroveOn)

        public double ModerateImpactSpeed { get; set; } = 120 / 3.6;
        public double SevereImpactSpeed { get; set; } = 190 / 3.6;
        public double SevereDecelerationG { get; set; } = 30;

        // an upset at this speed or above is Severe
        public double SevereUpsetSpeed { get; set; } = 130 / 3.6;

        // the viewed car's own g-force peak (real, not averaged over a sample gap). Recorded: kerbs
        // and gravel up to ~13 g, a 155 km/h concrete wall hit 54 g
        public double SevereViewedCarG { get; set; } = 40;
    }
}
