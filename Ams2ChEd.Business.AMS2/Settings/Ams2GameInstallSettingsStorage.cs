using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.PakPatching.Contracts;
using Ams2ChEd.Business.AMS2.UI;
using AMS2ChEd.Business.Settings.Contracts;
using System.Configuration;
using System.Windows;

namespace Ams2ChEd.Business.AMS2.Settings
{
    public class Ams2GameInstallSettingsStorage : IGameInstallSettingsStorage
    {
        private const string FOLDERPATH_SETTINGS_KEY = "AMS2FolderPath";
        private const string DRIVERNAME_SETTINGS_KEY = "AMS2DriverName";
        private const string RACELENGTH_SETTINGS_KEY = "AMS2RaceLength";

        private readonly IVehicleLiverySlotPatcher _vehicleLiverySlotPatcher;

        public Ams2GameInstallSettingsStorage(IVehicleLiverySlotPatcher vehicleLiverySlotPatcher)
        {
            _vehicleLiverySlotPatcher = vehicleLiverySlotPatcher;
        }

        public void SaveSettings(GameInstallSettings settings)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY] != null)
            {
                config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY].Value = settings.GameInstallFolder;
            }
            else
            {
                config.AppSettings.Settings.Add(FOLDERPATH_SETTINGS_KEY, settings.GameInstallFolder);
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public GameInstallSettings LoadSettings()
        {
            string savedPath = GetSavedPath();

            return new GameInstallSettings
            {
                GameInstallFolder = Directory.Exists(savedPath) ? savedPath : Ams2InstallPathDetector.DetectInstallPath(),
            };
        }

        public string LoadInGameName()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY] != null)
                {
                    return config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY].Value;
                }
            }
            catch
            {
                // Ignore errors, will use default
            }
            return string.Empty;
        }

        public void SaveInGameName(string name)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY] != null)
            {
                config.AppSettings.Settings[DRIVERNAME_SETTINGS_KEY].Value = name;
            }
            else
            {
                config.AppSettings.Settings.Add(DRIVERNAME_SETTINGS_KEY, name);
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public Ams2RaceLength LoadRaceLength()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var value = config.AppSettings.Settings[RACELENGTH_SETTINGS_KEY]?.Value;
                if (!string.IsNullOrEmpty(value) && Enum.TryParse<Ams2RaceLength>(value, out var parsed))
                {
                    return parsed;
                }
            }
            catch
            {
                // Ignore errors, will use default
            }
            return Ams2RaceLength.Default;
        }

        public void SaveRaceLength(Ams2RaceLength raceLength)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[RACELENGTH_SETTINGS_KEY] != null)
            {
                config.AppSettings.Settings[RACELENGTH_SETTINGS_KEY].Value = raceLength.ToString();
            }
            else
            {
                config.AppSettings.Settings.Add(RACELENGTH_SETTINGS_KEY, raceLength.ToString());
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public bool NeedsPlayerSetup() => string.IsNullOrEmpty(LoadInGameName());

        public bool ShowEditor(object ownerWindow)
        {
            var optionsWindow = new OptionsWindow(this, _vehicleLiverySlotPatcher);
            if (ownerWindow is Window owner)
            {
                optionsWindow.Owner = owner;
            }
            return optionsWindow.ShowDialog() == true;
        }

        private string GetSavedPath()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY] != null)
                {
                    return config.AppSettings.Settings[FOLDERPATH_SETTINGS_KEY].Value;
                }
            }
            catch
            {
                // Ignore errors, will use default path
            }
            return string.Empty;
        }
    }
}
