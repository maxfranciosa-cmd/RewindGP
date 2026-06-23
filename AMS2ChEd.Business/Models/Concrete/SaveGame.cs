using AMS2ChEd.Business.Services;
using System.Numerics;
using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    public class SaveGame : ISaveGame
    {
        public ISeason CurrentSeason { get; set; }
        public IEnumerable<IDriverData> Drivers { get; set; }
        public int NextGpIndex { get; set; }
        public IEnumerable<EntryListEntry> NextGpEntryList { get; set; }
        public IPlayerData PlayerData { get; set; }
        public IEnumerable<GrandPrixResult> GrandPrixResults { get; set; }
        public IEnumerable<HistoricalDriverStandingEntry> CurrentDriverStandings { get; set; }
        public IEnumerable<ConstructorStandingEntry> CurrentConstructorStandings { get; set; }
        public IEnumerable<HistoricalDriverStanding> HistoricalDriverStandings { get; set; }
        public IEnumerable<HistoricalConstructorStanding> HistoricalConstructorStandings { get; set; }
        public IEnumerable<IDriverData> RetiredDrivers { get; set; }
        public DateTime Timestamp { get; set; }
        public PreQualiStatus PreQualiStatus { get; set; }
        public List<EntryListEntry> PreQualiPoolEntries { get; set; }
        public List<ParticipantData> CurrentPreQualiDnpqResults { get; set; }
    }

    public class PlayerData : IPlayerData
    {
        public string DriverId { get; set; }
        public string Name { get; set; }
        public string Nationality { get; set; }
        public string TeamId { get; set; }
    }

    public class EntryListEntry
    {
        [JsonPropertyName("teamid")]
        public string TeamId { get; set; }

        [JsonPropertyName("driver1id")]
        public string Driver1Id { get; set; }

        [JsonPropertyName("driver1Reputation")]
        public DriverReputation Driver1Reputation { get; set; }

        [JsonPropertyName("driver1number")]
        public int Driver1Number { get; set; }

        [JsonPropertyName("driver2id")]
        public string Driver2Id { get; set; }

        [JsonPropertyName("driver2Reputation")]
        public DriverReputation Driver2Reputation { get; set; }

        [JsonPropertyName("driver2number")]
        public int Driver2Number { get; set; }
    }

    public class GrandPrixResult
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("grandPrixName")]
        public string GrandPrixName { get; set; }

        [JsonPropertyName("qualifyingResults")]
        public List<SessionResult> QualifyingResults { get; set; }

        [JsonPropertyName("raceResults")]
        public List<SessionResult> RaceResults { get; set; }
    }

    public class SessionResult
    {
        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("driver_id")]
        public string DriverId { get; set; }

        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }

        [JsonPropertyName("dnf")]
        public bool DNF { get; set; }

        [JsonPropertyName("points")]
        public double Points { get; set; }

        public bool FastestLap { get; set; }
        [JsonPropertyName("did_not_prequalify")]
        public bool DidNotPreQualify { get; set; }
    }

    public class HistoricalDriverStandingEntry
    {
        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("driver_id")]
        public string DriverId { get; set; }

        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }

        [JsonPropertyName("points")]
        public double Points { get; set; }

        [JsonPropertyName("positions_tally")]
        public PositionsTally PositionsTally { get; set; }
    }

    public class HisoricalDriverStandingEntry : HistoricalDriverStandingEntry
    {
        [JsonPropertyName("driver_name")]
        public string DriverName { get; set; }

        [JsonPropertyName("team_name")]
        public string TeamName { get; set; }
    }

    public class ConstructorStandingEntry
    {
        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }

        [JsonPropertyName("points")]
        public double Points { get; set; }

        [JsonPropertyName("positions_tally")]
        public PositionsTally PositionsTally { get; set; }
    }

    public class HistoricalConstructorStandingEntry : ConstructorStandingEntry
    {
        [JsonPropertyName("team_name")]
        public string TeamName { get; set; }
    }

    public class HistoricalDriverStanding
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("standing")]
        public IEnumerable<HisoricalDriverStandingEntry> Standing { get; set; }
    }

    public class HistoricalConstructorStanding
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("standing")]
        public IEnumerable<HistoricalConstructorStandingEntry> Standing { get; set; }
    }

    [JsonConverter(typeof(PositionsTallyConverter))]
    public class PositionsTally : IComparable<PositionsTally>
    {
        private const int MAX_POSITIONS = 26;
        private const int DIGITS_PER_POSITION = 2;

        BigInteger _tally;
        public PositionsTally(BigInteger tally)
        {
            _tally = tally;
        }

        public PositionsTally() : this(0)
        {

        }

        public PositionsTally AddPosition(int position)
        {
            if (position < 1 || position > MAX_POSITIONS)
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"Position must be between 1 and {MAX_POSITIONS}");

            _tally += 1 * (BigInteger.Pow(10, DIGITS_PER_POSITION * (MAX_POSITIONS - position)));
            return this;
        }

        public PositionsTally Clear()
        {
            _tally = 0;
            return this;
        }

        public BigInteger ToBigInteger()
        {
            return _tally; 
        }

        public int CompareTo(PositionsTally other)
        {
            return _tally.CompareTo(other?.ToBigInteger());
        }

        public override bool Equals(object obj)
        {
            return obj is PositionsTally other && _tally.Equals(other?.ToBigInteger());
        }

        public override string ToString()
        {
            return _tally.ToString();
        }

        // Operators for comparison
        public static bool operator >(PositionsTally left, PositionsTally right)
            => left?.ToBigInteger() > right?.ToBigInteger();

        public static bool operator <(PositionsTally left, PositionsTally right)
            => left?.ToBigInteger() < right?.ToBigInteger();

        public static bool operator >=(PositionsTally left, PositionsTally right)
            => left?.ToBigInteger() >= right?.ToBigInteger();

        public static bool operator <=(PositionsTally left, PositionsTally right)
            => left?.ToBigInteger() <= right?.ToBigInteger();

        public static bool operator ==(PositionsTally left, PositionsTally right)
            => left?.ToBigInteger() == right?.ToBigInteger();

        public static bool operator !=(PositionsTally left, PositionsTally right)
            => left?.ToBigInteger() != right?.ToBigInteger();

        public override int GetHashCode()
        {
            return _tally.GetHashCode();
        }
    }
}