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
    /// Confirmed against a real install: every new slot's NEWTEXTURE can simply point at the SAME
    /// texture entry an existing slot's plain-TEXTURE CONDITION already reuses - no genuinely new,
    /// distinct pak entry is needed for it to render correctly in-game. This was verified in-game
    /// after an earlier assumption (that a reused reference wouldn't render) turned out to be
    /// confounded by an unrelated bug (the _hr.rcf naming issue - see GetCandidateRcfPaths). That
    /// makes this the only texture-provisioning path: no _Livery.bff pak is read or written at
    /// all, so this class only ever touches the .rcf-bearing paks (base/_LD/_HD + persistent).
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

            // ---- How many new slots (and which LIVERY ids) are needed, based on the first
            // target's declared count - every pak copy is expected to agree on this. ----
            string firstRcfXml;
            try
            {
                (firstRcfXml, _) = DecodeRcfEntry(targets[0].Snapshot, targets[0].RcfEntries[0]);
            }
            catch (Exception ex)
            {
                return Unrecognized($"Could not decode .rcf entry in {targets[0].AbsolutePath}: {ex.Message}");
            }

            int currentSlotCount = RcfLiverySlotPatcher.PeekSlotCount(firstRcfXml);
            if (currentSlotCount >= requiredSlotCount)
                return new SlotPatchOutcome { Status = SlotPatchStatus.AlreadySufficient };

            var newIds = Enumerable.Range(
                Ams2LiveryConventions.BaseLiveryNumber + currentSlotCount,
                requiredSlotCount - currentSlotCount).ToList();

            // ---- Every new slot's NEWTEXTURE points at the same template texture an existing
            // plain-TEXTURE CONDITION already references - confirmed in-game to render correctly,
            // so no _Livery.bff pak is read, written, or even needs to exist. ----
            string? templatePath = RcfLiverySlotPatcher.TryGetReusableTexturePath(firstRcfXml);
            if (templatePath == null)
                return Unrecognized($"No plain-TEXTURE CONDITION found in {targets[0].AbsolutePath} to base a new texture name/content on.");

            var newTexturePathsByLiveryId = new Dictionary<int, string>();
            foreach (int id in newIds)
                newTexturePathsByLiveryId[id] = templatePath;

            // ---- Decide what actually needs patching in the .rcf-bearing paks (idempotency per
            // entry, unchanged from before) - now repointing each new slot's NEWTEXTURE at the
            // path chosen above instead of leaving it as the cloned template's own value. ----
            var changesByPak = new Dictionary<TargetPak, Dictionary<int, byte[]>>();
            bool anyChange = false;

            foreach (var target in targets)
            {
                var changedEntries = new Dictionary<int, byte[]>();

                foreach (var entry in target.RcfEntries)
                {
                    string rcfXml;
                    bool hasUtf8Bom;
                    try
                    {
                        (rcfXml, hasUtf8Bom) = DecodeRcfEntry(target.Snapshot, entry);
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
                            rcfXml, requiredSlotCount, Ams2LiveryConventions.BaseLiveryNumber,
                            id => newTexturePathsByLiveryId.TryGetValue(id, out var path)
                                ? path
                                : throw new InvalidDataException($"No planned texture path for new LIVERY id {id} ({target.AbsolutePath} may be out of sync with {targets[0].AbsolutePath})."),
                            out patchedXml, out _);
                    }
                    catch (Exception ex)
                    {
                        return Unrecognized($"Could not parse .rcf XML in {target.AbsolutePath}: {ex.Message}");
                    }

                    if (changed)
                    {
                        byte[] patchedBytes = Encoding.UTF8.GetBytes(patchedXml);
                        if (hasUtf8Bom)
                            patchedBytes = Encoding.UTF8.GetPreamble().Concat(patchedBytes).ToArray();
                        changedEntries[entry.Index] = patchedBytes;
                        anyChange = true;
                    }
                }

                changesByPak[target] = changedEntries;
            }

            if (!anyChange)
                return new SlotPatchOutcome { Status = SlotPatchStatus.AlreadySufficient };

            // ---- Backup every target pak up front, changed or not - a currently-untouched
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

        /// <summary>
        /// Confirmed against a real install: the high-res variant is named "{carModel}_hr.rcf"
        /// (suffix before the extension), not "{carModel}.rcf_hr" (a distinct "rcf_hr" extension)
        /// as this method assumed until now - that wrong candidate never matched anything, so the
        /// _hr variant silently went unpatched in every single patch attempt (found via decoding
        /// the pak's own ext-info to see the *actual* stored name, not just trying candidates - see
        /// AMS2-livery-modding-knowledge.md). The main .rcf was always patched correctly, which is
        /// exactly why -showLiveryIDs showed the new slot as valid while it still rendered empty:
        /// engine-relevant _hr resolution kept using the still-6-slot original.
        /// </summary>
        private static List<string> GetCandidateRcfPaths(string carModel)
        {
            string pakModel = PakModelNameExceptions.Resolve(carModel);
            string pakModelFolder = PakModelNameExceptions.ResolveRcfFolder(carModel);
            return new()
            {
                Path.Combine("vehicles", pakModelFolder, $"{pakModel}.rcf"),
                Path.Combine("vehicles", pakModelFolder, $"{pakModel}_hr.rcf"),
            };
        }

        /// <summary>
        /// Real .rcf entries are UTF-8-with-BOM (confirmed against a real install) - XDocument.Parse
        /// (a string overload, unlike Load(Stream)) treats a leading BOM character as invalid
        /// content, so it must be stripped here and restored on write-back to keep the entry
        /// byte-faithful to what AMS2 shipped.
        /// </summary>
        private static (string RcfXml, bool HasUtf8Bom) DecodeRcfEntry(BffPakSnapshot snapshot, BffTocEntry entry)
        {
            byte[] plaintext = BffEntryExtractor.ExtractPlaintext(snapshot, entry);
            var utf8Bom = Encoding.UTF8.GetPreamble();
            bool hasUtf8Bom = plaintext.Length >= utf8Bom.Length && plaintext.AsSpan(0, utf8Bom.Length).SequenceEqual(utf8Bom);
            string rcfXml = hasUtf8Bom
                ? Encoding.UTF8.GetString(plaintext, utf8Bom.Length, plaintext.Length - utf8Bom.Length)
                : Encoding.UTF8.GetString(plaintext);
            return (rcfXml, hasUtf8Bom);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }
}
