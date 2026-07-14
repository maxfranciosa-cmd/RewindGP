using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AMS2ChEd.SeasonPackEditor
{
    internal static class ExternalLiveryXmlImportHelper
    {
        public class LiveryCandidate
        {
            public string Name { get; set; }
            public string LiveryId { get; set; }
            public XElement LiveryOverrideNode { get; set; }
        }

        public static List<LiveryCandidate> FindLiveryOverrideCandidates(XDocument xmlDoc)
        {
            return xmlDoc.Descendants("LIVERY_OVERRIDE")
                .Where(node => node.Attribute("NAME") != null && node.Attribute("LIVERY") != null)
                .Select(node => new LiveryCandidate
                {
                    Name = node.Attribute("NAME").Value,
                    LiveryId = node.Attribute("LIVERY").Value,
                    LiveryOverrideNode = node
                })
                .ToList();
        }

        public static string ResolveXmlRelativePath(string xmlDirectory, string relativePath)
        {
            return Path.Combine(xmlDirectory, relativePath);
        }

        public static string GetTexturePath(XElement liveryOverrideNode, string textureName)
        {
            return liveryOverrideNode.Descendants("TEXTURE")
                .FirstOrDefault(t => t.Attribute("NAME")?.Value == textureName)
                ?.Attribute("PATH")?.Value;
        }

        public static string GetPreviewImagePath(XElement liveryOverrideNode)
        {
            return liveryOverrideNode.Descendants("PREVIEWIMAGE")
                .FirstOrDefault()
                ?.Attribute("PATH")?.Value;
        }

        /// <summary>
        /// Resolves the source path to store in an ExternalLiveriesEntry: relative to the folder
        /// 6 directory levels above the xml file's own folder. This matches the AMS2 mod-folder
        /// convention: &lt;mod root&gt;\&lt;ModFolderName&gt;\Vehicles\Textures\CustomLiveries\Overrides\&lt;car&gt;\&lt;xmlfile&gt;,
        /// where &lt;car&gt; (the xml's folder) sits 6 levels below the folder above &lt;ModFolderName&gt;.
        /// </summary>
        public static string ComputeExternalSourcePath(string xmlDirectory, string fullFilePath)
        {
            string ancestor = xmlDirectory;
            for (int i = 0; i < 6; i++)
            {
                ancestor = Path.GetDirectoryName(ancestor);
                if (string.IsNullOrEmpty(ancestor))
                {
                    throw new InvalidOperationException(
                        "The selected XML file is not located deep enough inside a mod folder structure to compute an external source path (expected at least 6 folder levels above the XML file).");
                }
            }

            return Path.GetRelativePath(ancestor, fullFilePath);
        }

        /// <summary>
        /// Builds a USER_OVERRIDES xml document containing full copies (including subchildren) of the
        /// LIVERY_OVERRIDE, HELMET_OVERRIDE and OUTFIT_OVERRIDE nodes whose LIVERY attribute equals liveryId.
        /// </summary>
        public static string BuildUserOverridesXml(XDocument xmlDoc, string liveryId)
        {
            var root = new XElement("USER_OVERRIDES");

            var liveryOverride = xmlDoc.Descendants("LIVERY_OVERRIDE")
                .FirstOrDefault(node => node.Attribute("LIVERY")?.Value == liveryId);
            if (liveryOverride != null)
                root.Add(new XElement(liveryOverride));

            var helmetOverride = xmlDoc.Descendants("HELMET_OVERRIDE")
                .FirstOrDefault(node => node.Attribute("LIVERY")?.Value == liveryId);
            if (helmetOverride != null)
                root.Add(new XElement(helmetOverride));

            var outfitOverride = xmlDoc.Descendants("OUTFIT_OVERRIDE")
                .FirstOrDefault(node => node.Attribute("LIVERY")?.Value == liveryId);
            if (outfitOverride != null)
                root.Add(new XElement(outfitOverride));

            return root.ToString();
        }

        /// <summary>
        /// returns a list of all the relative paths to external textures referenced in the
        /// xml document for that livery inside any "TEXTURE" elements for that specific liveryid.
        /// this is used for any static texture (so no livery, helmet or preview).
        /// </summary>
        public static string[] GetStaticExternalTexturesToLoad(XDocument xmlDoc, string liveryId)
        {
            var root = new XElement("USER_OVERRIDES");
            var result = new List<string>();

            var liveryOverride = xmlDoc.Descendants("LIVERY_OVERRIDE")
                .FirstOrDefault(node => node.Attribute("LIVERY")?.Value == liveryId);
            if (liveryOverride != null)
            {
                result.AddRange(liveryOverride.Descendants("TEXTURE")
                    .Where(t => t.Attribute("PATH")?.Value != null && t.Attribute("NAME")?.Value != "BODY")
                    .Select(t => t.Attribute("PATH").Value));
            }

            var outfitOverride = xmlDoc.Descendants("OUTFIT_OVERRIDE")
                .FirstOrDefault(node => node.Attribute("LIVERY")?.Value == liveryId);
            if (outfitOverride != null)
            {
                result.AddRange(outfitOverride.Descendants("TEXTURE")
                    .Where(t => t.Attribute("PATH")?.Value != null)
                    .Select(t => t.Attribute("PATH").Value));
            }

            return result.ToArray();
        }
    }
}
