using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;

namespace AMS2ChEd.Business.DependencyInjection
{
    public class StorageFactory<TDriverData, TSeason>
        where TDriverData : IDriverData
        where TSeason: ISeason
    {
        public IDriversLoader<TDriverData> DriversLoader { get; private set; }

        public ITeamsLoader TeamsLoader { get; private set; }

        public ISeasonLoader<TSeason> SeasonLoader { get; private set; }

        public IGameStorage GameStorage { get; private set; }

        public IAccoladesLoader AccoladesLoader { get; private set; }

        public StorageFactory(
            IDriversLoader<TDriverData> driversLoader,
            ITeamsLoader teamsLoader,
            ISeasonLoader<TSeason> seasonLoader,
            IGameStorage gameStorage,
            IAccoladesLoader accoladesLoader)
        {
            DriversLoader = driversLoader;
            TeamsLoader = teamsLoader;
            SeasonLoader = seasonLoader;
            GameStorage = gameStorage;
            AccoladesLoader = accoladesLoader;
        }
    }
}
