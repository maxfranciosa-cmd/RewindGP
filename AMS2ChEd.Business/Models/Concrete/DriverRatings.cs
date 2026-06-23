using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    public class DriverRatingsDatabase
    {
        [JsonPropertyName("drivers")]
        public IEnumerable<IDriverData> Drivers { get; set; }
    }
    public class DriverData : IDriverData
    {
        public string DriverId { get; set; }
        public string Name { get; set; }
        public string Nationality { get; set; }
        public int YearOfBirth { get; set; }
        public string PictureUrl { get; set; }
        public IEnumerable<int> FavouriteNumbers { get; set; }
        public DriverReputation Reputation { get; set; }
        public Dictionary<string, double> RatingValues { get; set; }
    }
}