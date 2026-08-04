using System.Buffers.Binary;
using System.Linq;
using System.Text;
using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    /// <summary>
    /// Builds a small, structurally-real .bff pak entirely from the primitives this project
    /// implements (RC4, JAMCRC, header/TOC byte layout), independently of BffPakReader/
    /// BffPakRepacker, then verifies those classes can read and patch it correctly. There's no
    /// real AMS2 install available to test against here, so this is the closest thing to an
    /// end-to-end check of the format logic in AMS2-livery-modding-knowledge.md - real-install
    /// verification (per that doc's own validation steps) is still required before this is wired
    /// into live race prep.
    /// </summary>
    [TestClass]
    public class BffPakRoundTripTests
    {
        private const int KeyIndex = 5;
        private const string RcfPath = @"vehicles\testcar\testcar.rcf";
        private const string DataPath = @"vehicles\testcar\other.dat";

        private static byte[] BuildFakePak(byte[] rcfPlaintext, byte[] otherPlaintext, out int rcfEntryIndex, out int otherEntryIndex)
        {
            byte[] key = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[KeyIndex]);

            byte[] rcfOnDisk = Zlib(rcfPlaintext);
            Rc4Cipher.Transform(rcfOnDisk, key);

            byte[] otherOnDisk = (byte[])otherPlaintext.Clone();
            Rc4Cipher.Transform(otherOnDisk, key);

            const int headerSize = BffPakReader.HeaderSize;
            const int entrySize = 0x2A;
            const uint fileCount = 2;
            uint tocSize = entrySize * fileCount;

            long rcfOffset = headerSize + tocSize;
            long otherOffset = rcfOffset + RepackOffsetPlanner.Align16(rcfOnDisk.Length);
            long dataEnd = otherOffset + RepackOffsetPlanner.Align16(otherOnDisk.Length);

            rcfEntryIndex = 0;
            otherEntryIndex = 1;

            byte[] file = new byte[dataEnd];

            // Header - magic is "PAK " stored byte-reversed on disk (see BffPakReader.Read).
            byte[] magic = "PAK "u8.ToArray();
            Array.Reverse(magic);
            magic.CopyTo(file, 0);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x04), 1); // version
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x08), fileCount);
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(0x0C), (ulong)rcfOffset); // dataOffset
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x14), 0x800); // sectorSize
            Encoding.ASCII.GetBytes("test.bff").CopyTo(file.AsSpan(0x18));
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x118), tocSize);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x11C), 0); // crcSize
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x120), 0); // extInfoSize (no tail in this fixture)
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x124), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x128), 0);
            file[0x12C] = 0; // flags
            file[0x12D] = 2; // encryption = RC4

            // TOC (plaintext first, encrypted as a whole afterwards)
            byte[] toc = new byte[tocSize];
            WriteTocEntry(toc, 0, RcfPath, rcfOffset, rcfOnDisk, rcfPlaintext.Length, compressionType: 1, extension: "rcf");
            WriteTocEntry(toc, 1, DataPath, otherOffset, otherOnDisk, otherPlaintext.Length, compressionType: 0, extension: "dat");
            Rc4Cipher.Transform(toc, key);
            toc.CopyTo(file.AsSpan(headerSize));

            // Entry data (tail is zero-length in this fixture)
            rcfOnDisk.CopyTo(file.AsSpan((int)rcfOffset));
            otherOnDisk.CopyTo(file.AsSpan((int)otherOffset));

            return file;
        }

        private static void WriteTocEntry(byte[] toc, int index, string relativePath, long dataOffset, byte[] onDiskBytes, int originalSize, byte compressionType, string extension)
        {
            var span = toc.AsSpan(index * 0x2A, 0x2A);
            BinaryPrimitives.WriteUInt64LittleEndian(span[0x00..], BffPathHash.ComputeUid(relativePath));
            BinaryPrimitives.WriteUInt64LittleEndian(span[0x08..], (ulong)dataOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x10..], (uint)onDiskBytes.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x14..], (uint)originalSize);
            BinaryPrimitives.WriteUInt64LittleEndian(span[0x18..], 0); // modifiedTime
            span[0x20] = compressionType;
            span[0x21] = 0; // unknownFlag
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x22..], Jamcrc32.Compute(onDiskBytes));
            Encoding.ASCII.GetBytes(extension.PadRight(4, '\0')).CopyTo(span[0x26..]);
        }

        private static byte[] Zlib(byte[] plaintext)
        {
            using var ms = new MemoryStream();
            using (var zlib = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(plaintext, 0, plaintext.Length);
            return ms.ToArray();
        }

        [TestMethod]
        public void Read_SyntheticPak_DecodesBothEntriesToOriginalPlaintext()
        {
            byte[] rcfPlaintext = Encoding.UTF8.GetBytes("<REPLACEMENT_SYSTEM><INPUTS><INPUT NAME=\"LIVERY\" OPTIONS=\"6\" /></INPUTS></REPLACEMENT_SYSTEM>");
            byte[] otherPlaintext = Encoding.ASCII.GetBytes("uncompressed entry data, unchanged");

            byte[] fileBytes = BuildFakePak(rcfPlaintext, otherPlaintext, out int rcfIndex, out int otherIndex);

            var snapshot = BffPakReader.Read(fileBytes);

            Assert.AreEqual(2, snapshot.Entries.Count);
            Assert.AreEqual(KeyIndex, snapshot.KeyIndex);

            var rcfEntry = BffPakReader.TryFindEntryByPath(snapshot, RcfPath);
            Assert.IsNotNull(rcfEntry);
            Assert.AreEqual(rcfIndex, rcfEntry!.Index);
            CollectionAssert.AreEqual(rcfPlaintext, BffEntryExtractor.ExtractPlaintext(snapshot, rcfEntry));

            var otherEntry = BffPakReader.TryFindEntryByPath(snapshot, DataPath);
            Assert.IsNotNull(otherEntry);
            Assert.AreEqual(otherIndex, otherEntry!.Index);
            CollectionAssert.AreEqual(otherPlaintext, BffEntryExtractor.ExtractPlaintext(snapshot, otherEntry));
        }

        [TestMethod]
        public void TryFindEntryByPath_UnknownPath_ReturnsNull()
        {
            byte[] fileBytes = BuildFakePak(Encoding.UTF8.GetBytes("<x/>"), Encoding.ASCII.GetBytes("y"), out _, out _);
            var snapshot = BffPakReader.Read(fileBytes);

            var result = BffPakReader.TryFindEntryByPath(snapshot, @"vehicles\nope\nope.rcf");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void PatchEntries_GrowsOneEntry_LeavesOtherEntryByteIdenticalAndUpdatesPatchedEntry()
        {
            byte[] originalRcf = Encoding.UTF8.GetBytes(
                "<REPLACEMENT_SYSTEM><INPUTS><INPUT NAME=\"LIVERY\" OPTIONS=\"6\" /></INPUTS></REPLACEMENT_SYSTEM>");
            byte[] otherPlaintext = Encoding.ASCII.GetBytes("uncompressed entry data, unchanged");

            byte[] fileBytes = BuildFakePak(originalRcf, otherPlaintext, out int rcfIndex, out _);
            var snapshot = BffPakReader.Read(fileBytes);

            // Simulate bumping the .rcf's slot count: a longer plaintext than the original.
            byte[] newRcf = Encoding.UTF8.GetBytes(
                "<REPLACEMENT_SYSTEM><INPUTS><INPUT NAME=\"LIVERY\" OPTIONS=\"7\" /></INPUTS>" +
                "<NAMES INPUT=\"LIVERY\"><NAME LIVERY=\"57\" NAME=\"Custom Slot 57\" /></NAMES></REPLACEMENT_SYSTEM>");

            string tempPath = Path.Combine(Path.GetTempPath(), $"bff-roundtrip-{Guid.NewGuid():N}.bff");
            try
            {
                BffPakRepacker.PatchEntries(snapshot, new Dictionary<int, byte[]> { [rcfIndex] = newRcf }, tempPath);

                byte[] patchedFileBytes = File.ReadAllBytes(tempPath);
                var patchedSnapshot = BffPakReader.Read(patchedFileBytes);

                Assert.AreEqual(2, patchedSnapshot.Entries.Count);

                var patchedRcfEntry = BffPakReader.TryFindEntryByPath(patchedSnapshot, RcfPath);
                Assert.IsNotNull(patchedRcfEntry);
                CollectionAssert.AreEqual(newRcf, BffEntryExtractor.ExtractPlaintext(patchedSnapshot, patchedRcfEntry!));

                var patchedOtherEntry = BffPakReader.TryFindEntryByPath(patchedSnapshot, DataPath);
                Assert.IsNotNull(patchedOtherEntry);
                CollectionAssert.AreEqual(otherPlaintext, BffEntryExtractor.ExtractPlaintext(patchedSnapshot, patchedOtherEntry!));
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void PatchEntries_RecomputesCrcSoPatchedEntryPassesJamcrcValidation()
        {
            byte[] originalRcf = Encoding.UTF8.GetBytes("<REPLACEMENT_SYSTEM><INPUTS><INPUT NAME=\"LIVERY\" OPTIONS=\"6\" /></INPUTS></REPLACEMENT_SYSTEM>");
            byte[] otherPlaintext = Encoding.ASCII.GetBytes("unchanged");

            byte[] fileBytes = BuildFakePak(originalRcf, otherPlaintext, out int rcfIndex, out _);
            var snapshot = BffPakReader.Read(fileBytes);

            byte[] newRcf = Encoding.UTF8.GetBytes("<REPLACEMENT_SYSTEM><INPUTS><INPUT NAME=\"LIVERY\" OPTIONS=\"9\" /></INPUTS></REPLACEMENT_SYSTEM>");

            string tempPath = Path.Combine(Path.GetTempPath(), $"bff-roundtrip-crc-{Guid.NewGuid():N}.bff");
            try
            {
                BffPakRepacker.PatchEntries(snapshot, new Dictionary<int, byte[]> { [rcfIndex] = newRcf }, tempPath);

                var patchedSnapshot = BffPakReader.Read(File.ReadAllBytes(tempPath));
                var patchedEntry = BffPakReader.TryFindEntryByPath(patchedSnapshot, RcfPath)!;

                byte[] onDiskBytes = patchedSnapshot.FullFileBytes
                    .AsSpan((int)patchedEntry.DataOffset, (int)patchedEntry.PakSize).ToArray();

                Assert.AreEqual(Jamcrc32.Compute(onDiskBytes), patchedEntry.Crc);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void AddEntry_AppendsNewEntry_ExistingEntriesStayByteIdenticalAndNewEntryDecodesCorrectly()
        {
            byte[] originalRcf = Encoding.UTF8.GetBytes(
                "<REPLACEMENT_SYSTEM><INPUTS><INPUT NAME=\"LIVERY\" OPTIONS=\"6\" /></INPUTS></REPLACEMENT_SYSTEM>");
            byte[] otherPlaintext = Encoding.ASCII.GetBytes("uncompressed entry data, unchanged");

            byte[] fileBytes = BuildFakePak(originalRcf, otherPlaintext, out _, out _);
            var snapshot = BffPakReader.Read(fileBytes);

            const string newPath = @"vehicles\testcar\new_texture.dds";
            byte[] newPlaintext = Encoding.ASCII.GetBytes("pretend this is compressed dds texture data");

            string tempPath = Path.Combine(Path.GetTempPath(), $"bff-addentry-{Guid.NewGuid():N}.bff");
            try
            {
                BffPakEntryInserter.AddEntry(snapshot, newPath, newPlaintext, compressionType: 0, tempPath);

                var patchedSnapshot = BffPakReader.Read(tempPath);
                Assert.AreEqual(3, patchedSnapshot.Entries.Count, "Should have the original 2 entries plus the new one.");

                var newEntry = BffPakReader.TryFindEntryByPath(patchedSnapshot, newPath);
                Assert.IsNotNull(newEntry);
                CollectionAssert.AreEqual(newPlaintext, BffEntryExtractor.ExtractPlaintext(patchedSnapshot, newEntry!));

                var rcfEntry = BffPakReader.TryFindEntryByPath(patchedSnapshot, RcfPath);
                Assert.IsNotNull(rcfEntry);
                CollectionAssert.AreEqual(originalRcf, BffEntryExtractor.ExtractPlaintext(patchedSnapshot, rcfEntry!));

                var otherEntry = BffPakReader.TryFindEntryByPath(patchedSnapshot, DataPath);
                Assert.IsNotNull(otherEntry);
                CollectionAssert.AreEqual(otherPlaintext, BffEntryExtractor.ExtractPlaintext(patchedSnapshot, otherEntry!));
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void AddEntry_PathAlreadyExists_Throws()
        {
            byte[] fileBytes = BuildFakePak(Encoding.UTF8.GetBytes("<x/>"), Encoding.ASCII.GetBytes("y"), out _, out _);
            var snapshot = BffPakReader.Read(fileBytes);

            string tempPath = Path.Combine(Path.GetTempPath(), $"bff-addentry-dup-{Guid.NewGuid():N}.bff");
            try
            {
                bool threw = false;
                try
                {
                    BffPakEntryInserter.AddEntry(snapshot, RcfPath, Encoding.ASCII.GetBytes("z"), compressionType: 0, tempPath);
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }

                Assert.IsTrue(threw, "Adding an entry at a path that already exists should throw InvalidOperationException.");
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}
