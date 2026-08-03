namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Computes the 64-bit UID a .bff TOC entry stores for its relative path (Bob Jenkins'
    /// "lookup8" 64-bit hash, applied to the lowercased, backslash-normalized path). Used to find
    /// a specific file's TOC entry by testing a candidate relative path's hash for membership,
    /// instead of decoding the separately-encrypted ext-info/filename table (which uses a
    /// different, harder-to-validate cipher AMS2 calls "Scribe") - see
    /// AMS2-livery-modding-knowledge.md's "ext-info" notes. Ported from PCarsTools
    /// (github.com/Nenkai/PCarsTools, PCarsTools/Base/BHashCode.cs, MIT licensed), which itself
    /// credits Bob Jenkins' public-domain lookup8.c.
    /// </summary>
    public static class BffPathHash
    {
        public static ulong ComputeUid(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return 0x8DB63936938575BF;

            string normalized = relativePath.ToLowerInvariant().Replace('/', '\\');
            return HashCode64(normalized, 0);
        }

        private static ulong HashCode64(string k, ulong initval)
        {
            int length = k.Length;
            ulong len = (ulong)length;
            ulong a = initval, b = initval;
            ulong c = 0x9e3779b97f4a7c13;

            int pos = 0;
            while (len >= 24)
            {
                a += ((ulong)k[pos + 0] << 56) + ((ulong)k[pos + 1] << 48) + ((ulong)k[pos + 2] << 40) + ((ulong)k[pos + 3] << 32)
                    + ((ulong)k[pos + 4] << 24) + ((ulong)k[pos + 5] << 16) + ((ulong)k[pos + 6] << 8) + k[pos + 7];

                b += ((ulong)k[pos + 8] << 56) + ((ulong)k[pos + 9] << 48) + ((ulong)k[pos + 10] << 40) + ((ulong)k[pos + 11] << 32)
                    + ((ulong)k[pos + 12] << 24) + ((ulong)k[pos + 13] << 16) + ((ulong)k[pos + 14] << 8) + k[pos + 15];

                c += ((ulong)k[pos + 16] << 56) + ((ulong)k[pos + 17] << 48) + ((ulong)k[pos + 18] << 40) + ((ulong)k[pos + 19] << 32)
                    + ((ulong)k[pos + 20] << 24) + ((ulong)k[pos + 21] << 16) + ((ulong)k[pos + 22] << 8) + k[pos + 23];

                Mix(ref a, ref b, ref c);
                pos += 24;
                len -= 24;
            }

            c += (ulong)length;

            if (len > 0)
            {
                int remaining = (int)len;
                // Falls through highest-to-lowest exactly like the original C switch, since each
                // remaining byte's contribution depends on its own offset within a,b,c.
                if (remaining >= 23) c += (ulong)k[pos + 22] << 56;
                if (remaining >= 22) c += (ulong)k[pos + 21] << 48;
                if (remaining >= 21) c += (ulong)k[pos + 20] << 40;
                if (remaining >= 20) c += (ulong)k[pos + 19] << 32;
                if (remaining >= 19) c += (ulong)k[pos + 18] << 24;
                if (remaining >= 18) c += (ulong)k[pos + 17] << 16;
                if (remaining >= 17) c += (ulong)k[pos + 16] << 8;
                if (remaining >= 16) b += (ulong)k[pos + 15] << 56;
                if (remaining >= 15) b += (ulong)k[pos + 14] << 48;
                if (remaining >= 14) b += (ulong)k[pos + 13] << 40;
                if (remaining >= 13) b += (ulong)k[pos + 12] << 32;
                if (remaining >= 12) b += (ulong)k[pos + 11] << 24;
                if (remaining >= 11) b += (ulong)k[pos + 10] << 16;
                if (remaining >= 10) b += (ulong)k[pos + 9] << 8;
                if (remaining >= 9) b += k[pos + 8];
                if (remaining >= 8) a += (ulong)k[pos + 7] << 56;
                if (remaining >= 7) a += (ulong)k[pos + 6] << 48;
                if (remaining >= 6) a += (ulong)k[pos + 5] << 40;
                if (remaining >= 5) a += (ulong)k[pos + 4] << 32;
                if (remaining >= 4) a += (ulong)k[pos + 3] << 24;
                if (remaining >= 3) a += (ulong)k[pos + 2] << 16;
                if (remaining >= 2) a += (ulong)k[pos + 1] << 8;
                if (remaining >= 1) a += k[pos + 0];
            }

            Mix(ref a, ref b, ref c);
            return c;
        }

        private static void Mix(ref ulong a, ref ulong b, ref ulong c)
        {
            a -= b; a -= c; a ^= c >> 43;
            b -= c; b -= a; b ^= a << 9;
            c -= a; c -= b; c ^= b >> 8;
            a -= b; a -= c; a ^= c >> 38;
            b -= c; b -= a; b ^= a << 23;
            c -= a; c -= b; c ^= b >> 5;
            a -= b; a -= c; a ^= c >> 35;
            b -= c; b -= a; b ^= a << 49;
            c -= a; c -= b; c ^= b >> 11;
            a -= b; a -= c; a ^= c >> 12;
            b -= c; b -= a; b ^= a << 18;
            c -= a; c -= b; c ^= b >> 22;
        }
    }
}
