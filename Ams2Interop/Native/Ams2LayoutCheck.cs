namespace Ams2Interop.Native;

/// <summary>
/// A read-only check, run before anything is written to a Custom Race object, that the object
/// still has the layout this library's slot numbers assume. A patch that shifts slot indices,
/// moves the value offset (node+0x18c) or changes the int-property tag would otherwise only show
/// up after the write - as a value landing in some other setting.
///
/// Each listed slot is read through exactly the path every write and read-back uses
/// (MemoryReads.ReadSlot: node = *(object + slot*8), tag at node-0x18, value at node+0x18c) and
/// its CURRENT value is compared against a deliberately conservative expected set - the values
/// AMS2 itself can hold there, whatever the player has selected.
///
/// RULE: all must pass. One slot that is unreadable, has the wrong tag, or holds a value outside
/// its set fails the whole object, and Ams2RaceConfigurator then writes nothing to it.
///
/// Only ranges already documented in Ams2Constants (or self-evident, like an hour or a month) are
/// listed. A slot whose range isn't clearly established is left out on purpose: a false failure
/// here blocks races, so a missing check is the safer error. Left out for that reason: VM550
/// DurationMinutes/LapCount (no documented bounds), PrivateQuali/MandatoryPit/Refuelling (written
/// as 0/1 but their stored range isn't confirmed), TimeProgression (1-4 written, other stored
/// values unknown), WeatherType (see its CONFLICTING note), DateType, SessionEnabled and its
/// paired slots; VM498 MaxOpponents, RivalCountPerClass and Aggression.
/// </summary>
public sealed class Ams2LayoutCheck
{
    /// <summary>One slot to check: its name (for messages), slot number and accepted values.</summary>
    public sealed record Expectation(string Name, int Slot, string Expected, Func<int, bool> Accepts);

    /// <summary>A slot that failed: unreadable, wrongly tagged or out of range (<see cref="Value"/> is null unless a value was read).</summary>
    public sealed record Failure(string Name, int Slot, string Problem, int? Value)
    {
        public override string ToString() => $"{Name} (slot {Slot}) {Problem}";
    }

    public sealed class Result
    {
        public IReadOnlyList<Failure> Failures { get; init; } = Array.Empty<Failure>();

        /// <summary>Every value that read cleanly, by Expectation.Name - including in-range ones, for diagnostics.</summary>
        public IReadOnlyDictionary<string, int> Values { get; init; } = new Dictionary<string, int>();

        public bool Passed => Failures.Count == 0;

        public string Describe() => Passed ? "layout OK" : string.Join("; ", Failures);
    }

    private static bool Between(int value, int min, int max) => value >= min && value <= max;

    /// <summary>
    /// VM550-shaped objects: the race session (VM550 itself) and each per-session VM (Practice1,
    /// Qualifying1, Race2) - see Ams2Constants.Vm550Slot for where each range comes from.
    /// </summary>
    public static readonly IReadOnlyList<Expectation> Vm550 = new[]
    {
        new Expectation(nameof(Ams2Constants.Vm550Slot.WeatherSlotCount), Ams2Constants.Vm550Slot.WeatherSlotCount, "0-4", v => Between(v, 0, 4)),
        new Expectation(nameof(Ams2Constants.Vm550Slot.WeatherSlot1), Ams2Constants.Vm550Slot.WeatherSlot1, "0-17", v => Between(v, 0, 17)),
        new Expectation(nameof(Ams2Constants.Vm550Slot.WeatherSlot2), Ams2Constants.Vm550Slot.WeatherSlot2, "0-17", v => Between(v, 0, 17)),
        new Expectation(nameof(Ams2Constants.Vm550Slot.WeatherSlot3), Ams2Constants.Vm550Slot.WeatherSlot3, "0-17", v => Between(v, 0, 17)),
        new Expectation(nameof(Ams2Constants.Vm550Slot.WeatherSlot4), Ams2Constants.Vm550Slot.WeatherSlot4, "0-17", v => Between(v, 0, 17)),
        new Expectation(nameof(Ams2Constants.Vm550Slot.Hour), Ams2Constants.Vm550Slot.Hour, "0-23", v => Between(v, 0, 23)),
        new Expectation(nameof(Ams2Constants.Vm550Slot.Day), Ams2Constants.Vm550Slot.Day, "1-31", v => Between(v, 1, 31)),
        new Expectation(nameof(Ams2Constants.Vm550Slot.Month), Ams2Constants.Vm550Slot.Month, "1-12", v => Between(v, 1, 12)),
        new Expectation(nameof(Ams2Constants.Vm550Slot.Year), Ams2Constants.Vm550Slot.Year, "1900-2100", v => Between(v, 1900, 2100)),
        new Expectation(nameof(Ams2Constants.Vm550Slot.DurationTypeFlag), Ams2Constants.Vm550Slot.DurationTypeFlag, "{0, 1}", v => v is 0 or 1),
        new Expectation(nameof(Ams2Constants.Vm550Slot.RollingStart), Ams2Constants.Vm550Slot.RollingStart, "{0, 1}", v => v is 0 or 1),
        new Expectation(nameof(Ams2Constants.Vm550Slot.MinTyres), Ams2Constants.Vm550Slot.MinTyres, "{-1, 0, 2, 4}", v => v is -1 or 0 or 2 or 4),
        new Expectation(nameof(Ams2Constants.Vm550Slot.MinFuel), Ams2Constants.Vm550Slot.MinFuel, "{-1, 0, 2, 4}", v => v is -1 or 0 or 2 or 4),
        new Expectation(nameof(Ams2Constants.Vm550Slot.ScheduledFcy), Ams2Constants.Vm550Slot.ScheduledFcy, "0-5", v => Between(v, 0, 5)),
    };

    /// <summary>
    /// The highest opponent count accepted as a stored RivalCount. Not a documented AMS2 limit - a
    /// safe ceiling well above any grid AMS2 builds, low enough that a value from a shifted layout
    /// (a hash, a pointer half, a flag word) won't pass.
    /// </summary>
    public const int MaxPlausibleRivalCount = 64;

    /// <summary>VM498 (race entrants) - see Ams2Constants.Vm498Slot and the Enums the ranges come from.</summary>
    public static readonly IReadOnlyList<Expectation> Vm498 = new[]
    {
        new Expectation(nameof(Ams2Constants.Vm498Slot.NumOpponentsType), Ams2Constants.Vm498Slot.NumOpponentsType, "0-2", v => Between(v, 0, 2)),
        new Expectation(nameof(Ams2Constants.Vm498Slot.OpponentsTypeKind), Ams2Constants.Vm498Slot.OpponentsTypeKind, "0-2", v => Between(v, 0, 2)),
        new Expectation(nameof(Ams2Constants.Vm498Slot.Skill), Ams2Constants.Vm498Slot.Skill, "70-120", v => Between(v, 70, 120)),
        new Expectation(nameof(Ams2Constants.Vm498Slot.WetWeatherSkill), Ams2Constants.Vm498Slot.WetWeatherSkill, "0-200", v => Between(v, 0, 200)),
        new Expectation(nameof(Ams2Constants.Vm498Slot.MistakeFrequency), Ams2Constants.Vm498Slot.MistakeFrequency, "0-6", v => Between(v, 0, 6)),
        new Expectation(nameof(Ams2Constants.Vm498Slot.RivalCount), Ams2Constants.Vm498Slot.RivalCount, $"0-{MaxPlausibleRivalCount}", v => Between(v, 0, MaxPlausibleRivalCount)),
    };

    /// <summary>
    /// master's own selection slots - what CheckSetCarReadBack reads after SetCar. Their values are
    /// hashes/indices with no range to check; they only have to read cleanly with a valid tag, or
    /// SetCar's success couldn't be verified at all.
    /// </summary>
    public static readonly IReadOnlyList<Expectation> Master = new[]
    {
        new Expectation(nameof(Ams2Constants.MasterSlot.Track), Ams2Constants.MasterSlot.Track, "any (readable)", _ => true),
        new Expectation(nameof(Ams2Constants.MasterSlot.Car), Ams2Constants.MasterSlot.Car, "any (readable)", _ => true),
        new Expectation(nameof(Ams2Constants.MasterSlot.Livery), Ams2Constants.MasterSlot.Livery, "any (readable)", _ => true),
    };

    private readonly IMemoryReader _mem;
    private readonly long _intPropertyTag;

    /// <param name="intPropertyTag">The absolute int-property tag for the running build (Ams2Constants.IntPropertyTypeTag) - a parameter so tests needn't touch the process-wide offsets.</param>
    public Ams2LayoutCheck(IMemoryReader mem, long intPropertyTag)
    {
        _mem = mem;
        _intPropertyTag = intPropertyTag;
    }

    public Result CheckVm550(long vm) => Check(vm, Vm550);
    public Result CheckVm498(long vm498) => Check(vm498, Vm498);
    public Result CheckMaster(long master) => Check(master, Master);

    /// <summary>Reads every expectation's slot on <paramref name="objectBase"/>; passes only if all read cleanly and are accepted.</summary>
    public Result Check(long objectBase, IReadOnlyList<Expectation> expectations)
    {
        var failures = new List<Failure>();
        var values = new Dictionary<string, int>();
        foreach (var e in expectations)
        {
            switch (_mem.ReadSlot(objectBase, e.Slot, _intPropertyTag, out var value))
            {
                case SlotReadStatus.Ok:
                    values[e.Name] = value;
                    if (!e.Accepts(value)) failures.Add(new Failure(e.Name, e.Slot, $"= {value}, expected {e.Expected}", value));
                    break;
                case SlotReadStatus.NodeUnreadable:
                    failures.Add(new Failure(e.Name, e.Slot, "unreadable (no plausible node pointer)", null));
                    break;
                case SlotReadStatus.WrongTag:
                    failures.Add(new Failure(e.Name, e.Slot, "wrong tag (not an int property)", null));
                    break;
                default:
                    failures.Add(new Failure(e.Name, e.Slot, "value unreadable", null));
                    break;
            }
        }
        return new Result { Failures = failures, Values = values };
    }
}
