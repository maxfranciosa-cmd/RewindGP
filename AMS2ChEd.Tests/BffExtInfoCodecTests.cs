using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    /// <summary>
    /// Covers the "Scribe" cipher and the .bff ext-info (filename table) codec, added after a real
    /// AMS2 install revealed BffPakEntryInserter's original verbatim-copy-the-ext-info-block
    /// approach was silently wrong: it never gave the newly-inserted file a name-table entry, and
    /// (more subtly) left every *pre-existing* entry's absolute NameOffset stale once the TOC's
    /// growth moved the whole block later in the file - see AMS2-livery-modding-knowledge.md's
    /// "Automated slot-injection debugging log" and BffPakEntryInserter/BffExtInfoCodec's doc
    /// comments. ScribeCipher.Encrypt (PCarsTools only ever implements decryption) and the
    /// ext-info entry/string-table layout were both derived and validated against real, unpatched
    /// AMS2 pak bytes before being wired into any write path - these tests keep that behavior
    /// covered without needing a real install.
    /// </summary>
    [TestClass]
    public class BffExtInfoCodecTests
    {
        [TestMethod]
        public void ScribeCipher_DecryptThenEncrypt_ReproducesOriginalBytes()
        {
            var rng = new Random(42);
            var data = new uint[4 * 37];
            for (int i = 0; i < data.Length; i++) data[i] = (uint)rng.Next();
            var original = (uint[])data.Clone();

            var cipher = new ScribeCipher();
            cipher.Decrypt(data);
            cipher.Encrypt(data);

            CollectionAssert.AreEqual(original, data);
        }

        [TestMethod]
        public void ScribeCipher_EncryptThenDecrypt_ReproducesOriginalBytes()
        {
            var rng = new Random(99);
            var data = new uint[4 * 12];
            for (int i = 0; i < data.Length; i++) data[i] = (uint)rng.Next();
            var original = (uint[])data.Clone();

            var cipher = new ScribeCipher();
            cipher.Encrypt(data);
            CollectionAssert.AreNotEqual(original, data, "Encrypt should actually change the data.");

            cipher.Decrypt(data);
            CollectionAssert.AreEqual(original, data);
        }

        /// <summary>
        /// Builds a Scribe-encrypted ext-info block (ext-header + entries table + string table) for
        /// the given paths, laid out exactly as real AMS2 paks store it (see BffExtInfoCodec's doc
        /// comment) - so it can be decoded by the class under test, or embedded in a synthetic pak.
        /// </summary>
        private static (byte[] Block, uint DeclaredExtInfoSize) BuildExtInfoBlock(IReadOnlyList<string> paths, int blockAbsoluteStart)
        {
            byte[] extHeader = new byte[BffExtInfoCodec.ExtHeaderSize];
            Encoding.ASCII.GetBytes("test.xml").CopyTo(extHeader.AsSpan(8));

            int entryTableSize = paths.Count * 0x10;
            int baseExtOffset = blockAbsoluteStart + BffExtInfoCodec.ExtHeaderSize;

            var stringTable = new List<byte>();
            var nameOffsets = new List<ulong>();
            foreach (var path in paths)
            {
                byte[] pathBytes = Encoding.ASCII.GetBytes(path);
                nameOffsets.Add((ulong)(baseExtOffset + entryTableSize + stringTable.Count));
                stringTable.Add((byte)pathBytes.Length);
                stringTable.AddRange(pathBytes);
            }

            int rawSize = entryTableSize + stringTable.Count;
            int alignedSize = (rawSize + 15) & ~15;

            byte[] entriesAndStrings = new byte[alignedSize];
            for (int i = 0; i < paths.Count; i++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(entriesAndStrings.AsSpan(i * 0x10), nameOffsets[i]);
                BinaryPrimitives.WriteUInt64LittleEndian(entriesAndStrings.AsSpan(i * 0x10 + 8), 0);
            }
            for (int i = 0; i < stringTable.Count; i++)
                entriesAndStrings[entryTableSize + i] = stringTable[i];

            BinaryPrimitives.WriteUInt32LittleEndian(extHeader.AsSpan(4), (uint)rawSize);

            new ScribeCipher().Encrypt(MemoryMarshal.Cast<byte, uint>(entriesAndStrings.AsSpan()));

            byte[] block = new byte[BffExtInfoCodec.ExtHeaderSize + alignedSize];
            extHeader.CopyTo(block, 0);
            entriesAndStrings.CopyTo(block, BffExtInfoCodec.ExtHeaderSize);

            return (block, (uint)(BffExtInfoCodec.ExtHeaderSize + rawSize));
        }

        [TestMethod]
        public void Decode_RealisticBlock_EveryEntryResolvesToItsOwnPath()
        {
            string[] paths = { @"vehicles\car\a.dds", @"vehicles\car\bb.dds", @"vehicles\car\ccc.rcf" };
            const int blockStart = 12345; // arbitrary - Decode only cares that it's self-consistent
            var (block, declaredSize) = BuildExtInfoBlock(paths, blockStart);

            var decoded = BffExtInfoCodec.Decode(block, declaredSize, blockStart, paths.Length);

            Assert.AreEqual(paths.Length, decoded.Entries.Count);
            for (int i = 0; i < paths.Length; i++)
                Assert.AreEqual(paths[i], decoded.Entries[i].Path);
        }

        [TestMethod]
        public void EncodeThenDecode_RebasesExistingEntriesAndAddsNewOne_AllResolveCorrectly()
        {
            string[] originalPaths = { @"vehicles\car\a.dds", @"vehicles\car\bb.dds" };
            const int oldBlockStart = 1000;
            var (oldBlock, oldDeclaredSize) = BuildExtInfoBlock(originalPaths, oldBlockStart);
            var decoded = BffExtInfoCodec.Decode(oldBlock, oldDeclaredSize, oldBlockStart, originalPaths.Length);

            const int newBlockStart = 1000 + 0x2A; // simulates the TOC growing by one entry
            const string newPath = @"vehicles\car\new_entry.dds";
            var (newBytes, newDeclaredSize) = BffExtInfoCodec.Encode(decoded, newBlockStart, new[] { (newPath, 0UL) });

            var redecoded = BffExtInfoCodec.Decode(newBytes, newDeclaredSize, newBlockStart, originalPaths.Length + 1);

            Assert.AreEqual(3, redecoded.Entries.Count);
            Assert.AreEqual(originalPaths[0], redecoded.Entries[0].Path, "Existing entry 0 should still resolve after rebasing.");
            Assert.AreEqual(originalPaths[1], redecoded.Entries[1].Path, "Existing entry 1 should still resolve after rebasing.");
            Assert.AreEqual(newPath, redecoded.Entries[2].Path, "New entry should resolve to the path it was given.");
        }

        [TestMethod]
        public void AddEntry_WithRealisticExtInfoBlock_AllEntriesUidCheckPasses()
        {
            byte[] rcfPlaintext = Encoding.UTF8.GetBytes("<REPLACEMENT_SYSTEM/>");
            byte[] otherPlaintext = Encoding.ASCII.GetBytes("other entry data");
            const string rcfPath = @"vehicles\testcar\testcar.rcf";
            const string otherPath = @"vehicles\testcar\other.dat";
            const string newPath = @"vehicles\testcar\new_texture.dds";

            byte[] fileBytes = BuildPakWithExtInfo(
                new[] { (rcfPath, rcfPlaintext, (byte)1, "rcf"), (otherPath, otherPlaintext, (byte)0, "dat") });

            var snapshot = BffPakReader.Read(fileBytes);
            string tempPath = Path.Combine(Path.GetTempPath(), $"bff-extinfo-addentry-{Guid.NewGuid():N}.bff");
            try
            {
                byte[] newPlaintext = Encoding.ASCII.GetBytes("new texture bytes");
                BffPakEntryInserter.AddEntry(snapshot, newPath, newPlaintext, compressionType: 0, tempPath);

                var patchedSnapshot = BffPakReader.Read(tempPath);
                Assert.AreEqual(3, patchedSnapshot.Entries.Count);

                Assert.AreEqual((byte)4, patchedSnapshot.Entries[2].UnknownFlag,
                    "The new entry's UnknownFlag must match the pak's existing entries - confirmed against both a real install " +
                    "and the user's manually-patched (and confirmed working in-game) files that every entry shares this byte. " +
                    "The old hardcoded-to-0 behavior was a real, previously undetected divergence from every known-working entry.");

                uint declaredExtInfoSize = BinaryPrimitives.ReadUInt32LittleEndian(patchedSnapshot.RawHeaderBytes.AsSpan(0x120));
                uint tocSize = BinaryPrimitives.ReadUInt32LittleEndian(patchedSnapshot.RawHeaderBytes.AsSpan(0x118));
                int blockStart = BffPakReader.HeaderSize + (int)tocSize;
                long firstOffset = patchedSnapshot.Entries.Min(e => e.DataOffset);
                byte[] extInfoAndGap = patchedSnapshot.FullFileBytes.AsSpan(blockStart, (int)(firstOffset - blockStart)).ToArray();

                var decoded = BffExtInfoCodec.Decode(extInfoAndGap, declaredExtInfoSize, blockStart, (int)patchedSnapshot.FileCount);

                Assert.AreEqual(3, decoded.Entries.Count);
                for (int i = 0; i < decoded.Entries.Count; i++)
                {
                    ulong computedUid = BffPathHash.ComputeUid(decoded.Entries[i].Path);
                    Assert.AreEqual(patchedSnapshot.Entries[i].Uid, computedUid,
                        $"Entry {i} ('{decoded.Entries[i].Path}') NameOffset should resolve to a path whose hash matches the TOC UID - this is exactly what a stale (unrebased) NameOffset would fail.");
                }
                Assert.AreEqual(newPath, decoded.Entries[2].Path);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void AddEntry_RepointsSectionInfoPosToWhereTheBlockActuallyMoved()
        {
            const string rcfPath = @"vehicles\testcar\testcar.rcf";
            const string otherPath = @"vehicles\testcar\other.dat";
            const string newPath = @"vehicles\testcar\new_texture.dds";

            byte[] fileBytes = BuildPakWithExtInfo(new[]
            {
                (rcfPath, Encoding.UTF8.GetBytes("<REPLACEMENT_SYSTEM/>"), (byte)1, "rcf"),
                (otherPath, Encoding.ASCII.GetBytes("other entry data"), (byte)0, "dat"),
            });

            var snapshot = BffPakReader.Read(fileBytes);
            uint originalSectionInfoPos = BinaryPrimitives.ReadUInt32LittleEndian(snapshot.RawHeaderBytes.AsSpan(0x124));
            byte[] originalMarker = fileBytes.AsSpan((int)originalSectionInfoPos, 32).ToArray();

            string tempPath = Path.Combine(Path.GetTempPath(), $"bff-sectioninfo-{Guid.NewGuid():N}.bff");
            try
            {
                BffPakEntryInserter.AddEntry(snapshot, newPath, Encoding.ASCII.GetBytes("new texture bytes"), compressionType: 0, tempPath);

                var patchedSnapshot = BffPakReader.Read(tempPath);
                uint patchedSectionInfoPos = BinaryPrimitives.ReadUInt32LittleEndian(patchedSnapshot.RawHeaderBytes.AsSpan(0x124));

                Assert.AreNotEqual(originalSectionInfoPos, patchedSectionInfoPos,
                    "The section-info block physically moved (ext-info grew) - the header's pointer must move with it.");

                byte[] patchedMarker = patchedSnapshot.FullFileBytes.AsSpan((int)patchedSectionInfoPos, 32).ToArray();
                CollectionAssert.AreEqual(originalMarker, patchedMarker,
                    "mSectionInfoPos must point at the real (unmodified) section-info bytes after the shift, not stale data at the old offset.");
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private const int KeyIndex = 5;

        private static byte[] BuildPakWithExtInfo((string Path, byte[] Plaintext, byte CompressionType, string Extension)[] files)
        {
            byte[] key = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[KeyIndex]);

            var onDiskByFile = new byte[files.Length][];
            for (int i = 0; i < files.Length; i++)
            {
                byte[] onDisk = files[i].CompressionType == 1 ? Zlib(files[i].Plaintext) : (byte[])files[i].Plaintext.Clone();
                Rc4Cipher.Transform(onDisk, key);
                onDiskByFile[i] = onDisk;
            }

            const int headerSize = BffPakReader.HeaderSize;
            const int entrySize = 0x2A;
            uint fileCount = (uint)files.Length;
            uint tocSize = entrySize * fileCount;

            int extInfoBlockStart = headerSize + (int)tocSize;
            var (extInfoBlock, declaredExtInfoSize) = BuildExtInfoBlock(files.Select(f => f.Path).ToList(), extInfoBlockStart);

            // Real paks reserve extra padding after the ext-info block before a 32-byte
            // "section info" block (see BffPakEntryInserter's mSectionInfoPos doc comment) -
            // mirror that here (a fixed-size marker + some slack) so tests can verify AddEntry
            // correctly repoints mSectionInfoPos rather than leaving it stale.
            const int sectionInfoReservedGap = 48;
            int sectionInfoPos = extInfoBlockStart + extInfoBlock.Length + sectionInfoReservedGap;
            byte[] sectionInfoMarker = Enumerable.Range(0, 32).Select(i => (byte)(0xD0 + i)).ToArray();

            long dataStart = sectionInfoPos + sectionInfoMarker.Length;
            var dataOffsets = new long[files.Length];
            long cursor = dataStart;
            for (int i = 0; i < files.Length; i++)
            {
                dataOffsets[i] = cursor;
                cursor += RepackOffsetPlanner.Align16(onDiskByFile[i].Length);
            }

            byte[] file = new byte[cursor];

            byte[] magic = "PAK "u8.ToArray();
            Array.Reverse(magic);
            magic.CopyTo(file, 0);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x04), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x08), fileCount);
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(0x0C), (ulong)dataStart);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x14), 0x800);
            Encoding.ASCII.GetBytes("test.bff").CopyTo(file.AsSpan(0x18));
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x118), tocSize);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x11C), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x120), declaredExtInfoSize);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x124), (uint)sectionInfoPos);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x128), (uint)sectionInfoMarker.Length);
            file[0x12C] = 0;
            file[0x12D] = 2; // RC4

            byte[] toc = new byte[tocSize];
            for (int i = 0; i < files.Length; i++)
            {
                var span = toc.AsSpan(i * entrySize, entrySize);
                BinaryPrimitives.WriteUInt64LittleEndian(span[0x00..], BffPathHash.ComputeUid(files[i].Path));
                BinaryPrimitives.WriteUInt64LittleEndian(span[0x08..], (ulong)dataOffsets[i]);
                BinaryPrimitives.WriteUInt32LittleEndian(span[0x10..], (uint)onDiskByFile[i].Length);
                BinaryPrimitives.WriteUInt32LittleEndian(span[0x14..], (uint)files[i].Plaintext.Length);
                BinaryPrimitives.WriteUInt64LittleEndian(span[0x18..], 0);
                span[0x20] = files[i].CompressionType;
                span[0x21] = 4; // UnknownFlag - real paks always have 4 here, see BffPakEntryInserter's doc comment
                BinaryPrimitives.WriteUInt32LittleEndian(span[0x22..], Jamcrc32.Compute(onDiskByFile[i]));
                Encoding.ASCII.GetBytes(files[i].Extension.PadRight(4, '\0')).CopyTo(span[0x26..]);
            }
            Rc4Cipher.Transform(toc, key);
            toc.CopyTo(file.AsSpan(headerSize));

            extInfoBlock.CopyTo(file.AsSpan(extInfoBlockStart));
            sectionInfoMarker.CopyTo(file.AsSpan(sectionInfoPos));

            for (int i = 0; i < files.Length; i++)
                onDiskByFile[i].CopyTo(file.AsSpan((int)dataOffsets[i]));

            return file;
        }

        private static byte[] Zlib(byte[] plaintext)
        {
            using var ms = new MemoryStream();
            using (var zlib = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(plaintext, 0, plaintext.Length);
            return ms.ToArray();
        }
    }
}
