using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using Moq;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class BakedNamesAllTimeRecordsLoaderTests
    {
        [TestMethod]
        public void DriverInABakedSeason_TakesItsBakedNameAndNationality_LatestSeasonWins()
        {
            using var folder = new TempFolder();
            folder.Drivers(1995, ("damon_hill", "D. Hill", "GBR"));
            folder.Drivers(1996, ("damon_hill", "Damon Hill", "GBR"), ("jacques_villeneuve", "Jacques Villeneuve", "CAN"));
            var inner = Inner(Record("damon_hill", "Damon Graham Devereux Hill", "ENG"), Record("ayrton_senna", "Ayrton Senna", "BRA"));

            var records = new BakedNamesAllTimeRecordsLoader(inner, folder.Root).Load();

            var hill = records.Drivers.Single(d => d.DriverId == "damon_hill");
            Assert.AreEqual("Damon Hill", hill.Name);
            Assert.AreEqual("GBR", hill.Nationality);
            Assert.AreEqual(2, hill.WinsByYear[1995], "the record itself is kept");
            Assert.AreEqual("Ayrton Senna", records.Drivers.Single(d => d.DriverId == "ayrton_senna").Name, "not in any baked season: the records file's name");
        }

        [TestMethod]
        public void TheInnerLoadersRecords_AreNotChanged()
        {
            using var folder = new TempFolder();
            folder.Drivers(1996, ("damon_hill", "Damon Hill", "GBR"));
            var inner = Inner(Record("damon_hill", "Damon Graham Devereux Hill", "ENG"));

            new BakedNamesAllTimeRecordsLoader(inner, folder.Root).Load();

            Assert.AreEqual("Damon Graham Devereux Hill", inner.Load().Drivers[0].Name);
        }

        [TestMethod]
        public void NoBakedSeasons_TheRecordsAsTheyAre()
        {
            var inner = Inner(Record("damon_hill", "Damon Hill", "GBR"));

            var records = new BakedNamesAllTimeRecordsLoader(inner, Path.Combine(Path.GetTempPath(), "no_baked_" + Guid.NewGuid().ToString("N"))).Load();

            Assert.AreSame(inner.Load(), records);
        }

        private static AllTimeDriverRecord Record(string id, string name, string nationality) =>
            new AllTimeDriverRecord { DriverId = id, Name = name, Nationality = nationality, WinsByYear = new Dictionary<int, int> { { 1995, 2 } } };

        private static IAllTimeRecordsLoader Inner(params AllTimeDriverRecord[] drivers)
        {
            var records = new AllTimeRecords { UpTo = 2025, Drivers = drivers.ToList() };
            var inner = new Mock<IAllTimeRecordsLoader>();
            inner.Setup(l => l.Load()).Returns(records);
            return inner.Object;
        }

        private sealed class TempFolder : IDisposable
        {
            public readonly string Root = Path.Combine(Path.GetTempPath(), "BakedNamesTests_" + Guid.NewGuid().ToString("N"));

            public void Drivers(int year, params (string id, string name, string nationality)[] drivers)
            {
                string folder = Path.Combine(Root, year.ToString());
                Directory.CreateDirectory(folder);
                string items = string.Join(",", drivers.Select(d =>
                    $@"{{ ""$type"": ""AMS2ChEd.Business.AMS2.Models.Ams2DriverData"", ""driver_id"": ""{d.id}"", ""name"": ""{d.name}"", ""nationality"": ""{d.nationality}"" }}"));
                File.WriteAllText(Path.Combine(folder, "drivers.json"), $@"{{ ""drivers"": [ {items} ] }}");
            }

            public void Dispose()
            {
                try { Directory.Delete(Root, true); } catch (IOException) { }
            }
        }
    }
}
