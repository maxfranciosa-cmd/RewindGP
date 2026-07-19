using Microsoft.Win32;
using System;
using System.IO;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Auto-detects the local Automobilista 2 Steam install folder, shared by any settings-storage
    /// implementation that needs a starting guess before the user confirms/overrides it.
    /// </summary>
    public static class Ams2InstallPathDetector
    {
        private const string AMS2_REGISTRY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1066890";
        private const string DEFAULT_STEAM_PATH = @"C:\Program Files (x86)\Steam\steamapps\common\Automobilista 2";

        public static string DetectInstallPath()
        {
            try
            {
                // Try to get from registry
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(AMS2_REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        string installLocation = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                        {
                            return installLocation;
                        }
                    }
                }

                // Try common Steam library locations
                string[] commonPaths = new[]
                {
                    DEFAULT_STEAM_PATH,
                    @"D:\SteamLibrary\steamapps\common\Automobilista 2",
                    @"E:\SteamLibrary\steamapps\common\Automobilista 2",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        @"Steam\steamapps\common\Automobilista 2")
                };

                foreach (var path in commonPaths)
                {
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }
                return DEFAULT_STEAM_PATH;
            }
            catch (Exception)
            {
                return DEFAULT_STEAM_PATH;
            }
        }
    }
}
