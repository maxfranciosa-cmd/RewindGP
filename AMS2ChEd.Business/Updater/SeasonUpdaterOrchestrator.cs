using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMS2ChEd.Business.Updater
{
    public class SeasonUpdaterOrchestrator
    {
        private readonly SeasonManifestService _manifest;
        private readonly ISeasonDownloadPrompt _prompt;
        private readonly ICurrentVersionCheckStore _preferences;

        public SeasonUpdaterOrchestrator(
            SeasonManifestService manifest,
            ISeasonDownloadPrompt prompt,
            ICurrentVersionCheckStore preferences)
        {
            _manifest= manifest;
            _prompt= prompt;
            _preferences = preferences;
        }

        /// <summary>
        /// Before a season starts (new game, off-season): a season with no data at all needs its
        /// pack; one the app ships data for is only offered its artwork pack, and a "not now" is
        /// remembered for that year. Returns true when a pack was installed.
        /// </summary>
        public async Task<bool> PrepareSeasonAsync(int year)
        {
            var availability = _manifest.GetAvailability(year);

            switch (availability)
            {
                case SeasonAvailability.UpToDate:
                case SeasonAvailability.InstalledNoRemote:
                case SeasonAvailability.BakedOnly:
                case SeasonAvailability.BakedSkinPackImported:
                    return false;

                case SeasonAvailability.BakedSkinPackAvailable:
                    {
                        // same "not now" memory as the artwork pack offer
                        var declinedKey = ArtworkOfferDeclinedKey(year);
                        if (_preferences.GetString(declinedKey) != null)
                            return false;

                        if (!await _prompt.PromptSkinPackAsync(year))
                        {
                            _preferences.SetString(declinedKey, "true");
                            return false;
                        }
                        return true;
                    }

                case SeasonAvailability.UpdateAvailable:
                    {
                        var entry = _manifest.GetEntry(year)!;
                        var updated = await _prompt.PromptUpdateAsync(entry);
                        return updated;
                    }

                case SeasonAvailability.BakedPackAvailable:
                    {
                        var declinedKey = ArtworkOfferDeclinedKey(year);
                        if (_preferences.GetString(declinedKey) != null)
                            return false;

                        if (!await _prompt.PromptArtworkOfferAsync(year))
                        {
                            _preferences.SetString(declinedKey, "true");
                            return false;
                        }
                        return await DownloadAsync(year);
                    }

                case SeasonAvailability.NotInstalled:
                case SeasonAvailability.DefaultSeason:
                    return await DownloadAsync(year);

                case SeasonAvailability.NotAvailable:
                default:
                    return false;
            }
        }

        /// <summary>
        /// When a save is loaded: offers the update of the season's installed pack, if there is
        /// one. A declined update isn't offered again until a newer one comes out. Returns true
        /// when the pack was updated.
        /// </summary>
        public async Task<bool> OfferUpdateOnLoadAsync(int year)
        {
            if (_manifest.GetAvailability(year) != SeasonAvailability.UpdateAvailable)
                return false;

            var entry = _manifest.GetEntry(year)!;
            var declinedKey = UpdateDeclinedKey(entry);
            var version = entry.LastUpdated.ToString("O");
            if (_preferences.GetString(declinedKey) == version)
                return false;

            var updated = await _prompt.PromptUpdateAsync(entry);
            if (!updated)
                _preferences.SetString(declinedKey, version);
            return updated;
        }

        private async Task<bool> DownloadAsync(int year)
        {
            var entries = _manifest.GetEntriesForYear(year)!;
            if (entries.Count == 1)
                return await _prompt.PromptDownloadAsync(entries.First());

            return await _prompt.PromptSlugPickerAsync(year, entries);
        }

        private static string ArtworkOfferDeclinedKey(int year) => $"season_artwork_offer_declined_{year}";

        private static string UpdateDeclinedKey(SeasonManifestEntry entry) => $"season_update_declined_{entry.EffectiveSlug}";
    }
}
