using System.Diagnostics;
using AMS2ChEd.Business.Settings.Contracts;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Checks DLC ownership via the Steam client, keyed by the same dlcId strings track_mapping.json
    /// uses. Delegates the actual Steam call to the separate AMS2ChEd.SteamDlcCheck helper process
    /// (copied next to this app's own exe at build time, same pattern as AMS2ChEd.Updater) rather
    /// than calling steam_api64.dll in-process.
    ///
    /// That's a deliberate choice, not an accident of packaging: an earlier version of this class
    /// P/Invoked steam_api64.dll directly, faking AMS2's own AppID (1066890) via SteamAPI_Init
    /// straight from inside Rewind GP's own process. SteamAPI_Init/SteamAPI_Shutdown does not
    /// behave as a clean, self-contained bracket - Init registers the CALLING PROCESS with Steam as
    /// "the one running AMS2" for as long as that process stays alive, and Shutdown does not undo
    /// that registration. Since Rewind GP is a long-lived process, that meant Steam treated Rewind
    /// GP itself as "running AMS2" for its entire lifetime, and `steam://run` for the real
    /// AMS2AVX.exe then silently refused to do anything until Rewind GP was closed
    /// (Ams2RaceLaunchAssistant's launch step would just hang/no-op). Running the check in a
    /// throwaway helper process that exits immediately after answering means whatever Steam does to
    /// that PID's registration dies with it - Rewind GP itself never registers as "running AMS2" at
    /// all. See AMS2ChEd.SteamDlcCheck/Program.cs for the actual SteamAPI calls.
    ///
    /// Resolves every known DLC in one helper-process invocation (see <see cref="WarmUpAsync"/>),
    /// not once per IsOwned call - callers that know AMS2 isn't running yet (Ams2RaceLaunchAssistant)
    /// call that before launching it, so the (much safer, but still real) one-time Steam touch never
    /// overlaps with AMS2's own live Steam session either.
    ///
    /// Fails safe: if the AMS2 install folder isn't configured yet, the helper exe is missing, Steam
    /// isn't running, or dlcId isn't in the map below, IsOwned returns false - the caller
    /// (Ams2GrandPrixTrackResolver) already treats false as "fall back to DefaultTrackId".
    /// </summary>
    public sealed class Ams2DlcOwnershipChecker : IAms2DlcOwnershipChecker
    {
        private static readonly TimeSpan HelperTimeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// dlcId (matches track_mapping.json's dlc_id field) -> every Steam AppID that grants that
        /// content: the DLC's own AppID, plus any bundle/expansion pack confirmed (via that bundle's
        /// own Steam store description) to separately re-sell the same content under its own AppID
        /// rather than as a proper Steam package/sub (which would already grant the individual
        /// AppID's own subscription and not need listing here). IsOwned is true if the player is
        /// subscribed to ANY of the ids for that key. Sourced from Automobilista 2's Steam store DLC
        /// listing - spot-check against your own Steam library/SteamDB before relying on this.
        /// </summary>
        private static readonly Dictionary<string, uint[]> DlcSteamAppIds = new()
        {
            ["historical_tracks_1"] = [1392090],           // Historical Track Pack Pt1 - no bundle relationship checked yet
            ["historical_tracks_2"] = [2697770],           // Historical Track Pack Pt2 (not currently referenced by any track_mapping.json entry, included for completeness) - no bundle relationship checked yet
            ["historical_tracks_3"] = [4044670],           // Historical Track Pack Pt3 - no bundle relationship checked yet
            ["historical_tracks_4"] = [4674610],           // Historical Track Pack Pt4 - no bundle relationship checked yet
            // Nürburgring/Barcelona/Silverstone/Hockenheim/Spa/Monza are also all separately re-sold,
            // together, as "Premium Track Pack" (1392100) - confirmed via that pack's own Steam store
            // description ("This pack includes the five original Premium Track DLCs plus a sixth
            // bonus track"). Hungaroring is NOT part of it (a later, separate release).
            ["nurburgring_pack"] = [1386931, 1392100],
            ["barcelona_pack"] = [2461740, 1392100],       // "Circuit de Barcelona-Catalunya" on Steam
            ["silverstone_pack"] = [1386930, 1392100],
            ["hockenheim_pack"] = [1377650, 1392100],      // "Hockenheimring Pack" on Steam
            ["hungaroring_pack"] = [4674620],              // not part of Premium Track Pack (released later, separately)
            ["spa_pack"] = [1386932, 1392100],             // "Spa-Francorchamps Pack" on Steam
            ["monza_pack"] = [1648060, 1392100],
            // "Racin' USA Pack Pt1" is also separately re-sold, combined with Pt2/Pt3/a bonus pack,
            // as "Racin' USA Expansion Pack" (1648110) - confirmed via that pack's own Steam store
            // description ("This DLC combines all 3 parts of the original Expansion Pack...").
            ["racin_usa_1"] = [1648061, 1648110],
            ["le_mans"] = [2697790],                       // "Circuit des 24 Heures du Mans" on Steam - no bundle relationship checked yet
        };

        private readonly IGameInstallSettingsStorage _installSettingsStorage;
        private readonly SemaphoreSlim _warmUpLock = new(1, 1);
        private HashSet<uint> _installedAppIds; // null until WarmUpAsync/IsOwned has run once

        public Ams2DlcOwnershipChecker(IGameInstallSettingsStorage installSettingsStorage)
        {
            _installSettingsStorage = installSettingsStorage;
        }

        public bool IsOwned(string dlcId)
        {
            if (string.IsNullOrEmpty(dlcId)) return false;
            if (!DlcSteamAppIds.TryGetValue(dlcId, out var steamAppIds)) return false;

            if (_installedAppIds == null)
            {
                // Caller didn't warm up ahead of time (e.g. AMS2 was already running when this got
                // called the first time) - fall back to a blocking wait rather than reporting
                // "not owned" outright.
                WarmUpAsync().GetAwaiter().GetResult();
            }

            // True if the player is subscribed to ANY of the mapped AppIDs - the DLC's own, or any
            // bundle/expansion pack confirmed to separately re-sell the same content.
            return steamAppIds.Any(_installedAppIds.Contains);
        }

        public async Task WarmUpAsync()
        {
            if (_installedAppIds != null) return;

            await _warmUpLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_installedAppIds != null) return;

                _installedAppIds = await RunHelperAsync().ConfigureAwait(false);
            }
            finally
            {
                _warmUpLock.Release();
            }
        }

        private async Task<HashSet<uint>> RunHelperAsync()
        {
            var installed = new HashSet<uint>();

            var installFolder = _installSettingsStorage.LoadSettings()?.GameInstallFolder;
            if (string.IsNullOrEmpty(installFolder)) return installed;

            var helperExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SteamDlcCheck", "AMS2ChEd.SteamDlcCheck.exe");
            if (!File.Exists(helperExe)) return installed;

            var appIds = DlcSteamAppIds.Values.SelectMany(ids => ids).Distinct().ToArray();

            var startInfo = new ProcessStartInfo(helperExe)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(installFolder);
            foreach (var appId in appIds)
            {
                startInfo.ArgumentList.Add(appId.ToString());
            }

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null) return installed;

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                using var cts = new CancellationTokenSource(HelperTimeout);
                try
                {
                    await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Helper is hung (Steam itself unresponsive, most likely) - don't let it linger.
                    try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                    return installed;
                }

                var stdout = await stdoutTask.ConfigureAwait(false);
                if (process.ExitCode != 0) return installed; // failed run - see Program.cs's exit codes; fail safe either way

                foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2 && uint.TryParse(parts[0], out var appId) && bool.TryParse(parts[1], out var owned) && owned)
                    {
                        installed.Add(appId);
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Helper couldn't be started at all - fail safe, not fatal.
            }

            return installed;
        }
    }
}
