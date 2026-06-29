using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface IAccoladesLoader
    {
        HistoricalAccolades LoadAccolades(int seasonYear);
    }
}
