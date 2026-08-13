using System.Runtime.InteropServices;
using AMS2ChEd.Business.Settings.Contracts;
using Steamworks;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Checks DLC ownership via the Steam client (Steamworks.NET), keyed by the same dlcId strings
    /// track_mapping.json uses. Loads steam_api64.dll straight out of the player's own AMS2 install
    /// folder (via IGameInstallSettingsStorage) instead of bundling a copy with this app - every
    /// Steam game ships its own copy of this DLL next to its own exe, and AMS2 is already a hard
    /// prerequisite for this whole app, so it's guaranteed to be there.
    ///
    /// Initializes the Steam API against AMS2's own AppID (1066890) - this app isn't itself a
    /// registered Steamworks title, so it fakes that context via the SteamAppId env var rather than
    /// SteamAPI_RestartAppIfNecessary (which would try to relaunch THIS app through Steam using
    /// AMS2's AppID, which is wrong).
    ///
    /// Fails safe: if the AMS2 install folder isn't configured yet, steam_api64.dll isn't found
    /// there, Steam isn't running, or dlcId isn't in the map below, IsOwned returns false - the
    /// caller (Ams2GrandPrixTrackResolver) already treats false as "fall back to DefaultTrackId",
    /// which is the conservative behavior this class's original stub intended but didn't actually
    /// implement.
    /// </summary>
    public sealed class Ams2DlcOwnershipChecker : IAms2DlcOwnershipChecker, IDisposable
    {
        private const uint Ams2AppId = 1066890;
        private const string NativeLibraryName = "steam_api64"; // matches Steamworks.NET's own DllImport name

        /// <summary>
        /// dlcId (matches track_mapping.json's dlc_id field) -> every Steam AppID that grants that
        /// content: the DLC's own AppID, plus any bundle/expansion pack confirmed (via that bundle's
        /// own Steam store description) to separately re-sell the same content under its own AppID
        /// rather than as a proper Steam package/sub (which would already grant the individual
        /// AppID's own subscription and not need listing here). IsOwned is true if the player is
        /// subscribed to ANY of the ids for that key. Sourced from Automobilista 2's Steam store DLC
        /// listing - spot-check against your own Steam library/SteamDB before relying on this (see
        /// this class's own top doc comment for where to cross-check).
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
        private readonly bool _steamAvailable;

        public Ams2DlcOwnershipChecker(IGameInstallSettingsStorage installSettingsStorage)
        {
            _installSettingsStorage = installSettingsStorage;
            RegisterNativeLibraryResolver();
            _steamAvailable = TryInitSteam();
        }

        public bool IsOwned(string dlcId)
        {
            if (!_steamAvailable || string.IsNullOrEmpty(dlcId)) return false;
            if (!DlcSteamAppIds.TryGetValue(dlcId, out var steamAppIds)) return false;

            try
            {
                // True if the player is subscribed to ANY of the mapped AppIDs - the DLC's own, or
                // any bundle/expansion pack confirmed to separately re-sell the same content.
                return steamAppIds.Any(steamAppId => SteamApps.BIsSubscribedApp((AppId_t)steamAppId));
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Redirects Steamworks.NET's "steam_api64" P/Invoke lookups to the copy sitting inside the
        /// player's own AMS2 install folder. Returning IntPtr.Zero from the resolver falls through
        /// to .NET's normal probing (app directory, PATH) - so a manually-placed copy still works
        /// too, this is just the primary source. Registered once per process (SetDllImportResolver
        /// throws if called twice for the same assembly).
        /// </summary>
        private void RegisterNativeLibraryResolver()
        {
            NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, (libraryName, assembly, searchPath) =>
            {
                if (libraryName != NativeLibraryName) return IntPtr.Zero;

                var installFolder = _installSettingsStorage.LoadSettings()?.GameInstallFolder;
                if (string.IsNullOrEmpty(installFolder)) return IntPtr.Zero;

                var candidate = Path.Combine(installFolder, "steam_api64.dll");
                return File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle)
                    ? handle
                    : IntPtr.Zero;
            });
        }

        private static bool TryInitSteam()
        {
            try
            {
                Environment.SetEnvironmentVariable("SteamAppId", Ams2AppId.ToString());
                Environment.SetEnvironmentVariable("SteamGameId", Ams2AppId.ToString());
                return SteamAPI.Init();
            }
            catch (DllNotFoundException)
            {
                return false; // steam_api64.dll not found (AMS2 install folder not set / missing DLL) - fail safe, not fatal
            }
        }

        public void Dispose()
        {
            if (_steamAvailable) SteamAPI.Shutdown();
        }
    }
}
