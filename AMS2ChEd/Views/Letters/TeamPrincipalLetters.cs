using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Resources;

namespace AMS2ChEd.Views.Letters
{
    /// <summary>
    /// News the team principal gives the player first-hand during the season (was
    /// TeamPrincipalLetterWindow).
    /// </summary>
    public static class TeamPrincipalLetters
    {
        /// <summary>
        /// With the first driver away for the next race, the decision on installing an improvement
        /// package is the second driver's.
        /// </summary>
        public static Letter ImprovementPackageDecision(string teamName, string teamPrincipal, string playerName, string firstDriverName, string grandPrixName, int remainingPackages)
        {
            return Letter.FromTeam(
                teamName,
                teamPrincipal,
                string.Format(Strings.LetterSubject_ImprovementPackage_Format, grandPrixName),
                string.Format(Strings.TeamPrincipalLetterWindow_Letter_Format, playerName, firstDriverName, grandPrixName, remainingPackages),
                Letter.NextAction());
        }

        /// <summary>An AI pay driver bought the player's own seat for the race.</summary>
        public static Letter SeatSold(string teamName, string teamPrincipal, string playerName, string grandPrixName, string driverInName)
        {
            return Letter.FromTeam(
                teamName,
                teamPrincipal,
                string.Format(Strings.LetterSubject_SeatSold_Format, grandPrixName),
                string.Format(Strings.SeasonOverviewWindow_SeatSoldLetter_Format, playerName, grandPrixName, driverInName),
                Letter.NextAction());
        }

        /// <summary>
        /// Straight after the player had the team install an improvement package: what it did to
        /// the car, and how many packages are left.
        /// </summary>
        public static Letter ImprovementPackageResult(string teamName, string teamPrincipal, string playerName, string grandPrixName, ImprovementPackage package, IReadOnlyList<ImprovementStat> stats, int remainingPackages)
        {
            var effects = ImprovementPackageEffects.Describe(package, stats);
            var gains = effects.Gains;
            var losses = effects.Losses;

            string verdict;
            if (gains.Any() && losses.Any())
                verdict = string.Format(Strings.ImprovementPackageResultLetter_Mixed_Format, ImprovementPackageEffects.JoinList(gains), ImprovementPackageEffects.JoinList(losses));
            else if (gains.Any())
                verdict = string.Format(Strings.ImprovementPackageResultLetter_Gains_Format, ImprovementPackageEffects.JoinList(gains));
            else if (losses.Any())
                verdict = string.Format(Strings.ImprovementPackageResultLetter_Losses_Format, ImprovementPackageEffects.JoinList(losses));
            else
                verdict = Strings.ImprovementPackageResultLetter_NoChange;

            string remaining = remainingPackages switch
            {
                0 => Strings.ImprovementPackageResultLetter_NoneLeft,
                1 => Strings.ImprovementPackageResultLetter_OneLeft,
                _ => string.Format(Strings.ImprovementPackageResultLetter_SomeLeft_Format, remainingPackages)
            };

            return Letter.FromTeam(
                teamName,
                teamPrincipal,
                string.Format(Strings.LetterSubject_ImprovementPackageResult_Format, grandPrixName),
                string.Format(Strings.ImprovementPackageResultLetter_Format, playerName, grandPrixName, verdict, remaining),
                Letter.NextAction());
        }
    }
}
