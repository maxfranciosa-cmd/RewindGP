using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface ISeasonLoader
    {
        IEnumerable<string> GetAvailableSeasons();

        DateTime GetSeasonUpdateDate(int seasonYear);

        ISeason LoadBaseSeason(int seasonYear);
    }

    public interface ISeasonLoader<TSeason> : ISeasonLoader
        where TSeason : ISeason
    {
        TSeason LoadSeason(int seasonYear);
    }
}
