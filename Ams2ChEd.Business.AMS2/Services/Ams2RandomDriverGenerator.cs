using System.Text.Json;
using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Services;

namespace Ams2ChEd.Business.AMS2.Services
{
    public class Ams2RandomDriverGenerator : RandomDriverGenerator
    {
        // strong, distinct helmet colours: no two keys of one helmet get the same
        private static readonly string[] HelmetColours =
        {
            "#FFFFFF", "#101010", "#D01010", "#F0D000", "#1040C0", "#10A040", "#F07000", "#80C0F0",
            "#600090", "#E060A0", "#008080", "#800010", "#C0C0C0", "#002060", "#90D020", "#A06030"
        };

        protected override IDriverData InitializeDriverData(IDriverData provisionalDriverData, int year)
        {
            var ams2DriverData = provisionalDriverData.ConvertToChild<IDriverData, Ams2DriverData>();

            // no helmet file of their own: the livery service builds their generic helmet for
            // whichever helmet model their car wears, with the plain default visor
            ams2DriverData.GenericHelmet = RandomGenericHelmet(StoragePaths.GenericHelmetDesignsFolder);

            return ams2DriverData;
        }

        // one of the generic designs drawn for every helmet model, every colour key it uses
        // (its <design>.json) in a random colour; null when no design is installed, leaving the
        // plain default helmet
        public static GenericHelmet RandomGenericHelmet(string folder)
        {
            if (!Directory.Exists(folder))
                return null;

            var designs = Directory.GetFiles(folder, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(design => Enum.GetValues<HelmetEra>().All(era => File.Exists(GenericHelmetComposer.DesignFile(folder, design, era))))
                .ToList();
            if (designs.Count == 0)
                return null;

            var design = designs[Random.Shared.Next(designs.Count)];
            var colours = HelmetColours.OrderBy(_ => Random.Shared.Next()).ToList();
            var palette = PaletteKeys(Path.Combine(folder, design + ".json"))
                .Select((key, i) => (key, colour: colours[i % colours.Count]))
                .ToDictionary(k => k.key, k => k.colour);

            return new GenericHelmet { Design = design, Palette = palette };
        }

        private static List<string> PaletteKeys(string designJson)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(designJson));
                if (document.RootElement.TryGetProperty("palette", out var palette))
                    return palette.EnumerateObject().Select(k => k.Name).ToList();
            }
            catch (Exception)
            {
                // an unreadable design keeps its own colours
            }
            return new List<string>();
        }
    }
}
