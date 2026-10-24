using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Ams2ChEd.Business.AMS2.GameLogic
{
    /// <summary>
    /// The livery preview AMS2 shows in its car selection screen (the team's LiveryPreview, or the
    /// race's livery override's), read from the season pack folder the race files are generated
    /// from. A team with no preview (baked season data with no pack) gets its drivers' helmets
    /// side by side on the team colour instead.
    /// </summary>
    public class Ams2TeamPreviewProvider : ITeamPreviewProvider
    {
        private readonly IDriversLoader<Ams2DriverData> _driversLoader;
        private readonly ICarModelCapacityLoader _carModelCapacityLoader;

        public Ams2TeamPreviewProvider(IDriversLoader<Ams2DriverData> driversLoader, ICarModelCapacityLoader carModelCapacityLoader)
        {
            _driversLoader = driversLoader;
            _carModelCapacityLoader = carModelCapacityLoader;
        }

        public byte[] LoadTeamPreviewPng(ISeason season, string teamId, int raceId, int maxWidth)
        {
            if (season.Teams.FirstOrDefault(t => t.TeamId == teamId) is not Ams2TeamEntry team)
                return null;

            var seasonDirectory = Path.GetDirectoryName(StoragePaths.SeasonFilePath(season.OriginalYear ?? season.Year));
            var previewFile = team.LiveryOverrides?.FirstOrDefault(o => o.RaceId == raceId)?.LiveryPreview ?? team.LiveryPreview;
            if (string.IsNullOrEmpty(previewFile))
                return LoadHelmetsTile(season, team, seasonDirectory, maxWidth);

            var previewPath = Path.IsPathRooted(previewFile) ? previewFile : Path.Combine(seasonDirectory, previewFile);
            if (!File.Exists(previewPath))
                return null;

            try
            {
                return DdsTextureComposer.LoadAsPng(previewPath, maxWidth);
            }
            catch (Exception ex)
            {
                // a picture that can't be decoded only costs the tile its picture
                Console.WriteLine($"Warning: livery preview could not be read: {previewPath} ({ex.Message})");
                return null;
            }
        }

        // The team's drivers' own helmet designs (no sponsor layers), each in the helmet model the
        // car's drivers wear; a driver with no design, or not in the season's data (the player),
        // wears the plain default helmet.
        private byte[] LoadHelmetsTile(ISeason season, Ams2TeamEntry team, string seasonDirectory, int maxWidth)
        {
            try
            {
                int year = season.OriginalYear ?? season.Year;
                var eras = _carModelCapacityLoader?.GetHelmetErasForClass((season as Ams2Season)?.Ams2Class);
                var era = team.Ams2Car != null && eras != null && eras.TryGetValue(team.Ams2Car, out var carEra) ? carEra : HelmetPicker.EraForYear(year);
                var maps = HelmetPreviewComposer.LoadMaps(Path.Combine(StoragePaths.HelmetPreviewsFolder, era.ToString()));
                if (maps == null)
                    return null;

                Dictionary<string, Ams2DriverData> drivers;
                try { drivers = _driversLoader.LoadDrivers(year); }
                catch { drivers = new Dictionary<string, Ams2DriverData>(); }

                var helmets = new List<Image<Rgba32>>();
                try
                {
                    foreach (var driverId in new[] { team.Driver1Contract?.DriverId, team.Driver2Contract?.DriverId }.Where(id => !string.IsNullOrEmpty(id)))
                    {
                        string design = drivers.TryGetValue(driverId, out var driver) ? HelmetPicker.PickHelmetFile(driver, era) : null;
                        string path = ResolveHelmet(design, seasonDirectory) ?? ResolveHelmet(HelmetPicker.DefaultBaseHelmetFile(era), seasonDirectory);
                        if (path == null)
                            continue;
                        using var texture = DdsTextureComposer.LoadImage(path);
                        helmets.Add(HelmetPreviewComposer.RenderHelmet(texture, maps));
                    }

                    string colour = team.GenericLivery?.Palette?.FirstOrDefault(p => string.Equals(p.Key, "red", StringComparison.OrdinalIgnoreCase)).Value ?? team.Color ?? "#808080";
                    using var tile = HelmetPreviewComposer.ComposeTeamTile(helmets, TemplateRecolour.ParseColour(colour));
                    if (maxWidth > 0 && tile.Width > maxWidth)
                        tile.Mutate(c => c.Resize(maxWidth, 0));
                    using var stream = new MemoryStream();
                    tile.SaveAsPng(stream);
                    return stream.ToArray();
                }
                finally
                {
                    foreach (var helmet in helmets)
                        helmet.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: helmet tile for {team.TeamId} could not be made: {ex.Message}");
                return null;
            }
        }

        private static string ResolveHelmet(string file, string seasonDirectory)
        {
            if (string.IsNullOrEmpty(file))
                return null;
            string path = Path.GetFullPath(Path.IsPathRooted(file) ? file : Path.Combine(seasonDirectory, file));
            return File.Exists(path) ? path : null;
        }
    }
}
