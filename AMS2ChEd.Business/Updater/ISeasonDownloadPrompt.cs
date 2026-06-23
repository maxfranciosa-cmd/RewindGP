using AMS2ChEd.Business.Updater.Models;

namespace AMS2ChEd.Business.Updater
{
    public interface ISeasonDownloadPrompt
    {
        /// <summary>
        /// Season is in the manifest but not installed locally.
        /// Opens the download page, waits for confirmation, shows file picker, installs.
        /// Returns true if installation completed successfully.
        /// </summary>
        Task<bool> PromptDownloadAsync(SeasonManifestEntry entry, bool isUpdate = false);

        /// <summary>
        /// Season is installed but an update is available.
        /// Asks user if they want to update. If yes, same flow as PromptDownloadAsync.
        /// Returns true if update completed, false if user declined or cancelled.
        /// </summary>
        Task<bool> PromptUpdateAsync(SeasonManifestEntry entry);

        Task<bool> PromptSlugPickerAsync(int year, List<SeasonManifestEntry> options);
    }
}
