namespace AMS2ChEd.Business.GameLogic.Contracts
{
    /// <summary>
    /// One performance value an improvement package can alter. ImprovedText/WorsenedText are
    /// ready-to-use (localized) sentence fragments for the news article, e.g. "power has been
    /// improved" / "the car has gained weight".
    /// </summary>
    public record ImprovementStat(string Key, string DisplayName, bool HigherIsBetter, string ImprovedText, string WorsenedText);

    /// <summary>
    /// Game-specific side of improvement packages: which performance values a package touches
    /// and how its random variations are generated. How the values are applied to the cars is
    /// up to each game's race preparation.
    /// </summary>
    public interface IImprovementPackageProvider
    {
        IReadOnlyList<ImprovementStat> Stats { get; }

        Dictionary<string, double> GenerateValues(Random random);
    }
}
