using System.Text;
using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class Jamcrc32Tests
    {
        [TestMethod]
        public void Compute_StandardCheckString_MatchesKnownJamcrcValue()
        {
            // Official CRC catalogue check value for JAMCRC ("check" field for the "123456789" vector).
            byte[] data = Encoding.ASCII.GetBytes("123456789");

            uint result = Jamcrc32.Compute(data);

            Assert.AreEqual(0x340BC6D9u, result);
        }

        [TestMethod]
        public void Compute_EmptyInput_ReturnsInitValue()
        {
            uint result = Jamcrc32.Compute(Array.Empty<byte>());

            Assert.AreEqual(0xFFFFFFFFu, result);
        }

        [TestMethod]
        public void Compute_DifferentInputs_ProduceDifferentValues()
        {
            uint a = Jamcrc32.Compute(Encoding.ASCII.GetBytes("hello"));
            uint b = Jamcrc32.Compute(Encoding.ASCII.GetBytes("world"));

            Assert.AreNotEqual(a, b);
        }
    }
}
