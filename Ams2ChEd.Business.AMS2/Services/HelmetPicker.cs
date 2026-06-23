using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;

namespace Ams2ChEd.Business.AMS2.Services
{
    public enum HelmetEra
    {
        Seventies,
        Eighties,
        Nineties,
        Modern
    }

    public class GenericHelmetDesign
    {
        public string HelmetFile { get; set; }
        public string VisorFile { get; set; }
        public string PreviewImage { get; set; }

        public HelmetEra Era { get; set; }
    }

    public class HelmetPicker
    {
        public static int HELMET_70s_EARLIEST_YEAR = 1970;
        public static int HELMET_80s_EARLIEST_YEAR = 1986;
        public static int HELMET_90s_EARLIEST_YEAR = 1988;
        public static int HELMET_MODERN_EARLIEST_YEAR = 1996;

        public static string PickHelmetFilePerYear(Ams2DriverData driver, int year)
        {
            if (year >= HELMET_70s_EARLIEST_YEAR && year < HELMET_80s_EARLIEST_YEAR)
            {
                return driver.BaseHelmetFile70s;
            }
            else if (year < HELMET_90s_EARLIEST_YEAR)
            {
                return driver.BaseHelmetFile80s;
            }
            else if (year < HELMET_MODERN_EARLIEST_YEAR)
            {
                return driver.BaseHelmetFile90s;
            }
            else
            {
                return driver.BaseHelmetFile;
            }
        }

        public static string PickVisorFilePerYear(Ams2DriverData driver, int year)
        {
            if (year >= HELMET_70s_EARLIEST_YEAR && year < HELMET_80s_EARLIEST_YEAR)
            {
                return driver.BaseVisorFile70s;
            }
            else if (year < HELMET_90s_EARLIEST_YEAR)
            {
                return driver.BaseVisorFile80s;
            }
            else if (year < HELMET_MODERN_EARLIEST_YEAR)
            {
                return "";
            }
            else
            {
                return driver.BaseVisorFile;
            }
        }

        public static string DefaultBaseHelmetFile(int year)
        {
            if (year >= HELMET_70s_EARLIEST_YEAR && year < HELMET_80s_EARLIEST_YEAR)
            {
                return Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_70s.png");
            }
            else if (year < HELMET_90s_EARLIEST_YEAR)
            {
                return Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_80s.png");
            }
            else if (year < HELMET_MODERN_EARLIEST_YEAR)
            {
                return Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_90s.png");
            }
            else
            {
                return Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet.png");
            }
        }

        public static string DefaultBaseVisorFile(int year)
        {
            if (year >= HELMET_70s_EARLIEST_YEAR && year < HELMET_80s_EARLIEST_YEAR)
            {
                return Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_70s.png");
            }
            else if (year < HELMET_90s_EARLIEST_YEAR)
            {
                return Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_80s.png");
            }
            else if (year < HELMET_MODERN_EARLIEST_YEAR)
            {
                return "";
            }
            else
            {
                return Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor.png");
            }
        }

        private static List<GenericHelmetDesign> _genericHelmetDesigns;
        public static IEnumerable<GenericHelmetDesign> LoadGenericHelmetDesigns()
        {
            if (_genericHelmetDesigns != null)
                return _genericHelmetDesigns;

            var baseHelmetLiveriesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Drivers", "base_helmet_liveries");
            var defaultHelmetsPath = Path.Combine(baseHelmetLiveriesPath, "player_helmets", "helmets");
            var defaultVisorsPath = Path.Combine(baseHelmetLiveriesPath, "player_helmets", "visors");
            var defaultPreviewsPath = Path.Combine(baseHelmetLiveriesPath, "player_helmets", "previews");

            _genericHelmetDesigns = new List<GenericHelmetDesign>();

            if (!Directory.Exists(defaultHelmetsPath))
            {
                return _genericHelmetDesigns;
            }

            _genericHelmetDesigns = Directory.GetFiles(defaultHelmetsPath).Select(h => {
                var fileNameWithNoPath = Path.GetFileName(h);
                var visorFile = Path.Combine(defaultVisorsPath, fileNameWithNoPath);
                var previewFile = Path.Combine(defaultPreviewsPath, Path.ChangeExtension(fileNameWithNoPath, "png"));
                return new GenericHelmetDesign
                {
                    HelmetFile = Path.Combine("..", "..", Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, h)),
                    VisorFile = File.Exists(visorFile) ? Path.Combine("..","..",Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, visorFile)) : "",
                    PreviewImage = previewFile,
                    Era = GetEraFromGenericHelmetName(fileNameWithNoPath)
                };
            }).ToList();

            return _genericHelmetDesigns;
        }

        public static IEnumerable<GenericHelmetDesign> LoadGenericHelmetDesignsPerYear(int year)
        {

            HelmetEra era = HelmetEra.Modern;
            if (year >= HELMET_70s_EARLIEST_YEAR && year < HELMET_80s_EARLIEST_YEAR)
            {
                era = HelmetEra.Seventies;
            }
            else if (year < HELMET_90s_EARLIEST_YEAR)
            {
                era = HelmetEra.Eighties;
            }
            else if (year < HELMET_MODERN_EARLIEST_YEAR)
            {
                era = HelmetEra.Nineties;
            }

            return LoadGenericHelmetDesigns().Where(h => h.Era == era).ToList();
        }

        private static HelmetEra GetEraFromGenericHelmetName(string fileName)
        {
            if (fileName.StartsWith("70s_"))
                return HelmetEra.Seventies;
            if (fileName.StartsWith("80s_"))
                return HelmetEra.Eighties;
            if (fileName.StartsWith("90s_"))
                return HelmetEra.Nineties;
            return HelmetEra.Modern;
        }

    }
}
