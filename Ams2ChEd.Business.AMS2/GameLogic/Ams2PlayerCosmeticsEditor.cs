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

        public IEnumerable<CosmeticsOption> GetDefaultCosmeticsOptions(int seasonYear)
        {
            return HelmetPicker.LoadGenericHelmetDesignsPerYear(seasonYear)
                .Select(h => new CosmeticsOption { Id = h.HelmetFile, PreviewImagePath = h.PreviewImage });
        }

        public void ApplySelectedCosmetics(IDriverData playerDriverData, string selectedOptionId, int seasonYear)
        {
            if (playerDriverData is not Ams2DriverData ams2DriverData) return;

            var selectedHelmet = HelmetPicker.LoadGenericHelmetDesignsPerYear(seasonYear)
                .FirstOrDefault(h => h.HelmetFile == selectedOptionId);
            if (selectedHelmet == null) return;

            if (seasonYear >= HelmetPicker.HELMET_MODERN_EARLIEST_YEAR)
            {
                ams2DriverData.BaseHelmetFile = selectedHelmet.HelmetFile;
                ams2DriverData.BaseVisorFile = selectedHelmet.VisorFile;
            }
            else if (seasonYear >= HelmetPicker.HELMET_90s_EARLIEST_YEAR)
            {
                ams2DriverData.BaseHelmetFile90s = selectedHelmet.HelmetFile;
            }
            else if (seasonYear >= HelmetPicker.HELMET_80s_EARLIEST_YEAR)
            {
                ams2DriverData.BaseHelmetFile80s = selectedHelmet.HelmetFile;
                ams2DriverData.BaseVisorFile80s = selectedHelmet.VisorFile;
            }
            else
            {
                ams2DriverData.BaseHelmetFile70s = selectedHelmet.HelmetFile;
                ams2DriverData.BaseVisorFile70s = selectedHelmet.VisorFile;
            }
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
