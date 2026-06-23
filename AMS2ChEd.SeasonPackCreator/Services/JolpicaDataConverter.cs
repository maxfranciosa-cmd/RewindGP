using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.SeasonPackCreator.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows.Navigation;

namespace AMS2ChEd.Business.Services
{
    /// <summary>
    /// Converts Jolpica F1 API data into application models (Ams2DriverData, Ams2TeamEntry, Race)
    /// </summary>
    public class JolpicaDataConverter
    {
        private static Dictionary<string, string> _driverIdsException = new() {
            { "jyrki_jarvilehto", "jj_letho" },
        };

        private static Dictionary<string, string> _teamIdsException = new() {
            { "ligier", "ligier_prost" },
            { "prost", "ligier_prost" },
            {  "midland", "jordan" },
            {  "spyker", "jordan" },
            {  "force_india", "jordan" },
            {  "racing_point", "jordan" },
            {  "aston_martin", "jordan" },
            {  "leyton", "march" },
            {  "toleman", "benetton" },
            {  "alpine", "benetton" },
            {  "alpha_tauri", "toro_rosso" },
            {  "rb", "toro_rosso" },
            {  "red_bull", "stewart" },
            {  "jaguar", "stewart" },
            {  "team_lotus", "lotus" },
        };

        /// <summary>
        /// Convert Jolpica season import into application models
        /// </summary>
        public SeasonImportResult ConvertToAppModels(JolpicaSeasonImport jolpicaData)
        {
            Dictionary<string, string> driverIdMapping = new Dictionary<string, string>();
            var result = new SeasonImportResult
            {
                Year = jolpicaData.Year,
                Drivers = ConvertDrivers(jolpicaData.Drivers, driverIdMapping),
                Teams = ConvertTeams(jolpicaData.Teams),
                Races = ConvertRaces(jolpicaData.Races, jolpicaData.Year)
            };

            // Assign drivers to teams based on first race results
            AssignDriversToTeams(result.Teams, jolpicaData.FirstRaceResults, result.Races, driverIdMapping);

            return result;
        }

        /// <summary>
        /// Assign drivers to teams using first race results to create DriverContract objects
        /// </summary>
        private void AssignDriversToTeams(List<Ams2TeamEntry> teams, List<JolpicaResult> firstRaceResults, List<Race> races, Dictionary<string, string> driverIdMapping)
        {
            // Group results by team
            var teamDrivers = firstRaceResults
                .GroupBy(r => r.Constructor.ConstructorId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var team in teams)
            {
                if (!teamDrivers.ContainsKey(team.TeamId))
                    continue;

                var drivers = teamDrivers[team.TeamId].Take(2).ToList();

                if (drivers.Count > 0)
                {
                    team.Driver1Contract = new DriverContract
                    {
                        DriverId = driverIdMapping[drivers[0].Driver.DriverId],
                        DriverNumber = int.TryParse(drivers[0].Driver.PermanentNumber, out int num1) ? num1 : 0
                    };
                }

                if (drivers.Count > 1)
                {
                    team.Driver2Contract = new DriverContract
                    {
                        DriverId = driverIdMapping[drivers[1].Driver.DriverId],
                        DriverNumber = int.TryParse(drivers[1].Driver.PermanentNumber, out int num2) ? num2 : 0
                    };
                }
            }
        }

        /// <summary>
        /// Convert Jolpica drivers to Ams2DriverData models
        /// </summary>
        private List<Ams2DriverData> ConvertDrivers(List<JolpicaDriver> jolpicaDrivers, Dictionary<string, string> driverIdMapping)
        {


            return jolpicaDrivers.Select(jd => ConvertSingleDriver(jd, driverIdMapping)).ToList();
        }

        private Ams2DriverData ConvertSingleDriver(JolpicaDriver driver, Dictionary<string, string> driverIdMapping)
        {
            var name = $"{driver.GivenName} {driver.FamilyName}";
            var driverId = DriverIdGenerator.GenerateDriverId($"{driver.GivenName} {driver.FamilyName}");
            
            if (_driverIdsException.ContainsKey(driverId))
            {
                driverId = _driverIdsException[driverId];
            }

            driverIdMapping[driver.DriverId] = driverId;
            var result = new Ams2DriverData
            {
                // Base DriverData properties
                DriverId = driverId,
                Name = name,
                Nationality = ConvertNationality(driver.Nationality),
                YearOfBirth = ParseDateOfBirth(driver.DateOfBirth) ?? 0,
                FavouriteNumbers = string.IsNullOrEmpty(driver.PermanentNumber)
                    ? new List<int>()
                    : new List<int> { int.Parse(driver.PermanentNumber) },
                Reputation = DriverReputation.PRIME_MIDFIELD, // Default, to be set manually
                RatingValues = new Dictionary<string, double>(), // Empty - to be filled manually
            };
            return result;
        }

        /// <summary>
        /// Convert Jolpica constructors to Ams2TeamEntry models
        /// </summary>
        private List<Ams2TeamEntry> ConvertTeams(List<JolpicaConstructor> jolpicaConstructors)
        {
            return jolpicaConstructors.Select(jc => new Ams2TeamEntry
            {
                TeamId = _teamIdsException.ContainsKey(jc.ConstructorId) ? _teamIdsException[jc.ConstructorId] : jc.ConstructorId,
                TeamName = jc.Name,
                Reputation = TeamReputation.MIDFIELD, // Default, to be set manually
            }).ToList();
        }

        /// <summary>
        /// Convert Jolpica races to Race models
        /// Creates separate race entries for sprint races (when Sprint field exists)
        /// </summary>
        private List<Race> ConvertRaces(List<JolpicaRace> jolpicaRaces, int year)
        {
            var races = new List<Race>();

            foreach (var jr in jolpicaRaces)
            {
                // If this weekend has a sprint, add a separate sprint race entry
                if (jr.Sprint != null)
                {   var sprintRace = new Race
                    {
                        RaceId = int.Parse(jr.Round),
                        RaceName = jr.RaceName + " (Sprint)",
                        RaceShortName = GenerateRaceShortName(jr.RaceName),
                        Circuit = FormatCircuitInfo(jr.Circuit),
                        RaceDate = jr.Sprint.Date,
                        IgnoreForPositionsTally = true

                    };

                    if (year == 2019)
                    {
                        sprintRace.PointsSystem = new Dictionary<string, int>
                    {
                        { "1", 3 },
                        { "2", 2 },
                        { "3", 1 }
                    };
                    }
                    else
                    {
                        sprintRace.PointsSystem = new Dictionary<string, int>
                    {
                        { "1", 8 },
                        { "2", 7 },
                        { "3", 6 },
                        { "4", 5 },
                        { "5", 4 },
                        { "6", 3 },
                        { "7", 2 },
                        { "8", 1 }
                    };
                    }
                }

                // Always add the main race
                races.Add(new Race
                {
                    RaceId = int.Parse(jr.Round),
                    RaceName = jr.RaceName,
                    RaceShortName = GenerateRaceShortName(jr.RaceName),
                    Circuit = FormatCircuitInfo(jr.Circuit),
                    RaceDate = jr.Date,
                    IgnoreForPositionsTally = false
                });
            }

            return races.OrderBy(r => r.RaceDate).ToList();
        }

        #region Helper Methods

        /// <summary>
        /// Parse date of birth string to year integer
        /// </summary>
        private int? ParseDateOfBirth(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;

            if (DateTime.TryParse(dateString, out DateTime date))
                return date.Year;

            return null;
        }

        /// <summary>
        /// Convert Jolpica nationality strings to 3-letter ISO codes
        /// </summary>
        private string ConvertNationality(string jolpicaNationality)
        {
            // Jolpica uses full nationality names, convert to ISO 3-letter codes
            var nationalityMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "British", "GBR" },
                { "German", "GER" },
                { "French", "FRA" },
                { "Italian", "ITA" },
                { "Spanish", "ESP" },
                { "Brazilian", "BRA" },
                { "Finnish", "FIN" },
                { "Australian", "AUS" },
                { "Canadian", "CAN" },
                { "Dutch", "NED" },
                { "Belgian", "BEL" },
                { "Austrian", "AUT" },
                { "Swiss", "SUI" },
                { "Swedish", "SWE" },
                { "Danish", "DEN" },
                { "Japanese", "JPN" },
                { "American", "USA" },
                { "Mexican", "MEX" },
                { "Argentine", "ARG" },
                { "South African", "RSA" },
                { "New Zealander", "NZL" },
                { "Irish", "IRL" },
                { "Polish", "POL" },
                { "Russian", "RUS" },
                { "Colombian", "COL" },
                { "Venezuelan", "VEN" },
                { "Thai", "THA" },
                { "Chinese", "CHN" },
                { "Indian", "IND" },
                { "Malaysian", "MYS" },
                { "Portuguese", "POR" },
                { "Monegasque", "MCO" },
                { "Czech", "CZE" },
                { "Hungarian", "HUN" },
                { "Uruguayan", "URU" },
                { "Chilean", "CHI" }
            };

            return nationalityMap.TryGetValue(jolpicaNationality, out var code)
                ? code
                : jolpicaNationality.Substring(0, Math.Min(3, jolpicaNationality.Length)).ToUpper();
        }

        /// <summary>
        /// Generate 3-letter race short name from race name
        /// </summary>
        private string GenerateRaceShortName(string raceName)
        {
            var shortNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Australian Grand Prix", "AUS" },
                { "Bahrain Grand Prix", "BAH" },
                { "Chinese Grand Prix", "CHN" },
                { "Azerbaijan Grand Prix", "AZE" },
                { "Spanish Grand Prix", "ESP" },
                { "Monaco Grand Prix", "MON" },
                { "Canadian Grand Prix", "CAN" },
                { "French Grand Prix", "FRA" },
                { "Austrian Grand Prix", "AUT" },
                { "British Grand Prix", "GBR" },
                { "German Grand Prix", "GER" },
                { "Hungarian Grand Prix", "HUN" },
                { "Belgian Grand Prix", "BEL" },
                { "Italian Grand Prix", "ITA" },
                { "Singapore Grand Prix", "SIN" },
                { "Russian Grand Prix", "RUS" },
                { "Japanese Grand Prix", "JPN" },
                { "United States Grand Prix", "USA" },
                { "Mexican Grand Prix", "MEX" },
                { "Brazilian Grand Prix", "BRA" },
                { "Abu Dhabi Grand Prix", "ABU" },
                { "Argentine Grand Prix", "ARG" },
                { "San Marino Grand Prix", "SMR" },
                { "European Grand Prix", "EUR" },
                { "Portuguese Grand Prix", "POR" },
                { "Dutch Grand Prix", "NED" },
                { "Swiss Grand Prix", "SUI" },
                { "Swedish Grand Prix", "SWE" },
                { "South African Grand Prix", "RSA" },
                { "Turkish Grand Prix", "TUR" },
                { "Indian Grand Prix", "IND" },
                { "Korean Grand Prix", "KOR" },
                { "Malaysian Grand Prix", "MAL" },
                { "Saudi Arabian Grand Prix", "SAU" },
                { "Miami Grand Prix", "MIA" },
                { "Las Vegas Grand Prix", "LVG" },
                { "Qatar Grand Prix", "QAT" },
                { "Emilia Romagna Grand Prix", "EMI" },
                { "Styrian Grand Prix", "STY" },
                { "70th Anniversary Grand Prix", "70A" },
                { "Tuscan Grand Prix", "TUS" },
                { "Eifel Grand Prix", "EIF" },
                { "Sakhir Grand Prix", "SAK" },
                { "Pacific Grand Prix", "PAC" },
                { "Luxembourg Grand Prix", "LUX" }
            };

            if (shortNameMap.TryGetValue(raceName, out var shortName))
                return shortName;

            // Fallback: extract first word and take 3 letters
            var firstWord = raceName.Split(' ')[0];
            return firstWord.Substring(0, Math.Min(3, firstWord.Length)).ToUpper();
        }

        /// <summary>
        /// Format circuit information as "Country CircuitName, Location"
        /// </summary>
        private string FormatCircuitInfo(JolpicaCircuit circuit)
        {
            if (circuit?.Location == null)
                return circuit?.CircuitName ?? "Unknown Circuit";

            return $"{circuit.Location.Country} {circuit.CircuitName}, {circuit.Location.Locality}";
        }

        #endregion
    }

    #region Result Models

    public class SeasonImportResult
    {
        public int Year { get; set; }
        public List<Ams2DriverData> Drivers { get; set; } = new();
        public List<Ams2TeamEntry> Teams { get; set; } = new();
        public List<Race> Races { get; set; } = new();
    }

    #endregion
}