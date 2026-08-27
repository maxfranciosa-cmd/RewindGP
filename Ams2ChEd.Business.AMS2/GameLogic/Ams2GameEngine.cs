using Ams2ChEd.Business.AMS2.DependencyInjection;
using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.AMS2.GameLogic
{
    public class Ams2GameEngine : GameEngine
    {
        private readonly Ams2StorageFactory _storageFactory;

        public Ams2GameEngine(Ams2StorageFactory storageFactory)
        {
            _storageFactory = storageFactory;
        }

        protected override HistoricalAccolades LoadAccoladesForNewGame(int seasonYear)
            => _storageFactory.AccoladesLoader.LoadAccolades(seasonYear);

        protected override IDriverData InitializeConcretePlayerDriverData(IDriverData provisionalDriverData, IPlayerData playerData, ISeason season)
        {
            var ams2PlayerDriverData = provisionalDriverData.ConvertToChild<IDriverData, Ams2DriverData>();

            ams2PlayerDriverData.BaseHelmetFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet.png");
            ams2PlayerDriverData.BaseVisorFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor.png");

            ams2PlayerDriverData.BaseHelmetFile90s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_90s.png");

            ams2PlayerDriverData.BaseHelmetFile80s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_80s.png");
            ams2PlayerDriverData.BaseVisorFile80s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_80s.png");

            ams2PlayerDriverData.BaseHelmetFile70s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_70s.png");
            ams2PlayerDriverData.BaseVisorFile70s = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_70s.png");

            return ams2PlayerDriverData;
        }

        protected override ISeason LoadUpdatedSeasonForRefresh(int year)
            => _storageFactory.SeasonLoader.LoadSeason(year);

        protected override Dictionary<string, IDriverData> LoadUpdatedDriversForRefresh(int year)
            => _storageFactory.DriversLoader.LoadDrivers(year).ToDictionary(kv => kv.Key, kv => (IDriverData)kv.Value);

        protected override void ApplyConcreteSeasonUpdates(ISeason currentSeason, ISeason updatedSeason)
        {
            ((Ams2Season)currentSeason).Ams2Class = ((Ams2Season)updatedSeason).Ams2Class;
        }

        protected override void ApplyConcreteTeamEntryUpdates(ITeamEntry updatedTeamEntry, int updatedSeasonYear)
        {
            PrefixTeamTexturePaths((Ams2TeamEntry)updatedTeamEntry, updatedSeasonYear);
        }

        // The team entry just came from that year's freshly-loaded season pack, whose texture
        // paths are relative to "Seasons/<year>/". Asset resolution for this save's season,
        // however, is anchored to Season.OriginalYear (set when the season was cloned before the
        // real pack existed) and is not reset here, so every texture reference on this team needs
        // "../<year>/" prepended to still resolve into the correct, newly-installed year's folder.
        private static void PrefixTeamTexturePaths(Ams2TeamEntry teamEntry, int year)
        {
            string Prefix(string path) => string.IsNullOrEmpty(path) ? path : $"../{year}/{path}";

            teamEntry.BaseLiveryDriver1 = Prefix(teamEntry.BaseLiveryDriver1);
            teamEntry.BaseLiveryDriver2 = Prefix(teamEntry.BaseLiveryDriver2);
            teamEntry.HelmetSponsors = Prefix(teamEntry.HelmetSponsors);
            teamEntry.VisorSponsors = Prefix(teamEntry.VisorSponsors);
            teamEntry.LiveryPreview = Prefix(teamEntry.LiveryPreview);
            teamEntry.LiveryXml = Prefix(teamEntry.LiveryXml);

            if (teamEntry.DriversSpecificHelmet != null)
            {
                teamEntry.DriversSpecificHelmet = teamEntry.DriversSpecificHelmet
                    .ToDictionary(kv => kv.Key, kv => Prefix(kv.Value));
            }

            if (teamEntry.NumbersPlacements != null)
            {
                foreach (var placement in teamEntry.NumbersPlacements)
                {
                    placement.NumbersTexture = Prefix(placement.NumbersTexture);
                    placement.NumbersTextureDriver2 = Prefix(placement.NumbersTextureDriver2);
                }
            }

            if (teamEntry.LiveryOverrides != null)
            {
                foreach (var liveryOverride in teamEntry.LiveryOverrides)
                {
                    liveryOverride.Driver1Livery = Prefix(liveryOverride.Driver1Livery);
                    liveryOverride.Driver2Livery = Prefix(liveryOverride.Driver2Livery);
                    liveryOverride.HelmetSponsors = Prefix(liveryOverride.HelmetSponsors);
                    liveryOverride.VisorSponsors = Prefix(liveryOverride.VisorSponsors);
                    liveryOverride.LiveryPreview = Prefix(liveryOverride.LiveryPreview);

                    if (liveryOverride.DriversSpecificHelmet != null)
                    {
                        liveryOverride.DriversSpecificHelmet = liveryOverride.DriversSpecificHelmet
                            .ToDictionary(kv => kv.Key, kv => Prefix(kv.Value));
                    }

                    if (liveryOverride.NumbersPlacements != null)
                    {
                        foreach (var placement in liveryOverride.NumbersPlacements)
                        {
                            placement.NumbersTexture = Prefix(placement.NumbersTexture);
                            placement.NumbersTextureDriver2 = Prefix(placement.NumbersTextureDriver2);
                        }
                    }
                }
            }
        }
    }
}