using System.Buffers.Binary;
using System.Text;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Appends a brand-new entry to a .bff pak - something BffPakRepacker deliberately never does
    /// (it only patches entries that already exist in the TOC; its own docs call out
    /// "never adds/renames/removes files" as an intentional invariant). Needed to inject a new
    /// livery texture (e.g. reused from a sibling model) as a genuinely new entry, since a livery
    /// slot only actually renders in-game when the .rcf's NEWTEXTURE reference points at an entry
    /// that physically exists in the pak - see Ams2VehicleLiverySlotPatcher.
    ///
    /// Growing the TOC by one 0x2A-byte record shifts everything after it (the ext-info/filename
    /// table block, and all entry data) later in the file, unlike BffPakRepacker's PatchEntries
    /// which never changes entry count and so never needs to move that block at all.
    ///
    /// The ext-info block (see BffExtInfoCodec) is NOT copied verbatim - it must be rebuilt: (a) a
    /// name-table entry has to exist for the new file, or the game's own ext-info parsing sees a
    /// TOC entry with no matching name record past the last real one, and (b) every *existing*
    /// entry's absolute NameOffset needs rebasing by however far the TOC's growth pushes the whole
    /// block later in the file - confirmed empirically against a real AMS2 install: an earlier
    /// version of this method that copied extInfoAndGap byte-for-byte (correct for
    /// BffPakRepacker, which never moves this block) left the *pre-existing* entries' NameOffsets
    /// silently pointing at the wrong strings too, not just the new entry missing one - see
    /// AMS2-livery-modding-knowledge.md's "Automated slot-injection debugging log".
    /// </summary>
    public static class BffPakEntryInserter
    {
        private const int TocEntrySize = 0x2A;

        public static void AddEntry(
            BffPakSnapshot snapshot,
            string relativePath,
            byte[] plaintext,
            byte compressionType,
            string outputPath)
        {
            ulong uid = BffPathHash.ComputeUid(relativePath);
            if (snapshot.Entries.Any(e => e.Uid == uid))
                throw new InvalidOperationException($"An entry for '{relativePath}' already exists in this pak.");

            string extension = Path.GetExtension(relativePath).TrimStart('.');
            if (extension.Length == 0 || extension.Length > 4)
                throw new ArgumentException($"'{relativePath}' needs a 1-4 character file extension.");

            byte[] onDiskBytes = BffEntryExtractor.EncodePlaintext(plaintext, compressionType, snapshot);

            int oldFileCount = snapshot.Entries.Count;
            int newFileCount = oldFileCount + 1;
            int oldTocSize = oldFileCount * TocEntrySize;
            int newTocSize = newFileCount * TocEntrySize;

            long firstEntryOriginalOffset = snapshot.Entries.Min(e => e.DataOffset);
            if (firstEntryOriginalOffset < BffPakReader.HeaderSize + oldTocSize)
                throw new InvalidDataException("Malformed pak: entry data overlaps header/TOC region.");

            // Everything between the end of the (old-size) TOC and the first entry's data - the
            // ext-info/filename-table block plus any padding. Unlike BffPakRepacker (which never
            // adds/removes files and so copies this verbatim), this block must be rebuilt: see the
            // class doc comment.
            int oldBlockAbsoluteStart = BffPakReader.HeaderSize + oldTocSize;
            byte[] extInfoAndGap = snapshot.FullFileBytes
                .AsSpan(oldBlockAbsoluteStart, (int)(firstEntryOriginalOffset - oldBlockAbsoluteStart))
                .ToArray();

            uint declaredExtInfoSize = BinaryPrimitives.ReadUInt32LittleEndian(snapshot.RawHeaderBytes.AsSpan(0x120));
            int newBlockAbsoluteStart = BffPakReader.HeaderSize + newTocSize;

            byte[] newExtInfoBytes;
            uint newExtInfoSize;
            if (declaredExtInfoSize >= BffExtInfoCodec.ExtHeaderSize)
            {
                var decodedExtInfo = BffExtInfoCodec.Decode(extInfoAndGap, declaredExtInfoSize, oldBlockAbsoluteStart, oldFileCount);
                (newExtInfoBytes, newExtInfoSize) = BffExtInfoCodec.Encode(
                    decodedExtInfo,
                    newBlockAbsoluteStart,
                    new[] { (relativePath, 0UL) });
            }
            else
            {
                // No real ext-info block present (declaredExtInfoSize is 0 or too small to hold
                // even the ext-header) - nothing to rebase or add a name entry to; carry the block
                // forward unchanged, same as before this class tracked ext-info at all.
                newExtInfoBytes = extInfoAndGap;
                newExtInfoSize = declaredExtInfoSize;
            }

            long newDataRegionStart = newBlockAbsoluteStart + newExtInfoBytes.Length;
            long offsetShift = newDataRegionStart - firstEntryOriginalOffset;

            var newEntries = new List<BffTocEntry>(newFileCount);
            foreach (var e in snapshot.Entries)
            {
                newEntries.Add(new BffTocEntry
                {
                    Index = e.Index,
                    Uid = e.Uid,
                    DataOffset = e.DataOffset + offsetShift,
                    PakSize = e.PakSize,
                    OriginalSize = e.OriginalSize,
                    ModifiedTime = e.ModifiedTime,
                    CompressionType = e.CompressionType,
                    UnknownFlag = e.UnknownFlag,
                    Crc = e.Crc,
                    Extension = e.Extension,
                });
            }

            long lastExistingEnd = newEntries.Count == 0
                ? newDataRegionStart
                : newEntries.Max(e => e.DataOffset + e.PakSize);
            long newEntryOffset = RepackOffsetPlanner.Align16(lastExistingEnd);

            // UnknownFlag: confirmed against a real install that every single TOC entry in every
            // pak checked - any file type, any compression type - shares the same value (4 in
            // every pak sampled, including the newly-added entries in a manually-patched copy the
            // user separately confirmed renders correctly in-game). Meaning still not decoded, but
            // hardcoding 0 here (this method's original behavior) is a real, previously-undetected
            // divergence from every known-working entry - see AMS2-livery-modding-knowledge.md.
            // Sourced from an existing entry in this same pak rather than hardcoded, so this stays
            // correct even if the value ever differs between paks/versions.
            byte unknownFlag = snapshot.Entries.Count > 0 ? snapshot.Entries[0].UnknownFlag : (byte)0;

            var newEntry = new BffTocEntry
            {
                Index = oldFileCount,
                Uid = uid,
                DataOffset = newEntryOffset,
                PakSize = (uint)onDiskBytes.Length,
                OriginalSize = (uint)plaintext.Length,
                ModifiedTime = 0,
                CompressionType = compressionType,
                UnknownFlag = unknownFlag,
                Crc = Jamcrc32.Compute(onDiskBytes),
                Extension = extension,
            };
            newEntries.Add(newEntry);

            byte[] header = (byte[])snapshot.RawHeaderBytes.Clone();
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x08), (uint)newFileCount);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x118), (uint)newTocSize);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x120), newExtInfoSize);

            // mSectionInfoPos (0x124): points at a 32-byte "section info" block that lives inside
            // what this method otherwise treats as opaque gap bytes after the ext-info region.
            // Real paks reserve a fixed-size budget for ext-info growth specifically so this block
            // (and mSectionInfoPos itself) never needs to move - confirmed empirically:
            // extInfoRegionSize + gap-before-sectionInfo is a constant across a real pristine pak
            // and a real, confirmed-working repacked copy of it. This method doesn't respect that
            // reserved budget (BffExtInfoCodec tight-packs), so growing the ext-info table pushes
            // the section-info block physically later in the file - and until this fix, the header
            // still pointed at its old (now wrong) location. Uses the exact same offsetShift as
            // entry data, which is algebraically the correct shift for this block too (both live
            // inside the same "extInfoAndGap" region, whose start moves by the same amount as its
            // now-bigger ext-info prefix pushes everything after it). See
            // AMS2-livery-modding-knowledge.md.
            uint oldSectionInfoPos = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x124));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x124), (uint)(oldSectionInfoPos + offsetShift));

            byte[] tocBuffer = new byte[newTocSize];
            foreach (var entry in newEntries)
            {
                var span = tocBuffer.AsSpan(entry.Index * TocEntrySize, TocEntrySize);
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

            if (snapshot.KeyIndex >= 0)
            {
                byte[] key = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[snapshot.KeyIndex]);
                Rc4Cipher.Transform(tocBuffer, key);
            }

            long dataSectionLength = RepackOffsetPlanner.Align16(newEntryOffset + onDiskBytes.Length) - newDataRegionStart;
            byte[] dataSection = new byte[dataSectionLength];

            foreach (var entry in newEntries)
            {
                byte[] bytes = entry.Uid == uid
                    ? onDiskBytes
                    : snapshot.FullFileBytes.AsSpan((int)(entry.DataOffset - offsetShift), (int)entry.PakSize).ToArray();

                long relativeOffset = entry.DataOffset - newDataRegionStart;
                Array.Copy(bytes, 0, dataSection, relativeOffset, bytes.Length);
            }

            using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            output.Write(header, 0, header.Length);
            output.Write(tocBuffer, 0, tocBuffer.Length);
            output.Write(newExtInfoBytes, 0, newExtInfoBytes.Length);
            output.Write(dataSection, 0, dataSection.Length);
        }
    }
}
