using System.Globalization;
using System.Text;

namespace AMS2ChEd.Business.Services.CrashDetection
{
    /// <summary>
    /// TelemetryFrames as CSV, one row per car per frame: what the telemetry recorder writes, and
    /// what the crash detector's tests read back as fixtures. The frame's own fields repeat on each
    /// of its rows; a frame with no car on track is written as one row with an empty car.
    /// </summary>
    public static class TelemetryCsv
    {
        public const string Header =
            "frame,time,game_state,session_state,viewed,marker," +
            "index,name,race_state,pit_state,speed,pitch,yaw,roll,x,y,z,lap_distance,lap,yellow,flag_reason," +
            "accel_g,crash_state,wall,wheels_detached,aero_damage,engine_damage,suspension_damage";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static void WriteHeader(TextWriter writer) => writer.WriteLine(Header);

        public static void WriteFrame(TextWriter writer, long frameNumber, TelemetryFrame frame)
        {
            var framePart = string.Join(",",
                frameNumber.ToString(Invariant),
                frame.Time.ToString("0.000", Invariant),
                frame.GameState,
                frame.SessionState,
                frame.ViewedCarIndex.ToString(Invariant),
                frame.Marker.ToString(Invariant));

            if (frame.Cars.Count == 0)
            {
                writer.WriteLine(framePart + new string(',', 22));
                return;
            }

            foreach (var car in frame.Cars)
            {
                writer.WriteLine(string.Join(",",
                    framePart,
                    car.Index.ToString(Invariant),
                    Quote(car.Name),
                    car.RaceState,
                    car.PitState,
                    car.Speed.ToString("0.000", Invariant),
                    car.Pitch.ToString("0.00", Invariant),
                    car.Yaw.ToString("0.00", Invariant),
                    car.Roll.ToString("0.00", Invariant),
                    car.X.ToString("0.00", Invariant),
                    car.Y.ToString("0.00", Invariant),
                    car.Z.ToString("0.00", Invariant),
                    car.LapDistance.ToString("0.0", Invariant),
                    car.CurrentLap.ToString(Invariant),
                    car.YellowFlag ? "1" : "0",
                    car.FlagReason,
                    car.AccelerationG?.ToString("0.00", Invariant) ?? "",
                    car.CrashState?.ToString() ?? "",
                    car.WallContact == null ? "" : car.WallContact.Value ? "1" : "0",
                    car.WheelsDetached?.ToString(Invariant) ?? "",
                    car.AeroDamage?.ToString("0.000", Invariant) ?? "",
                    car.EngineDamage?.ToString("0.000", Invariant) ?? "",
                    car.SuspensionDamage?.ToString("0.000", Invariant) ?? ""));
            }
        }

        public static List<TelemetryFrame> Read(TextReader reader)
        {
            var frames = new List<TelemetryFrame>();
            TelemetryFrame current = null;
            string currentFrameNumber = null;

            var header = reader.ReadLine();
            if (header == null)
                return frames;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0)
                    continue;

                var fields = Split(line);
                if (fields.Count < 6)
                    continue;

                if (fields[0] != currentFrameNumber)
                {
                    currentFrameNumber = fields[0];
                    current = new TelemetryFrame
                    {
                        Time = double.Parse(fields[1], Invariant),
                        GameState = Enum.Parse<TelemetryGameState>(fields[2]),
                        SessionState = Enum.Parse<TelemetrySessionState>(fields[3]),
                        ViewedCarIndex = int.Parse(fields[4], Invariant),
                        Marker = int.Parse(fields[5], Invariant)
                    };
                    frames.Add(current);
                }

                if (fields.Count < 28 || fields[6].Length == 0)
                    continue;

                current.Cars.Add(new TelemetryCarSample
                {
                    Index = int.Parse(fields[6], Invariant),
                    Name = fields[7],
                    RaceState = Enum.Parse<TelemetryRaceState>(fields[8]),
                    PitState = Enum.Parse<TelemetryPitState>(fields[9]),
                    Speed = double.Parse(fields[10], Invariant),
                    Pitch = double.Parse(fields[11], Invariant),
                    Yaw = double.Parse(fields[12], Invariant),
                    Roll = double.Parse(fields[13], Invariant),
                    X = double.Parse(fields[14], Invariant),
                    Y = double.Parse(fields[15], Invariant),
                    Z = double.Parse(fields[16], Invariant),
                    LapDistance = double.Parse(fields[17], Invariant),
                    CurrentLap = int.Parse(fields[18], Invariant),
                    YellowFlag = fields[19] == "1",
                    FlagReason = Enum.Parse<TelemetryFlagReason>(fields[20]),
                    AccelerationG = NullableDouble(fields[21]),
                    CrashState = fields[22].Length == 0 ? null : Enum.Parse<TelemetryCrashState>(fields[22]),
                    WallContact = fields[23].Length == 0 ? null : fields[23] == "1",
                    WheelsDetached = fields[24].Length == 0 ? null : int.Parse(fields[24], Invariant),
                    AeroDamage = NullableDouble(fields[25]),
                    EngineDamage = NullableDouble(fields[26]),
                    SuspensionDamage = NullableDouble(fields[27])
                });
            }

            return frames;
        }

        private static double? NullableDouble(string field) =>
            field.Length == 0 ? null : double.Parse(field, Invariant);

        private static string Quote(string value)
        {
            value ??= "";
            return value.IndexOfAny(new[] { ',', '"' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static List<string> Split(string line)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            var quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else if (c == '"')
                        quoted = false;
                    else
                        field.Append(c);
                }
                else if (c == '"')
                    quoted = true;
                else if (c == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else
                    field.Append(c);
            }

            fields.Add(field.ToString());
            return fields;
        }
    }
}
