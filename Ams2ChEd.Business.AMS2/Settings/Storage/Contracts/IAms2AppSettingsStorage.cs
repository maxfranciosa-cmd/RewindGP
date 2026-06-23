namespace Ams2ChEd.Business.AMS2.Settings.Storage.Contracts
{
    public interface IAms2AppSettingsStorage
    {
        Ams2AppSettings LoadSettings();

        void SaveSettings(Ams2AppSettings settings);
    }
}
