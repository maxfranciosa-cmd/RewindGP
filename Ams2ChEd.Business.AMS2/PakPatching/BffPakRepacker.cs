using System.Buffers.Binary;
using System.Text;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Repacks a .bff pak with one or more entries' plaintext replaced, per
    /// AMS2-livery-modding-knowledge.md's "Repacking algorithm". No packer exists anywhere for
    /// this format (PCarsTools is unpack-only) - this is a from-scratch implementation of the
    /// write path, validated against that doc's empirically-confirmed algorithm.
    ///
    /// Header, and the ext-header/ext-info/filename-table tail, are always copied byte-for-byte
    /// verbatim - this tool never adds/renames/removes files, so neither changes. Only the TOC
    /// (offsets/sizes/CRC of changed entries, re-encrypted as a whole buffer) and the entry data
    /// region are rebuilt.
    /// </summary>
    public static class BffPakRepacker
    {
        private const int TocEntrySize = 0x2A;

        /// <summary>
        /// Writes a repacked copy of the pak to <paramref name="outputPath"/> (never overwrites
        /// the source in place). <paramref name="newPlaintextByEntryIndex"/> maps TOC entry index
        /// to its new plaintext content; every other entry's on-disk bytes are copied unchanged.
        /// </summary>
        public static void PatchEntries(
            BffPakSnapshot snapshot,
            IReadOnlyDictionary<int, byte[]> newPlaintextByEntryIndex,
            string outputPath)
        {
            if (newPlaintextByEntryIndex.Count == 0)
                throw new ArgumentException("Nothing to patch.", nameof(newPlaintextByEntryIndex));

            // Capture each changed entry's new on-disk bytes (and original bytes for untouched
            // entries) BEFORE mutating any BffTocEntry, since untouched-entry lookups below use
            // each entry's *original* DataOffset/PakSize into snapshot.FullFileBytes.
            var entriesByIndex = snapshot.Entries.ToDictionary(e => e.Index);

            var newOnDiskBytesByIndex = new Dictionary<int, byte[]>(newPlaintextByEntryIndex.Count);
            foreach (var (entryIndex, plaintext) in newPlaintextByEntryIndex)
            {
                byte compressionType = entriesByIndex[entryIndex].CompressionType;
                newOnDiskBytesByIndex[entryIndex] = BffEntryExtractor.EncodePlaintext(plaintext, compressionType, snapshot);
            }

            var entryPlans = new List<RepackOffsetPlanner.EntryPlan>(snapshot.Entries.Count);
            foreach (var entry in snapshot.Entries)
            {
                int newSize = newOnDiskBytesByIndex.TryGetValue(entry.Index, out var bytes)
                    ? bytes.Length
                    : (int)entry.PakSize;
                entryPlans.Add(new RepackOffsetPlanner.EntryPlan(entry.Index, entry.DataOffset, newSize));
            }

            var plannedOffsets = RepackOffsetPlanner.ComputeNewOffsets(entryPlans);
            var newOffsetByIndex = plannedOffsets.ToDictionary(p => p.EntryIndex, p => p.NewOffset);

            long firstEntryOriginalOffset = snapshot.Entries.Min(e => e.DataOffset);
            if (firstEntryOriginalOffset < BffPakReader.HeaderSize)
                throw new InvalidDataException("Malformed pak: entry data overlaps header/TOC region.");

            // Snapshot each untouched entry's original on-disk bytes before DataOffset/PakSize/Crc
            // are overwritten below.
            var originalOnDiskBytesByIndex = new Dictionary<int, byte[]>();
            foreach (var entry in snapshot.Entries)
            {
                if (!newOnDiskBytesByIndex.ContainsKey(entry.Index))
                {
                    originalOnDiskBytesByIndex[entry.Index] = snapshot.FullFileBytes
                        .AsSpan((int)entry.DataOffset, (int)entry.PakSize).ToArray();
                }
            }

            // Mutate TOC entries in place with their new offset and (if changed) size/CRC.
            foreach (var entry in snapshot.Entries)
            {
                entry.DataOffset = newOffsetByIndex[entry.Index];

                if (newOnDiskBytesByIndex.TryGetValue(entry.Index, out var newBytes))
                {
                    entry.PakSize = (uint)newBytes.Length;
                    entry.OriginalSize = (uint)newPlaintextByEntryIndex[entry.Index].Length;
                    entry.Crc = Jamcrc32.Compute(newBytes);
                }
            }

            byte[] tocBuffer = SerializeToc(snapshot.Entries, (int)snapshot.FileCount);
            if (snapshot.KeyIndex >= 0)
            {
                byte[] key = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[snapshot.KeyIndex]);
                Rc4Cipher.Transform(tocBuffer, key);
            }

            // Prefix = header + TOC region (about to be overwritten) + tail + any original padding
            // gap before the first entry's data - all copied verbatim except the TOC bytes.
            byte[] prefix = snapshot.FullFileBytes.AsSpan(0, (int)firstEntryOriginalOffset).ToArray();
            Array.Copy(tocBuffer, 0, prefix, BffPakReader.HeaderSize, tocBuffer.Length);

            byte[] dataSection = BuildDataSection(snapshot.Entries, newOnDiskBytesByIndex, originalOnDiskBytesByIndex, firstEntryOriginalOffset);

            using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            output.Write(prefix, 0, prefix.Length);
            output.Write(dataSection, 0, dataSection.Length);
        }

        private static byte[] SerializeToc(IReadOnlyList<BffTocEntry> entries, int fileCount)
        {
            byte[] buffer = new byte[fileCount * TocEntrySize];

            foreach (var entry in entries)
            {
                var span = buffer.AsSpan(entry.Index * TocEntrySize, TocEntrySize);

                BinaryPrimitives.WriteUInt64LittleEndian(span[0x00..], entry.Uid);
                BinaryPrimitives.WriteUInt64LittleEndian(span[0x08..], (ulong)entry.DataOffset);
                BinaryPrimitives.WriteUInt32LittleEndian(span[0x10..], entry.PakSize);
                BinaryPrimitives.WriteUInt32LittleEndian(span[0x14..], entry.OriginalSize);
                BinaryPrimitives.WriteUInt64LittleEndian(span[0x18..], entry.ModifiedTime);
                span[0x20] = entry.CompressionType;
                span[0x21] = entry.UnknownFlag;
                BinaryPrimitives.WriteUInt32LittleEndian(span[0x22..], entry.Crc);

                byte[] extBytes = Encoding.ASCII.GetBytes(entry.Extension.PadRight(4, '\0')[..4]);
                extBytes.CopyTo(span[0x26..]);
            }

            return buffer;
        }

        private static byte[] BuildDataSection(
            IReadOnlyList<BffTocEntry> entries,
            IReadOnlyDictionary<int, byte[]> newOnDiskBytesByIndex,
            IReadOnlyDictionary<int, byte[]> originalOnDiskBytesByIndex,
            long firstEntryOriginalOffset)
        {
            var byNewOffset = entries.OrderBy(e => e.DataOffset).ToList();

            var last = byNewOffset[^1];
            long lastEnd = last.DataOffset + RepackOffsetPlanner.Align16(last.PakSize);
            long totalLength = lastEnd - firstEntryOriginalOffset;

            byte[] section = new byte[totalLength];

            foreach (var entry in byNewOffset)
            {
                byte[] bytes = newOnDiskBytesByIndex.TryGetValue(entry.Index, out var nb)
                    ? nb
                    : originalOnDiskBytesByIndex[entry.Index];

                long relativeOffset = entry.DataOffset - firstEntryOriginalOffset;
                Array.Copy(bytes, 0, section, relativeOffset, bytes.Length);
            }

            return section;
        }
    }
}
