using System.Text.Json.Serialization;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.Models
{
    public interface IDriverData
    {
        [JsonPropertyName("driver_id")]
        string DriverId { get; set; }

        [JsonPropertyName("name")]
        string Name { get; set; }

        [JsonPropertyName("nationality")]
        string Nationality { get; set; }

        [JsonPropertyName("year_of_birth")]
        int YearOfBirth { get; set; }

        [JsonPropertyName("picture")]
        string PictureUrl { get; set; }

        [JsonPropertyName("favourite_numbers")]
        IEnumerable<int> FavouriteNumbers { get; set; }

        [JsonPropertyName("reputation")]
        DriverReputation Reputation { get; set; }

        [JsonPropertyName("rating_values")]
        Dictionary<string, double> RatingValues { get; set; }
    }
}