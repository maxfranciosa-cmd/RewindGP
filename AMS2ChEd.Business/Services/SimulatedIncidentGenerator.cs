using AMS2ChEd.Business.Services.CrashDetection;

namespace AMS2ChEd.Business.Services
{
    /// <summary>
    /// The incidents of a simulated race, so a player waiting outside the grid (a reserve, a free
    /// agent) sees the events that open seats: teammate collisions, each earning the driver at fault
    /// a CONTACT_WITH_TEAMMATE reprimand (ReprimandManager) with the usual consequences up to a
    /// release for the rest of the season; and crashes, which can injure (CrashIncidentManager).
    /// Only simulated races: in a race the player drives, AI-vs-AI contact stays undetected and
    /// isn't invented, and crashes come from the telemetry instead.
    /// </summary>
    public static class SimulatedIncidentGenerator
    {
        // per team running two cars, per race: a handful of incidents across a season's grid
        public const double TeammateCollisionChance = 0.025;

        // a retirement that was a crash rather than a failure; a teammate collision always is one
        public const double RetirementCrashChance = 0.4;

        // how hard a crash was: only a Severe one can hurt (CrashOutcomeRoller). Together with
        // ~10% retirements per car, a 26-car grid has a Severe crash every seven races or so
        public const double SevereCrashChance = 0.15;
        public const double ModerateCrashChance = 0.35;

        // per car that finished: a heavy shunt it carried on from (CrashOutcomeRoller's lower odds).
        // About one in fifteen races, against one in seven for the Severe crashes among retirements
        public const double DroveOnShuntChance = 0.003;

        /// <summary>
        /// The retirements that were crashes, each with a random severity; and the cars that finished
        /// after a crash: the driver at fault in a teammate collision who carried on (random
        /// severity), or a rare heavy shunt (Severe). The player can't crash in a simulated race:
        /// they aren't in the car.
        /// </summary>
        public static IReadOnlyList<RaceCrash> RollCrashes(List<ParticipantData> raceResults, IReadOnlyCollection<TeammateContact> teammateContacts, string playerId, Random random)
        {
            var crashes = new List<RaceCrash>();
            if (raceResults == null)
                return crashes;

            var collided = (teammateContacts ?? Array.Empty<TeammateContact>())
                .SelectMany(c => new[] { c.AtFaultDriverId, c.OtherDriverId })
                .ToHashSet();

            foreach (var result in raceResults.Where(r => r.DNF && !r.DidNotPreQualify && !string.IsNullOrEmpty(r.DriverId) && r.DriverId != playerId))
            {
                if (!collided.Contains(result.DriverId) && random.NextDouble() >= RetirementCrashChance)
                    continue;

                crashes.Add(new RaceCrash
                {
                    DriverId = result.DriverId,
                    TeamId = result.TeamId,
                    Severity = RollSeverity(random)
                });
            }

            foreach (var result in raceResults.Where(r => !r.DNF && !r.DidNotPreQualify && !string.IsNullOrEmpty(r.DriverId) && r.DriverId != playerId))
            {
                CrashSeverity severity;
                if (collided.Contains(result.DriverId))
                    severity = RollSeverity(random);
                else if (random.NextDouble() < DroveOnShuntChance)
                    severity = CrashSeverity.Severe;
                else
                    continue;

                crashes.Add(new RaceCrash
                {
                    DriverId = result.DriverId,
                    TeamId = result.TeamId,
                    Severity = severity,
                    DroveOn = true
                });
            }

            return crashes;
        }

        private static CrashSeverity RollSeverity(Random random)
        {
            var roll = random.NextDouble();
            return roll < SevereCrashChance ? CrashSeverity.Severe
                : roll < SevereCrashChance + ModerateCrashChance ? CrashSeverity.Moderate
                : CrashSeverity.Minor;
        }

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
