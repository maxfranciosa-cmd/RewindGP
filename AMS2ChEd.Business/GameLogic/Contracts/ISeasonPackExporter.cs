using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public class SeasonPackExportResult
    {
        public string OutputDirectory { get; init; }
        public int RaceCount { get; init; }
        public string SelectorScriptName { get; init; }
    }

    /// <summary>
    /// Optional, per game: exports a whole season pack as a standalone livery pack for players
    /// outside Rewind GP - every race's liveries and AI roster, the first race installed by
    /// default, plus a way to switch to any other race. A game module offers it by having its
    /// IRacePreparator implement this too; callers check with a cast.
    /// </summary>
    public interface ISeasonPackExporter
    {
        SeasonPackExportResult ExportSeasonPack(ISeason season, IEnumerable<IDriverData> drivers, string outputDirectory);
    }
}
