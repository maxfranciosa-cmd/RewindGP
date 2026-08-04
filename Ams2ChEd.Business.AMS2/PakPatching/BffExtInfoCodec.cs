using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Decodes/encodes a .bff pak's ext-info block: the plaintext 0x308-byte ext-header, followed
    /// by a <see cref="ScribeCipher"/>-encrypted table of 16-byte entries (each an absolute
    /// file-offset pointing at a 1-byte-length-prefixed filename string, plus a modified-time) and
    /// the filename string table itself. This is the block <see cref="BffPakEntryInserter"/> must
    /// keep internally consistent when it grows a pak's TOC - see that class's doc comment for why
    /// it matters, and AMS2-livery-modding-knowledge.md's ext-info notes.
    ///
    /// Format confirmed empirically against a real, never-yet-patched AMS2 pak: every one of a
    /// pak's TOC entries has a same-index ext-info entry whose NameOffset, read as an *absolute*
    /// file offset and treated as `[1-byte length][that many ASCII chars]`, decodes to a path whose
    /// BffPathHash.ComputeUid exactly matches that TOC entry's own UID (34/34 matched, zero
    /// mismatches, on an unpatched pak). Also confirmed the outer header's mExtInfoSize field
    /// stores 0x308 + the entries-table-and-string-table size *before* 16-byte alignment (not the
    /// aligned on-disk size) - derived from a pak whose ext-header's own inner mInfoSize field
    /// (plaintext, unencrypted) was always exactly outer-mExtInfoSize-minus-0x308.
    /// </summary>
    public static class BffExtInfoCodec
    {
        /// <summary>Size of the plaintext ext-header preceding the encrypted entries/string table.</summary>
        public const int ExtHeaderSize = 0x308;
        private const int EntryRecordSize = 0x10;

        public sealed class Entry
        {
            public required ulong NameOffset { get; init; }
            public required ulong ModifiedTime { get; init; }
            public required string Path { get; init; }
        }

        public sealed class Decoded
        {
            /// <summary>Plaintext, verbatim, never touched by this tool (mConfigName/mTargetRoot/mPlatformName).</summary>
            public required byte[] ExtHeaderBytes { get; init; }

            /// <summary>Index-aligned with the pak's TOC entries.</summary>
            public required List<Entry> Entries { get; init; }

            /// <summary>Any bytes after the (aligned) entries+string table and before the first entry's data - copied verbatim.</summary>
            public required byte[] ExtraGapBytes { get; init; }
        }

        /// <summary>
        /// Decodes the ext-info-and-gap block (everything between the end of the TOC and the first
        /// entry's data - <c>extInfoAndGap</c> in <see cref="BffPakEntryInserter"/>), for a pak
        /// whose ext-info NameOffset values are still consistent with this block's own current
        /// absolute position in the file. <paramref name="blockAbsoluteStart"/> is that position
        /// (i.e. HeaderSize + current TOC size).
        /// </summary>
        public static Decoded Decode(byte[] extInfoAndGap, uint declaredExtInfoSize, int blockAbsoluteStart, int fileCount)
        {
            byte[] extHeaderBytes = extInfoAndGap.AsSpan(0, ExtHeaderSize).ToArray();

            int entriesAndStringsSizeAligned = (int)AlignUp(declaredExtInfoSize - ExtHeaderSize, 0x10);
            byte[] entriesAndStrings = extInfoAndGap.AsSpan(ExtHeaderSize, entriesAndStringsSizeAligned).ToArray();

            var cipher = new ScribeCipher();
            cipher.Decrypt(MemoryMarshal.Cast<byte, uint>(entriesAndStrings.AsSpan()));

            int baseExtOffset = blockAbsoluteStart + ExtHeaderSize;

            var entries = new List<Entry>(fileCount);
            for (int i = 0; i < fileCount; i++)
            {
                int recordOffset = i * EntryRecordSize;
                ulong nameOffset = BinaryPrimitives.ReadUInt64LittleEndian(entriesAndStrings.AsSpan(recordOffset));
                ulong modifiedTime = BinaryPrimitives.ReadUInt64LittleEndian(entriesAndStrings.AsSpan(recordOffset + 8));

                int localNameOffset = (int)nameOffset - baseExtOffset;
                if (localNameOffset < 0 || localNameOffset >= entriesAndStrings.Length)
                    throw new InvalidDataException($"Ext-info entry {i} NameOffset {nameOffset} resolves outside the entries/string table - pak may already have inconsistent ext-info (see BffPakEntryInserter's doc comment).");

                int len = entriesAndStrings[localNameOffset];
                if (localNameOffset + 1 + len > entriesAndStrings.Length)
                    throw new InvalidDataException($"Ext-info entry {i}'s filename string runs past the end of the table.");

                string path = Encoding.ASCII.GetString(entriesAndStrings, localNameOffset + 1, len);
                entries.Add(new Entry { NameOffset = nameOffset, ModifiedTime = modifiedTime, Path = path });
            }

            byte[] extraGap = extInfoAndGap.AsSpan(ExtHeaderSize + entriesAndStringsSizeAligned).ToArray();

            return new Decoded { ExtHeaderBytes = extHeaderBytes, Entries = entries, ExtraGapBytes = extraGap };
        }

        /// <summary>
        /// Re-encodes a decoded ext-info block for a new absolute file position
        /// (<paramref name="newBlockAbsoluteStart"/>, i.e. HeaderSize + new TOC size), rebasing
        /// every existing entry's NameOffset to match, and appends one new entry per element of
        /// <paramref name="newEntries"/>. The string table is always rebuilt fresh (existing paths
        /// re-laid-out sequentially in their original order, followed by the new ones) rather than
        /// trying to preserve the original table's exact byte layout - only internal consistency
        /// (NameOffset actually pointing at its own string) is required.
        /// </summary>
        public static (byte[] Bytes, uint NewExtInfoSize) Encode(
            Decoded decoded,
            int newBlockAbsoluteStart,
            IReadOnlyList<(string Path, ulong ModifiedTime)> newEntries)
        {
            int totalEntryCount = decoded.Entries.Count + newEntries.Count;
            int entryTableSize = totalEntryCount * EntryRecordSize;
            int newBaseExtOffset = newBlockAbsoluteStart + ExtHeaderSize;

            var stringTable = new List<byte>();
            var nameOffsets = new List<ulong>(totalEntryCount);

            void AppendString(string path)
            {
                byte[] pathBytes = Encoding.ASCII.GetBytes(path);
                if (pathBytes.Length > 255)
                    throw new ArgumentException($"Path '{path}' is {pathBytes.Length} bytes - ext-info string table uses a 1-byte length prefix (max 255).");

                nameOffsets.Add((ulong)(newBaseExtOffset + entryTableSize + stringTable.Count));
                stringTable.Add((byte)pathBytes.Length);
                stringTable.AddRange(pathBytes);
            }

            foreach (var e in decoded.Entries) AppendString(e.Path);
            foreach (var e in newEntries) AppendString(e.Path);

            int rawSize = entryTableSize + stringTable.Count;
            int alignedSize = (int)AlignUp((uint)rawSize, 0x10);

            byte[] entriesAndStrings = new byte[alignedSize];
            int index = 0;
            foreach (var e in decoded.Entries)
            {
                WriteRecord(entriesAndStrings, index, nameOffsets[index], e.ModifiedTime);
                index++;
            }
            foreach (var (_, modifiedTime) in newEntries)
            {
                WriteRecord(entriesAndStrings, index, nameOffsets[index], modifiedTime);
                index++;
            }

            for (int i = 0; i < stringTable.Count; i++)
                entriesAndStrings[entryTableSize + i] = stringTable[i];

            var cipher = new ScribeCipher();
            cipher.Encrypt(MemoryMarshal.Cast<byte, uint>(entriesAndStrings.AsSpan()));

            byte[] result = new byte[ExtHeaderSize + entriesAndStrings.Length + decoded.ExtraGapBytes.Length];
            decoded.ExtHeaderBytes.CopyTo(result, 0);
            entriesAndStrings.CopyTo(result, ExtHeaderSize);
            decoded.ExtraGapBytes.CopyTo(result, ExtHeaderSize + entriesAndStrings.Length);

            uint newExtInfoSize = (uint)(ExtHeaderSize + rawSize);
            return (result, newExtInfoSize);
        }

        private static void WriteRecord(byte[] buffer, int entryIndex, ulong nameOffset, ulong modifiedTime)
        {
            int offset = entryIndex * EntryRecordSize;
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(offset), nameOffset);
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(offset + 8), modifiedTime);
        }

        private static uint AlignUp(uint x, uint alignment) => (x + (alignment - 1)) & ~(alignment - 1);
    }
}
