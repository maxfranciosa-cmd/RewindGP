namespace AMS2ChEd.Business.Updater
{
    public interface IExternalLiveriesInstaller
    {
        bool HasExternalLiveries(int seasonYear);

        Task<bool> InstallAsync(int seasonYear, IExternalLiveriesPrompt prompt);
    }
}
