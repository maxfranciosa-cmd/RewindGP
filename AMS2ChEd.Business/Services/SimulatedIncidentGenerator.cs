namespace AMS2ChEd.Business.Services
{
    /// <summary>
    /// The incidents of a simulated race, so a player waiting outside the grid (a reserve, a free
    /// agent) sees the events that open seats. For now teammate collisions: each one earns the
    /// driver at fault a CONTACT_WITH_TEAMMATE reprimand (ReprimandManager), with the usual
    /// consequences up to a release for the rest of the season. Only simulated races: in a race the
    /// player drives, AI-vs-AI contact stays undetected and isn't invented.
    /// </summary>
    public static class SimulatedIncidentGenerator
    {
        // per team running two cars, per race: a handful of incidents across a season's grid
        public const double TeammateCollisionChance = 0.025;

        /// <summary>
        /// Rolls the teammate collisions of the race and applies them to the results: the victim
        /// retires, the driver at fault retires too or drops to the back of the classified cars
        /// (a coin toss each, as is who was at fault). Positions are numbered again.
        /// </summary>
        public static IReadOnlyList<TeammateContact> ApplyTeammateCollisions(List<ParticipantData> raceResults, Random random)
        {
            var contacts = new List<TeammateContact>();
            if (raceResults == null || raceResults.Count == 0)
                return contacts;

            var droppedBack = new HashSet<ParticipantData>();

            var teams = raceResults
                .Where(r => !string.IsNullOrEmpty(r.TeamId) && !string.IsNullOrEmpty(r.DriverId) && !r.DNF && !r.DidNotPreQualify)
                .GroupBy(r => r.TeamId)
                .Where(g => g.Count() == 2)
                .OrderBy(g => g.Key);

            foreach (var team in teams)
            {
                if (random.NextDouble() >= TeammateCollisionChance)
                    continue;

                var cars = team.ToList();
                var atFaultIndex = random.Next(2);
                var atFault = cars[atFaultIndex];
                var victim = cars[1 - atFaultIndex];

                victim.DNF = true;
                if (random.Next(2) == 0)
                    atFault.DNF = true;
                else
                    droppedBack.Add(atFault);

                contacts.Add(new TeammateContact { AtFaultDriverId = atFault.DriverId, OtherDriverId = victim.DriverId });
            }

            if (contacts.Count == 0)
                return contacts;

            // classified cars in their order with the ones dropping back behind them, then the retirements
            var reordered = raceResults.Where(r => !r.DNF && !droppedBack.Contains(r)).OrderBy(r => r.Position)
                .Concat(raceResults.Where(r => droppedBack.Contains(r)).OrderBy(r => r.Position))
                .Concat(raceResults.Where(r => r.DNF).OrderBy(r => r.Position))
                .ToList();

            raceResults.Clear();
            raceResults.AddRange(reordered);
            for (int i = 0; i < raceResults.Count; i++)
            {
                raceResults[i].Position = i + 1;
            }

            return contacts;
        }
    }
}
