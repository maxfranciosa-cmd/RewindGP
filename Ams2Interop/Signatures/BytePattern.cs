using System.Globalization;
using System.Text;

namespace Ams2Interop.Signatures;

/// <summary>
/// A byte pattern with wildcards, written the usual way: "48 89 5C 24 ?? 57". Wildcards stand for
/// the bytes of an instruction that change when the game is rebuilt (call targets, RIP-relative
/// displacements) - see SignatureBuilder.
/// </summary>
public sealed class BytePattern
{
    private readonly byte[] _bytes;
    private readonly bool[] _known;

    // the longest run of known bytes, searched for first with a vectorized IndexOf
    private readonly int _anchorStart;
    private readonly int _anchorLength;

    public int Length => _bytes.Length;

    public BytePattern(byte[] bytes, bool[] known)
    {
        if (bytes.Length != known.Length) throw new ArgumentException("bytes and mask lengths differ");
        if (bytes.Length == 0) throw new ArgumentException("empty pattern");
        _bytes = bytes;
        _known = known;
        (_anchorStart, _anchorLength) = FindLongestKnownRun(known);
        if (_anchorLength == 0) throw new ArgumentException("pattern has no known bytes");
    }

    public static BytePattern Parse(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[tokens.Length];
        var known = new bool[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i] is "??" or "?")
                continue;
            bytes[i] = byte.Parse(tokens[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            known[i] = true;
        }
        return new BytePattern(bytes, known);
    }

    public override string ToString()
    {
        var sb = new StringBuilder(_bytes.Length * 3);
        for (int i = 0; i < _bytes.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(_known[i] ? _bytes[i].ToString("X2", CultureInfo.InvariantCulture) : "??");
        }
        return sb.ToString();
    }

    public bool MatchesAt(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset + _bytes.Length > data.Length) return false;
        for (int i = 0; i < _bytes.Length; i++)
        {
            if (_known[i] && data[offset + i] != _bytes[i]) return false;
        }
        return true;
    }

    /// <summary>Every offset in <paramref name="data"/> where the pattern matches, stopping after <paramref name="limit"/> hits.</summary>
    public List<int> FindAll(ReadOnlySpan<byte> data, int limit = int.MaxValue)
    {
        var hits = new List<int>();
        var anchor = _bytes.AsSpan(_anchorStart, _anchorLength);
        int searchFrom = _anchorStart;

        while (searchFrom <= data.Length - (_bytes.Length - _anchorStart))
        {
            int idx = data[searchFrom..].IndexOf(anchor);
            if (idx < 0) break;
            int anchorAt = searchFrom + idx;
            int start = anchorAt - _anchorStart;
            if (MatchesAt(data, start))
            {
                hits.Add(start);
                if (hits.Count >= limit) break;
            }
            searchFrom = anchorAt + 1;
        }
        return hits;
    }

    private static (int Start, int Length) FindLongestKnownRun(bool[] known)
    {
        int bestStart = 0, bestLength = 0, runStart = 0;
        for (int i = 0; i <= known.Length; i++)
        {
            if (i < known.Length && known[i]) continue;
            int runLength = i - runStart;
            if (runLength > bestLength)
            {
                bestStart = runStart;
                bestLength = runLength;
            }
            runStart = i + 1;
        }
        return (bestStart, bestLength);
    }
}
