using System.Xml.Linq;
using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class RcfLiverySlotPatcherTests
    {
        private const int BaseLiveryNumber = 51;

        private static string MakeRcfXml(int slotCount)
        {
            var names = new List<XElement>();
            var conditions = new List<XElement>();

            for (int i = 0; i < slotCount; i++)
            {
                int id = BaseLiveryNumber + i;
                names.Add(new XElement("NAME", new XAttribute("LIVERY", id), new XAttribute("NAME", $"Team #{id}")));
                conditions.Add(new XElement("CONDITION", new XAttribute("LIVERY", id),
                    new XElement("REPLACE", new XAttribute("TEXTURE", "body_diff.dds"), new XAttribute("NEWTEXTURE", $"body_diff_{id}.dds"))));
            }

            var doc = new XDocument(
                new XElement("REPLACEMENT_SYSTEM",
                    new XElement("CONFIG",
                        new XElement("ALLOWUSEROVERRIDES", new XAttribute("VALUE", "1")),
                        new XElement("USEROVERRIDESFILE", new XAttribute("VALUE", @"Vehicles\Textures\CustomLiveries\Overrides\car\car.xml"))),
                    new XElement("INPUTS",
                        new XElement("INPUT", new XAttribute("NAME", "LIVERY"), new XAttribute("OPTIONS", slotCount))),
                    new XElement("NAMES", new XAttribute("INPUT", "LIVERY"), names),
                    conditions));

            return doc.ToString();
        }

        [TestMethod]
        public void TryEnsureSlotCount_AlreadySufficient_ReturnsFalseAndLeavesXmlUnchanged()
        {
            string rcf = MakeRcfXml(6);

            bool changed = RcfLiverySlotPatcher.TryEnsureSlotCount(rcf, requiredSlotCount: 6, BaseLiveryNumber,
                out string patchedXml, out int currentSlotCount);

            Assert.IsFalse(changed);
            Assert.AreEqual(6, currentSlotCount);
            Assert.AreEqual(rcf, patchedXml);
        }

        [TestMethod]
        public void TryEnsureSlotCount_FewerSlotsThanRequired_StillReturnsFalse()
        {
            // requiredSlotCount below what's already declared must also be a no-op.
            string rcf = MakeRcfXml(8);

            bool changed = RcfLiverySlotPatcher.TryEnsureSlotCount(rcf, requiredSlotCount: 6, BaseLiveryNumber,
                out _, out int currentSlotCount);

            Assert.IsFalse(changed);
            Assert.AreEqual(8, currentSlotCount);
        }

        [TestMethod]
        public void TryEnsureSlotCount_NeedsOneMoreSlot_BumpsOptionsAndAddsOneNameAndCondition()
        {
            string rcf = MakeRcfXml(6);

            bool changed = RcfLiverySlotPatcher.TryEnsureSlotCount(rcf, requiredSlotCount: 7, BaseLiveryNumber,
                out string patchedXml, out int currentSlotCount);

            Assert.IsTrue(changed);
            Assert.AreEqual(6, currentSlotCount);

            var doc = XDocument.Parse(patchedXml);
            var root = doc.Root!;

            int options = (int)root.Element("INPUTS")!.Elements("INPUT").Single().Attribute("OPTIONS")!;
            Assert.AreEqual(7, options);

            var names = root.Element("NAMES")!.Elements("NAME").ToList();
            Assert.AreEqual(7, names.Count);
            Assert.IsTrue(names.Any(n => (int)n.Attribute("LIVERY")! == 57));

            var conditions = root.Elements("CONDITION").ToList();
            Assert.AreEqual(7, conditions.Count);
            var newCondition = conditions.Single(c => (int)c.Attribute("LIVERY")! == 57);
            Assert.AreEqual("body_diff.dds", (string)newCondition.Element("REPLACE")!.Attribute("TEXTURE")!);
        }

        [TestMethod]
        public void TryEnsureSlotCount_NeedsSeveralMoreSlots_AddsSequentialIdsFromCurrentCount()
        {
            string rcf = MakeRcfXml(6);

            bool changed = RcfLiverySlotPatcher.TryEnsureSlotCount(rcf, requiredSlotCount: 10, BaseLiveryNumber,
                out string patchedXml, out _);

            Assert.IsTrue(changed);

            var doc = XDocument.Parse(patchedXml);
            var root = doc.Root!;

            var newIds = root.Element("NAMES")!.Elements("NAME")
                .Select(n => (int)n.Attribute("LIVERY")!)
                .OrderBy(id => id)
                .ToList();

            CollectionAssert.AreEqual(new[] { 51, 52, 53, 54, 55, 56, 57, 58, 59, 60 }, newIds);

            var newConditionIds = root.Elements("CONDITION")
                .Select(c => (int)c.Attribute("LIVERY")!)
                .OrderBy(id => id)
                .ToList();
            CollectionAssert.AreEqual(newIds, newConditionIds);
        }

        [TestMethod]
        public void TryEnsureSlotCount_CloneUsesHighestExistingSlotAsTemplate()
        {
            // Build a fixture where slot ids aren't in document order, to confirm the patcher
            // picks the highest LIVERY id (not the last element) as its clone template.
            var doc = new XDocument(
                new XElement("REPLACEMENT_SYSTEM",
                    new XElement("INPUTS",
                        new XElement("INPUT", new XAttribute("NAME", "LIVERY"), new XAttribute("OPTIONS", 2))),
                    new XElement("NAMES", new XAttribute("INPUT", "LIVERY"),
                        new XElement("NAME", new XAttribute("LIVERY", 52), new XAttribute("NAME", "Second")),
                        new XElement("NAME", new XAttribute("LIVERY", 51), new XAttribute("NAME", "First"))),
                    new XElement("CONDITION", new XAttribute("LIVERY", 51),
                        new XElement("REPLACE", new XAttribute("TEXTURE", "a.dds"), new XAttribute("NEWTEXTURE", "a1.dds"))),
                    new XElement("CONDITION", new XAttribute("LIVERY", 52),
                        new XElement("REPLACE", new XAttribute("TEXTURE", "b.dds"), new XAttribute("NEWTEXTURE", "b1.dds")))));

            bool changed = RcfLiverySlotPatcher.TryEnsureSlotCount(doc.ToString(), requiredSlotCount: 3, BaseLiveryNumber,
                out string patchedXml, out _);

            Assert.IsTrue(changed);
            var root = XDocument.Parse(patchedXml).Root!;
            var newCondition = root.Elements("CONDITION").Single(c => (int)c.Attribute("LIVERY")! == 53);
            Assert.AreEqual("b.dds", (string)newCondition.Element("REPLACE")!.Attribute("TEXTURE")!, "Should have cloned slot 52 (the highest id), not slot 51.");
        }
    }
}
