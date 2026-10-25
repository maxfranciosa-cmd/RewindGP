using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Models;

namespace AMS2ChEd.Tests.Business.Updater
{
    [TestClass]
    public class SeasonUpdaterOrchestratorTests
    {
        private static readonly DateTime BaseDate = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);

        private class FakePrompt : ISeasonDownloadPrompt
        {
            public bool Answer { get; set; }
            public List<string> Calls { get; } = new();

            public Task<bool> PromptDownloadAsync(SeasonManifestEntry entry, bool isUpdate = false)
            {
                Calls.Add($"download {entry.Year}");
                return Task.FromResult(Answer);
            }

            public Task<bool> PromptUpdateAsync(SeasonManifestEntry entry)
            {
                Calls.Add($"update {entry.Year}");
                return Task.FromResult(Answer);
            }

            public Task<bool> PromptSlugPickerAsync(int year, List<SeasonManifestEntry> options)
            {
                Calls.Add($"slugs {year}");
                return Task.FromResult(Answer);
            }

            public Task<bool> PromptArtworkOfferAsync(int year)
            {
                Calls.Add($"artwork {year}");
                return Task.FromResult(Answer);
            }
        }

        private static (SeasonUpdaterOrchestrator orchestrator, FakePrompt prompt) Create(MockSeasonLoader loader)
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "pack-1996"), (1994, BaseDate, "pack-1994"));
            var prompt = new FakePrompt();
            var orchestrator = new SeasonUpdaterOrchestrator(
                SeasonManifestServiceFactory.Create(manifest, loader), prompt, new InMemoryCurrentVersionCheckStore());
            return (orchestrator, prompt);
        }

        [TestMethod]
        public async Task PrepareSeason_NoDataAtAll_AsksForTheDownload()
        {
            var (orchestrator, prompt) = Create(new MockSeasonLoader());

            await orchestrator.PrepareSeasonAsync(1994);

            CollectionAssert.AreEqual(new[] { "download 1994" }, prompt.Calls);
        }

        [TestMethod]
        public async Task PrepareSeason_AppData_OffersTheArtworkOnce()
        {
            var loader = new MockSeasonLoader();
            loader.AddBakedOnlySeason("1996");
            var (orchestrator, prompt) = Create(loader);
            prompt.Answer = false;

            await orchestrator.PrepareSeasonAsync(1996);
            await orchestrator.PrepareSeasonAsync(1996);

            CollectionAssert.AreEqual(new[] { "artwork 1996" }, prompt.Calls, "a \"not now\" isn't asked again");
        }

        [TestMethod]
        public async Task PrepareSeason_AppData_ArtworkWanted_GoesOnToTheDownload()
        {
            var loader = new MockSeasonLoader();
            loader.AddBakedOnlySeason("1996");
            var (orchestrator, prompt) = Create(loader);
            prompt.Answer = true;

            await orchestrator.PrepareSeasonAsync(1996);

            CollectionAssert.AreEqual(new[] { "artwork 1996", "download 1996" }, prompt.Calls);
        }

        [TestMethod]
        public async Task PrepareSeason_AppDataNoPackListed_AsksNothing()
        {
            var loader = new MockSeasonLoader();
            loader.AddBakedOnlySeason("1988");
            var (orchestrator, prompt) = Create(loader);

            Assert.IsFalse(await orchestrator.PrepareSeasonAsync(1988));
            Assert.AreEqual(0, prompt.Calls.Count);
        }

        [TestMethod]
        public async Task OfferUpdateOnLoad_DeclinedUpdate_IsNotOfferedAgain()
        {
            var loader = new MockSeasonLoader();
            loader.AddBakedSeason("1996");
            loader.AddSeason("1996", BaseDate.AddDays(-5));
            var (orchestrator, prompt) = Create(loader);
            prompt.Answer = false;

            await orchestrator.OfferUpdateOnLoadAsync(1996);
            await orchestrator.OfferUpdateOnLoadAsync(1996);

            CollectionAssert.AreEqual(new[] { "update 1996" }, prompt.Calls);
        }

        [TestMethod]
        public async Task OfferUpdateOnLoad_NoPackInstalled_OffersNothing()
        {
            var loader = new MockSeasonLoader();
            loader.AddBakedOnlySeason("1996");
            var (orchestrator, prompt) = Create(loader);

            Assert.IsFalse(await orchestrator.OfferUpdateOnLoadAsync(1996));
            Assert.AreEqual(0, prompt.Calls.Count);
        }
    }
}
