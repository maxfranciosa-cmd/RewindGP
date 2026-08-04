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

        private static string ExpectedLivery => Path.Combine(InstallFolder, "Pakfiles", "Vehicles", $"{Model}_Livery.bff");
        private static string ExpectedHdLivery => Path.Combine(InstallFolder, "Pakfiles", "Vehicles", $"{Model}_HD_Livery.bff");
        private static string ExpectedLdLivery => Path.Combine(InstallFolder, "Pakfiles", "Vehicles", $"{Model}_LD_Livery.bff");

        [TestMethod]
        public void GetLiveryPakPaths_OnlyBaseLiveryPakExists_ReturnsJustBaseLiveryPak()
        {
            var result = PakPathResolver.GetLiveryPakPaths(InstallFolder, Model, path => path == ExpectedLivery);

            CollectionAssert.AreEqual(new[] { ExpectedLivery }, result.ToList());
        }

        [TestMethod]
        public void GetLiveryPakPaths_AllThreeVariantsExist_ReturnsAllInOrder()
        {
            var result = PakPathResolver.GetLiveryPakPaths(InstallFolder, Model, _ => true);

            CollectionAssert.AreEqual(new[] { ExpectedLivery, ExpectedHdLivery, ExpectedLdLivery }, result.ToList());
        }

        [TestMethod]
        public void GetLiveryPakPaths_NothingExists_ReturnsEmpty()
        {
            var result = PakPathResolver.GetLiveryPakPaths(InstallFolder, Model, _ => false);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void GetLiveryPakPaths_ExceptionModel_ResolvesToPakName()
        {
            var result = PakPathResolver.GetLiveryPakPaths(InstallFolder, "formula_v10_g2_m", _ => true);

            var expected = new[]
            {
                Path.Combine(InstallFolder, "Pakfiles", "Vehicles", "formula_v10_m_Livery.bff"),
                Path.Combine(InstallFolder, "Pakfiles", "Vehicles", "formula_v10_m_HD_Livery.bff"),
                Path.Combine(InstallFolder, "Pakfiles", "Vehicles", "formula_v10_m_LD_Livery.bff"),
            };
            CollectionAssert.AreEqual(expected, result.ToList());
        }
    }
}
