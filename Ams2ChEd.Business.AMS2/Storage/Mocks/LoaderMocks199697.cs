using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.Models.Concrete;
using Ams2ChEd.Business.AMS2.Storage.Mocks;

namespace AMS2ChEd.Business.AMS2.DataLoaders.Mocks
{
    public class LoaderMocks199697 : IDriversLoader<Ams2DriverData>, ITeamsLoader, ISeasonLoader<Ams2Season>
    {
        public IEnumerable<string> GetAvailableSeasons()
        {
            return new List<string> { "1996", "1997" };
        }

        public DateTime GetSeasonUpdateDate(int seasonYear)
        {
            throw new NotImplementedException();
        }

        public Team GetTeam(string teamId)
        {
            return StaticDataDriversAndTeams.TEAMS[teamId];
        }

        public ISeason LoadBaseSeason(int seasonYear)
        {
            return LoadSeason(seasonYear);
        }

        public Dictionary<string, Ams2DriverData> LoadDrivers(int seasonYear)
        {
            switch (seasonYear)
            {
                case 1996:
                    return StaticDataDriversAndTeams.DRIVERS_96;
                case 1997:
                    return StaticDataDriversAndTeams.DRIVERS_97;
                default:
                    throw new ArgumentException($"{seasonYear} season does not exist.");
            }
        }

        public Ams2Season LoadSeason(int seasonYear)
        {
            switch (seasonYear)
            {
                case 1996:
                    return StaticData1996.Season;
                case 1997:
                    return StaticData1997.Season;
                default:
                    throw new ArgumentException($"{seasonYear} season does not exist.");
            }
        }

        public Dictionary<string, Team> LoadTeams()
        {
            return StaticDataDriversAndTeams.TEAMS;
        }

        public void SaveDrivers(Dictionary<string, Ams2DriverData> drivers)
        {
            throw new NotImplementedException();
        }

        public void SaveTeams(Dictionary<string, Team> teams)
        {
            throw new NotImplementedException();
        }
    }
}
