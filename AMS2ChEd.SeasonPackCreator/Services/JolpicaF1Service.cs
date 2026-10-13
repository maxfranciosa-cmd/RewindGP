using AMS2ChEd.Business.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
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
        private const int PAGE_SIZE = 100;
        private const int MaxRetries = 6;
        private static readonly HttpClient _httpClient = new HttpClient();

        // Jolpica enforces a hard cap of ~4 req/sec. Since ImportAsync fans out several
        // drivers/teams concurrently (each doing multiple sequential calls), concurrency
        // limits alone don't stop bursts — every request is funneled through this gate
        // so the *actual* dispatch rate stays under the limit regardless of caller count.
        private static readonly SemaphoreSlim _rateGate = new SemaphoreSlim(1, 1);
        private static readonly TimeSpan _minRequestInterval = TimeSpan.FromMilliseconds(300);
        private static DateTime _lastRequestUtc = DateTime.MinValue;

        private static async Task<string> GetWithRetryAsync(string url)
        {
            var backoff = TimeSpan.FromMilliseconds(500);

            for (int attempt = 0; ; attempt++)
            {
                await _rateGate.WaitAsync();
                try
                {
                    var wait = _minRequestInterval - (DateTime.UtcNow - _lastRequestUtc);
                    if (wait > TimeSpan.Zero)
                        await Task.Delay(wait);
                    _lastRequestUtc = DateTime.UtcNow;
                }
                finally
                {
                    _rateGate.Release();
                }

                using var response = await _httpClient.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxRetries)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? backoff;
                    await Task.Delay(retryAfter);
                    backoff += backoff;
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

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
            var response = await GetWithRetryAsync(url);
            var data = JsonSerializer.Deserialize<JolpicaRootResponse<JolpicaRaceTable>>(response);

            return data?.MRData?.RaceTable?.Races ?? new List<JolpicaRace>();
        }

        /// <summary>
        /// Fetch all drivers who participated in the season
        /// </summary>
        public async Task<List<JolpicaDriver>> FetchDriversAsync(int year)
        {
            var url = $"{BASE_URL}/{year}/drivers.json?limit=100";
            var response = await GetWithRetryAsync(url);
            var data = JsonSerializer.Deserialize<JolpicaRootResponse<JolpicaDriverTable>>(response);

            return data?.MRData?.DriverTable?.Drivers ?? new List<JolpicaDriver>();
        }

        /// <summary>
        /// Fetch all constructors/teams for the season
        /// </summary>
        public async Task<List<JolpicaConstructor>> FetchConstructorsAsync(int year)
        {
            var url = $"{BASE_URL}/{year}/constructors.json?limit=100";
            var response = await GetWithRetryAsync(url);
            var data = JsonSerializer.Deserialize<JolpicaRootResponse<JolpicaConstructorTable>>(response);

            return data?.MRData?.ConstructorTable?.Constructors ?? new List<JolpicaConstructor>();
        }

        /// <summary>
        /// Fetch the final constructor standings for a whole season in a single call, including
        /// points — used to derive car-performance malus from actual championship results.
        /// </summary>
        public async Task<List<JolpicaConstructorStanding>> GetConstructorStandingsAsync(int year)
        {
            var url = $"{BASE_URL}/{year}/constructorStandings.json";
            var response = await GetWithRetryAsync(url);
            var data = JsonSerializer.Deserialize<JolpicaRootResponse<JolpicaStandingsTable>>(response);

            return data?.MRData?.StandingsTable?.StandingsLists?.FirstOrDefault()?.ConstructorStandings
                ?? new List<JolpicaConstructorStanding>();
        }

        /// <summary>
        /// Fetch the final driver standings for a whole season in a single call, including points.
        /// </summary>
        public async Task<List<JolpicaDriverStanding>> GetDriverStandingsAsync(int year)
        {
            var url = $"{BASE_URL}/{year}/driverStandings.json";
            var response = await GetWithRetryAsync(url);
            var data = JsonSerializer.Deserialize<JolpicaRootResponse<JolpicaStandingsTable>>(response);

            return data?.MRData?.StandingsTable?.StandingsLists?.FirstOrDefault()?.DriverStandings
                ?? new List<JolpicaDriverStanding>();
        }

        /// <summary>
        /// Fetch first race results to determine driver-team pairings
        /// </summary>
        private async Task<List<JolpicaResult>> FetchFirstRaceResultsAsync(int year)
        {
            var url = $"{BASE_URL}/{year}/1/results.json?limit=100";
            var response = await GetWithRetryAsync(url);
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

        [JsonPropertyName("StandingsTable")]
        public T StandingsTable { get; set; }

        [JsonPropertyName("SeasonTable")]
        public T SeasonTable { get; set; }
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

    /// <summary>
    /// Root of a per-season driverStandings/constructorStandings response (Jolpica requires a
    /// season in the URL for standings queries — there's no career-wide equivalent).
    /// </summary>
    public class JolpicaStandingsTable
    {
        [JsonPropertyName("season")]
        public string Season { get; set; }

        [JsonPropertyName("StandingsLists")]
        public List<JolpicaStandingsList> StandingsLists { get; set; }
    }

    public class JolpicaStandingsList
    {
        [JsonPropertyName("season")]
        public string Season { get; set; }

        [JsonPropertyName("round")]
        public string Round { get; set; }

        [JsonPropertyName("DriverStandings")]
        public List<JolpicaDriverStanding> DriverStandings { get; set; }

        [JsonPropertyName("ConstructorStandings")]
        public List<JolpicaConstructorStanding> ConstructorStandings { get; set; }
    }

    public class JolpicaDriverStanding
    {
        [JsonPropertyName("position")]
        public string Position { get; set; }

        [JsonPropertyName("points")]
        public string Points { get; set; }

        [JsonPropertyName("Driver")]
        public JolpicaDriver Driver { get; set; }

        [JsonPropertyName("Constructors")]
        public List<JolpicaConstructor> Constructors { get; set; }
    }

    public class JolpicaConstructorStanding
    {
        [JsonPropertyName("position")]
        public string Position { get; set; }

        [JsonPropertyName("points")]
        public string Points { get; set; }

        [JsonPropertyName("Constructor")]
        public JolpicaConstructor Constructor { get; set; }
    }

    #endregion
}