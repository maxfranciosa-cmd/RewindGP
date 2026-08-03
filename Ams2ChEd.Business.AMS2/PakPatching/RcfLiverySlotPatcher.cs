using System.Xml.Linq;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Bumps a car's .rcf "REPLACEMENT_SYSTEM" livery slot count, per
    /// AMS2-livery-modding-knowledge.md's "Adding a genuinely new slot" section:
    /// bump &lt;INPUT NAME="LIVERY" OPTIONS="N"&gt;, and for each new slot id, clone the
    /// highest-existing &lt;NAME LIVERY="id"&gt; and &lt;CONDITION LIVERY="id"&gt; blocks (nothing in the
    /// schema requires the underlying texture reference to be unique, so bootstrapping a new slot
    /// by duplicating an existing one's CONDITION is safe - the loose Overrides XML is what
    /// actually points it at real content).
    /// </summary>
    public static class RcfLiverySlotPatcher
    {
        private const string LiveryInputName = "LIVERY";

        public static bool TryEnsureSlotCount(
            string rcfXml,
            int requiredSlotCount,
            int baseLiveryNumber,
            out string patchedXml,
            out int currentSlotCount)
        {
            var doc = XDocument.Parse(rcfXml);
            var root = doc.Root ?? throw new InvalidDataException("Empty .rcf document.");
            if (root.Name != "REPLACEMENT_SYSTEM")
                throw new InvalidDataException($"Unexpected .rcf root element '{root.Name}' (expected REPLACEMENT_SYSTEM).");

            var inputElement = root.Element("INPUTS")?.Elements("INPUT")
                .FirstOrDefault(e => (string?)e.Attribute("NAME") == LiveryInputName)
                ?? throw new InvalidDataException("No <INPUT NAME=\"LIVERY\"> element found in .rcf.");

            currentSlotCount = (int?)inputElement.Attribute("OPTIONS")
                ?? throw new InvalidDataException("<INPUT NAME=\"LIVERY\"> has no OPTIONS attribute.");

            if (currentSlotCount >= requiredSlotCount)
            {
                patchedXml = rcfXml;
                return false;
            }

            var namesElement = root.Elements("NAMES")
                .FirstOrDefault(e => (string?)e.Attribute("INPUT") == LiveryInputName)
                ?? throw new InvalidDataException("No <NAMES INPUT=\"LIVERY\"> element found in .rcf.");

            var nameChildren = namesElement.Elements("NAME").ToList();
            if (nameChildren.Count == 0)
                throw new InvalidDataException("<NAMES INPUT=\"LIVERY\"> has no <NAME> children to clone from.");

            var conditionElements = root.Elements("CONDITION").ToList();

            var templateName = nameChildren
                .OrderByDescending(e => (int?)e.Attribute("LIVERY") ?? int.MinValue)
                .First();
            int templateId = (int)templateName.Attribute("LIVERY")!;

            var templateCondition = conditionElements
                .FirstOrDefault(e => (int?)e.Attribute("LIVERY") == templateId)
                ?? throw new InvalidDataException($"No <CONDITION LIVERY=\"{templateId}\"> found matching the template <NAME> slot.");

            for (int newId = baseLiveryNumber + currentSlotCount; newId < baseLiveryNumber + requiredSlotCount; newId++)
            {
                var newName = new XElement(templateName);
                newName.SetAttributeValue("LIVERY", newId);
                newName.SetAttributeValue("NAME", $"Custom Slot {newId}");
                namesElement.Add(newName);

                var newCondition = new XElement(templateCondition);
                newCondition.SetAttributeValue("LIVERY", newId);
                root.Add(newCondition);
            }

            inputElement.SetAttributeValue("OPTIONS", requiredSlotCount);

            patchedXml = doc.Declaration != null
                ? doc.Declaration + Environment.NewLine + doc.ToString()
                : doc.ToString();
            return true;
        }
    }
}
