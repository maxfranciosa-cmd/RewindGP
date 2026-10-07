using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public class InstalledImprovementPackage
    {
        public string TeamId { get; set; }
        public ImprovementPackage Package { get; set; }
    }

    public interface IImprovementPackageManager
    {
        /// <summary>
        /// The performance values an improvement package alters in the current game.
        /// </summary>
        IReadOnlyList<ImprovementStat> Stats { get; }

        int GetRemainingPackages(ITeamEntry team);

        /// <summary>
        /// True if the driver may instruct their team to install a package before the next race:
        /// the driver is allowed to decide, the team has packages left and hasn't installed one
        /// for the next race yet.
        /// </summary>
        bool CanDriverInstall(ISaveGame saveGame, string driverId);

        /// <summary>
        /// True if the driver is a second driver who may decide for the next race only because
        /// the team's first driver is absent.
        /// </summary>
        bool IsSecondDriverCoveringForAbsentLeader(ISaveGame saveGame, string driverId);

        ImprovementPackage Install(ISaveGame saveGame, string teamId, string driverId);

        /// <summary>
        /// Lets the AI drivers decide whether to install a package before the next race.
        /// Must be called exactly once per race, after NextGpIndex has moved on to it.
        /// </summary>
        IReadOnlyList<InstalledImprovementPackage> ProcessAiDecisions(ISaveGame saveGame);

        IReadOnlyList<InstalledImprovementPackage> GetPackagesForRace(ISaveGame saveGame, int raceId);
    }
}
