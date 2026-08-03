namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Everything read from a .bff pak needed to repack it: the raw header bytes (unchanged by
    /// this tool, since entry count never changes), the RC4 KeyIndex used for this specific pak,
    /// the decoded TOC entries, and the ext-header/ext-info/filename-table tail copied verbatim
    /// (this tool never adds/renames files, so that block is always byte-identical on repack -
    /// see AMS2-livery-modding-knowledge.md's "Files that turned out NOT to matter" /
    /// repacking notes).
    /// </summary>
    public sealed class BffPakSnapshot
    {
        public required byte[] RawHeaderBytes { get; init; }
        public required int KeyIndex { get; init; }
        public required byte EncryptionType { get; init; }
        public required uint FileCount { get; init; }
        public required List<BffTocEntry> Entries { get; init; }
        public required byte[] TailBytes { get; init; }

        /// <summary>Full original file bytes, kept so untouched entries' data can be copied verbatim.</summary>
        public required byte[] FullFileBytes { get; init; }
    }
}
