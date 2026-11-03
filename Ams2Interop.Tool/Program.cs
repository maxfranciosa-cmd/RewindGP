using Ams2Interop;
using Ams2Interop.Signatures;

// Developer tool for Ams2Interop's offset signatures. AMS2 must be running.
//
//   status   [--signatures <file>] [--cache <file>]
//       Resolves the offsets for the running build exactly as the app does, and says which were
//       found and how (built-in unverified / cached / scanned / built-in rejected). Prints the
//       build id to copy into Ams2Constants.BuiltIn.ConfirmedBuildIds. Read-only.
//
//   generate [<output file>]
//       Builds a signature file from the running build, taking Ams2Constants.BuiltIn as correct.
//       Only run it on a build those values are confirmed working on. Default output:
//       ams2_signatures.json in the current directory - copy it into the Ams2Interop project folder.

var command = args.FirstOrDefault()?.ToLowerInvariant();
if (command is not ("status" or "generate"))
{
    Console.WriteLine("usage: Ams2Interop.Tool status [--signatures <file>] [--cache <file>]");
    Console.WriteLine("       Ams2Interop.Tool generate [<output file>]");
    return 2;
}

var options = new Ams2OffsetOptions
{
    SignatureFilePath = OptionValue("--signatures") ?? new Ams2OffsetOptions().SignatureFilePath,
    CacheFilePath = OptionValue("--cache") ?? new Ams2OffsetOptions().CacheFilePath,
};

using var configurator = new Ams2RaceConfigurator(
    new Dictionary<string, int>(), new Dictionary<string, int>(), Console.WriteLine, options);

if (!await configurator.AttachAsync())
{
    Console.WriteLine("Could not attach to AMS2AVX.exe - is the game running?");
    return 1;
}

if (command == "status")
{
    var offsets = configurator.Offsets!;
    var confirmed = Ams2Constants.BuiltIn.ConfirmedBuildIds;
    Console.WriteLine();
    Console.WriteLine("========================================================");
    Console.WriteLine($"  AMS2 BUILD ID: {offsets.BuildId ?? "UNKNOWN (PE header unreadable)"}");
    Console.WriteLine(confirmed.Length == 0
        ? "  (Ams2Constants.BuiltIn.ConfirmedBuildIds is empty - if the built-in values work on this"
          + Environment.NewLine + "   build, copy the id above into it)"
        : offsets.BuildId != null && confirmed.Contains(offsets.BuildId, StringComparer.OrdinalIgnoreCase)
            ? "  (a confirmed build for Ams2Constants.BuiltIn)"
            : "  (NOT one of Ams2Constants.BuiltIn.ConfirmedBuildIds)");
    Console.WriteLine("========================================================");
    Console.WriteLine(offsets.Describe());
    foreach (var name in Ams2OffsetNames.All)
    {
        var value = offsets.Rvas.TryGetValue(name, out var rva) ? $"0x{rva:X}" : "NOT FOUND";
        var builtIn = Ams2OffsetNames.BuiltInRvas[name];
        Console.WriteLine($"  {name,-26} {value,-12} (built-in 0x{builtIn:X})");
    }
    Console.WriteLine($"  custom race: {offsets.CanWriteSlots}, car/track/livery: {offsets.CanSetCar}, " +
                      $"opponent recount: {offsets.CanCommit}, wear settings: {offsets.CanChangeGameplaySettings}");
    return offsets.IsDegraded ? 1 : 0;
}

var output = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--")) ?? Ams2OffsetOptions.DefaultSignatureFileName;
var result = configurator.GenerateSignatures(output);
Console.WriteLine();
if (result.Complete)
{
    Console.WriteLine($"Wrote {Path.GetFullPath(output)} - copy it into the Ams2Interop project folder so it ships with the app.");
    return 0;
}

Console.WriteLine($"NOT written - no unique signature for: {string.Join(", ", result.Failed)}.");
Console.WriteLine("Check those values in Ams2Constants.BuiltIn still work on this build first.");
return 1;

string? OptionValue(string name)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
