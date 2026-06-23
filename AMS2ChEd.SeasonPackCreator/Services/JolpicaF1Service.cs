using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AMS2ChEd.Business.Services
{
    /// <summary>
    /// Service for fetching F1 historical data from Jolpica F1 API (Ergast successor)
    /// API Documentation: https://github.com/jolpica/jolpica-f1
    /// </summary>
    public class JolpicaF1Service
    {
        private const string BASE_URL = "https://api.jolpi.ca/ergast/f1";
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Import complete season data: races, drivers, teams, and driver-team assignments
        /// </summary>
        public async Task<JolpicaSeasonImport> ImportSeasonAsync(int year)
        {
            var result = new JolpicaSeasonImport { Year = year };

            try
            {
                // Fetch all data in parallel for efficiency
                var racesTask = FetchRacesAsync(year);
                var driversTask = FetchDriversAsync(year);
                var constructorsTask = FetchConstructorsAsync(year);
                var resultsTask = FetchFirstRaceResultsAsync(year);

                await Task.WhenAll(racesTask, driversTask, constructorsTask, resultsTask);

                result.Races = await racesTask;
                result.Drivers = await driversTask;
                result.Teams = await constructorsTask;
                result.FirstRaceResults = await resultsTask;

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to import season {year} from Jolpica API: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Fetch all races for a season
        /// </summary>
        private async Task<List<JolpicaRace>> FetchRacesAsync(int year)
        {
            var url = $"{BASE_URL}/{year}.json?limit=100";
            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JolpicaRootResponse<JolpicaRaceTable>>(response);

            return data?.MRData?.RaceTable?.Races ?? new List<JolpicaRace>();
        }

        /// <summary>
        /// Fetch all drivers who participated in the season
        /// </summary>
        private async Task<List<JolpicaDriver>> FetchDriversAsync(int year)
        {
            var url = $"{BASE_URL}/{year}/drivers.json?limit=100";
            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JolpicaRootResponse<JolpicaDriverTable>>(response);

            return data?.MRData?.DriverTable?.Drivers ?? new List<JolpicaDriver>();
        }

        /// <summary>
        /// Fetch all constructors/teams for the season
        /// </summary>
        private async Task<List<JolpicaConstructor>> FetchConstructorsAsync(int year)
        {
            var url = $"{BASE_URL}/{year}/constructors.json?limit=100";
            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JolpicaRootResponse<JolpicaConstructorTable>>(response);

            return data?.MRData?.ConstructorTable?.Constructors ?? new List<JolpicaConstructor>();
        }

        /// <summary>
        /// Fetch first race results to determine driver-team pairings
        /// </summary>
        private async Task<List<JolpicaResult>> FetchFirstRaceResultsAsync(int year)
        {
            var url = $"{BASE_URL}/{year}/1/results.json?limit=100";
            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JolpicaRootResponse<JolpicaRaceTable>>(response);

            var firstRace = data?.MRData?.RaceTable?.Races?.FirstOrDefault();
            return firstRace?.Results ?? new List<JolpicaResult>();
        }
    }

    #region Jolpica API Response Models

    public class JolpicaSeasonImport
    {
        public int Year { get; set; }
        public List<JolpicaRace> Races { get; set; } = new();
        public List<JolpicaDriver> Drivers { get; set; } = new();
        public List<JolpicaConstructor> Teams { get; set; } = new();
        public List<JolpicaResult> FirstRaceResults { get; set; } = new();
    }

    public class JolpicaRootResponse<T>
    {
        [JsonPropertyName("MRData")]
        public JolpicaMRData<T> MRData { get; set; }
    }

    public class JolpicaMRData<T>
    {
        [JsonPropertyName("series")]
        public string Series { get; set; }

        [JsonPropertyName("limit")]
        public string Limit { get; set; }

        [JsonPropertyName("offset")]
        public string Offset { get; set; }

        [JsonPropertyName("total")]
        public string Total { get; set; }

        [JsonPropertyName("RaceTable")]
        public T RaceTable { get; set; }

        [JsonPropertyName("DriverTable")]
        public T DriverTable { get; set; }

        [JsonPropertyName("ConstructorTable")]
        public T ConstructorTable { get; set; }
    }

    public class JolpicaRaceTable
    {
        [JsonPropertyName("season")]
        public string Season { get; set; }

        [JsonPropertyName("Races")]
        public List<JolpicaRace> Races { get; set; }
    }

    public class JolpicaRace
    {
        [JsonPropertyName("season")]
        public string Season { get; set; }

        [JsonPropertyName("round")]
        public string Round { get; set; }

        [JsonPropertyName("raceName")]
        public string RaceName { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("time")]
        public string Time { get; set; }

        [JsonPropertyName("Circuit")]
        public JolpicaCircuit Circuit { get; set; }

        [JsonPropertyName("Sprint")]
        public JolpicaSprint Sprint { get; set; }

        [JsonPropertyName("Results")]
        public List<JolpicaResult> Results { get; set; }
    }

    public class JolpicaCircuit
    {
        [JsonPropertyName("circuitId")]
        public string CircuitId { get; set; }

        [JsonPropertyName("circuitName")]
        public string CircuitName { get; set; }

        [JsonPropertyName("Location")]
        public JolpicaLocation Location { get; set; }
    }

    public class JolpicaLocation
    {
        [JsonPropertyName("locality")]
        public string Locality { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }
    }

    public class JolpicaSprint
    {
        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("time")]
        public string Time { get; set; }
    }

    public class JolpicaDriverTable
    {
        [JsonPropertyName("season")]
        public string Season { get; set; }

        [JsonPropertyName("Drivers")]
        public List<JolpicaDriver> Drivers { get; set; }
    }

    public class JolpicaDriver
    {
        [JsonPropertyName("driverId")]
        public string DriverId { get; set; }

        [JsonPropertyName("permanentNumber")]
        public string PermanentNumber { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("givenName")]
        public string GivenName { get; set; }

        [JsonPropertyName("familyName")]
        public string FamilyName { get; set; }

        [JsonPropertyName("dateOfBirth")]
        public string DateOfBirth { get; set; }

        [JsonPropertyName("nationality")]
        public string Nationality { get; set; }
    }

    public class JolpicaConstructorTable
    {
        [JsonPropertyName("season")]
        public string Season { get; set; }

        [JsonPropertyName("Constructors")]
        public List<JolpicaConstructor> Constructors { get; set; }
    }

    public class JolpicaConstructor
    {
        [JsonPropertyName("constructorId")]
        public string ConstructorId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("nationality")]
        public string Nationality { get; set; }
    }

    public class JolpicaResult
    {
        [JsonPropertyName("number")]
        public string Number { get; set; }

        [JsonPropertyName("position")]
        public string Position { get; set; }

        [JsonPropertyName("Driver")]
        public JolpicaDriver Driver { get; set; }

        [JsonPropertyName("Constructor")]
        public JolpicaConstructor Constructor { get; set; }
    }

    #endregion
}