using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;

namespace AMS2ChEd.Business.DependencyInjection
{
    public class GameLogicFactory
    {
        public IStandingsManager StandingsManager { get; private set; }
        public IAbsenceManager AbsenceManager { get; private set; }
        public IContractNegotiationEngine ContractNegotiationEngine { get; private set; }
        public IEntryListGenerator EntryListGenerator { get; private set; }
        public IGameEngine GameEngine { get; private set; }
        public IRacePreparator RacePreparator { get; private set; }

        public IRaceDataService RaceDataService { get; private set; }

        public IEndOfSeasonManager EndOfSeasonManager { get; private set; }

        public IPreQualiPoolResolver PreQualiPoolResolver { get; private set; }

        public SeasonUpdaterOrchestrator SeasonUpdaterOrchestrator { get; private set; }
        
        public GameLogicFactory(
            IStandingsManager standingsManager, 
            IAbsenceManager absenceManager, 
            IContractNegotiationEngine contractNegotiationEngine, 
            IEntryListGenerator entryListGenerator, 
            IGameEngine gameEngine, 
            IRacePreparator racePreparator,
            IEndOfSeasonManager endOfSeasonManager, 
            IRaceDataService raceDataService,
            IPreQualiPoolResolver preQualiPoolResolver,
            SeasonUpdaterOrchestrator seasonUpdaterOrchestrator)
        {
            StandingsManager = standingsManager;
            AbsenceManager = absenceManager;
            ContractNegotiationEngine = contractNegotiationEngine;
            EntryListGenerator = entryListGenerator;
            GameEngine = gameEngine;
            RacePreparator = racePreparator;
            EndOfSeasonManager = endOfSeasonManager;
            RaceDataService = raceDataService;
            PreQualiPoolResolver = preQualiPoolResolver;
            SeasonUpdaterOrchestrator = seasonUpdaterOrchestrator;
        }
    }
}
