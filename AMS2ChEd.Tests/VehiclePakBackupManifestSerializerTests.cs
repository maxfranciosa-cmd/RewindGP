using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class VehiclePakBackupManifestSerializerTests
    {
        [TestMethod]
        public void Serialize_ThenParse_RoundTripsAllFields()
        {
            var manifest = new VehiclePakBackupManifest
            {
                InstallFolder = @"C:\Games\AMS2",
                Entries =
                {
                    new VehiclePakBackupEntry
                    {
                        RelativePakPath = @"Pakfiles\Vehicles\formula_hitech_g1m3.bff",
                        BackupFileName = "Pakfiles_Vehicles_formula_hitech_g1m3.bff.bak",
                        BackedUpAtUtc = new DateTime(2026, 1, 15, 12, 30, 0, DateTimeKind.Utc),
                        OriginalSha256 = "ABCDEF1234567890",
                    },
                    new VehiclePakBackupEntry
                    {
                        RelativePakPath = @"Pakfiles\Vehicles\vehiclespersistent.bff",
                        BackupFileName = "Pakfiles_Vehicles_vehiclespersistent.bff.bak",
                        BackedUpAtUtc = new DateTime(2026, 1, 15, 12, 30, 1, DateTimeKind.Utc),
                        OriginalSha256 = "1234567890ABCDEF",
                    },
                },
            };

            string json = VehiclePakBackupManifestSerializer.Serialize(manifest);
            var result = VehiclePakBackupManifestSerializer.Parse(json);

            Assert.AreEqual(manifest.InstallFolder, result.InstallFolder);
            Assert.AreEqual(2, result.Entries.Count);
            Assert.AreEqual(manifest.Entries[0].RelativePakPath, result.Entries[0].RelativePakPath);
            Assert.AreEqual(manifest.Entries[0].BackupFileName, result.Entries[0].BackupFileName);
            Assert.AreEqual(manifest.Entries[0].BackedUpAtUtc, result.Entries[0].BackedUpAtUtc);
            Assert.AreEqual(manifest.Entries[0].OriginalSha256, result.Entries[0].OriginalSha256);
            Assert.AreEqual(manifest.Entries[1].RelativePakPath, result.Entries[1].RelativePakPath);
        }

        [TestMethod]
        public void Serialize_EmptyManifest_RoundTrips()
        {
            var manifest = new VehiclePakBackupManifest { InstallFolder = @"C:\Games\AMS2" };

            var result = VehiclePakBackupManifestSerializer.Parse(VehiclePakBackupManifestSerializer.Serialize(manifest));

            Assert.AreEqual(manifest.InstallFolder, result.InstallFolder);
            Assert.AreEqual(0, result.Entries.Count);
        }

        [TestMethod]
        public void Parse_UnknownJsonShape_ReturnsEmptyManifestRatherThanThrowing()
        {
            var result = VehiclePakBackupManifestSerializer.Parse("null");

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Entries.Count);
        }
    }
}
