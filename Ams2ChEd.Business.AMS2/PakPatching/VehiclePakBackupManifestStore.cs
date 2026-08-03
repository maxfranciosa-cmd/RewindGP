using System.Security.Cryptography;
using System.Text;
using Ams2ChEd.Business.AMS2.Helpers;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Backs up pak files before they're first patched, and restores them on request. Backups
    /// are keyed per-install (a short hash of the normalized install folder path) under
    /// <see cref="StoragePaths.VehiclePakBackupsFolder"/>, so restoring always targets whichever
    /// install folder the caller currently has configured - not necessarily the one recorded at
    /// backup time (a player could repoint the app at a different AMS2 install in between).
    /// </summary>
    public class VehiclePakBackupManifestStore
    {
        public VehiclePakBackupManifest Load(string ams2InstallFolder)
        {
            string manifestPath = GetManifestPath(ams2InstallFolder);
            if (!File.Exists(manifestPath))
                return new VehiclePakBackupManifest { InstallFolder = ams2InstallFolder };

            return VehiclePakBackupManifestSerializer.Parse(File.ReadAllText(manifestPath));
        }

        public bool HasAnyBackup(string ams2InstallFolder) => Load(ams2InstallFolder).Entries.Count > 0;

        /// <summary>Backs up the file at <paramref name="absolutePakPath"/> if not already backed up for this install. Returns true if a new backup was made, false if one already existed (no-op).</summary>
        public bool EnsureBackedUp(string ams2InstallFolder, string absolutePakPath)
        {
            string relativePath = Path.GetRelativePath(ams2InstallFolder, absolutePakPath);
            var manifest = Load(ams2InstallFolder);

            if (manifest.Entries.Any(e => string.Equals(e.RelativePakPath, relativePath, StringComparison.OrdinalIgnoreCase)))
                return false;

            string backupsFolder = GetBackupFilesFolder(ams2InstallFolder);
            Directory.CreateDirectory(backupsFolder);

            string backupFileName = SanitizeFileName(relativePath) + ".bak";
            string backupPath = Path.Combine(backupsFolder, backupFileName);

            File.Copy(absolutePakPath, backupPath, overwrite: true);

            manifest.InstallFolder = ams2InstallFolder;
            manifest.Entries.Add(new VehiclePakBackupEntry
            {
                RelativePakPath = relativePath,
                BackupFileName = backupFileName,
                BackedUpAtUtc = DateTime.UtcNow,
                OriginalSha256 = ComputeSha256(absolutePakPath),
            });

            Save(ams2InstallFolder, manifest);
            return true;
        }

        /// <summary>Returns the backup file path for an already-backed-up pak (relative to <paramref name="ams2InstallFolder"/>), or null if it was never backed up. Used to roll back a partially-committed patch attempt.</summary>
        public string? TryGetBackupPath(string ams2InstallFolder, string absolutePakPath)
        {
            string relativePath = Path.GetRelativePath(ams2InstallFolder, absolutePakPath);
            var entry = Load(ams2InstallFolder).Entries
                .FirstOrDefault(e => string.Equals(e.RelativePakPath, relativePath, StringComparison.OrdinalIgnoreCase));

            return entry is null ? null : Path.Combine(GetBackupFilesFolder(ams2InstallFolder), entry.BackupFileName);
        }

        /// <summary>Restores every backed-up pak for this install back over the current file, then clears the manifest. Best-effort per file (a missing backup file is skipped, not fatal) - the caller decides whether a partial restore still counts as success.</summary>
        public (int restored, int missing) RestoreAll(string ams2InstallFolder)
        {
            var manifest = Load(ams2InstallFolder);
            if (manifest.Entries.Count == 0)
                return (0, 0);

            int restored = 0, missing = 0;
            foreach (var entry in manifest.Entries)
            {
                string targetPath = Path.Combine(ams2InstallFolder, entry.RelativePakPath);
                string backupPath = Path.Combine(GetBackupFilesFolder(ams2InstallFolder), entry.BackupFileName);

                if (!File.Exists(backupPath))
                {
                    missing++;
                    continue;
                }

                File.Copy(backupPath, targetPath, overwrite: true);
                File.Delete(backupPath);
                restored++;
            }

            Save(ams2InstallFolder, new VehiclePakBackupManifest { InstallFolder = ams2InstallFolder });
            return (restored, missing);
        }

        private static void Save(string ams2InstallFolder, VehiclePakBackupManifest manifest)
        {
            string manifestPath = GetManifestPath(ams2InstallFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath, VehiclePakBackupManifestSerializer.Serialize(manifest));
        }

        private static string GetManifestPath(string ams2InstallFolder) =>
            Path.Combine(StoragePaths.VehiclePakBackupsFolder, InstallHash(ams2InstallFolder), "manifest.json");

        private static string GetBackupFilesFolder(string ams2InstallFolder) =>
            Path.Combine(StoragePaths.VehiclePakBackupsFolder, InstallHash(ams2InstallFolder), "files");

        private static string InstallHash(string ams2InstallFolder)
        {
            string normalized = ams2InstallFolder.Trim().TrimEnd('\\', '/').ToLowerInvariant();
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(hash)[..16];
        }

        private static string SanitizeFileName(string relativePath) =>
            string.Concat(relativePath.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '\\' or '/' ? '_' : c));

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
