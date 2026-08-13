using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Keeps a WPF overlay window top-centered over AMS2AVX's window (not stretched to fill it - the
    /// overlay is just its own content-sized box), by polling its window rect via Win32 (no
    /// WinEventHook - a simple timer is enough and there's no existing hook-based precedent in this
    /// codebase to build on). Converts the physical-pixel rect Win32 returns through the overlay's
    /// own device-to-DIU transform so this works correctly across mixed-DPI multi-monitor setups.
    /// </summary>
    public class Ams2WindowTracker : IDisposable
    {
        private const string ProcessName = "AMS2AVX";
        private const double TopMargin = 60;

        private readonly Window _overlayWindow;
        private readonly DispatcherTimer _timer;

        /// <summary>Raised (on the UI thread) when AMS2's process can no longer be found/tracked.</summary>
        public event EventHandler ProcessLost;

        /// <summary>
        /// AMS2's window handle as of the last successful poll - IntPtr.Zero if it's never been
        /// resolved yet. Lets callers hand OS focus back to the game (e.g. after closing a topmost
        /// overlay) without a second GetProcessesByName lookup of their own.
        /// </summary>
        public IntPtr LastKnownGameHwnd { get; private set; }

        public Ams2WindowTracker(Window overlayWindow, TimeSpan? pollInterval = null)
        {
            _overlayWindow = overlayWindow;
            _timer = new DispatcherTimer { Interval = pollInterval ?? TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (_, _) => UpdatePosition();
        }

        public void Start()
        {
            UpdatePosition();
            _timer.Start();
        }

        public void Stop() => _timer.Stop();

        private void UpdatePosition()
        {
            var process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            if (process == null || process.HasExited)
            {
                _timer.Stop();
                ProcessLost?.Invoke(this, EventArgs.Empty);
                return;
            }

            // MainWindowHandle can briefly read IntPtr.Zero right after launch, before AMS2's
            // window is created - just skip this tick and try again on the next one.
            var hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                return;
            }

            LastKnownGameHwnd = hwnd;

            var source = PresentationSource.FromVisual(_overlayWindow);
            if (source?.CompositionTarget == null)
            {
                return;
            }

            var transform = source.CompositionTarget.TransformFromDevice;
            var topLeft = transform.Transform(new Point(rect.Left, rect.Top));
            var bottomRight = transform.Transform(new Point(rect.Right, rect.Bottom));
            var centerX = (topLeft.X + bottomRight.X) / 2;

            // The overlay stays its own (content-sized) dimensions - just top-center it over AMS2's
            // window instead of stretching to fill it.
            _overlayWindow.Left = centerX - _overlayWindow.ActualWidth / 2;
            _overlayWindow.Top = topLeft.Y + TopMargin;
        }

        public void Dispose() => _timer.Stop();

        /// <summary>
        /// Hands OS foreground focus back to AMS2's window. Needed because nothing else in the
        /// launch flow ever activates AMS2's window - the config-apply path talks to it purely via
        /// memory injection, never OS focus - so once a Topmost overlay that WAS focused (e.g. from
        /// a button click) closes, Windows falls back to reactivating whatever window was active in
        /// THIS process beforehand (RaceWeekendWindow/MainWindow) instead of the game, which is what
        /// "other windows popping in front of the game" actually is. Call this after closing such an
        /// overlay. No-op if AMS2's window was never resolved (LastKnownGameHwnd still Zero).
        /// </summary>
        public void RestoreGameFocus()
        {
            if (LastKnownGameHwnd != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(LastKnownGameHwnd);
        }

        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetForegroundWindow(IntPtr hWnd);
        }
    }
}
