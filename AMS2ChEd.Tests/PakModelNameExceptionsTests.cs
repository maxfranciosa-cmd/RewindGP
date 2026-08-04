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

        [TestMethod]
        public void Resolve_ModelWithNoException_ReturnsSameModel()
        {
            Assert.AreEqual("formula_hitech_g1m3", PakModelNameExceptions.Resolve("formula_hitech_g1m3"));
        }
    }
}
