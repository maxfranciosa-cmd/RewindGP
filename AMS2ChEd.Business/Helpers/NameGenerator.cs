using System.Text;

namespace AMS2ChEd.Business.Helpers
{
    public class NameGenerator
    {
        public static string GenerateName(IEnumerable<string> namesBase)
        {
            var random = new Random();
            var firstNames = namesBase
                .Select(n => ExtractFirstName(n))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            var lastNames = namesBase
                .Select(n => ExtractLastName(n))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            // Pick a random first name
            string newFirstName = firstNames[random.Next(firstNames.Count)];

            // Generate pot1 and pot2
            var pot1 = GeneratePot1(lastNames);
            var pot2 = GeneratePot2(lastNames);

            // Generate initial last name components
            var selectedPot1Indices = new List<int>();
            int basePot1Count = random.Next(1, 3); // 1 or 2

            // Select initial pot1 segments
            for (int i = 0; i < basePot1Count; i++)
            {
                int index = SelectUnusedIndex(pot1.Count, selectedPot1Indices);
                selectedPot1Indices.Add(index);
            }

            // Select one from pot2
            int pot2Index = random.Next(pot2.Count);

            // Build the name and check for duplicates
            string newLastName;
            string fullName;
            const int maxAttempts = 100;
            int attempts = 0;

            do
            {
                // Build last name from selected indices
                var result = new StringBuilder();
                foreach (var index in selectedPot1Indices)
                {
                    result.Append(pot1[index]);
                }
                result.Append(pot2[pot2Index]);

                newLastName = result.ToString();
                fullName = $"{newFirstName} {newLastName}";

                // If duplicate and not at max attempts, add another pot1 segment
                if (namesBase.Any(n => n.Equals(fullName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (selectedPot1Indices.Count < pot1.Count * 2) // Allow some reuse if needed
                    {
                        int newIndex = SelectUnusedIndex(pot1.Count, selectedPot1Indices);
                        selectedPot1Indices.Add(newIndex);
                        attempts++;
                    }
                    else
                    {
                        attempts = maxAttempts; // Can't add more unique segments
                    }
                }
                else
                {
                    break; // Unique name found!
                }
            } while (attempts < maxAttempts);

            if (attempts >= maxAttempts)
            {
                throw new InvalidOperationException($"Could not generate unique driver name after {maxAttempts} attempts");
            }

            return fullName;
        }

        private static int SelectUnusedIndex(int maxCount, List<int> usedIndices)
        {
            var random = new Random();
            if (maxCount == 1)
            {
                return 0; // Only one option
            }

            // Try to find an unused index
            var availableIndices = Enumerable.Range(0, maxCount)
                .Where(i => !usedIndices.Contains(i))
                .ToList();

            if (availableIndices.Count > 0)
            {
                // Pick from unused indices
                return availableIndices[random.Next(availableIndices.Count)];
            }
            else
            {
                // All used, pick any (allow reuse)
                return random.Next(maxCount);
            }
        }

        private static string ExtractFirstName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return string.Empty;

            int i = 0;
            while (i < fullName.Length && char.IsLetter(fullName[i]))
            {
                i++;
            }

            return fullName.Substring(0, i);
        }

        private static string ExtractLastName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return string.Empty;

            int i = fullName.Length - 1;
            while (i >= 0 && char.IsLetter(fullName[i]))
            {
                i--;
            }

            return fullName.Substring(i + 1);
        }

        private static List<string> GeneratePot1(List<string> lastNames)
        {
            var pot1 = new HashSet<string>();

            foreach (var lastName in lastNames)
            {
                if (string.IsNullOrEmpty(lastName)) continue;

                bool firstIsVowel = IsVowel(lastName[0]);
                int vowelCount = 0;
                int targetVowels = firstIsVowel ? 2 : 1;

                for (int i = 0; i < lastName.Length; i++)
                {
                    if (IsVowel(lastName[i]))
                    {
                        vowelCount++;
                        if (vowelCount == targetVowels)
                        {
                            pot1.Add(lastName.Substring(0, i + 1));
                            break;
                        }
                    }
                }
            }

            return pot1.ToList();
        }

        private static List<string> GeneratePot2(List<string> lastNames)
        {
            var pot2 = new HashSet<string>();

            foreach (var lastName in lastNames)
            {
                if (string.IsNullOrEmpty(lastName)) continue;

                bool lastIsConsonant = !IsVowel(lastName[lastName.Length - 1]);
                int consonantCount = 0;
                int targetConsonants = lastIsConsonant ? 2 : 1;

                for (int i = lastName.Length - 1; i >= 0; i--)
                {
                    if (!IsVowel(lastName[i]))
                    {
                        consonantCount++;
                        if (consonantCount == targetConsonants)
                        {
                            string segment = lastName.Substring(i);
                            pot2.Add(segment);

                            // Check for doubled consonant
                            if (i > 0 && lastName[i] == lastName[i - 1])
                            {
                                pot2.Add(lastName.Substring(i - 1));
                            }

                            break;
                        }
                    }
                }
            }

            return pot2.ToList();
        }

        private static bool IsVowel(char c)
        {
            char lower = char.ToLower(c);
            return lower == 'a' || lower == 'e' || lower == 'i' || lower == 'o' || lower == 'u';
        }
    }
}
