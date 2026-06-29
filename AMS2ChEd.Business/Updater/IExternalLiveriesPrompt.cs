namespace AMS2ChEd.Business.Updater
{
    public interface IExternalLiveriesPrompt
    {
        /// <summary>
        /// Opens the download page and lets the user locate the downloaded archive.
        /// Returns the local file path, or null if the user cancelled.
        /// </summary>
        Task<string?> PromptDownloadAsync(string url);
    }
}
