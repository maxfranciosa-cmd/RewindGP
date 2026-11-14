using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>Where an academy member drives this season.</summary>
    public enum AcademyMemberSeat
    {
        // a race seat at one of the academy's parent teams
        PARENT_SEAT,
        // a race seat at one of its sister teams
        SISTER_SEAT,
        // a race seat at a team outside the academy (only the player can hold one and stay a member)
        OTHER_SEAT,
        // a team's reserve
        RESERVE,
        // no seat at all
        NO_SEAT
    }

    public class AcademyMemberEntry
    {
        public string DriverId { get; set; }
        public string Name { get; set; }
        public bool IsPlayer { get; set; }
        // null when the year of birth isn't known
        public int? Age { get; set; }
        public DriverReputation Reputation { get; set; }

        public AcademyMemberSeat Seat { get; set; }
        // the team of the seat (race seat or reserve); null without one. For a former member, the
        // team outside the academy he signed for
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public string TeamShortName { get; set; }
        public string TeamColor { get; set; }

        // this season is his last as a member: he's too old for the academy next season
        public bool AgesOut { get; set; }

        // his FP1 outings of this season that have happened, and their average score (null for none)
        public int PracticeOutings { get; set; }
        public double? PracticeAverage { get; set; }
        // what that average is worth at the end of the season to a driver without a race seat
        public PracticeReward PracticeReward { get; set; }

        // his championship this season: null when he isn't in the standings
        public double? Points { get; set; }
        public int? ChampionshipPosition { get; set; }

        // place in the academy's pecking order, from 1 (see AcademyStandings.Build)
        public int Rank { get; set; }
    }

    public class AcademyStandingsEntry
    {
        public Academy Academy { get; set; }
        public bool IsPlayersAcademy { get; set; }
        // in pecking order
        public List<AcademyMemberEntry> Members { get; set; } = new();
        // AI drivers the academy still lists, still young, who signed for a team outside it (and so
        // are out until they sign for one of its teams again: AcademyMembership.AcademyOf)
        public List<AcademyMemberEntry> FormerMembers { get; set; } = new();
    }

    /// <summary>
    /// The current season's junior academies as the player would want to read them: every member,
    /// where he drives, how his FP1 outings and his season are going, and the academy's pecking
    /// order. Nothing is stored: everything comes from AcademyMembership, the save's FP1 outings and
    /// the current standings.
    /// </summary>
    public static class AcademyStandings
    {
        /// <summary>
        /// The academies of the current season, the player's first, then in the season's order. The
        /// members are ranked the way the academy's teams look at them when they hire: reputation
        /// first (DriverHirer.ReputationScore, what the academy fit and the hiring draw are made
        /// of), then the FP1 average (what lifts a seatless member's reputation at the end of the
        /// season; no outing ranks last), then championship points, then the younger first.
        /// </summary>
        public static List<AcademyStandingsEntry> Build(ISaveGame saveGame)
        {
            var season = saveGame?.CurrentSeason;
            var academies = season?.Academies;
            if (academies == null || academies.Count == 0)
                return new List<AcademyStandingsEntry>();

            int year = season.Year;
            string playerId = saveGame.PlayerData?.DriverId;
            string playerAcademyId = saveGame.PlayerData?.AcademyId;
            var teams = (season.Teams ?? Enumerable.Empty<ITeamEntry>()).Where(t => t != null).ToList();
            var lastTeams = AcademyMembership.LastTeams(saveGame);
            var practiceScores = PracticeOutings.ScoresOf(saveGame, year);
            var standings = (saveGame.CurrentDriverStandings ?? Enumerable.Empty<HistoricalDriverStandingEntry>())
                .Where(s => !string.IsNullOrEmpty(s?.DriverId))
                .GroupBy(s => s.DriverId)
                .ToDictionary(g => g.Key, g => g.First());

            var result = academies
                .Where(a => a != null)
                .Select(a => new AcademyStandingsEntry { Academy = a, IsPlayersAcademy = !string.IsNullOrEmpty(playerAcademyId) && a.Id == playerAcademyId })
                .ToList();

            var seen = new HashSet<string>();
            foreach (var driver in saveGame.Drivers ?? Enumerable.Empty<IDriverData>())
            {
                if (string.IsNullOrEmpty(driver?.DriverId) || !seen.Add(driver.DriverId))
                    continue;

                lastTeams.TryGetValue(driver.DriverId, out var lastTeamId);
                var academy = AcademyMembership.AcademyOf(academies, year, driver.DriverId, driver.YearOfBirth, playerId, playerAcademyId, lastTeamId);
                if (academy != null)
                {
                    var entry = result.First(e => e.Academy == academy);
                    entry.Members.Add(ToMember(driver, academy, teams, year, playerId, practiceScores, standings));
                    continue;
                }

                // listed and young, but signed elsewhere: out of the academy for now
                if (driver.DriverId == playerId || !AcademyMembership.IsYoung(year, driver.YearOfBirth))
                    continue;
                var listing = academies.FirstOrDefault(a => a?.Drivers?.Contains(driver.DriverId) == true);
                if (listing == null)
                    continue;

                var former = ToMember(driver, listing, teams, year, playerId, practiceScores, standings);
                var team = teams.FirstOrDefault(t => t.TeamId == lastTeamId);
                former.TeamId = lastTeamId;
                former.TeamName = team?.TeamName;
                former.TeamShortName = team?.ShortTeamName();
                former.TeamColor = team?.Color;
                result.First(e => e.Academy == listing).FormerMembers.Add(former);
            }

            foreach (var entry in result)
            {
                entry.Members = entry.Members
                    .OrderByDescending(m => DriverHirer.ReputationScore(m.Reputation))
                    .ThenByDescending(m => m.PracticeAverage ?? double.MinValue)
                    .ThenByDescending(m => m.Points ?? 0)
                    .ThenBy(m => m.Age ?? int.MaxValue)
                    .ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                for (int i = 0; i < entry.Members.Count; i++)
                    entry.Members[i].Rank = i + 1;

                entry.FormerMembers = entry.FormerMembers
                    .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            // the player's academy first; OrderBy is stable, so the others keep the season's order
            return result.OrderBy(e => e.IsPlayersAcademy ? 0 : 1).ToList();
        }

        private static AcademyMemberEntry ToMember(IDriverData driver, Academy academy, List<ITeamEntry> teams, int year, string playerId,
            Dictionary<string, List<int>> practiceScores, Dictionary<string, HistoricalDriverStandingEntry> standings)
        {
            var member = new AcademyMemberEntry
            {
                DriverId = driver.DriverId,
                Name = driver.Name,
                IsPlayer = !string.IsNullOrEmpty(playerId) && driver.DriverId == playerId,
                Age = driver.YearOfBirth > 0 ? year - driver.YearOfBirth : null,
                Reputation = driver.Reputation,
                AgesOut = driver.YearOfBirth > 0 && !AcademyMembership.IsYoung(year + 1, driver.YearOfBirth),
                Seat = AcademyMemberSeat.NO_SEAT
            };

            // the race seat first: a driver holding one isn't anybody's reserve
            var raceTeam = teams.FirstOrDefault(t => t.Driver1Contract?.DriverId == driver.DriverId || t.Driver2Contract?.DriverId == driver.DriverId);
            var reserveTeam = raceTeam == null ? teams.FirstOrDefault(t => t.ReserveDriverId() == driver.DriverId) : null;
            var team = raceTeam ?? reserveTeam;
            if (team != null)
            {
                member.Seat = reserveTeam != null ? AcademyMemberSeat.RESERVE
                    : AcademyMembership.IsParentTeam(academy, team.TeamId) ? AcademyMemberSeat.PARENT_SEAT
                    : AcademyMembership.IsSisterTeam(academy, team.TeamId) ? AcademyMemberSeat.SISTER_SEAT
                    : AcademyMemberSeat.OTHER_SEAT;
                member.TeamId = team.TeamId;
                member.TeamName = team.TeamName;
                member.TeamShortName = team.ShortTeamName();
                member.TeamColor = team.Color;
            }

            if (practiceScores.TryGetValue(driver.DriverId, out var scores) && scores.Count > 0)
            {
                member.PracticeOutings = scores.Count;
                member.PracticeAverage = scores.Average();
                member.PracticeReward = PracticeOutings.RewardFor(scores);
            }

            if (standings.TryGetValue(driver.DriverId, out var standing))
            {
                member.Points = standing.Points;
                member.ChampionshipPosition = standing.Position > 0 ? standing.Position : null;
            }

            return member;
        }
    }
}
