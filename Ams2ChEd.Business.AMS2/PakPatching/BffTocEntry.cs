namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// One 42-byte (0x2A) TOC record from a .bff pak. See
    /// AMS2-livery-modding-knowledge.md's "TOC" section for the exact byte layout.
    /// </summary>
    public sealed class BffTocEntry
    {
        /// <summary>Index of this entry within the TOC, i.e. its declared (not physical) order.</summary>
        public int Index { get; set; }

        /// <summary>Hash of the entry's relative path - see <see cref="BffPathHash"/>.</summary>
        public ulong Uid { get; set; }

        /// <summary>Absolute file offset of this entry's data.</summary>
        public long DataOffset { get; set; }

        /// <summary>On-disk size (compressed + encrypted).</summary>
        public uint PakSize { get; set; }

        /// <summary>Uncompressed/plaintext size.</summary>
        public uint OriginalSize { get; set; }

        public ulong ModifiedTime { get; set; }

        /// <summary>0=None, 1=ZLib, 2=LZX, 3=Mermaid, 4=Kraken.</summary>
        public byte CompressionType { get; set; }

        public byte UnknownFlag { get; set; }

        public uint Crc { get; set; }

        /// <summary>4-ASCII-char extension, e.g. "rcf ".</summary>
        public string Extension { get; set; } = string.Empty;
    }
}
