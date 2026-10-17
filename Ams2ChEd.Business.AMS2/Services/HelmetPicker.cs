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

        /// <summary>
        /// The helmet model a season's year implies. A car can override it with its own
        /// (car_model_capacities.json helmet_era) - pick by era wherever the car is known.
        /// </summary>
        public static HelmetEra EraForYear(int year)
        {
            if (year >= HELMET_70s_EARLIEST_YEAR && year < HELMET_80s_EARLIEST_YEAR)
                return HelmetEra.Seventies;
            if (year < HELMET_90s_EARLIEST_YEAR)
                return HelmetEra.Eighties;
            if (year < HELMET_MODERN_EARLIEST_YEAR)
                return HelmetEra.Nineties;
            return HelmetEra.Modern;
        }

        public static string PickHelmetFilePerYear(Ams2DriverData driver, int year) => PickHelmetFile(driver, EraForYear(year));

        public static string PickVisorFilePerYear(Ams2DriverData driver, int year) => PickVisorFile(driver, EraForYear(year));

        public static string DefaultBaseHelmetFile(int year) => DefaultBaseHelmetFile(EraForYear(year));

        public static string DefaultBaseVisorFile(int year) => DefaultBaseVisorFile(EraForYear(year));

        public static string PickHelmetFile(Ams2DriverData driver, HelmetEra era) => era switch
        {
            HelmetEra.Seventies => driver.BaseHelmetFile70s,
            HelmetEra.Eighties => driver.BaseHelmetFile80s,
            HelmetEra.Nineties => driver.BaseHelmetFile90s,
            _ => driver.BaseHelmetFile
        };

        // The 90s helmet has no separate visor texture.
        public static string PickVisorFile(Ams2DriverData driver, HelmetEra era) => era switch
        {
            HelmetEra.Seventies => driver.BaseVisorFile70s,
            HelmetEra.Eighties => driver.BaseVisorFile80s,
            HelmetEra.Nineties => "",
            _ => driver.BaseVisorFile
        };

        public static string DefaultBaseHelmetFile(HelmetEra era) => era switch
        {
            HelmetEra.Seventies => Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_70s.png"),
            HelmetEra.Eighties => Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_80s.png"),
            HelmetEra.Nineties => Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_90s.png"),
            _ => Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet.png")
        };

        public static string DefaultBaseVisorFile(HelmetEra era) => era switch
        {
            HelmetEra.Seventies => Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_70s.png"),
            HelmetEra.Eighties => Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_80s.png"),
            HelmetEra.Nineties => "",
            _ => Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor.png")
        };

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
