using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models
{
    public interface ISaveGame
    {
        [JsonPropertyName("currentSeason")]
        public ISeason CurrentSeason { get; set; }

        [JsonPropertyName("drivers")]
        public IEnumerable<IDriverData> Drivers { get; set; }

        [JsonPropertyName("retired_drivers")]
        public IEnumerable<IDriverData> RetiredDrivers { get; set; }

        [JsonPropertyName("nextGpIndex")]
        public int NextGpIndex { get; set; }

        [JsonPropertyName("nextGpEntryList")]
        public IEnumerable<EntryListEntry> NextGpEntryList { get; set; }

        [JsonPropertyName("playerData")]
        public IPlayerData PlayerData { get; set; }

        [JsonPropertyName("grandPrixResults")]
        public IEnumerable<GrandPrixResult> GrandPrixResults { get; set; }

        [JsonPropertyName("currentDriverStandings")]
        public IEnumerable<HistoricalDriverStandingEntry> CurrentDriverStandings { get; set; }

        [JsonPropertyName("currentConstructorStandings")]
        public IEnumerable<ConstructorStandingEntry> CurrentConstructorStandings { get; set; }

        [JsonPropertyName("historicalDriverStandings")]
        public IEnumerable<HistoricalDriverStanding> HistoricalDriverStandings { get; set; }

        [JsonPropertyName("historicalConstructorStandings")]
        public IEnumerable<HistoricalConstructorStanding> HistoricalConstructorStandings { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("pre-quali_status")]
        public PreQualiStatus PreQualiStatus { get; set; }

        [JsonPropertyName("pre_quali_pool_entries")]
        public List<EntryListEntry> PreQualiPoolEntries { get; set; }

        [JsonPropertyName("current_pre_quali_dnpq_results")]
        public List<ParticipantData> CurrentPreQualiDnpqResults { get; set; }
    }

    public enum PreQualiStatus
    {
        NotApplicable,
        Pending,
        Completed
    }

    public interface IPlayerData
    {
        [JsonPropertyName("driverid")]
        string DriverId { get; set; }

        [JsonPropertyName("name")]
        string Name { get; set; }

        [JsonPropertyName("nationality")]
        string Nationality { get; set; }

        [JsonPropertyName("teamid")]
        string TeamId { get; set; }

    }

    public class Scenario
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("picture")]
        public string PictureUrl { get; set; }

        [JsonPropertyName("game_file")]
        public string GameFile { get; set; }
    }
}