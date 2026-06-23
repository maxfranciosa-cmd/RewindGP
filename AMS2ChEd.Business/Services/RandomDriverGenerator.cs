using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;


namespace AMS2ChEd.Business.Services
{
    public abstract class RandomDriverGenerator : IRandomDriverGenerator
    {
        Random _random = new Random();

        public IDriverData GenerateDriver(IEnumerable<IDriverData> existingDrivers, int year)
        {
            var reputation = (_random.Next(2) == 1) ? DriverReputation.YOUNG_TALENT : DriverReputation.PAY_DRIVER_SEASON;
            var nationality = existingDrivers.ElementAt(_random.Next(existingDrivers.Count())).Nationality;
            var namesForThatNationality = existingDrivers
                                            .Where(d => d.Nationality == nationality)
                                            .Select(d => d.Name);
            var fullDriverName = NameGenerator.GenerateName(namesForThatNationality);
            var playerToPickAsBase = existingDrivers
                                        .Where(d => d.Reputation == DriverReputation.YOUNG_TALENT || d.Reputation == DriverReputation.PAY_DRIVER_SEASON)
                                        .First();
            var result = new DriverData
            {
                Name = fullDriverName,
                Nationality = nationality,
                PictureUrl = null,
                Reputation = playerToPickAsBase.Reputation,
                RatingValues = playerToPickAsBase.RatingValues,
                FavouriteNumbers = playerToPickAsBase.FavouriteNumbers,
                YearOfBirth = playerToPickAsBase.YearOfBirth,
                DriverId = $"generated_{fullDriverName.ToLower().Replace(" ", "_")}"
            };


            return InitializeDriverData(result, year);
        }

        protected virtual IDriverData InitializeDriverData(IDriverData provisionalDriverData, int year)
        {
            return provisionalDriverData;
        }
    }
}
