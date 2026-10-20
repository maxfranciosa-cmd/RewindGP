using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.Helpers
{
    /// <summary>
    /// Shorter versions of drivers' and teams' full names, for tight spaces (timing towers,
    /// standings, tables).
    ///
    /// Drivers: the last name, i.e. the last word plus the short words right before it
    /// ("Pedro De La Rosa" -> "De La Rosa"), never the first word. When several drivers in the save
    /// share it, the start of the first name is added until they differ: "M Schumacher" next to
    /// Ralf, "Mich Schumacher" when Mick is there too.
    ///
    /// Teams: the pack's own short name when it has one; otherwise the words of the full name that
    /// match the team id ("Mild Seven Benetton Renault", benetton -> "Benetton"); otherwise the full
    /// name without its last word, usually the engine ("Willis Rellaux" -> "Willis").
    /// </summary>
    public static class ShortNameExtensions
    {
        // words up to this long, right before the last one, belong to the last name (De, La, Van, Der)
        private const int MaxParticleLength = 3;

        /// <summary>
        /// The driver's short name among every driver of the save: active, retired and the player.
        /// </summary>
        public static string ShortDriverName(this ISaveGame saveGame, string driverId)
        {
            var names = AllDriverNames(saveGame);
            return names.TryGetValue(driverId ?? string.Empty, out var fullName)
                ? ShortDriverName(fullName, names.Values)
                : driverId;
        }

        /// <summary>Every driver's short name in the save at once, by driver id: for lists, so names are worked out once.</summary>
        public static Dictionary<string, string> ShortDriverNames(this ISaveGame saveGame)
        {
            var names = AllDriverNames(saveGame);
            var shortNames = ShortDriverNames(names.Values);
            return names.ToDictionary(n => n.Key, n => shortNames.GetValueOrDefault(Normalize(n.Value), n.Value));
        }

        /// <summary>The driver's short name, telling him apart from <paramref name="allDrivers"/> (which may include him).</summary>
        public static string ShortName(this IDriverData driver, IEnumerable<IDriverData> allDrivers) =>
            ShortDriverName(driver?.Name, (allDrivers ?? Enumerable.Empty<IDriverData>()).Select(d => d.Name));

        /// <summary>
        /// The short name of one full name, telling it apart from <paramref name="allFullNames"/>
        /// (which may include it).
        /// </summary>
        public static string ShortDriverName(string fullName, IEnumerable<string> allFullNames)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return fullName;

            // first, so its own spelling wins over a namesake spelled in another case
            var names = (allFullNames ?? Enumerable.Empty<string>()).Prepend(fullName);
            return ShortDriverNames(names).GetValueOrDefault(Normalize(fullName), fullName.Trim());
        }

        /// <summary>The driver's surname: the last word plus the short words right before it, never the first word.</summary>
        public static string Surname(this IDriverData driver) => LastName(driver?.Name);

        /// <summary>
        /// The driver's first name: every word of the full name that isn't part of the surname
        /// ("Juan Pablo Montoya" -> "Juan Pablo", "Pedro De La Rosa" -> "Pedro"); empty for a
        /// one-word name.
        /// </summary>
        public static string FirstName(this IDriverData driver) => FirstName(driver?.Name);

        /// <summary>The last name alone: the last word plus the short words right before it, never the first word.</summary>
        public static string LastName(string fullName)
        {
            var words = Words(fullName);
            if (words.Length == 0)
                return fullName;

            return string.Join(' ', words[SurnameStart(words)..]);
        }

        /// <summary>Every word of the full name before the surname; empty for a one-word name.</summary>
        public static string FirstName(string fullName)
        {
            var words = Words(fullName);
            if (words.Length == 0)
                return fullName;

            return string.Join(' ', words[..SurnameStart(words)]);
        }

        // where the surname begins: the last word, plus the short words right before it, never the first
        private static int SurnameStart(string[] words)
        {
            var start = words.Length - 1;
            while (start > 1 && words[start - 1].Length <= MaxParticleLength)
                start--;
            return start;
        }

        /// <summary>
        /// The team's short name: <see cref="ITeamEntry.ShortName"/> when the pack sets one; otherwise
        /// the words of the full name that match the team id; otherwise the full name without its
        /// last word (usually the engine); a one-word name stays whole.
        /// </summary>
        public static string ShortTeamName(this ITeamEntry team)
        {
            if (team == null)
                return null;

            if (!string.IsNullOrWhiteSpace(team.ShortName))
                return team.ShortName.Trim();

            return ShortTeamName(team.TeamName, team.TeamId);
        }

        public static string ShortTeamName(string teamName, string teamId)
        {
            var words = Words(teamName);
            if (words.Length <= 1)
                return teamName?.Trim();

            var fromId = WordsMatchingId(words, teamId);
            if (fromId != null)
                return fromId;

            return string.Join(' ', words[..^1]);
        }

        // full name (normalized) -> short name, for every distinct name given
        private static Dictionary<string, string> ShortDriverNames(IEnumerable<string> fullNames)
        {
            var distinct = fullNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in distinct.GroupBy(LastName, StringComparer.OrdinalIgnoreCase))
            {
                var lastName = group.Key;
                var namesake = group.ToList();
                foreach (var name in namesake)
                {
                    result[name] = namesake.Count == 1
                        ? lastName
                        : $"{DistinguishingPrefix(FirstWord(name), namesake.Where(n => n != name).Select(FirstWord))} {lastName}";
                }
            }
            return result;
        }

        // the shortest start of firstName that none of the others starts with the same way
        private static string DistinguishingPrefix(string firstName, IEnumerable<string> otherFirstNames)
        {
            var others = otherFirstNames.ToList();
            for (int length = 1; length <= firstName.Length; length++)
            {
                var prefix = firstName[..length];
                if (!others.Any(o => o.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    return prefix;
            }
            // the same first name too: nothing shorter tells them apart
            return firstName;
        }

        // the longest run of consecutive words that, joined, spell the team id or one of its parts
        // ("red_bull" -> "Red Bull"; "ligier_prost" -> "Ligier")
        private static string WordsMatchingId(string[] words, string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                return null;

            var idParts = teamId.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(Letters).Where(p => p.Length > 0).ToArray();
            if (idParts.Length == 0)
                return null;

            // whole id first, then each part, longest first
            var targets = new List<string> { string.Concat(idParts) };
            targets.AddRange(idParts.OrderByDescending(p => p.Length));

            foreach (var target in targets.Distinct())
            {
                for (int length = words.Length; length >= 1; length--)
                {
                    for (int start = 0; start + length <= words.Length; start++)
                    {
                        var run = words[start..(start + length)];
                        if (string.Equals(string.Concat(run.Select(Letters)), target, StringComparison.OrdinalIgnoreCase))
                            return string.Join(' ', run);
                    }
                }
            }
            return null;
        }

        private static Dictionary<string, string> AllDriverNames(ISaveGame saveGame)
        {
            var names = new Dictionary<string, string>();
            foreach (var driver in (saveGame?.Drivers ?? Enumerable.Empty<IDriverData>()).Concat(saveGame?.RetiredDrivers ?? Enumerable.Empty<IDriverData>()))
            {
                if (!string.IsNullOrEmpty(driver?.DriverId) && !string.IsNullOrWhiteSpace(driver.Name))
                    names.TryAdd(driver.DriverId, driver.Name);
            }

            var player = saveGame?.PlayerData;
            if (!string.IsNullOrEmpty(player?.DriverId) && !string.IsNullOrWhiteSpace(player.Name))
                names[player.DriverId] = player.Name;

            return names;
        }

        private static string[] Words(string name) =>
            (name ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        private static string Normalize(string name) => string.Join(' ', Words(name));

        private static string FirstWord(string name) => Words(name).FirstOrDefault() ?? string.Empty;

        private static string Letters(string word) => new(word.Where(char.IsLetterOrDigit).ToArray());
    }
}
