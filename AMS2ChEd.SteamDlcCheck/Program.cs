using System.Runtime.InteropServices;

// Short-lived, standalone helper: checks Steam DLC ownership for AMS2 (AppID 1066890) via
// steam_api64.dll's classic flat API, prints one "appid=true|false" line per requested AppID to
// stdout, then exits immediately.
//
// This exists as a SEPARATE PROCESS specifically because SteamAPI_Init/SteamAPI_Shutdown does not
// behave as a clean, self-contained bracket - calling SteamAPI_Init under AMS2's own AppID
// registers the CALLING PROCESS with Steam as "the one running AMS2" for as long as that process
// stays alive (SteamAPI_Shutdown does not undo this registration). An earlier version of this
// check ran in-process inside Rewind GP itself, which meant Steam treated Rewind GP as "running
// AMS2" for its entire lifetime - `steam://run` for the real AMS2AVX.exe then silently refused to
// do anything until Rewind GP itself was closed. Running the check in a throwaway process that
// exits right after answering means whatever Steam does to that PID's registration dies with it,
// long before Rewind GP (or the real AMS2AVX.exe) needs Steam to behave normally again.
//
// Usage: AMS2ChEd.SteamDlcCheck.exe <ams2InstallFolder> <appId1> [<appId2> ...]
// Exit codes: 0 = ran, one "appid=bool" line per requested AppID printed to stdout (regardless of
// individual ownership results). 1 = bad arguments. 2 = SteamAPI_Init failed / SteamApps interface
// unavailable (Steam not running, most likely). 3 = steam_api64.dll not found in the given
// install folder. 4 = steam_api64.dll found but missing an expected export (API shape changed).
internal static class Program
{
    private const uint Ams2AppId = 1066890;
    private const string NativeLibraryName = "steam_api64";

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_Init();

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_Shutdown();

    // Classic un-versioned flat accessor for the ISteamApps interface pointer - the only form
    // AMS2's steam_api64.dll exports (no SteamInternal_CreateInterface, no SteamAPI_SteamApps_v0XX).
    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamApps")]
    private static extern IntPtr SteamApps_GetInterface();

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ISteamApps_BIsSubscribedApp(IntPtr instancePtr, uint appId);

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: AMS2ChEd.SteamDlcCheck.exe <ams2InstallFolder> <appId1> [<appId2> ...]");
            return 1;
        }

        var installFolder = args[0];
        uint[] appIds;
        try
        {
            appIds = args.Skip(1).Select(uint.Parse).Distinct().ToArray();
        }
        catch (FormatException)
        {
            Console.Error.WriteLine("All arguments after the install folder must be numeric Steam AppIDs.");
            return 1;
        }

        // Redirect this process's own "steam_api64" P/Invoke lookups to the copy sitting inside
        // AMS2's own install folder - every Steam game ships its own copy next to its own exe.
        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, (libraryName, _, _) =>
        {
            if (libraryName != NativeLibraryName) return IntPtr.Zero;
            var candidate = Path.Combine(installFolder, "steam_api64.dll");
            return File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle) ? handle : IntPtr.Zero;
        });

        // This app isn't itself a registered Steamworks title, so it fakes AMS2's own AppID via
        // these env vars rather than SteamAPI_RestartAppIfNecessary (which would try to relaunch
        // THIS process through Steam using AMS2's AppID, which is wrong).
        Environment.SetEnvironmentVariable("SteamAppId", Ams2AppId.ToString());
        Environment.SetEnvironmentVariable("SteamGameId", Ams2AppId.ToString());

        var steamInitialized = false;
        try
        {
            steamInitialized = SteamAPI_Init();
            if (!steamInitialized)
            {
                Console.Error.WriteLine("SteamAPI_Init failed - is Steam running?");
                return 2;
            }

            var steamAppsInterface = SteamApps_GetInterface();
            if (steamAppsInterface == IntPtr.Zero)
            {
                Console.Error.WriteLine("SteamApps interface unavailable.");
                return 2;
            }

            foreach (var appId in appIds)
            {
                var owned = SteamAPI_ISteamApps_BIsSubscribedApp(steamAppsInterface, appId);
                Console.WriteLine($"{appId}={owned}");
            }

            return 0;
        }
        catch (DllNotFoundException)
        {
            Console.Error.WriteLine("steam_api64.dll not found in the given install folder.");
            return 3;
        }
        catch (EntryPointNotFoundException ex)
        {
            Console.Error.WriteLine($"steam_api64.dll is missing an expected export: {ex.Message}");
            return 4;
        }
        finally
        {
            // The whole point of this being a separate process: Steam's registration of "this PID
            // is running AMS2" dies when the process exits right after this, regardless of whatever
            // SteamAPI_Shutdown itself does or doesn't undo.
            if (steamInitialized) SteamAPI_Shutdown();
        }
    }
}
