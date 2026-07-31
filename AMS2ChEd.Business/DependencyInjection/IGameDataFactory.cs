using AMS2ChEd.Business.Storage.Contracts;

namespace AMS2ChEd.Business.DependencyInjection
{
    public interface IGameDataFactory
    {
        IDriversLoader DriversLoader { get; }

        ITeamsLoader TeamsLoader { get; }

        ISeasonLoader SeasonLoader { get; }

        IGameStorage GameStorage { get; }

        IAccoladesLoader AccoladesLoader { get; }
    }
}
