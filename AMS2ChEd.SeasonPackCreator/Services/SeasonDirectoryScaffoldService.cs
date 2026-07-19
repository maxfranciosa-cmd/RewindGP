using System.IO;
using System.Linq;
using static AMS2ChEd.SeasonPackEditor.MainWindow;

namespace AMS2ChEd.SeasonPackEditor.Services
{
    /// <summary>
    /// Builds the on-disk subfolders of a Seasons/&lt;year&gt;/ layout that have no in-memory
    /// resolvable equivalent (unlike textures/livery-xml, which SeasonPackPathResolver resolves
    /// directly from TextureFiles/XmlFiles without touching disk).
    /// </summary>
    public static class SeasonDirectoryScaffoldService
    {
        public static void BuildStaticAssetsOnly(SeasonPackProject project, string tempSeasonDir)
        {
            if (project.StaticAssetFiles == null || !project.StaticAssetFiles.Any())
                return;

            var staticAssetsDir = Path.Combine(tempSeasonDir, "static_assets");
            Directory.CreateDirectory(staticAssetsDir);

            foreach (var assetFile in project.StaticAssetFiles)
            {
                if (File.Exists(assetFile.FullPath))
                {
                    var destPath = Path.Combine(staticAssetsDir, assetFile.FilePath);
                    var destDir = Path.GetDirectoryName(destPath);

                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(assetFile.FullPath, destPath, true);
                }
            }
        }
    }
}
