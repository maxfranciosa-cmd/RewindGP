using System.Text;
using Ams2ChEd.Business.AMS2.PakPatching.Contracts;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Ensures a car model's .bff pak files declare enough livery slots, patching whichever of
    /// its base/_LD/_HD paks exist plus the global vehiclespersistent.bff in lockstep - see
    /// AMS2-livery-modding-knowledge.md. A model's pak set is never left half-patched: everything
    /// is read and validated in memory before any file is touched, originals are backed up before
    /// the first write, and a failure partway through committing rolls back whatever was already
    /// committed in that attempt.
    ///
    /// Note: this assumes the _LD/_HD variant paks use the same internal relative path
    /// (vehicles\{model}\{model}.rcf[_hr]) as the base pak - unverified against a real AMS2
    /// install (no install was available while writing this). If that assumption is wrong for a
    /// given car, this fails closed (SkippedUnrecognizedFormat, zero files touched) rather than
    /// silently patching the wrong thing - see the class's real-install validation notes in the
    /// project's implementation plan.
    /// </summary>
    public class Ams2VehicleLiverySlotPatcher : IVehicleLiverySlotPatcher
    {
        private readonly VehiclePakBackupManifestStore _backupStore;

        public Ams2VehicleLiverySlotPatcher() : this(new VehiclePakBackupManifestStore())
        {
        }

        public Ams2VehicleLiverySlotPatcher(VehiclePakBackupManifestStore backupStore)
        {
            _backupStore = backupStore;
        }

        private sealed class TargetPak
        {
            public required string AbsolutePath { get; init; }
            public required BffPakSnapshot Snapshot { get; init; }
            public required List<BffTocEntry> RcfEntries { get; init; }
        }

        public SlotPatchOutcome EnsureSlots(string ams2InstallFolder, string carModel, int requiredSlotCount)
        {
            try
            {
                return EnsureSlotsCore(ams2InstallFolder, carModel, requiredSlotCount);
            }
            catch (Exception ex)
            {
                return new SlotPatchOutcome { Status = SlotPatchStatus.Failed, Message = ex.Message };
            }
        }

        private SlotPatchOutcome EnsureSlotsCore(string ams2InstallFolder, string carModel, int requiredSlotCount)
        {
            if (!Directory.Exists(ams2InstallFolder))
                return new SlotPatchOutcome { Status = SlotPatchStatus.SkippedInstallNotFound };

            var perCarPakPaths = PakPathResolver.GetPerCarPakPaths(ams2InstallFolder, carModel, File.Exists);
            if (perCarPakPaths.Count == 0)
                return new SlotPatchOutcome { Status = SlotPatchStatus.SkippedPakNotFound };

            string persistentPakPath = PakPathResolver.GetPersistentPakPath(ams2InstallFolder);
            if (!File.Exists(persistentPakPath))
                return new SlotPatchOutcome { Status = SlotPatchStatus.SkippedPakNotFound };

            var allPakPaths = perCarPakPaths.Append(persistentPakPath).ToList();
            var candidateRcfPaths = GetCandidateRcfPaths(carModel);

            // ---- Pre-flight: read + locate entries in every target pak, fully in memory. ----
            var targets = new List<TargetPak>();
            foreach (var pakPath in allPakPaths)
            {
                BffPakSnapshot snapshot;
                try
                {
                    snapshot = BffPakReader.Read(pakPath);
                }
                catch (Exception ex)
                {
                    return Unrecognized($"Could not read {pakPath}: {ex.Message}");
                }

                var found = candidateRcfPaths
                    .Select(p => BffPakReader.TryFindEntryByPath(snapshot, p))
                    .Where(e => e != null)
                    .Select(e => e!)
                    .ToList();

                if (found.Count == 0)
                    return Unrecognized($"No recognized .rcf entry for model '{carModel}' found in {pakPath}.");

                targets.Add(new TargetPak { AbsolutePath = pakPath, Snapshot = snapshot, RcfEntries = found });
            }

            // ---- Decide what actually needs patching (idempotency). ----
            var changesByPak = new Dictionary<TargetPak, Dictionary<int, byte[]>>();
            bool anyChange = false;

            foreach (var target in targets)
            {
                var changedEntries = new Dictionary<int, byte[]>();

                foreach (var entry in target.RcfEntries)
                {
                    byte[] plaintext;
                    string rcfXml;
                    try
                    {
                        plaintext = BffEntryExtractor.ExtractPlaintext(target.Snapshot, entry);
                        rcfXml = Encoding.UTF8.GetString(plaintext);
                    }
                    catch (Exception ex)
                    {
                        return Unrecognized($"Could not decode .rcf entry in {target.AbsolutePath}: {ex.Message}");
                    }

                    bool changed;
                    string patchedXml;
                    try
                    {
                        changed = RcfLiverySlotPatcher.TryEnsureSlotCount(
                            rcfXml, requiredSlotCount, Ams2LiveryConventions.BaseLiveryNumber, out patchedXml, out _);
                    }
                    catch (Exception ex)
                    {
                        return Unrecognized($"Could not parse .rcf XML in {target.AbsolutePath}: {ex.Message}");
                    }

                    if (changed)
                    {
                        changedEntries[entry.Index] = Encoding.UTF8.GetBytes(patchedXml);
                        anyChange = true;
                    }
                }

                changesByPak[target] = changedEntries;
            }

            if (!anyChange)
                return new SlotPatchOutcome { Status = SlotPatchStatus.AlreadySufficient };

            // ---- Backup every target pak up front (changed or not - a currently-untouched
            // variant might need patching next race, and we want its original saved now). ----
            foreach (var pakPath in allPakPaths)
                _backupStore.EnsureBackedUp(ams2InstallFolder, pakPath);

            // ---- Repack each changed pak to a temp file, validating before commit. ----
            var tempFilesByOriginal = new Dictionary<string, string>();
            try
            {
                foreach (var (target, changes) in changesByPak)
                {
                    if (changes.Count == 0)
                        continue;

                    string tempPath = target.AbsolutePath + ".rewgp_tmp";
                    BffPakRepacker.PatchEntries(target.Snapshot, changes, tempPath);
                    BffPakReader.Read(tempPath); // cheap correctness gate: must parse cleanly

                    tempFilesByOriginal[target.AbsolutePath] = tempPath;
                }
            }
            catch (Exception ex)
            {
                foreach (var tempPath in tempFilesByOriginal.Values)
                    TryDelete(tempPath);
                return new SlotPatchOutcome { Status = SlotPatchStatus.Failed, Message = $"Failed to repack: {ex.Message}" };
            }

            // ---- Commit: swap each original for its validated temp file. Roll back everything
            // already committed in this pass if a later swap fails partway. ----
            var committed = new List<string>();
            try
            {
                foreach (var (originalPath, tempPath) in tempFilesByOriginal)
                {
                    File.Replace(tempPath, originalPath, destinationBackupFileName: null);
                    committed.Add(originalPath);
                }
            }
            catch (Exception ex)
            {
                foreach (var originalPath in committed)
                {
                    string? backupPath = _backupStore.TryGetBackupPath(ams2InstallFolder, originalPath);
                    if (backupPath != null && File.Exists(backupPath))
                        File.Copy(backupPath, originalPath, overwrite: true);
                }
                foreach (var tempPath in tempFilesByOriginal.Values)
                    TryDelete(tempPath);

                return new SlotPatchOutcome
                {
                    Status = SlotPatchStatus.Failed,
                    Message = $"Failed to commit patched pak(s), rolled back already-committed files: {ex.Message}",
                };
            }

            return new SlotPatchOutcome { Status = SlotPatchStatus.Patched };
        }

        public bool HasBackups(string ams2InstallFolder) => _backupStore.HasAnyBackup(ams2InstallFolder);

        public RestoreResult RestoreAll(string ams2InstallFolder)
        {
            try
            {
                var (restored, missing) = _backupStore.RestoreAll(ams2InstallFolder);
                if (restored == 0 && missing == 0)
                    return new RestoreResult { Success = true, FilesRestored = 0, Message = "Nothing to restore." };

                return new RestoreResult
                {
                    Success = missing == 0,
                    FilesRestored = restored,
                    Message = missing > 0 ? $"{missing} backup file(s) were missing and could not be restored." : null,
                };
            }
            catch (Exception ex)
            {
                return new RestoreResult { Success = false, FilesRestored = 0, Message = ex.Message };
            }
        }

        private static SlotPatchOutcome Unrecognized(string message) =>
            new() { Status = SlotPatchStatus.SkippedUnrecognizedFormat, Message = message };

        private static List<string> GetCandidateRcfPaths(string carModel) => new()
        {
            Path.Combine("vehicles", carModel, $"{carModel}.rcf"),
            Path.Combine("vehicles", carModel, $"{carModel}.rcf_hr"),
        };

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }
}
