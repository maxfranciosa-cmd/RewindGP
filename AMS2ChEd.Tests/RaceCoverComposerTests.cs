using AMS2ChEd.Business.Artwork;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class RaceCoverComposerTests
    {
        private static string Folder => Path.Combine(RepoRoot(), "AMS2ChEd", "RaceCovers");

        [TestMethod]
        public void PickColours_SameRace_SameSet_NeighbouringRaces_Differ()
        {
            var first = RaceCoverComposer.PickColours(Folder, 1998, 4);
            var again = RaceCoverComposer.PickColours(Folder, 1998, 4);
            var next = RaceCoverComposer.PickColours(Folder, 1998, 5);

            Assert.AreEqual(first.Panel, again.Panel);
            Assert.AreNotEqual(first.Panel, next.Panel);
        }

        [TestMethod]
        public void Compose_PanelsRulesAndChequerTakeTheSet_ThePaintingStays()
        {
            var colours = new RaceCoverComposer.CoverColours { Panel = "#6E0E14", Rule = "#F2F2F2", Title = "#FDDA39", Chequer = "#8C2A2E" };

            using var cover = RaceCoverComposer.Compose(Folder, "Monaco Grand Prix", 1998, "1998-05-24", colours);
            using var painting = Image.Load<Rgba32>(Path.Combine(Folder, "fixed.png"));

            Assert.AreEqual(735, cover.Width);
            Assert.AreEqual(953, cover.Height);
            Assert.AreEqual(Rgba32.ParseHex("#6E0E14"), cover[300, 10], "top panel");
            Assert.AreEqual(Rgba32.ParseHex("#F2F2F2"), cover[300, 222], "rule");
            Assert.AreEqual(painting[300, 450], cover[300, 450], "painting");
            Assert.AreEqual(Rgba32.ParseHex("#6E0E14"), cover[720, 940], "price box painted out");
        }

        private static string RepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AMS2ChEd", "AMS2ChEd.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }
}
