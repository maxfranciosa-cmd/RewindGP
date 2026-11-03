using System.Text.Json;
using Ams2Interop;
using Ams2Interop.Discovery;
using Ams2Interop.Signatures;

// Developer tool for Ams2Interop's offset signatures and Custom Race layout. AMS2 must be running.
// Every command only reads the game's memory; none writes to it or calls into it.
//
//   status   [--signatures <file>] [--cache <file>]
//       Resolves the offsets for the running build exactly as the app does, and says which were
//       found and how (built-in unverified / known build / cached / scanned / built-in rejected).
//       Prints the build id, and - when everything was found - the build's known_builds entry,
//       ready to paste into the signature file.
//
//   generate [<output file>]
//       Builds a signature file from the running build, taking as correct the output file's own
//       known_builds entry for this build if it has one, else Ams2Constants.BuiltIn - but only on
//       a build listed in Ams2Constants.BuiltIn.ConfirmedBuildIds; on any other build it refuses.
//       Records the build in known_builds and keeps the known builds already in the output file.
//       Default output: ams2_signatures.json in the current directory.
//
//   find-tags  [--save <signature file>]
//       Finds the three Custom Race object tags (Vm550IdentityTag, Vm498IdentityTag,
//       Vm498ContainerTag) on this build without using the current values: you change a few
//       settings in AMS2's Custom Race screen, one at a time, and the tool watches which objects
//       change. With --save, writes this build's known_builds entry (the tags found plus the
//       values status resolves) into the signature file - only once all eight values are known.
//
//   find-slots
//       Same, for every slot number the interop uses (Ams2Constants.Vm550Slot / Vm498Slot /
//       MasterSlot) and the fixed offsets around them. Prints the constants that moved.

var command = args.FirstOrDefault()?.ToLowerInvariant();
if (command is not ("status" or "generate" or "find-tags" or "find-slots"))
{
    Console.WriteLine("usage: Ams2Interop.Tool status [--signatures <file>] [--cache <file>]");
    Console.WriteLine("       Ams2Interop.Tool generate [<output file>]");
    Console.WriteLine("       Ams2Interop.Tool find-tags [--save <signature file>]");
    Console.WriteLine("       Ams2Interop.Tool find-slots");
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

var offsets = configurator.Offsets!;

if (command == "status")
{
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
    if (offsets.Source == Ams2OffsetSource.KnownBuild)
        Console.WriteLine("  (listed in the signature file's known_builds - its values are used)");
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

    if (!offsets.IsDegraded && offsets.BuildId != null && offsets.Source != Ams2OffsetSource.KnownBuild)
    {
        Console.WriteLine();
        Console.WriteLine("known_builds entry for this build - add it to the signature file once a test launch works:");
        Console.WriteLine(KnownBuildJson(offsets.BuildId, offsets.Rvas));
    }
    return offsets.IsDegraded ? 1 : 0;
}

if (command == "generate")
{
    var output = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--")) ?? Ams2OffsetOptions.DefaultSignatureFileName;
    var result = configurator.GenerateSignatures(output);
    Console.WriteLine();
    if (result.Complete)
    {
        Console.WriteLine($"Wrote {Path.GetFullPath(output)} - copy it into the Ams2Interop project folder so it ships with the app.");
        return 0;
    }

    if (result.RefusedBecause != null)
    {
        Console.WriteLine($"NOT written - {result.RefusedBecause}.");
        Console.WriteLine("(find-tags --save, or the entry status prints, gives you that entry.)");
        return 1;
    }

    Console.WriteLine($"NOT written - no unique signature for: {string.Join(", ", result.Failed)}.");
    Console.WriteLine("Check those values are right for this build first.");
    return 1;
}

// ---- find-tags / find-slots ----

var survey = configurator.CreateSurvey();
if (survey == null)
{
    Console.WriteLine("IntPropertyTypeTag isn't resolved on this build - the survey finds everything else through it.");
    Console.WriteLine("Re-derive it by hand first (see the README's developer workflow).");
    return 1;
}

var steps = command == "find-tags" ? CustomRaceSurvey.TagSteps : CustomRaceSurvey.SlotSteps;
Console.WriteLine();
Console.WriteLine("Open a SINGLE RACE in AMS2's Custom Race screen and stay on it.");
Console.WriteLine($"You'll be asked to change {steps.Count} settings, one at a time. Change exactly that one");
Console.WriteLine("setting (any new value), then press Enter here. 's' + Enter skips a step, 'q' + Enter stops.");
Console.WriteLine("Press Enter when the Custom Race screen is open.");
if (Console.ReadLine() == null) return 1;

Console.WriteLine("Reading every setting in memory - don't touch the game until asked (this can take a minute)...");
var report = survey.Run(steps, Ask, configurator.CurrentSurveyTags(), () => Thread.Sleep(TimeSpan.FromSeconds(5)));

Console.WriteLine();
Console.WriteLine(report.Describe());

if (command == "find-slots")
{
    var moved = report.Slots.Where(s => s.Moved).ToList();
    var missing = report.Slots.Where(s => s.NewSlot == null && s.Note != "skipped").ToList();
    Console.WriteLine();
    if (moved.Count == 0 && missing.Count == 0)
    {
        Console.WriteLine("No slot moved.");
    }
    else
    {
        if (moved.Count > 0)
        {
            Console.WriteLine("Change in Ams2Interop/Ams2Constants.cs:");
            foreach (var s in moved)
                Console.WriteLine($"  {ClassFor(s.Object)}.{s.Name} = {s.NewSlot};   // was {s.OldSlot}");
        }
        if (missing.Count > 0)
            Console.WriteLine($"Not settled (redo those steps, or check by hand): {string.Join(", ", missing.Select(s => s.Name))}");
    }
    return missing.Count == 0 ? 0 : 1;
}

// find-tags
Console.WriteLine();
if (!report.AllTagsDecided)
{
    Console.WriteLine("Not every tag was decided - run find-tags again, changing each setting by a clear step.");
    return 1;
}

var entry = new Dictionary<string, long>(offsets.Rvas);
foreach (var (name, rva) in report.TagRvas()) entry[name] = rva;
var stillMissing = Ams2OffsetNames.All.Where(n => !entry.ContainsKey(n)).ToList();
if (stillMissing.Count > 0 || offsets.BuildId == null)
{
    Console.WriteLine($"Tags found, but these values are still missing for a known_builds entry: {string.Join(", ", stillMissing)}.");
    return 1;
}

Console.WriteLine("known_builds entry for this build:");
Console.WriteLine(KnownBuildJson(offsets.BuildId, entry));

if (OptionValue("--save") is string savePath)
{
    var file = Ams2SignatureFile.Load(savePath) ?? new Ams2SignatureFile { GeneratedAt = DateTime.UtcNow };
    file.KnownBuilds[offsets.BuildId] = Ams2OffsetNames.All.ToDictionary(n => n, n => entry[n]);
    file.Save(savePath);
    Console.WriteLine($"Saved into {Path.GetFullPath(savePath)}.");
    Console.WriteLine("Next: a test race launch from the app; if it works, run generate on this build to rebuild the signatures.");
}
return 0;

SurveyAnswer Ask(SurveyStep step, int index)
{
    Console.WriteLine();
    Console.WriteLine($"[{index + 1}/{steps.Count}] {step.Instruction} - then press Enter  (s = skip, q = stop)");
    var line = Console.ReadLine();
    if (line == null) return SurveyAnswer.Quit;
    Console.WriteLine("  reading...");
    return line.Trim().ToLowerInvariant() switch
    {
        "s" => SurveyAnswer.Skip,
        "q" => SurveyAnswer.Quit,
        _ => SurveyAnswer.Changed,
    };
}

static string ClassFor(SurveyObject kind) => kind switch
{
    SurveyObject.Vm550 => "Vm550Slot",
    SurveyObject.Vm498 => "Vm498Slot",
    _ => "MasterSlot",
};

static string KnownBuildJson(string buildId, IReadOnlyDictionary<string, long> values) =>
    JsonSerializer.Serialize(
        new Dictionary<string, Dictionary<string, long>> { [buildId] = Ams2OffsetNames.All.ToDictionary(n => n, n => values[n]) },
        new JsonSerializerOptions { WriteIndented = true });

string? OptionValue(string name)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
