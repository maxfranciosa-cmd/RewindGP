using System.Text.Json;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    public static class VehiclePakBackupManifestSerializer
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static string Serialize(VehiclePakBackupManifest manifest) =>
            JsonSerializer.Serialize(manifest, Options);

        public static VehiclePakBackupManifest Parse(string json) =>
            JsonSerializer.Deserialize<VehiclePakBackupManifest>(json, Options) ?? new VehiclePakBackupManifest();
    }
}
