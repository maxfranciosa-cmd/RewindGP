using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>
    /// Generates entry lists for Grand Prix races
    /// </summary>
    public class EntryListGenerator : IEntryListGenerator
    {
        public event EventHandler<EntryListGeneratedEventArgs> EntryListGenerated;

        /// <summary>
        /// Generate entry list for the next Grand Prix
        /// </summary>
        public List<EntryListEntry> GenerateEntryList(ISaveGame saveGame)
        {
            var entryList = new List<EntryListEntry>();

            // Create base entry list from teams
            foreach (var team in saveGame.CurrentSeason.Teams)
            {
                var driver1Reputation = GetDriverReputation(team.Driver1Contract.DriverId, saveGame);
                var driver2Reputation = GetDriverReputation(team.Driver2Contract.DriverId, saveGame);

                entryList.Add(new EntryListEntry
                {
                    TeamId = team.TeamId,
                    Driver1Id = team.Driver1Contract.DriverId,
                    Driver1Reputation = driver1Reputation,
                    Driver1Number = team.Driver1Contract.DriverNumber,
                    Driver2Id = team.Driver2Contract.DriverId,
                    Driver2Reputation = driver2Reputation,
                    Driver2Number = team.Driver2Contract.DriverNumber
                });
            }

            EntryListGenerated?.Invoke(this, new EntryListGeneratedEventArgs
            {
                EntryList = entryList,
                RaceIndex = saveGame.NextGpIndex
            });

            return entryList;
        }

        /// <summary>
        /// Get absences for a specific Grand Prix
        /// </summary>
        public List<Absence> GetAbsencesForGrandPrix(ISaveGame saveGame)
        {
            if (saveGame.CurrentSeason.Absences == null || !saveGame.CurrentSeason.Absences.Any())
                return new List<Absence>();

            if (saveGame.NextGpIndex >= saveGame.CurrentSeason.Races.Count())
                return new List<Absence>();

            var nextGpId = saveGame.CurrentSeason.Races.ElementAt(saveGame.NextGpIndex).RaceId;
            return saveGame.CurrentSeason.Absences
                .Where(a => a.RaceId == nextGpId)
                .ToList();
        }

        private DriverReputation GetDriverReputation(string driverId, ISaveGame saveGame)
        {
            var season = saveGame.CurrentSeason.Year;
            var driver = saveGame.Drivers.FirstOrDefault(d => d.DriverId == driverId);

            if (driver != null)
            {
                return driver.Reputation;
            }

            // Default fallback
            return DriverReputation.PRIME_MIDFIELD;
        }
    }
}