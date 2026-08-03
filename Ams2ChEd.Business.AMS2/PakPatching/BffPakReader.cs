using System.Buffers.Binary;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Reads AMS2 ".bff" pak archives: the 0x130-byte header and the RC4-encrypted TOC. Format
    /// verified against AMS2-livery-modding-knowledge.md and against PCarsTools
    /// (github.com/Nenkai/PCarsTools, PCarsTools/Pak/BPakFile.cs, MIT licensed) - reimplemented
    /// here rather than taken as a package dependency (PCarsTools isn't published to NuGet), and
    /// intentionally narrower: this only ever needs to read AMS2's ZLib-compressed .rcf entries,
    /// not the Oodle/LZX paths PCarsTools also supports for other file types/older titles.
    ///
    /// Deliberately does NOT decode the ext-info/filename table (which is encrypted with a
    /// different, harder-to-validate cipher AMS2's tooling calls "Scribe") - entries are instead
    /// located by testing candidate relative paths against each TOC entry's UID hash (see
    /// <see cref="BffPathHash"/>), which is all this tool needs since it never adds/renames files.
    /// </summary>
    public static class BffPakReader
    {
        public const int HeaderSize = 0x130;
        private const byte Rc4Encryption = 2;

        public static BffPakSnapshot Read(string pakPath) => Read(File.ReadAllBytes(pakPath));

        public static BffPakSnapshot Read(byte[] fileBytes)
        {
            if (fileBytes.Length < HeaderSize || fileBytes[0] != (byte)'P' || fileBytes[1] != (byte)'A' || fileBytes[2] != (byte)'K' || fileBytes[3] != (byte)' ')
                throw new InvalidDataException("Not a recognized .bff pak file (bad magic).");

            byte[] header = fileBytes.AsSpan(0, HeaderSize).ToArray();

            uint fileCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x08));
            uint tocSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x118));
            byte encryptionType = header[0x12D];

            if (HeaderSize + tocSize > fileBytes.Length)
                throw new InvalidDataException("Malformed .bff pak: TOC size extends past end of file.");

            byte[] tocBuffer = fileBytes.AsSpan(HeaderSize, (int)tocSize).ToArray();

            int keyIndex;
            if (encryptionType == Rc4Encryption)
            {
                keyIndex = DetectRc4KeyIndexAndDecrypt(tocBuffer);
            }
            else if (encryptionType == 0)
            {
                keyIndex = -1; // None - TOC already plaintext.
            }
            else
            {
                throw new NotSupportedException($"Unsupported .bff encryption type {encryptionType} (only RC4/None are supported).");
            }

            var entries = ParseTocEntries(tocBuffer, fileCount);

            byte[] tailBytes = fileBytes.AsSpan(HeaderSize + (int)tocSize).ToArray();

            return new BffPakSnapshot
            {
                RawHeaderBytes = header,
                KeyIndex = keyIndex,
                EncryptionType = encryptionType,
                FileCount = fileCount,
                Entries = entries,
                TailBytes = tailBytes,
                FullFileBytes = fileBytes,
            };
        }

        /// <summary>
        /// Tries every one of the 32 PC2AndAbove keys against a copy of the encrypted TOC buffer,
        /// decrypting in place with whichever key first produces a plausible first entry (its data
        /// offset's high two bytes both zero - the same sanity check PCarsTools itself falls back
        /// to when it can't determine KeyIndex from its own pattern-matching config). Mutates
        /// <paramref name="tocBuffer"/> to the decrypted bytes on success.
        /// </summary>
        private static int DetectRc4KeyIndexAndDecrypt(byte[] tocBuffer)
        {
            if (tocBuffer.Length < 16)
                throw new InvalidDataException("TOC buffer too small to contain even one entry.");

            for (int keyIndex = 0; keyIndex < Rc4KeySet.Pc2AndAboveRawKeys.Length; keyIndex++)
            {
                byte[] candidate = (byte[])tocBuffer.Clone();
                byte[] key = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[keyIndex]);
                Rc4Cipher.Transform(candidate, key);

                if (candidate[14] == 0 && candidate[15] == 0)
                {
                    Array.Copy(candidate, tocBuffer, tocBuffer.Length);
                    return keyIndex;
                }
            }

            throw new InvalidDataException("Could not determine RC4 KeyIndex for this .bff pak (tried all 32 PC2AndAbove keys).");
        }

        private static List<BffTocEntry> ParseTocEntries(byte[] tocBuffer, uint fileCount)
        {
            const int entrySize = 0x2A;
            var entries = new List<BffTocEntry>((int)fileCount);

            for (int i = 0; i < fileCount; i++)
            {
                int offset = i * entrySize;
                if (offset + entrySize > tocBuffer.Length)
                    throw new InvalidDataException($"TOC buffer too small for entry {i}.");

                var span = tocBuffer.AsSpan(offset, entrySize);

                entries.Add(new BffTocEntry
                {
                    Index = i,
                    Uid = BinaryPrimitives.ReadUInt64LittleEndian(span[0x00..]),
                    DataOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(span[0x08..]),
                    PakSize = BinaryPrimitives.ReadUInt32LittleEndian(span[0x10..]),
                    OriginalSize = BinaryPrimitives.ReadUInt32LittleEndian(span[0x14..]),
                    ModifiedTime = BinaryPrimitives.ReadUInt64LittleEndian(span[0x18..]),
                    CompressionType = span[0x20],
                    UnknownFlag = span[0x21],
                    Crc = BinaryPrimitives.ReadUInt32LittleEndian(span[0x22..]),
                    Extension = System.Text.Encoding.ASCII.GetString(span.Slice(0x26, 4)).TrimEnd('\0'),
                });
            }

            return entries;
        }

        /// <summary>
        /// Finds the TOC entry whose UID matches the hash of <paramref name="candidateRelativePath"/>,
        /// or null if no entry has that path (the caller should treat this as "pak doesn't contain
        /// this file", not throw - see <c>PakPathResolver</c>/<c>Ams2VehicleLiverySlotPatcher</c>'s
        /// fail-closed handling of unrecognized paks).
        /// </summary>
        public static BffTocEntry? TryFindEntryByPath(BffPakSnapshot snapshot, string candidateRelativePath)
        {
            ulong targetUid = BffPathHash.ComputeUid(candidateRelativePath);
            foreach (var entry in snapshot.Entries)
            {
                if (entry.Uid == targetUid)
                    return entry;
            }
            return null;
        }
    }
}
