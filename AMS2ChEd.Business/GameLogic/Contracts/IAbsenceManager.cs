using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Contracts
{
    public interface IAbsenceManager
    {
        event EventHandler<AbsenceOpportunityEventArgs> AbsenceOpportunityAvailable;
        event EventHandler<AbsenceDecisionEventArgs> AbsenceDecisionMade;

        void ProcessAbsences(
            List<EntryListEntry> entryList,
            List<Absence> absences,
            ISaveGame saveGame,
            IAbsenceDecisionProvider decisionProvider);

        bool IsDriverInAnyAbsence(string driverId, List<Absence> absences);

    }
    public interface IAbsenceDecisionProvider
    {
        bool DoesPlayerWantToApply(AbsenceOpportunity opportunity,bool playerAlreadySteppedIn);
        bool DoesPlayerTeamAllowLeave(string playerTeamId, Absence proposedAbsence);
    }

    public class AbsenceOpportunity
    {
        public string DriverOut { get; set; }
        public string TeamId { get; set; }
        public int RaceId { get; set; }
        public string DriverIn { get; set; }
    }

    public class AbsenceDecision
    {
        public AbsenceDecisionType DecisionType { get; set; }
        public Absence Absence { get; set; }
    }

    public enum AbsenceDecisionType
    {
        PlayerAccepted,
        PlayerDeclined,
        PlayerRefused,
        TeamRefused,
        AutoExecuted
    }

    public class AbsenceOpportunityEventArgs : EventArgs
    {
        public AbsenceOpportunity Opportunity { get; set; }
    }

    public class AbsenceDecisionEventArgs : EventArgs
    {
        public AbsenceDecision Decision { get; set; }
    }
}
