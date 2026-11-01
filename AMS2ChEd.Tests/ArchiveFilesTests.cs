using System.IO;
using System.IO.Compression;
using Ams2ChEd.Business.AMS2.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class ArchiveFilesTests
    {
        private string _root;

        [TestInitialize]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ArchiveFilesTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void TearDown() => Directory.Delete(_root, true);

        private string Zip(params string[] entries)
        {
            var path = Path.Combine(_root, "download.zip");
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var entry in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(entry).Open());
                writer.Write("content");
            }
            return path;
        }

        [TestMethod]
        public void ExtractSoleFile_OnlyFileIsThePack_ExtractsIt()
        {
            var extracted = ArchiveFiles.ExtractSoleFile(Zip("Season 1996/season_1996.rwgp"), ".rwgp", Path.Combine(_root, "out"));

            Assert.IsNotNull(extracted);
            Assert.AreEqual("season_1996.rwgp", Path.GetFileName(extracted));
            Assert.AreEqual("content", File.ReadAllText(extracted));
        }

        [TestMethod]
        public void ExtractSoleFile_ExtensionIgnoresCase()
        {
            Assert.IsNotNull(ArchiveFiles.ExtractSoleFile(Zip("SEASON.RWGP"), ".rwgp", Path.Combine(_root, "out")));
        }

        [TestMethod]
        public void ExtractSoleFile_OtherFilesBesideThePack_ReturnsNullAndExtractsNothing()
        {
            var output = Path.Combine(_root, "out");

            Assert.IsNull(ArchiveFiles.ExtractSoleFile(Zip("season.rwgp", "readme.txt"), ".rwgp", output));
            Assert.IsFalse(Directory.Exists(output));
        }

        [TestMethod]
        public void ExtractSoleFile_OnlyFileIsSomethingElse_ReturnsNull()
        {
            Assert.IsNull(ArchiveFiles.ExtractSoleFile(Zip("Vehicles/livery.dds"), ".rwgp", Path.Combine(_root, "out")));
        }
    }
}
