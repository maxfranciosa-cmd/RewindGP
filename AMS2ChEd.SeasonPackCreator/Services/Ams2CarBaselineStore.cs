using AMS2ChEd.Business.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AMS2ChEd.SeasonPackEditor.Services
{
    /// <summary>
    /// Real-world power/weight for an AMS2 car model, used to correct championship-result-based
    /// malus generation for cars that are already inherently stronger/weaker than the field average.
    /// </summary>
    public class Ams2CarBaseline
    {
        public double PowerHp { get; set; }
        public double WeightKg { get; set; }

        public double PowerToWeight => WeightKg > 0 ? PowerHp / WeightKg : 0.0;
    }

    /// <summary>
    /// Loads/saves the author-maintained AMS2 car baseline reference table. Kept as a single file
    /// reused across season packs, since the same physical AMS2 car model gets reused across years.
    /// </summary>
    public static class Ams2CarBaselineStore
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ams2CarBaselines.json");

        public static Dictionary<string, Ams2CarBaseline> Load()
        {
            if (!File.Exists(FilePath))
                return new Dictionary<string, Ams2CarBaseline>(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<Dictionary<string, Ams2CarBaseline>>(json, DefaultJsonSerializerOptions.Instance)
                ?? new Dictionary<string, Ams2CarBaseline>(StringComparer.OrdinalIgnoreCase);
        }

        public static void Save(Dictionary<string, Ams2CarBaseline> baselines)
        {
            var json = JsonSerializer.Serialize(baselines, DefaultJsonSerializerOptions.Instance);
            File.WriteAllText(FilePath, json);
        }
    }
}
