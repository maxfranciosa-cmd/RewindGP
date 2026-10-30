using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class PakModelNameExceptionsTests
    {
        [TestMethod]
        public void Resolve_FormulaV10G2B_ReturnsFormulaV10()
        {
            Assert.AreEqual("formula_v10", PakModelNameExceptions.Resolve("formula_v10_g2_b"));
        }

        [TestMethod]
        public void Resolve_FormulaV10G2M_ReturnsFormulaV10M()
        {
            Assert.AreEqual("formula_v10_m", PakModelNameExceptions.Resolve("formula_v10_g2_m"));
        }

        [DataTestMethod]
        [DataRow("formula_v8_g1_m", "formula_v8_g1")]
        [DataRow("formula_v10_g3_m", "formula_v10_g3")]
        public void Resolve_MichelinCarWithNoSuffix_ReturnsPlainName(string model, string pak)
        {
            Assert.AreEqual(pak, PakModelNameExceptions.Resolve(model));
            Assert.AreEqual(pak, PakModelNameExceptions.ResolveRcfFolder(model));
        }

        [DataTestMethod]
        [DataRow("formula_v8_g1_b", "formula_v8_g1")]
        [DataRow("formula_v10_g3_b", "formula_v10_g3")]
        public void ResolveRcfFolder_BridgestoneCar_SharesTheFolder_KeepsItsPak(string model, string folder)
        {
            Assert.AreEqual(model, PakModelNameExceptions.Resolve(model));
            Assert.AreEqual(folder, PakModelNameExceptions.ResolveRcfFolder(model));
        }

        [TestMethod]
        public void Resolve_ModelWithNoException_ReturnsSameModel()
        {
            Assert.AreEqual("formula_hitech_g1m3", PakModelNameExceptions.Resolve("formula_hitech_g1m3"));
        }
    }
}
