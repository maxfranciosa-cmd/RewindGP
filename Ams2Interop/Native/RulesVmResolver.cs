namespace Ams2Interop.Native;

/// <summary>
/// Finds the Custom Race's own Rules &amp; Regulations object (pit lane speed limit, full course
/// yellow - see Ams2Constants.RulesSlot).
///
/// AMS2 keeps several objects with this tag (nine in the live check of 2026-11-18): one per game
/// mode or menu, plus the Rules page's shared working copy, which the page fills from the event's
/// own object when it opens. A tag scan alone can't tell them apart. The event's own one is the
/// one stored right next to the live master and VM498: the game keeps them together as
/// consecutive pointers [master][vm498][rules] (a context object, twice), and the custom event
/// object holds them 16 bytes apart (master +0xF0, vm498 +0x100, rules +0x110). So the scan looks
/// for master's pointer value and reads the rules pointer at either of those two places.
///
/// Conservative on purpose: a candidate must carry the Rules tag, start with a vtable in the
/// module and pass Ams2LayoutCheck.Rules; every place found must point at the same object, or
/// none is taken.
/// </summary>
public sealed class RulesVmResolver
{
    /// <summary>Where vm498 and the rules object sit after master's pointer: consecutive, or 16 bytes apart.</summary>
    private static readonly (int Vm498, int Rules)[] Layouts = { (8, 0x10), (0x10, 0x20) };

    private readonly IScannableMemory _mem;
    private readonly long _moduleBase;
    private readonly long _moduleSize;
    private readonly long _rulesTag;
    private readonly Ams2LayoutCheck _check;
    private readonly Action<string>? _log;

    /// <param name="rulesTag">Ams2Constants.RulesIdentityTag for the running build - a parameter so tests needn't touch the process-wide offsets.</param>
    /// <param name="intPropertyTag">Ams2Constants.IntPropertyTypeTag for the running build.</param>
    public RulesVmResolver(IScannableMemory mem, long moduleBase, long moduleSize, long rulesTag, long intPropertyTag, Action<string>? log = null)
    {
        _mem = mem;
        _moduleBase = moduleBase;
        _moduleSize = moduleSize;
        _rulesTag = rulesTag;
        _check = new Ams2LayoutCheck(mem, intPropertyTag);
        _log = log;
    }

    /// <summary>The rules object stored beside <paramref name="master"/> and <paramref name="vm498"/>, or null (logged) when there's none or they disagree.</summary>
    public long? Resolve(long master, long vm498)
    {
        var found = new HashSet<long>();
        var rejected = new HashSet<long>();
        foreach (var hit in MemoryScanner.FindOccurrences(_mem, master))
        {
            foreach (var (vm498Offset, rulesOffset) in Layouts)
            {
                if (!_mem.TryReadInt64(hit + vm498Offset, out var v) || v != vm498) continue;
                if (!_mem.TryReadInt64(hit + rulesOffset, out var candidate) || found.Contains(candidate) || rejected.Contains(candidate)) continue;
                if (IsRulesObject(candidate)) found.Add(candidate);
                else rejected.Add(candidate);
            }
        }

        if (found.Count == 1)
        {
            var rules = found.First();
            _log?.Invoke($"RulesVmResolver: rules object 0x{rules:X}");
            return rules;
        }
        _log?.Invoke(found.Count == 0
            ? $"RulesVmResolver: no rules object found beside master 0x{master:X} / vm498 0x{vm498:X}"
              + (rejected.Count > 0 ? $" ({rejected.Count} pointer(s) there aren't one)" : "")
            : $"RulesVmResolver: {found.Count} different rules objects beside master ({string.Join(", ", found.Select(f => $"0x{f:X}"))}) - none taken");
        return null;
    }

    private bool IsRulesObject(long candidate)
    {
        if (!_mem.TryReadInt64(candidate, out var vtable) || vtable < _moduleBase || vtable >= _moduleBase + _moduleSize) return false;
        if (!_mem.TryReadInt64(candidate + 8, out var tag) || tag != _rulesTag) return false;
        var layout = _check.CheckRules(candidate);
        if (!layout.Passed) _log?.Invoke($"RulesVmResolver: 0x{candidate:X} has the rules tag but fails the layout check - {layout.Describe()}");
        return layout.Passed;
    }
}
