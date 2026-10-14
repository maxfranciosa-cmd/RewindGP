using System.Buffers.Binary;

namespace Ams2Interop.Signatures;

/// <summary>A pattern for an instruction that references an address (RIP-relative), plus where to read that address back out of a match.</summary>
public sealed record DataRefSignature(BytePattern Pattern, int DispOffset, int InstructionLength);

/// <summary>
/// Builds and resolves signatures over an executable section's bytes. Everything here is a pure
/// function of those bytes, so it's unit-testable without a running game:
///
/// - a CODE signature is the start of a function, decoded instruction by instruction, with every
///   call/jump target and RIP-relative displacement wildcarded (those are what move between AMS2
///   builds), grown until it matches exactly one place.
/// - a DATA-REF signature finds the code that references a data address (a type tag, a global
///   pointer) and builds the same kind of pattern starting at that instruction; the address is
///   then read back out of the instruction's own displacement on the new build.
/// </summary>
public static class SignatureBuilder
{
    public const int DefaultMinLength = 16;
    public const int DefaultMaxLength = 128;

    // references tried per data address when building a data-ref signature
    private const int MaxReferenceCandidates = 64;

    /// <summary>
    /// The shortest unique pattern starting at <paramref name="offset"/>, or null when none fits
    /// within <paramref name="maxLength"/> bytes before the function ends (ret/int3) or an
    /// instruction can't be decoded.
    /// </summary>
    public static BytePattern? BuildCodeSignature(byte[] text, int offset, int minLength = DefaultMinLength, int maxLength = DefaultMaxLength)
        => BuildUniquePatternFrom(text, offset, minLength, maxLength, out _);

    /// <summary>
    /// A unique pattern for one of the instructions that reference <paramref name="targetRva"/>
    /// RIP-relatively - the shortest of the first few candidates - or null when no reference
    /// yields one.
    /// </summary>
    public static DataRefSignature? BuildDataRefSignature(byte[] text, long textRva, long targetRva,
        int minLength = DefaultMinLength, int maxLength = DefaultMaxLength)
    {
        DataRefSignature? best = null;
        foreach (var reference in FindRipReferences(text, textRva, targetRva).Take(MaxReferenceCandidates))
        {
            var pattern = BuildUniquePatternFrom(text, reference, minLength, maxLength, out var first);
            if (pattern == null) continue;
            if (best == null || pattern.Length < best.Pattern.Length)
                best = new DataRefSignature(pattern, first.RipDisp32Offset, first.Length);
        }
        return best;
    }

    /// <summary>Offsets in <paramref name="text"/> of every instruction whose RIP-relative operand points at <paramref name="targetRva"/>.</summary>
    public static IEnumerable<int> FindRipReferences(byte[] text, long textRva, long targetRva)
    {
        for (int i = 0; i + 4 <= text.Length; i++)
        {
            int disp = BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(i, 4));

            // target = rva(end of instruction) + disp, and the instruction ends 0-4 bytes after
            // the displacement (an immediate may follow it) - cheap pre-filter before decoding
            long distance = targetRva - (textRva + i + 4 + (long)disp);
            if (distance < 0 || distance > 4) continue;

            // the displacement sits 2-7 bytes into its instruction (prefixes, REX, opcode, ModRM)
            for (int start = Math.Max(0, i - 7); start <= i - 2; start++)
            {
                if (!X64LengthDecoder.TryDecode(text.AsSpan(start), out var ins)) continue;
                if (ins.RipDisp32Offset != i - start) continue;
                if (textRva + start + ins.Length + disp != targetRva) continue;
                yield return start;
                break;
            }
        }
    }

    /// <summary>The single offset where <paramref name="pattern"/> matches, or null when it matches nowhere or more than once.</summary>
    public static int? ResolveUnique(byte[] text, BytePattern pattern)
    {
        var hits = pattern.FindAll(text, limit: 2);
        return hits.Count == 1 ? hits[0] : null;
    }

    /// <summary>The RVA a data-ref signature points at on this build, or null when it doesn't match exactly once.</summary>
    public static long? ResolveDataRef(byte[] text, long textRva, DataRefSignature signature)
    {
        if (ResolveUnique(text, signature.Pattern) is not int hit) return null;
        if (hit + signature.DispOffset + 4 > text.Length) return null;
        int disp = BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(hit + signature.DispOffset, 4));
        return textRva + hit + signature.InstructionLength + disp;
    }

    private static BytePattern? BuildUniquePatternFrom(byte[] text, int offset, int minLength, int maxLength, out DecodedInstruction firstInstruction)
    {
        firstInstruction = default;
        if (offset < 0 || offset >= text.Length) return null;

        var bytes = new List<byte>();
        var known = new List<bool>();
        int position = offset;
        bool first = true;

        while (bytes.Count < maxLength && position < text.Length)
        {
            if (!X64LengthDecoder.TryDecode(text.AsSpan(position), out var ins)) return null;
            if (first)
            {
                firstInstruction = ins;
                first = false;
            }

            for (int k = 0; k < ins.Length; k++)
            {
                bytes.Add(text[position + k]);
                known.Add(!IsRelocatable(ins, k));
            }
            position += ins.Length;

            if (bytes.Count >= minLength)
            {
                var pattern = new BytePattern(bytes.ToArray(), known.ToArray());
                var hits = pattern.FindAll(text, limit: 2);
                if (hits.Count == 1 && hits[0] == offset) return pattern;
            }

            // end of the function: anything after this belongs to whatever the linker put next
            byte opcode = text[position - ins.Length + PrefixLength(text, position - ins.Length)];
            if (opcode is 0xC3 or 0xCC) return null;
        }
        return null;
    }

    private static bool IsRelocatable(DecodedInstruction ins, int byteIndex) =>
        (ins.Rel32Offset >= 0 && byteIndex >= ins.Rel32Offset && byteIndex < ins.Rel32Offset + 4)
        || (ins.RipDisp32Offset >= 0 && byteIndex >= ins.RipDisp32Offset && byteIndex < ins.RipDisp32Offset + 4)
        || (ins.Imm64Offset >= 0 && byteIndex >= ins.Imm64Offset && byteIndex < ins.Imm64Offset + 8);

    // legacy prefixes + REX before the opcode byte, so ret/int3 are recognised even when prefixed (e.g. "F3 C3")
    private static int PrefixLength(byte[] text, int start)
    {
        int n = 0;
        while (start + n < text.Length && text[start + n] is 0x66 or 0x67 or 0xF0 or 0xF2 or 0xF3 or 0x2E or 0x36 or 0x3E or 0x26 or 0x64 or 0x65)
            n++;
        if (start + n < text.Length && text[start + n] >= 0x40 && text[start + n] <= 0x4F)
            n++;
        return n;
    }
}
