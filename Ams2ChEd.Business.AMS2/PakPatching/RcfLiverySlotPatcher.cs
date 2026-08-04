using System.Xml.Linq;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Bumps a car's .rcf "REPLACEMENT_SYSTEM" livery slot count, per
    /// AMS2-livery-modding-knowledge.md's "Adding a genuinely new slot" section:
    /// bump &lt;INPUT NAME="LIVERY" OPTIONS="N"&gt;, and for each new slot id, clone an
    /// existing &lt;NAME LIVERY="id"&gt; and &lt;CONDITION LIVERY="id"&gt; pair, then repoint the clone's
    /// NEWTEXTURE at a caller-supplied path (see newSlotTexturePath below). Confirmed against a
    /// real install that the caller-supplied path can simply be the SAME texture an existing slot
    /// already references (see Ams2VehicleLiverySlotPatcher's class doc comment) - no new, distinct
    /// texture entry is required for the new slot to render correctly in-game. Prefers the
    /// highest-id slot that uses a plain TEXTURE replace over one using a MATERIAL replace - see
    /// the template-selection comment below.
    /// </summary>
    public static class RcfLiverySlotPatcher
    {
        private const string LiveryInputName = "LIVERY";

        /// <summary>Reads a .rcf's currently-declared LIVERY slot count without patching anything.</summary>
        public static int PeekSlotCount(string rcfXml)
        {
            var doc = XDocument.Parse(rcfXml);
            var root = doc.Root ?? throw new InvalidDataException("Empty .rcf document.");
            var inputElement = root.Element("INPUTS")?.Elements("INPUT")
                .FirstOrDefault(e => (string?)e.Attribute("NAME") == LiveryInputName)
                ?? throw new InvalidDataException("No <INPUT NAME=\"LIVERY\"> element found in .rcf.");

            return (int?)inputElement.Attribute("OPTIONS")
                ?? throw new InvalidDataException("<INPUT NAME=\"LIVERY\"> has no OPTIONS attribute.");
        }

        /// <summary>
        /// Finds the lowest-id plain-TEXTURE-replace CONDITION's NEWTEXTURE value - the template
        /// texture every new slot's own NEWTEXTURE is repointed at directly (confirmed in-game to
        /// render correctly - see Ams2VehicleLiverySlotPatcher's class doc comment). Returns null
        /// if the .rcf can't be parsed or has no such slot.
        /// </summary>
        public static string? TryGetReusableTexturePath(string rcfXml)
        {
            XElement? root;
            try { root = XDocument.Parse(rcfXml).Root; }
            catch { return null; }
            if (root == null) return null;

            var candidate = root.Elements("CONDITION")
                .Where(e => e.Attribute("LIVERY") != null && IsPlainTextureCondition(e))
                .OrderBy(e => (int)e.Attribute("LIVERY")!)
                .FirstOrDefault();

            var textureReplace = candidate?.Elements("REPLACE").FirstOrDefault(r => r.Attribute("TEXTURE") != null);
            return (string?)textureReplace?.Attribute("NEWTEXTURE");
        }

        // Some cars mix a plain TEXTURE-replace pattern (what the loose Overrides XML's
        // LIVERY_OVERRIDE/<TEXTURE NAME="BODY"> actually controls) with a MATERIAL-replace pattern
        // on their last few slots (observed on formula_hitech_g1m3's ids 55/56, which route paint
        // through a shared Vehicles\_Generic_Materials .mtx). A plain-TEXTURE slot is both the safe
        // clone template (see TryEnsureSlotCount) and the safe slot to borrow a texture reference
        // from (see TryGetReusableTexturePath).
        private static bool IsPlainTextureCondition(XElement c)
        {
            var replaces = c.Elements("REPLACE").ToList();
            return replaces.Count > 0 && replaces.All(r => r.Attribute("MATERIAL") == null);
        }

        /// <param name="newSlotTexturePath">
        /// Given a new LIVERY id, returns the NEWTEXTURE value its cloned CONDITION should use -
        /// the caller (Ams2VehicleLiverySlotPatcher) is expected to have already ensured a texture
        /// entry actually exists at that path in the corresponding _Livery.bff pak(s).
        /// </param>
        public static bool TryEnsureSlotCount(
            string rcfXml,
            int requiredSlotCount,
            int baseLiveryNumber,
            Func<int, string> newSlotTexturePath,
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

            // Pick the clone template by CONDITION shape, not just the highest LIVERY id - see
            // IsPlainTextureCondition's doc comment. Prefer the highest-id plain-TEXTURE condition;
            // only fall back to the overall highest id (old behavior) if every existing slot uses
            // the MATERIAL pattern.
            var conditionsWithId = conditionElements.Where(e => e.Attribute("LIVERY") != null).ToList();
            var preferredConditions = conditionsWithId.Where(IsPlainTextureCondition).ToList();

            var templateCondition = (preferredConditions.Count > 0 ? preferredConditions : conditionsWithId)
                .OrderByDescending(e => (int)e.Attribute("LIVERY")!)
                .FirstOrDefault()
                ?? throw new InvalidDataException("No <CONDITION LIVERY=\"...\"> element found to use as a clone template.");
            int templateId = (int)templateCondition.Attribute("LIVERY")!;

            var templateName = nameChildren.FirstOrDefault(e => (int?)e.Attribute("LIVERY") == templateId)
                ?? throw new InvalidDataException($"No <NAME LIVERY=\"{templateId}\"> found matching the template <CONDITION> slot.");

            // Insert new CONDITION elements right after the template (grouped with the other
            // LIVERY conditions), not appended at the very end of root - a real .rcf interleaves
            // CONDITION blocks for multiple INPUTs as flat root-level siblings (LIVERY conditions,
            // then TIRE conditions, then DIRTTYPE conditions, ...), and root.Add() was dropping the
            // new slot after all of them, physically separated from every other LIVERY condition.
            var conditionInsertionPoint = templateCondition;

            for (int newId = baseLiveryNumber + currentSlotCount; newId < baseLiveryNumber + requiredSlotCount; newId++)
            {
                var newName = new XElement(templateName);
                newName.SetAttributeValue("LIVERY", newId);
                newName.SetAttributeValue("NAME", $"Custom Slot {newId}");
                namesElement.Add(newName);

                var newCondition = new XElement(templateCondition);
                newCondition.SetAttributeValue("LIVERY", newId);

                // Both known real-world CONDITION shapes (plain-TEXTURE, and the MATERIAL-replace
                // fallback which still ends with one TEXTURE replace targeting a legacy/placeholder
                // asset) have exactly one REPLACE with a TEXTURE attribute - that's the one whose
                // NEWTEXTURE needs to point at this new slot's own texture, not the template's.
                var textureReplace = newCondition.Elements("REPLACE").FirstOrDefault(r => r.Attribute("TEXTURE") != null)
                    ?? throw new InvalidDataException($"Template <CONDITION LIVERY=\"{templateId}\"> has no <REPLACE TEXTURE=\"...\"> element to repoint.");
                textureReplace.SetAttributeValue("NEWTEXTURE", newSlotTexturePath(newId));

                conditionInsertionPoint.AddAfterSelf(newCondition);
                conditionInsertionPoint = newCondition;
            }

            inputElement.SetAttributeValue("OPTIONS", requiredSlotCount);

            patchedXml = doc.Declaration != null
                ? doc.Declaration + Environment.NewLine + doc.ToString()
                : doc.ToString();
            return true;
        }
    }
}
