using AMS2ChEd.Business.Storage.Concrete;

namespace AMS2ChEd.Tests.Business.Storage
{
    [TestClass]
    public class AllTimeRecordsLoaderTests
    {
        [TestMethod]
        public void Load_FileAsTheUpdateScriptWritesIt_ReadsDriversAndTeams()
        {
            var path = Path.Combine(Path.GetTempPath(), $"all_time_records_{Guid.NewGuid():N}.json");
            File.WriteAllText(path, """
                {
                  "up_to": 2025,
                  "drivers": [
                    {"driver_id":"jean_alesi","name":"Jean Alesi","nationality":"FRA","wins_by_year":{"1995":1},"podiums_by_year":{"1990":2,"1995":5},"poles_by_year":{"1994":1,"1997":1},"championships":[]}
                  ],
                  "teams": [
                    {"team_id":"benetton","name":"Toleman / Benetton / Renault / Alpine","wins_by_year":{"1986":1},"podiums_by_year":{"1984":3},"poles_by_year":{},"championships":[1995]}
                  ]
                }
                """);

            try
            {
                var records = new AllTimeRecordsLoader(path).Load();

                Assert.AreEqual(2025, records.UpTo);
                var driver = records.Drivers.Single();
                Assert.AreEqual("jean_alesi", driver.DriverId);
                Assert.AreEqual("FRA", driver.Nationality);
                Assert.AreEqual(1, driver.WinsByYear[1995]);
                Assert.AreEqual(7, driver.PodiumsByYear.Values.Sum());
                Assert.AreEqual(2, driver.PolesByYear.Count);
                var team = records.Teams.Single();
                Assert.AreEqual("benetton", team.TeamId);
                Assert.AreEqual(3, team.PodiumsByYear[1984]);
                CollectionAssert.AreEqual(new List<int> { 1995 }, team.Championships);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_MissingFile_ReturnsEmptyRecords()
        {
            var records = new AllTimeRecordsLoader(Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.json")).Load();

            Assert.AreEqual(0, records.UpTo);
            Assert.AreEqual(0, records.Drivers.Count);
            Assert.AreEqual(0, records.Teams.Count);
        }
    }
}
