namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>Tracks every pak file backed up (and not yet restored) for one AMS2 install.</summary>
    public sealed class VehiclePakBackupManifest
    {
        /// <summary>Informational only - the folder recorded at backup time. Restore always targets whatever folder the caller passes in, not this field, so a player who repoints the app at a different install still restores the right place.</summary>
        public string InstallFolder { get; set; } = string.Empty;

        public List<VehiclePakBackupEntry> Entries { get; set; } = new();
    }

    public sealed class VehiclePakBackupEntry
    {
        /// <summary>Path relative to the AMS2 install folder, e.g. "Pakfiles\Vehicles\formula_hitech_g1m3.bff".</summary>
        public string RelativePakPath { get; set; } = string.Empty;

        /// <summary>File name of the backed-up original inside this install's backup folder.</summary>
        public string BackupFileName { get; set; } = string.Empty;

        public DateTime BackedUpAtUtc { get; set; }

        public string OriginalSha256 { get; set; } = string.Empty;
    }
}
