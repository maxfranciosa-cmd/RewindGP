using Ams2Interop.Native;

namespace Ams2Interop.Discovery;

/// <summary>Which Custom Race object a setting lives on.</summary>
public enum SurveyObject
{
    /// <summary>Session/rules (the race's, and each session's: they share the VM550 tag).</summary>
    Vm550,
    /// <summary>Race entrants / AI.</summary>
    Vm498,
    /// <summary>The selection object: car, track, livery; holds VM498.</summary>
    Master
}

/// <summary>One slot a survey step is expected to change, with its number in the current constants.</summary>
public sealed record SurveySlot(string Name, int OldSlot);

/// <summary>
/// One thing for the developer to change in AMS2's Custom Race screen, and the slot(s) that
/// change is expected to show up in. A group (e.g. the opponent count, which AMS2 writes to two
/// slots at once) lists its slots in the order of their current numbers.
/// </summary>
public sealed record SurveyStep(string Instruction, SurveyObject Object, IReadOnlyList<SurveySlot> Slots)
{
    public SurveyStep(string instruction, SurveyObject obj, string name, int oldSlot)
        : this(instruction, obj, new[] { new SurveySlot(name, oldSlot) }) { }
}

public enum SurveyAnswer { Changed, Skip, Quit }

/// <summary>A changed int property, and an object/slot it's referenced from: node = *(Object + Slot*8).</summary>
public sealed record SlotOwner(long Node, long Location, long Object, long Tag, int Slot, int Before, int After);

public sealed record StepObservation(SurveyStep Step, bool Skipped, int ChangedProperties, IReadOnlyList<SlotOwner> Owners);

/// <summary>
/// Developer tool: finds where the Custom Race objects and their settings are on a running AMS2
/// build, without trusting the current constants - for after a patch that broke the object tags
/// (Ams2OffsetNames.Vm498IdentityTag/Vm550IdentityTag/Vm498ContainerTag) or moved slot numbers
/// (Ams2Constants.Vm550Slot/Vm498Slot/MasterSlot). Reads only; never writes or calls into the game.
///
/// How: every int property in memory is found by its type tag (IntPropertyTypeTag - which has
/// several signatures, so it's the value most likely to survive a patch), and its value recorded.
/// The developer then changes one setting at a time in the game's UI; after each change the
/// properties whose values moved are the ones behind that setting. Each such property is looked
/// up from the other side - every pointer to it - and each pointer's object found by walking back
/// to the object's header (two qwords inside the game module: its vtable, then its tag). That
/// gives, from one observation, the object's tag (at +8) and the slot ((pointer - object) / 8).
///
/// Deciding, never guessing: a tag is taken only when it's seen in every step for its object and
/// beats every other candidate (properties that change on their own are filtered out by a
/// baseline read with nothing touched; a tag that also shows up in other objects' steps scores
/// lower). A slot is taken only when exactly the expected number of slots changed in that object;
/// anything else is reported with its candidates and left to the developer.
/// </summary>
public sealed class CustomRaceSurvey
{
    /// <summary>How far back from a pointer an object's header is looked for (bounds the slot number at 0x1000/8).</summary>
    public const int MaxHeaderDistance = 0x1000;

    private readonly IScannableMemory _mem;
    private readonly long _moduleBase;
    private readonly long _moduleSize;
    private readonly long _intPropertyTag;
    private readonly Action<string>? _log;

    public CustomRaceSurvey(IScannableMemory mem, long moduleBase, long moduleSize, long intPropertyTag, Action<string>? log = null)
    {
        _mem = mem;
        _moduleBase = moduleBase;
        _moduleSize = moduleSize;
        _intPropertyTag = intPropertyTag;
        _log = log;
    }

    // ---- the step lists ----

    /// <summary>Enough steps to tell the three object tags apart, two per object.</summary>
    public static IReadOnlyList<SurveyStep> TagSteps { get; } = new[]
    {
        new SurveyStep("Change the race date's YEAR", SurveyObject.Vm550, nameof(Ams2Constants.Vm550Slot.Year), Ams2Constants.Vm550Slot.Year),
        new SurveyStep("Change the race START TIME (the hour)", SurveyObject.Vm550, nameof(Ams2Constants.Vm550Slot.Hour), Ams2Constants.Vm550Slot.Hour),
        new SurveyStep("Change the AI SKILL", SurveyObject.Vm498, nameof(Ams2Constants.Vm498Slot.Skill), Ams2Constants.Vm498Slot.Skill),
        new SurveyStep("Change the AI MISTAKES setting", SurveyObject.Vm498, nameof(Ams2Constants.Vm498Slot.MistakeFrequency), Ams2Constants.Vm498Slot.MistakeFrequency),
        new SurveyStep("Pick a different CAR", SurveyObject.Master, nameof(Ams2Constants.MasterSlot.Car), Ams2Constants.MasterSlot.Car),
        new SurveyStep("Pick a different LIVERY (same car)", SurveyObject.Master, nameof(Ams2Constants.MasterSlot.Livery), Ams2Constants.MasterSlot.Livery),
    };

    /// <summary>Every slot the interop writes or checks, one setting per step.</summary>
    public static IReadOnlyList<SurveyStep> SlotSteps { get; } = BuildSlotSteps();

    private static IReadOnlyList<SurveyStep> BuildSlotSteps()
    {
        static SurveyStep V550(string instruction, string name, int slot) => new(instruction, SurveyObject.Vm550, name, slot);
        static SurveyStep V498(string instruction, string name, int slot) => new(instruction, SurveyObject.Vm498, name, slot);
        static SurveyStep Master(string instruction, string name, int slot) => new(instruction, SurveyObject.Master, name, slot);
        const string skipIfUnsure = " (skip if you can't tell which setting this is)";

        return new[]
        {
            V550("Change the race date's YEAR", nameof(Ams2Constants.Vm550Slot.Year), Ams2Constants.Vm550Slot.Year),
            V550("Change the race date's MONTH", nameof(Ams2Constants.Vm550Slot.Month), Ams2Constants.Vm550Slot.Month),
            V550("Change the race date's DAY", nameof(Ams2Constants.Vm550Slot.Day), Ams2Constants.Vm550Slot.Day),
            V550("Change the race START TIME (the hour)", nameof(Ams2Constants.Vm550Slot.Hour), Ams2Constants.Vm550Slot.Hour),
            V550("Change the DATE TYPE (custom/real date)" + skipIfUnsure, nameof(Ams2Constants.Vm550Slot.DateType), Ams2Constants.Vm550Slot.DateType),
            V550("Switch the race between TIMED and LAPS", nameof(Ams2Constants.Vm550Slot.DurationTypeFlag), Ams2Constants.Vm550Slot.DurationTypeFlag),
            V550("With the race TIMED, change its DURATION", nameof(Ams2Constants.Vm550Slot.DurationMinutes), Ams2Constants.Vm550Slot.DurationMinutes),
            V550("With the race on LAPS, change the NUMBER OF LAPS", nameof(Ams2Constants.Vm550Slot.LapCount), Ams2Constants.Vm550Slot.LapCount),
            V550("Toggle PRIVATE QUALIFYING", nameof(Ams2Constants.Vm550Slot.PrivateQuali), Ams2Constants.Vm550Slot.PrivateQuali),
            V550("Toggle ROLLING START", nameof(Ams2Constants.Vm550Slot.RollingStart), Ams2Constants.Vm550Slot.RollingStart),
            V550("Toggle the MANDATORY PIT STOP", nameof(Ams2Constants.Vm550Slot.MandatoryPit), Ams2Constants.Vm550Slot.MandatoryPit),
            V550("Change the MINIMUM TYRE change (pit rules)", nameof(Ams2Constants.Vm550Slot.MinTyres), Ams2Constants.Vm550Slot.MinTyres),
            V550("Change the MINIMUM FUEL (pit rules)", nameof(Ams2Constants.Vm550Slot.MinFuel), Ams2Constants.Vm550Slot.MinFuel),
            V550("Change the FULL COURSE YELLOW setting", nameof(Ams2Constants.Vm550Slot.Fcy), Ams2Constants.Vm550Slot.Fcy),
            V550("Toggle REFUELLING", nameof(Ams2Constants.Vm550Slot.Refuelling), Ams2Constants.Vm550Slot.Refuelling),
            V550("Change the TIME PROGRESSION", nameof(Ams2Constants.Vm550Slot.TimeProgression), Ams2Constants.Vm550Slot.TimeProgression),
            V550("Switch the weather between CUSTOM and HISTORIC", nameof(Ams2Constants.Vm550Slot.WeatherType), Ams2Constants.Vm550Slot.WeatherType),
            V550("With CUSTOM weather, change the NUMBER OF WEATHER SLOTS", nameof(Ams2Constants.Vm550Slot.WeatherSlotCount), Ams2Constants.Vm550Slot.WeatherSlotCount),
            V550("Change the 1st WEATHER slot", nameof(Ams2Constants.Vm550Slot.WeatherSlot1), Ams2Constants.Vm550Slot.WeatherSlot1),
            V550("Change the 2nd WEATHER slot", nameof(Ams2Constants.Vm550Slot.WeatherSlot2), Ams2Constants.Vm550Slot.WeatherSlot2),
            V550("Change the 3rd WEATHER slot", nameof(Ams2Constants.Vm550Slot.WeatherSlot3), Ams2Constants.Vm550Slot.WeatherSlot3),
            V550("Change the 4th WEATHER slot", nameof(Ams2Constants.Vm550Slot.WeatherSlot4), Ams2Constants.Vm550Slot.WeatherSlot4),
            new SurveyStep("Turn PRACTICE on or off", SurveyObject.Vm550, new[]
            {
                new SurveySlot(nameof(Ams2Constants.Vm550Slot.SessionEnabled), Ams2Constants.Vm550Slot.SessionEnabled),
                new SurveySlot(nameof(Ams2Constants.Vm550Slot.SessionEnabledPaired1), Ams2Constants.Vm550Slot.SessionEnabledPaired1),
                new SurveySlot(nameof(Ams2Constants.Vm550Slot.SessionEnabledPaired2), Ams2Constants.Vm550Slot.SessionEnabledPaired2),
            }),
            V498("Change how the OPPONENT COUNT is chosen (max available / custom / manual)", nameof(Ams2Constants.Vm498Slot.NumOpponentsType), Ams2Constants.Vm498Slot.NumOpponentsType),
            new SurveyStep("With a custom count, change the NUMBER OF OPPONENTS", SurveyObject.Vm498, new[]
            {
                new SurveySlot(nameof(Ams2Constants.Vm498Slot.RivalCount), Ams2Constants.Vm498Slot.RivalCount),
                new SurveySlot(nameof(Ams2Constants.Vm498Slot.RivalCountPerClass), Ams2Constants.Vm498Slot.RivalCountPerClass),
            }),
            V498("Change the MAXIMUM OPPONENTS" + skipIfUnsure, nameof(Ams2Constants.Vm498Slot.MaxOpponents), Ams2Constants.Vm498Slot.MaxOpponents),
            V498("Change the AI SKILL", nameof(Ams2Constants.Vm498Slot.Skill), Ams2Constants.Vm498Slot.Skill),
            V498("Change the AI AGGRESSION", nameof(Ams2Constants.Vm498Slot.Aggression), Ams2Constants.Vm498Slot.Aggression),
            V498("Change the AI WET WEATHER SKILL", nameof(Ams2Constants.Vm498Slot.WetWeatherSkill), Ams2Constants.Vm498Slot.WetWeatherSkill),
            V498("Change the AI MISTAKES setting", nameof(Ams2Constants.Vm498Slot.MistakeFrequency), Ams2Constants.Vm498Slot.MistakeFrequency),
            V498("Change the OPPONENT TYPE (identical / same class / multiclass)", nameof(Ams2Constants.Vm498Slot.OpponentsTypeKind), Ams2Constants.Vm498Slot.OpponentsTypeKind),
            Master("Pick a different TRACK", nameof(Ams2Constants.MasterSlot.Track), Ams2Constants.MasterSlot.Track),
            Master("Pick a different CAR", nameof(Ams2Constants.MasterSlot.Car), Ams2Constants.MasterSlot.Car),
            Master("Pick a different LIVERY (same car)", nameof(Ams2Constants.MasterSlot.Livery), Ams2Constants.MasterSlot.Livery),
        };
    }

    // ---- running it ----

    /// <param name="ask">Shows a step to the developer and returns once they've made the change (or skip/quit).</param>
    /// <param name="currentTags">The tags the running build currently resolves, if any - only used to break a tie.</param>
    /// <param name="baselinePause">Waits a moment with nothing touched, so properties that change on their own can be ignored.</param>
    public SurveyReport Run(IReadOnlyList<SurveyStep> steps, Func<SurveyStep, int, SurveyAnswer> ask,
        IReadOnlyDictionary<SurveyObject, long>? currentTags = null, Action? baselinePause = null)
    {
        var values = SnapshotProperties();
        _log?.Invoke($"survey: {values.Count:N0} int properties found");

        baselinePause?.Invoke();
        var baseline = Reread(values.Keys);
        var noise = Diff(values, baseline).Select(c => c.Node).ToHashSet();
        if (noise.Count > 0) _log?.Invoke($"survey: {noise.Count} properties changed with nothing touched - ignored");
        values = baseline;

        var observations = new List<StepObservation>();
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var answer = ask(step, i);
            if (answer == SurveyAnswer.Quit) break;
            if (answer == SurveyAnswer.Skip)
            {
                observations.Add(new StepObservation(step, true, 0, Array.Empty<SlotOwner>()));
                continue;
            }

            var after = Reread(values.Keys);
            var changes = Diff(values, after).Where(c => !noise.Contains(c.Node)).ToList();
            values = after;

            var owners = FindOwners(changes);
            observations.Add(new StepObservation(step, false, changes.Count, owners));
            _log?.Invoke($"survey: '{step.Instruction}': {changes.Count} properties changed, "
                         + $"{owners.Select(o => o.Object).Distinct().Count()} owning object(s)");
        }

        return Analyse(observations, currentTags);
    }

    /// <summary>Every int property in memory (node -> value), found by its type tag at node-0x18.</summary>
    public Dictionary<long, int> SnapshotProperties()
    {
        var nodes = MemoryScanner.FindOccurrences(_mem, _intPropertyTag)
            .Select(hit => hit - MemoryReads.SlotTagOffset)
            .ToList();
        return Reread(nodes);
    }

    /// <summary>Current values of the given properties; ones that can't be read are left out.</summary>
    public Dictionary<long, int> Reread(IEnumerable<long> nodes)
    {
        const long pageMask = ~0xFFFL;
        var result = new Dictionary<long, int>();
        // one read per 4 KB page rather than one per property - there are tens of thousands
        foreach (var page in nodes.GroupBy(n => (n + MemoryReads.SlotValueOffset) & pageMask))
        {
            byte[]? buffer = _mem.ReadRaw(page.Key, 0x1000, out var b) ? b : null;
            foreach (var node in page)
            {
                long at = node + MemoryReads.SlotValueOffset;
                int offset = (int)(at - page.Key);
                if (buffer != null && offset <= 0x1000 - 4)
                    result[node] = BitConverter.ToInt32(buffer, offset);
                else if (_mem.TryReadInt32(at, out var value))
                    result[node] = value;
            }
        }
        return result;
    }

    public static List<(long Node, int Before, int After)> Diff(IReadOnlyDictionary<long, int> before, IReadOnlyDictionary<long, int> after) =>
        before.Where(kv => after.TryGetValue(kv.Key, out var now) && now != kv.Value)
              .Select(kv => (kv.Key, kv.Value, after[kv.Key]))
              .ToList();

    /// <summary>Every object/slot each changed property is referenced from.</summary>
    public List<SlotOwner> FindOwners(IReadOnlyList<(long Node, int Before, int After)> changes)
    {
        var owners = new List<SlotOwner>();
        if (changes.Count == 0) return owners;
        var byNode = changes.GroupBy(c => c.Node).ToDictionary(g => g.Key, g => g.First());

        foreach (var (location, node) in MemoryScanner.FindPointersTo(_mem, byNode.Keys.ToHashSet()))
        {
            if (FindObjectStart(location) is not long obj) continue;
            if (!_mem.TryReadInt64(obj + 8, out var tag)) continue;
            var change = byNode[node];
            owners.Add(new SlotOwner(node, location, obj, tag, (int)((location - obj) / 8), change.Before, change.After));
        }
        return owners;
    }

    /// <summary>
    /// The start of the object a slot pointer at <paramref name="location"/> belongs to: the nearest
    /// address below it (slot 2 or further) holding two qwords inside the game module - the
    /// object's vtable and its tag. Null when there's none within MaxHeaderDistance.
    /// </summary>
    public long? FindObjectStart(long location)
    {
        long from = location - MaxHeaderDistance;
        if (_mem.ReadRaw(from, MaxHeaderDistance, out var buffer))
        {
            for (long o = location - 0x10; o >= from; o -= 8)
            {
                int i = (int)(o - from);
                if (InModule(BitConverter.ToInt64(buffer, i)) && InModule(BitConverter.ToInt64(buffer, i + 8)))
                    return o;
            }
            return null;
        }

        // the window runs off the start of the readable memory: walk back until it does
        for (long o = location - 0x10; o >= from; o -= 8)
        {
            if (!_mem.TryReadInt64(o, out var first) || !_mem.TryReadInt64(o + 8, out var second)) return null;
            if (InModule(first) && InModule(second)) return o;
        }
        return null;
    }

    private bool InModule(long value) => value >= _moduleBase && value < _moduleBase + _moduleSize;

    // ---- deciding ----

    public SurveyReport Analyse(IReadOnlyList<StepObservation> observations, IReadOnlyDictionary<SurveyObject, long>? currentTags = null)
    {
        var report = new SurveyReport { ModuleBase = _moduleBase, Observations = observations };
        var objects = Enum.GetValues<SurveyObject>();

        foreach (var kind in objects)
            report.Tags[kind] = DecideTag(kind, observations, currentTags);

        BreakTiesStructurally(report, observations);

        foreach (var observation in observations)
            report.Slots.AddRange(DecideSlots(observation, report.Tags[observation.Step.Object].Tag));

        CheckFixedOffsets(report, observations);
        return report;
    }

    private static TagFinding DecideTag(SurveyObject kind, IReadOnlyList<StepObservation> observations, IReadOnlyDictionary<SurveyObject, long>? currentTags)
    {
        long? current = currentTags != null && currentTags.TryGetValue(kind, out var c) ? c : null;
        var own = observations.Where(o => !o.Skipped && o.Step.Object == kind && o.Owners.Count > 0).ToList();
        var others = observations.Where(o => !o.Skipped && o.Step.Object != kind && o.Owners.Count > 0).ToList();
        if (own.Count == 0)
            return new TagFinding(kind, null, Array.Empty<long>(), current, "no change seen for this object");

        var scored = own.SelectMany(o => o.Owners.Select(w => w.Tag)).Distinct()
            .Select(tag => (Tag: tag,
                            In: own.Count(o => o.Owners.Any(w => w.Tag == tag)),
                            Out: others.Count(o => o.Owners.Any(w => w.Tag == tag))))
            .Where(t => t.In == own.Count) // seen in every step for this object
            .Select(t => (t.Tag, Score: t.In - t.Out))
            .OrderByDescending(t => t.Score)
            .ToList();

        if (scored.Count == 0)
            return new TagFinding(kind, null, own.SelectMany(o => o.Owners.Select(w => w.Tag)).Distinct().ToList(), current,
                "no tag is common to every step for this object");

        var best = scored.Where(t => t.Score == scored[0].Score).Select(t => t.Tag).ToList();
        if (best.Count == 1)
            return new TagFinding(kind, best[0], scored.Select(t => t.Tag).ToList(), current, null);
        if (current is long cur && best.Contains(cur))
            return new TagFinding(kind, cur, best, current, "tie broken by the tag the build currently resolves");
        return new TagFinding(kind, null, best, current, "tie between several tags");
    }

    /// <summary>
    /// When a tag is tied, the objects' links to each other decide: master holds VM498, and VM498's
    /// session wrappers lead to VM550s. Only applied where it leaves exactly one candidate.
    /// </summary>
    private void BreakTiesStructurally(SurveyReport report, IReadOnlyList<StepObservation> observations)
    {
        IEnumerable<long> ObjectsWithTags(SurveyObject kind, IEnumerable<long> tags) =>
            observations.Where(o => o.Step.Object == kind).SelectMany(o => o.Owners)
                        .Where(w => tags.Contains(w.Tag)).Select(w => w.Object).Distinct();

        IReadOnlyList<long> Candidates(SurveyObject kind) =>
            report.Tags[kind].Tag is long t ? new[] { t } : report.Tags[kind].Candidates;

        void Settle(SurveyObject kind, Func<long, bool> linked, string how)
        {
            var finding = report.Tags[kind];
            if (finding.Tag != null || finding.Candidates.Count < 2) return;
            var kept = finding.Candidates.Where(tag => ObjectsWithTags(kind, new[] { tag }).Any(linked)).ToList();
            if (kept.Count == 1)
                report.Tags[kind] = finding with { Tag = kept[0], Note = $"tie broken by {how}" };
        }

        var vm498Objects = ObjectsWithTags(SurveyObject.Vm498, Candidates(SurveyObject.Vm498)).ToHashSet();
        var masterObjects = ObjectsWithTags(SurveyObject.Master, Candidates(SurveyObject.Master)).ToList();

        Settle(SurveyObject.Vm498, vm498 => masterObjects.Any(m => PointerOffsetsTo(m, vm498).Count > 0), "being held by a master object");
        Settle(SurveyObject.Master, master => vm498Objects.Any(v => PointerOffsetsTo(master, v).Count > 0), "holding a VM498 object");
        var vm550Reached = vm498Objects.SelectMany(SessionVms).ToHashSet();
        Settle(SurveyObject.Vm550, vm550 => vm550Reached.Contains(vm550), "being one of VM498's sessions");
    }

    private static IEnumerable<SlotFinding> DecideSlots(StepObservation observation, long? tag)
    {
        var expected = observation.Step.Slots.OrderBy(s => s.OldSlot).ToList();
        SlotFinding Finding(SurveySlot s, int? newSlot, IReadOnlyList<int> candidates, string? note) =>
            new(s.Name, observation.Step.Object, s.OldSlot, newSlot, candidates, note);

        if (observation.Skipped)
            return expected.Select(s => Finding(s, null, Array.Empty<int>(), "skipped"));
        if (tag is not long t)
            return expected.Select(s => Finding(s, null, Array.Empty<int>(), "the object's tag wasn't decided"));

        var changed = observation.Owners.Where(o => o.Tag == t).Select(o => o.Slot).Distinct().OrderBy(s => s).ToList();
        if (changed.Count == 0)
            return expected.Select(s => Finding(s, null, changed, "no change seen in this object"));

        if (changed.Count == expected.Count)
            return expected.Select((s, i) => Finding(s, changed[i], changed,
                expected.Count > 1 ? "matched by order (assumes the group kept its order)" : null));

        if (expected.All(s => changed.Contains(s.OldSlot)))
            return expected.Select(s => Finding(s, s.OldSlot, changed,
                $"other slots changed too: {string.Join(", ", changed.Except(expected.Select(e => e.OldSlot)))}"));

        return expected.Select(s => Finding(s, null, changed,
            $"{changed.Count} slots changed, {expected.Count} expected - can't tell which"));
    }

    // ---- the fixed offsets around the slots ----

    private void CheckFixedOffsets(SurveyReport report, IReadOnlyList<StepObservation> observations)
    {
        long? Primary(SurveyObject kind) =>
            report.Tags[kind].Tag is long tag
                ? observations.Where(o => o.Step.Object == kind).SelectMany(o => o.Owners).Where(w => w.Tag == tag)
                              .GroupBy(w => w.Object).OrderByDescending(g => g.Count()).Select(g => (long?)g.Key).FirstOrDefault()
                : null;

        var master = Primary(SurveyObject.Master);
        var vm498 = Primary(SurveyObject.Vm498);
        var vm550 = Primary(SurveyObject.Vm550);
        report.MasterObject = master;
        report.Vm498Object = vm498;
        report.Vm550Object = vm550;

        if (master is long m && vm498 is long v)
            report.Vm498OffsetsInMaster = PointerOffsetsTo(m, v);

        if (master is long m2 && _mem.ReadRaw(m2 + Ams2Constants.MasterActiveFlagsOffset, 2, out var flags))
            report.MasterActiveFlagsSet = flags[0] == 1 && flags[1] == 1;

        if (vm498 is long v2 && report.Tags[SurveyObject.Vm550].Tag is long vm550Tag)
            report.SessionWrappersWithVm550Tag = Enumerable.Range(0, 8)
                .Count(i => TryReadSessionVm(v2, i, out var vm) && _mem.TryReadInt64(vm + 8, out var t) && t == vm550Tag);

        // the packed date must decode to the date VM550's own slots hold
        if (vm498 is long v3 && vm550 is long race
            && TryReadFoundSlot(report, race, nameof(Ams2Constants.Vm550Slot.Year), Ams2Constants.Vm550Slot.Year, out var year)
            && TryReadFoundSlot(report, race, nameof(Ams2Constants.Vm550Slot.Month), Ams2Constants.Vm550Slot.Month, out var month)
            && TryReadFoundSlot(report, race, nameof(Ams2Constants.Vm550Slot.Day), Ams2Constants.Vm550Slot.Day, out var day)
            && _mem.ReadRaw(v3, 0x800, out var body))
        {
            report.RaceDate = (year, month, day);
            report.PackedDateOffsets = Enumerable.Range(0, 0x800 / 4)
                .Select(i => i * 4)
                .Where(off => Vm498PackedDate.Decode(BitConverter.ToUInt32(body, off)) is var d && d.Year == year && d.Month == month && d.Day == day)
                .ToList();
        }
    }

    private bool TryReadFoundSlot(SurveyReport report, long vm, string name, int oldSlot, out int value)
    {
        var slot = report.Slots.FirstOrDefault(s => s.Name == name && s.NewSlot != null)?.NewSlot ?? oldSlot;
        return _mem.TryReadSlot(vm, slot, _intPropertyTag, out value);
    }

    private List<int> PointerOffsetsTo(long obj, long target)
    {
        var offsets = new List<int>();
        if (!_mem.ReadRaw(obj, MaxHeaderDistance, out var body)) return offsets;
        for (int off = 0x10; off <= MaxHeaderDistance - 8; off += 8)
            if (BitConverter.ToInt64(body, off) == target) offsets.Add(off);
        return offsets;
    }

    private IEnumerable<long> SessionVms(long vm498)
    {
        for (int i = 0; i < 8; i++)
            if (TryReadSessionVm(vm498, i, out var vm)) yield return vm;
    }

    private bool TryReadSessionVm(long vm498, int index, out long vm)
    {
        vm = 0;
        return _mem.TryReadPointerSafe(vm498 + 0x18 + index * 8L, out var wrapper)
               && _mem.TryReadPointerSafe(wrapper + 0x18, out vm);
    }
}

public sealed record TagFinding(SurveyObject Object, long? Tag, IReadOnlyList<long> Candidates, long? CurrentTag, string? Note);

public sealed record SlotFinding(string Name, SurveyObject Object, int OldSlot, int? NewSlot, IReadOnlyList<int> Candidates, string? Note)
{
    public bool Moved => NewSlot is int n && n != OldSlot;
}

/// <summary>What a CustomRaceSurvey found. Describe() is the developer-facing report.</summary>
public sealed class SurveyReport
{
    public long ModuleBase { get; init; }
    public IReadOnlyList<StepObservation> Observations { get; init; } = Array.Empty<StepObservation>();
    public Dictionary<SurveyObject, TagFinding> Tags { get; } = new();
    public List<SlotFinding> Slots { get; } = new();

    public long? MasterObject { get; set; }
    public long? Vm498Object { get; set; }
    public long? Vm550Object { get; set; }

    /// <summary>Where in master a pointer to VM498 was found (Ams2Constants.Vm498Offset expects exactly that one).</summary>
    public IReadOnlyList<int>? Vm498OffsetsInMaster { get; set; }
    public bool? MasterActiveFlagsSet { get; set; }
    /// <summary>Of VM498's 8 session wrappers, how many lead to a VM550-tagged object.</summary>
    public int? SessionWrappersWithVm550Tag { get; set; }
    public (int Year, int Month, int Day)? RaceDate { get; set; }
    /// <summary>Offsets in VM498 whose value decodes as RaceDate (Ams2Constants.Vm498DatePackedOffset expects it among them).</summary>
    public IReadOnlyList<int>? PackedDateOffsets { get; set; }

    public static string OffsetName(SurveyObject kind) => kind switch
    {
        SurveyObject.Vm550 => Ams2OffsetNames.Vm550IdentityTag,
        SurveyObject.Vm498 => Ams2OffsetNames.Vm498IdentityTag,
        _ => Ams2OffsetNames.Vm498ContainerTag,
    };

    /// <summary>The decided tags as known_builds values (RVAs), by offset name.</summary>
    public Dictionary<string, long> TagRvas() =>
        Tags.Values.Where(t => t.Tag != null).ToDictionary(t => OffsetName(t.Object), t => t.Tag!.Value - ModuleBase);

    public bool AllTagsDecided => Tags.Count == 3 && Tags.Values.All(t => t.Tag != null);

    public string Describe()
    {
        var lines = new List<string> { "OBJECT TAGS" };
        foreach (var t in Tags.Values.OrderBy(t => t.Object))
        {
            var name = OffsetName(t.Object);
            var current = t.CurrentTag is long c ? $"0x{c - ModuleBase:X}" : "not resolved";
            lines.Add(t.Tag is long tag
                ? $"  {name,-20} RVA 0x{tag - ModuleBase:X} ({tag - ModuleBase})  current: {current}"
                  + (t.CurrentTag is long cur && cur != tag ? "  ** CHANGED **" : "")
                  + (t.Note != null ? $"  [{t.Note}]" : "")
                : $"  {name,-20} NOT DECIDED - {t.Note}; candidates: {string.Join(", ", t.Candidates.Select(x => $"0x{x - ModuleBase:X}"))}");
        }

        if (Slots.Count > 0)
        {
            lines.Add("");
            lines.Add("SLOTS");
            foreach (var s in Slots)
            {
                var skipped = s.Note == "skipped";
                var status = s.NewSlot is int n
                    ? (n == s.OldSlot ? $"{n} (unchanged)" : $"{s.OldSlot} -> {n}  ** MOVED **")
                    : skipped ? $"skipped (was {s.OldSlot})" : $"NOT FOUND (was {s.OldSlot})";
                lines.Add($"  {$"{s.Object}.{s.Name}",-30} {status}"
                          + (s.Note != null && !skipped ? $"  [{s.Note}]" : "")
                          + (s.NewSlot == null && s.Candidates.Count > 0 ? $"  candidates: {string.Join(", ", s.Candidates)}" : ""));
            }
        }

        lines.Add("");
        lines.Add("FIXED OFFSETS");
        lines.Add(Vm498OffsetsInMaster is { } offs
            ? offs.Count == 1 && offs[0] == Ams2Constants.Vm498Offset
                ? $"  Vm498Offset 0x{Ams2Constants.Vm498Offset:X}: OK"
                : $"  Vm498Offset: expected 0x{Ams2Constants.Vm498Offset:X}, VM498 found at {(offs.Count == 0 ? "no offset" : string.Join(", ", offs.Select(o => $"0x{o:X}")))}  ** CHECK **"
            : "  Vm498Offset: not checked (master or VM498 not found)");
        lines.Add(MasterActiveFlagsSet is bool active
            ? $"  MasterActiveFlagsOffset 0x{Ams2Constants.MasterActiveFlagsOffset:X}: {(active ? "OK" : "flags not set on the master found  ** CHECK **")}"
            : "  MasterActiveFlagsOffset: not checked");
        lines.Add(SessionWrappersWithVm550Tag is int wrappers
            ? $"  session wrappers (vm498+0x18+i*8, +0x18): {wrappers} of 8 lead to a VM550{(wrappers < 8 ? "  ** CHECK **" : "")}"
            : "  session wrappers: not checked");
        lines.Add(PackedDateOffsets is { } dates && RaceDate is { } date
            ? dates.Contains(Ams2Constants.Vm498DatePackedOffset)
                ? $"  Vm498DatePackedOffset 0x{Ams2Constants.Vm498DatePackedOffset:X}: OK ({date.Year}-{date.Month:00}-{date.Day:00})"
                : $"  Vm498DatePackedOffset: {date.Year}-{date.Month:00}-{date.Day:00} not at 0x{Ams2Constants.Vm498DatePackedOffset:X}; found at {(dates.Count == 0 ? "no offset" : string.Join(", ", dates.Select(o => $"0x{o:X}")))}  ** CHECK **"
            : "  Vm498DatePackedOffset: not checked (needs the date slots and VM498)");
        return string.Join(Environment.NewLine, lines);
    }
}
