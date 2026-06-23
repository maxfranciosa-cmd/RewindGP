using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AMS2ChEd.Business.Helpers
{
    /// <summary>
    /// Registers the .rwgp file extension with Windows so that double-clicking
    /// a season pack file opens it directly in RewindGP.
    ///
    /// Uses CurrentUser registry hive — no elevation required.
    /// Safe to call on every launch; re-registration is instant and keeps the
    /// path current if the user moves their RewindGP folder.
    /// </summary>
    public static class FileAssociationHelper
    {
        private const string Extension = ".rwgp";
        private const string ProgId = "RewindGP.SeasonPack";
        private const string FriendlyName = "Rewind GP Season Pack";

        public static void Register(string exePath, string iconPath)
        {
            // .rwgp → ProgID
            using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}"))
            {
                extKey.SetValue("", ProgId);
                extKey.SetValue("Content Type", "application/x-rewindgp-season");
            }

            // ProgID — friendly name
            using (var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
            {
                progKey.SetValue("", FriendlyName);
            }

            // Icon — uses the first icon embedded in the exe
            using (var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon"))
            {
                iconKey.SetValue("", $"\"{iconPath}\",0");
            }

            // Open command
            using (var cmdKey = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{ProgId}\shell\open\command"))
            {
                cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
            }

            // Friendly app name shown in "Open with" dialogs
            using (var shellKey = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{ProgId}\shell\open"))
            {
                shellKey.SetValue("FriendlyAppName", "Rewind GP");
            }

            // Notify Explorer so the icon refreshes immediately
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Removes the file association. Call this if you ever add an uninstall
        /// or reset option, though it is not required for normal operation.
        /// </summary>
        public static void Unregister()
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\{Extension}", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\{ProgId}", throwOnMissingSubKey: false);

            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(
            uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
