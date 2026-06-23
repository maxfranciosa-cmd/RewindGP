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

        public SeasonUpdaterOrchestrator(
            SeasonManifestService manifest,
            ISeasonDownloadPrompt prompt) 
        {
            _manifest= manifest;
            _prompt= prompt;
        }
        public async Task<bool> PrepareSeasonAsync(int year)
        {
            var availability = _manifest.GetAvailability(year);

            switch (availability)
            {
                case SeasonAvailability.UpToDate:
                case SeasonAvailability.InstalledNoRemote:
                    return false;

                case SeasonAvailability.UpdateAvailable:
                    {
                        var entry = _manifest.GetEntry(year)!;
                        var updated = await _prompt.PromptUpdateAsync(entry);
                        return updated;
                    }

                case SeasonAvailability.NotInstalled:
                case SeasonAvailability.DefaultSeason:
                    {
                        var entries = _manifest.GetEntriesForYear(year)!;
                        if(entries.Count == 1)
                        {
                            var installed = await _prompt.PromptDownloadAsync(entries.First());
                            return installed;
                        }
                        else
                        {
                            var installed = await _prompt.PromptSlugPickerAsync(year, entries);
                            return installed;
                        }
                        
                    }

                case SeasonAvailability.NotAvailable:
                default:
                    return false;
            }
        }
    }
}
