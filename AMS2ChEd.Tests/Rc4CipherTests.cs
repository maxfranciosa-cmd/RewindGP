using System.Text;
using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class Rc4CipherTests
    {
        [TestMethod]
        public void Transform_AppliedTwiceWithSameKey_RoundTripsToOriginal()
        {
            byte[] key = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[0]);
            byte[] original = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog.");
            byte[] data = (byte[])original.Clone();

            Rc4Cipher.Transform(data, key);
            Assert.IsFalse(data.SequenceEqual(original), "Encryption should change the bytes.");

            Rc4Cipher.Transform(data, key);
            CollectionAssert.AreEqual(original, data);
        }

        [TestMethod]
        public void Transform_DifferentKeyIndexes_ProduceDifferentCiphertext()
        {
            byte[] keyA = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[0]);
            byte[] keyB = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[4]);
            byte[] plaintext = Encoding.ASCII.GetBytes("some pak entry bytes");

            byte[] a = (byte[])plaintext.Clone();
            byte[] b = (byte[])plaintext.Clone();
            Rc4Cipher.Transform(a, keyA);
            Rc4Cipher.Transform(b, keyB);

            CollectionAssert.AreNotEqual(a, b);
        }

        [TestMethod]
        public void Descramble_TrimsAtFirstZeroByte_BeforeApplyingXorSwap()
        {
            // Raw key { 0x01, 0x02, 0x00, 0xFF, 0xFF } should be treated as only { 0x01, 0x02 }.
            byte[] raw = { 0x01, 0x02, 0x00, 0xFF, 0xFF };
            byte[] descrambled = Rc4Cipher.Descramble(raw);

            Assert.AreEqual(2, descrambled.Length);
        }

        [TestMethod]
        public void Descramble_AllThirtyTwoPc2AndAboveKeys_ProduceNonEmptyDistinctKeys()
        {
            var descrambled = Rc4KeySet.Pc2AndAboveRawKeys.Select(k => Rc4Cipher.Descramble(k)).ToList();

            Assert.AreEqual(32, descrambled.Count);
            Assert.IsTrue(descrambled.All(k => k.Length > 0));
            Assert.AreEqual(32, descrambled.Select(k => Convert.ToBase64String(k)).Distinct().Count(),
                "All 32 raw keys should descramble to distinct keys.");
        }
    }
}
