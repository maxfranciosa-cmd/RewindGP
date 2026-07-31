namespace AMS2ChEd.Business.Settings.Contracts
{
    public class GameInstallSettings
    {
        public string GameInstallFolder { get; set; }

        public string PlayerInGameName { get; set; }
    }

    public interface IGameInstallSettingsStorage
    {
        GameInstallSettings LoadSettings();

        void SaveSettings(GameInstallSettings settings);
    }
}
