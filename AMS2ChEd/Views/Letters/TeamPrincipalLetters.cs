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
    }
}
