using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AMS2ChEd.SeasonPackEditor.MainWindow;

namespace AMS2ChEd.SeasonPackEditor.Services
{
    /// <summary>
    /// Resolves a SeasonPackProject's relative texture/livery-xml references (TextureFiles/XmlFiles,
    /// "relative path -> absolute path" / "team id -> xml content") into self-contained clones that
    /// downstream consumers (Ams2LiveryService, SaveGame builders) can use without the project needing
    /// to live on disk in the canonical Seasons/&lt;year&gt;/ layout.
    /// </summary>
    public static class SeasonPackPathResolver
    {
        public static List<Ams2TeamEntry> ResolveTeams(IEnumerable<Ams2TeamEntry> teams, SeasonPackProject project)
        {
            var resolved = teams.Select(t => t.DeepClone()).ToList();

            foreach (var teamEntry in resolved)
            {
                // Embed livery XML content
                if (project.XmlFiles.TryGetValue(teamEntry.TeamId, out var xmlContent)
                    && !string.IsNullOrWhiteSpace(xmlContent))
                {
                    teamEntry.LiveryXml = ResolveXmlTexturePaths(xmlContent);
                }

                // Resolve team-level paths
                teamEntry.BaseLiveryDriver1 = Resolve(teamEntry.BaseLiveryDriver1, project.TextureFiles);
                teamEntry.BaseLiveryDriver2 = Resolve(teamEntry.BaseLiveryDriver2, project.TextureFiles);
                teamEntry.HelmetSponsors = Resolve(teamEntry.HelmetSponsors, project.TextureFiles);
                teamEntry.VisorSponsors = Resolve(teamEntry.VisorSponsors, project.TextureFiles);
                teamEntry.LiveryPreview = Resolve(teamEntry.LiveryPreview, project.TextureFiles);

                if (teamEntry.DriversSpecificHelmet != null)
                    teamEntry.DriversSpecificHelmet = teamEntry.DriversSpecificHelmet
                        .ToDictionary(kvp => kvp.Key, kvp => Resolve(kvp.Value, project.TextureFiles));

                if (teamEntry.NumbersPlacements != null)
                    foreach (var p in teamEntry.NumbersPlacements)
                        p.NumbersTexture = Resolve(p.NumbersTexture, project.TextureFiles);

                if (teamEntry.LiveryOverrides != null)
                {
                    foreach (var ov in teamEntry.LiveryOverrides)
                    {
                        ov.Driver1Livery = Resolve(ov.Driver1Livery, project.TextureFiles);
                        ov.Driver2Livery = Resolve(ov.Driver2Livery, project.TextureFiles);
                        ov.HelmetSponsors = Resolve(ov.HelmetSponsors, project.TextureFiles);
                        ov.VisorSponsors = Resolve(ov.VisorSponsors, project.TextureFiles);
                        ov.LiveryPreview = Resolve(ov.LiveryPreview, project.TextureFiles);

                        if (ov.DriversSpecificHelmet != null)
                            ov.DriversSpecificHelmet = ov.DriversSpecificHelmet
                                .ToDictionary(kvp => kvp.Key, kvp => Resolve(kvp.Value, project.TextureFiles));

                        if (ov.NumbersPlacements != null)
                            foreach (var p in ov.NumbersPlacements)
                                p.NumbersTexture = Resolve(p.NumbersTexture, project.TextureFiles);
                    }
                }
            }

            return resolved;
        }

        public static List<Ams2DriverData> ResolveDrivers(IEnumerable<Ams2DriverData> drivers, Dictionary<string, string> textureFiles)
        {
            var resolved = drivers.Select(d => d.DeepClone()).ToList();

            foreach (var driver in resolved)
            {
                driver.PictureUrl = Resolve(driver.PictureUrl, textureFiles);
                driver.BaseHelmetFile = Resolve(driver.BaseHelmetFile, textureFiles);
                driver.BaseVisorFile = Resolve(driver.BaseVisorFile, textureFiles);
                driver.BaseHelmetFile90s = Resolve(driver.BaseHelmetFile90s, textureFiles);
                driver.BaseHelmetFile80s = Resolve(driver.BaseHelmetFile80s, textureFiles);
                driver.BaseVisorFile80s = Resolve(driver.BaseVisorFile80s, textureFiles);
                driver.BaseHelmetFile70s = Resolve(driver.BaseHelmetFile70s, textureFiles);
                driver.BaseVisorFile70s = Resolve(driver.BaseVisorFile70s, textureFiles);
            }

            return resolved;
        }

        public static string ResolveXmlTexturePaths(string xmlContent)
        {
            string sampleBodiesPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SampleBodies");

            return System.Text.RegularExpressions.Regex.Replace(
                xmlContent,
                @"(?i)(PATH="")(Driver\\)([^""]*"")",
                m => m.Groups[1].Value
                    + Path.Combine(sampleBodiesPath, m.Groups[3].Value.TrimEnd('"'))
                    + "\"");
        }

        public static string Resolve(string relativePath, Dictionary<string, string> textureFiles)
        {
            if (string.IsNullOrEmpty(relativePath)) return relativePath;
            return textureFiles.TryGetValue(relativePath, out var absolute) ? absolute : relativePath;
        }
    }
}
