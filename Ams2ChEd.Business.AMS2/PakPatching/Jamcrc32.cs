namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// CRC-32/JAMCRC: reflected CRC-32 (poly 0xEDB88320), init 0xFFFFFFFF, no final XOR.
    /// This is the per-entry CRC AMS2 validates when loading a .bff pak ("CRC error loading
    /// file" if stale) - see AMS2-livery-modding-knowledge.md, "Per-entry CRC" section. Must be
    /// computed over the exact on-disk bytes (RC4-encrypted + zlib-compressed), not the plaintext.
    /// </summary>
    public static class Jamcrc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            const uint poly = 0xEDB88320;
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }
}
