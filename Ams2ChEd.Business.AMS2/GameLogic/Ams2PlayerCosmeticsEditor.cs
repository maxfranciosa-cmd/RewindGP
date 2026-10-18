using Ams2ChEd.Business.AMS2.Services;
using Ams2ChEd.Business.AMS2.UI;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.AMS2.GameLogic
{
    public class Ams2PlayerCosmeticsEditor : IPlayerCosmeticsEditor
    {
        public bool HasCosmeticsSupport => true;

        // Only designs made for every helmet model are offered, so one pick dresses the player
        // for the whole career; each shows its 90s helmet preview whatever the season.
        public IEnumerable<CosmeticsOption> GetDefaultCosmeticsOptions(int seasonYear)
        {
            return HelmetPicker.LoadCompleteGenericHelmetDesigns()
                .Select(g => new CosmeticsOption
                {
                    Id = g.Key,
                    PreviewImagePath = g.First(h => h.Era == HelmetEra.Nineties).PreviewImage
                });
        }

        public void ApplySelectedCosmetics(IDriverData playerDriverData, string selectedOptionId, int seasonYear)
        {
            if (playerDriverData is not Ams2DriverData ams2DriverData) return;

            var design = HelmetPicker.LoadCompleteGenericHelmetDesigns()
                .FirstOrDefault(g => g.Key == selectedOptionId);
            if (design == null) return;

            GenericHelmetDesign ForEra(HelmetEra era) => design.First(h => h.Era == era);

            // A design with no visor of its own keeps the era's default visor already set.
            ams2DriverData.BaseHelmetFile = ForEra(HelmetEra.Modern).HelmetFile;
            if (!string.IsNullOrEmpty(ForEra(HelmetEra.Modern).VisorFile))
                ams2DriverData.BaseVisorFile = ForEra(HelmetEra.Modern).VisorFile;

            ams2DriverData.BaseHelmetFile90s = ForEra(HelmetEra.Nineties).HelmetFile;

            ams2DriverData.BaseHelmetFile80s = ForEra(HelmetEra.Eighties).HelmetFile;
            if (!string.IsNullOrEmpty(ForEra(HelmetEra.Eighties).VisorFile))
                ams2DriverData.BaseVisorFile80s = ForEra(HelmetEra.Eighties).VisorFile;

            ams2DriverData.BaseHelmetFile70s = ForEra(HelmetEra.Seventies).HelmetFile;
            if (!string.IsNullOrEmpty(ForEra(HelmetEra.Seventies).VisorFile))
                ams2DriverData.BaseVisorFile70s = ForEra(HelmetEra.Seventies).VisorFile;
        }

        public bool ShowEditor(IPlayerData playerData, ISaveGame saveGame, object ownerWindow)
        {
            var editorWindow = new Ams2PlayerCosmeticsEditorWindow(playerData, saveGame)
            {
                Owner = ownerWindow as System.Windows.Window
            };
            return editorWindow.ShowDialog() == true;
        }
    }
}
