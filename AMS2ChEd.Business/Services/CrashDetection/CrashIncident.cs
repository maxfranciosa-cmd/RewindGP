namespace AMS2ChEd.Business.Services.CrashDetection
{
    /// <summary>
    /// A crash the detector confirmed. The fields from PeakAccelerationG on are only known when the
    /// crashed car was the viewed one.
    /// </summary>
    public class CrashIncident
    {
        public string CarName { get; set; }
        public int CarIndex { get; set; }

        // the trigger's frame time
        public double Time { get; set; }
        public int Lap { get; set; }
        public double LapDistance { get; set; }

        // m/s, the speed before the impact
        public double ImpactSpeed { get; set; }
        public double PeakDecelerationG { get; set; }

        // the largest pitch or roll in the window, in degrees
        public double MaxUpsetAngle { get; set; }

        public bool Retired { get; set; }

        // a Severe crash the car drove on from (or into the pits): it can still have hurt the driver
        public bool DroveOn { get; set; }
        public CrashSeverity Severity { get; set; }

        public bool WasViewedCar { get; set; }
        public double? PeakAccelerationG { get; set; }
        public bool Rolled { get; set; }
        public bool WallContact { get; set; }
        public int WheelsDetached { get; set; }
    }
}
