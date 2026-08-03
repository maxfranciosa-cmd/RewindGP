namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// RC4 stream cipher plus AMS2's key-descrambling pass, as used to encrypt/decrypt .bff pak
    /// TOC and entry data. Ported from PCarsTools (github.com/Nenkai/PCarsTools,
    /// PCarsTools/Encryption/RC4.cs and BPakFileEncryption.DecryptKey), MIT licensed.
    /// RC4 is a pure XOR stream cipher (KSA+PRGA), so encrypt and decrypt are the identical
    /// operation - the same Transform method is used both ways.
    /// </summary>
    public static class Rc4Cipher
    {
        private static readonly byte[] DescrambleXor = { 0xAC, 0xC7, 0x91 };

        /// <summary>
        /// Trims the raw key at its first 0x00 byte (raw keys are null-padded to 27 bytes), then
        /// applies AMS2's descrambling pass: process 2 bytes at a time, XOR each with the next
        /// byte of the repeating 3-byte pattern {0xAC,0xC7,0x91}, then swap the pair; any trailing
        /// odd byte is XORed (not swapped) with the next pattern byte.
        /// </summary>
        public static byte[] Descramble(ReadOnlySpan<byte> rawKey)
        {
            int len = rawKey.IndexOf((byte)0x00);
            if (len < 0) len = rawKey.Length;

            byte[] key = rawKey[..len].ToArray();

            int tIndex = 0;
            int i;
            for (i = 0; i + 1 < key.Length; i += 2)
            {
                byte tmp1 = (byte)(DescrambleXor[tIndex++] ^ key[i]);
                tIndex %= 3;

                byte tmp2 = (byte)(DescrambleXor[tIndex++] ^ key[i + 1]);
                tIndex %= 3;

                // Reversed
                key[i] = tmp2;
                key[i + 1] = tmp1;
            }

            for (; i < key.Length; i++)
            {
                tIndex %= 3;
                key[i] ^= DescrambleXor[tIndex++];
            }

            return key;
        }

        /// <summary>
        /// Encrypts/decrypts <paramref name="data"/> in place using RC4 (KSA+PRGA) with the given
        /// already-descrambled key.
        /// </summary>
        public static void Transform(Span<byte> data, ReadOnlySpan<byte> key)
        {
            var box = new int[256];
            var keyBytes = new int[256];
            for (int i = 0; i < 256; i++)
            {
                keyBytes[i] = key[i % key.Length];
                box[i] = i;
            }

            for (int i = 0, j = 0; i < 256; i++)
            {
                j = (j + box[i] + keyBytes[i]) % 256;
                (box[i], box[j]) = (box[j], box[i]);
            }

            for (int i = 0, a = 0, j = 0; i < data.Length; i++)
            {
                a = (a + 1) % 256;
                j = (j + box[a]) % 256;
                (box[a], box[j]) = (box[j], box[a]);
                int k = box[(box[a] + box[j]) % 256];
                data[i] = (byte)(data[i] ^ k);
            }
        }
    }
}
