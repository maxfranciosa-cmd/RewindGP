using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;

namespace Ams2ChEd.Business.AMS2.Services
{
    public class Ams2RandomDriverGenerator : RandomDriverGenerator
    {
        protected override IDriverData InitializeDriverData(IDriverData provisionalDriverData, int year)
        {
            var ams2PlayerDriverData = provisionalDriverData.ConvertToChild<IDriverData, Ams2DriverData>();

            if (year >= HelmetPicker.HELMET_MODERN_EARLIEST_YEAR)
            {
                ams2PlayerDriverData.BaseHelmetFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet.png");
                ams2PlayerDriverData.BaseVisorFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor.png");

            }
            else if (year >= HelmetPicker.HELMET_90s_EARLIEST_YEAR)
            {
                ams2PlayerDriverData.BaseHelmetFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_90s.png");
                ams2PlayerDriverData.BaseVisorFile = "";
            }
            else if (year >= HelmetPicker.HELMET_80s_EARLIEST_YEAR)
            {
                ams2PlayerDriverData.BaseHelmetFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_80s.png");
                ams2PlayerDriverData.BaseVisorFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_80s.png");
            }
            else
            {
                ams2PlayerDriverData.BaseHelmetFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaulthelmet_70s.png");
                ams2PlayerDriverData.BaseVisorFile = Path.Combine(StoragePaths.BaseHelmetLiveriesPath, "defaultvisor_70s.png");
            }

            return ams2PlayerDriverData;
        }
    }
}
