using System.Diagnostics;

namespace Ams2Interop;

/// <summary>
/// Best-effort helper for finding and launching AMS2 - a convenience on top of
/// Ams2RaceConfigurator (which only ever talks to an already-running AMS2AVX.exe and never
/// launches it itself). Any application that needs to get AMS2 running before attaching can use
/// this instead of reimplementing Steam-path detection and launch handling.
/// </summary>
public static class Ams2Launcher
{
    private const string ProcessName = "AMS2AVX";
    private const string RelativeExePath = @"steamapps\common\Automobilista 2\AMS2AVX.exe";

    /// <summary>
    /// AMS2's real Steam AppID - confirmed live via the actual install's own
    /// steamapps\appmanifest_1066890.acf (`"name" "Automobilista 2"`), not guessed.
    /// </summary>
    private const string SteamAppId = "1066890";

    public static bool IsRunning() => Process.GetProcessesByName(ProcessName).Length > 0;

    /// <summary>
    /// Launches AMS2AVX.exe through Steam's own `steam://run/&lt;appid&gt;` protocol rather than
    /// starting the exe directly. A directly-launched process (`Process.Start` on the exe path)
    /// bypasses Steam's own launch path (no DRM/session handshake, no Steam-provided environment/
    /// command-line the game may expect) and cannot be attached to by Ams2RaceConfigurator -
    /// VirtualAllocEx fails with ERROR_ACCESS_DENIED for such a process, consistently, not just as
    /// a timing issue. Launching via the Steam protocol goes through the normal Steam-launch
    /// handshake instead, and the resulting process attaches fine.
    /// </summary>
    public static void Launch() =>
        Process.Start(new ProcessStartInfo($"steam://run/{SteamAppId}")
        {
            UseShellExecute = true,
        });

    public static async Task<bool> WaitForProcessAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsRunning()) return true;
            await Task.Delay(1000).ConfigureAwait(false);
        }
        return false;
    }

    /// <summary>
    /// Best-effort close of AMS2AVX.exe - tries a graceful WM_CLOSE first (CloseMainWindow) and
    /// only force-kills the process if it doesn't exit within <paramref name="gracePeriod"/>.
    /// Never throws - a process that's already gone, or one we don't have permission to touch, is
    /// treated the same as a successful close.
    /// </summary>
    public static async Task CloseAsync(TimeSpan? gracePeriod = null)
    {
        var grace = gracePeriod ?? TimeSpan.FromSeconds(5);

        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            try
            {
                if (process.CloseMainWindow())
                {
                    using var cts = new CancellationTokenSource(grace);
                    try
                    {
                        await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        // Didn't exit within the grace period - fall through to force-kill.
                    }
                }

                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // Best-effort - the process may have already exited, or access could be denied.
            }
        }
    }
}
