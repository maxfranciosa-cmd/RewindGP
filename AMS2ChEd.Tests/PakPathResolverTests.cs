using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class PakPathResolverTests
    {
        private const string InstallFolder = @"C:\Games\AMS2";
        private const string Model = "formula_hitech_g1m3";

        private static string ExpectedBase => Path.Combine(InstallFolder, "Pakfiles", "Vehicles", $"{Model}.bff");
        private static string ExpectedLd => Path.Combine(InstallFolder, "Pakfiles", "Vehicles", $"{Model}_LD.bff");
        private static string ExpectedHd => Path.Combine(InstallFolder, "Pakfiles", "Vehicles", $"{Model}_HD.bff");

        [TestMethod]
        public void GetPerCarPakPaths_OnlyBasePakExists_ReturnsJustBasePak()
        {
            var result = PakPathResolver.GetPerCarPakPaths(InstallFolder, Model, path => path == ExpectedBase);

            CollectionAssert.AreEqual(new[] { ExpectedBase }, result.ToList());
        }

        [TestMethod]
        public void GetPerCarPakPaths_BaseAndLdExist_ReturnsBoth()
        {
            var result = PakPathResolver.GetPerCarPakPaths(InstallFolder, Model,
                path => path == ExpectedBase || path == ExpectedLd);

            CollectionAssert.AreEqual(new[] { ExpectedBase, ExpectedLd }, result.ToList());
        }

        [TestMethod]
        public void GetPerCarPakPaths_AllThreeVariantsExist_ReturnsAllInOrder()
        {
            var result = PakPathResolver.GetPerCarPakPaths(InstallFolder, Model, _ => true);

            CollectionAssert.AreEqual(new[] { ExpectedBase, ExpectedLd, ExpectedHd }, result.ToList());
        }

        [TestMethod]
        public void GetPerCarPakPaths_NothingExists_ReturnsEmpty()
        {
            var result = PakPathResolver.GetPerCarPakPaths(InstallFolder, Model, _ => false);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void GetPerCarPakPaths_ExceptionModel_ResolvesToPakName()
        {
            var result = PakPathResolver.GetPerCarPakPaths(InstallFolder, "formula_v10_g2_b", _ => true);

            var expected = new[]
            {
                Path.Combine(InstallFolder, "Pakfiles", "Vehicles", "formula_v10.bff"),
                Path.Combine(InstallFolder, "Pakfiles", "Vehicles", "formula_v10_LD.bff"),
                Path.Combine(InstallFolder, "Pakfiles", "Vehicles", "formula_v10_HD.bff"),
            };
            CollectionAssert.AreEqual(expected, result.ToList());
        }

        [TestMethod]
        public void GetPersistentPakPath_ReturnsExpectedPath()
        {
            string result = PakPathResolver.GetPersistentPakPath(InstallFolder);

            Assert.AreEqual(Path.Combine(InstallFolder, "Pakfiles", "Vehicles", "vehiclespersistent.bff"), result);
        }
    }
}
