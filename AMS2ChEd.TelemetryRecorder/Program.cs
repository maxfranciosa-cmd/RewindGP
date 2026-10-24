using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.Services.CrashDetection;
using AMS2SharedMemoryNet;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Developer tool for calibrating the crash detector. Run it next to AMS2 (any session, no career
// needed): it logs every car's telemetry to CSV at ~20 Hz until Ctrl+C. Ctrl+F12, pressed in the
// game, writes a marker on the current frame - press it right after each event worth finding again.
//
//   AMS2ChEd.TelemetryRecorder [<output file>] [--hz <samples per second>]
//
// Default output: telemetry_<date>_<time>.csv in the current directory.

const int DefaultHz = 20;
const int VkControl = 0x11;
const int VkF12 = 0x7B;

var output = args.FirstOrDefault(a => !a.StartsWith("--")) ?? $"telemetry_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
var hz = int.TryParse(OptionValue("--hz"), out var parsedHz) && parsedHz > 0 ? parsedHz : DefaultHz;
var interval = TimeSpan.FromSeconds(1.0 / hz);

var stopping = false;
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stopping = true;
};

MemoryParser? parser = null;
Console.WriteLine("Waiting for AMS2's shared memory (start AMS2, enable shared memory in its options)...");
while (parser == null && !stopping)
{
    try
    {
        parser = new MemoryParser("$pcars2$");
    }
    catch
    {
        Thread.Sleep(1000);
    }
}

if (parser == null)
    return 1;

// Sleep's default resolution (~15.6 ms) would make the samples uneven
TimeBeginPeriod(1);
try
{
    using var writer = new StreamWriter(output);
    TelemetryCsv.WriteHeader(writer);

    Console.WriteLine($"Recording to {Path.GetFullPath(output)} at {hz} Hz. Ctrl+F12 = marker, Ctrl+C = stop.");

    var clock = Stopwatch.StartNew();
    var nextSample = TimeSpan.Zero;
    var nextFlush = TimeSpan.FromSeconds(1);
    var nextStatus = TimeSpan.Zero;
    long frames = 0;
    long tornPages = 0;
    var markers = 0;
    var markerKeysDown = false;

    while (!stopping)
    {
        var wait = nextSample - clock.Elapsed;
        if (wait > TimeSpan.Zero)
            Thread.Sleep(wait);
        nextSample += interval;

        // a page AMS2 was writing while we read it: read again, a few times at most
        TelemetryFrame? frame = null;
        for (int attempt = 0; attempt < 3 && frame == null; attempt++)
        {
            frame = Ams2TelemetryFrameReader.Read(parser.GetPage(), clock.Elapsed.TotalSeconds);
            if (frame == null)
                tornPages++;
        }
        if (frame == null)
            continue;

        var keysDown = IsDown(VkControl) && IsDown(VkF12);
        if (keysDown && !markerKeysDown)
        {
            frame.Marker = ++markers;
            Console.WriteLine($"  marker {markers} at {frame.Time:0.00} s");
        }
        markerKeysDown = keysDown;

        TelemetryCsv.WriteFrame(writer, frames++, frame);

        if (clock.Elapsed >= nextFlush)
        {
            writer.Flush();
            nextFlush = clock.Elapsed + TimeSpan.FromSeconds(1);
        }

        if (clock.Elapsed >= nextStatus)
        {
            var viewed = frame.Cars.FirstOrDefault(c => c.Index == frame.ViewedCarIndex);
            var viewedText = viewed == null ? "no viewed car" : $"{viewed.Name} {viewed.Speed * 3.6:0} km/h";
            Console.WriteLine($"{frame.Time,8:0.0} s  {frame.GameState,-10} {frame.SessionState,-12} {frame.Cars.Count,2} cars  {viewedText}  ({frames} frames)");
            nextStatus = clock.Elapsed + TimeSpan.FromSeconds(5);
        }
    }

    writer.Flush();
    Console.WriteLine($"Stopped: {frames} frames, {markers} markers, {tornPages} torn pages re-read.");
}
finally
{
    TimeEndPeriod(1);
    parser.Close();
}

return 0;

string? OptionValue(string name)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

[DllImport("user32.dll")]
static extern short GetAsyncKeyState(int virtualKey);

[DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
static extern uint TimeBeginPeriod(uint milliseconds);

[DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
static extern uint TimeEndPeriod(uint milliseconds);
