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
    /// Confirmed against a real install: bumping OPTIONS/NAME/CONDITION alone is not enough for a
    /// new slot to actually render - its NEWTEXTURE must point at a texture entry that genuinely
    /// exists in the model's own _Livery.bff/_HD_Livery.bff/_LD_Livery.bff pak(s), not just reuse
    /// an existing slot's reference. This class provisions that entry per new slot, preferring (in
    /// order): a genuine spare texture already in the model's own pak (FindSpareTextures), then a
    /// newly-inserted entry whose content is duplicated from the SAME model's own existing texture
    /// and whose path stays inside that model's own real, already-shipped texture folder (e.g.
    /// "...\Formula_Hitech_g1m3\f_hitech_g1m3_livery09_1.dds", not a brand-new folder), and only as
    /// a defensive last resort a sibling model's texture (siblingModelsForTextureReuse). Injected
    /// via BffPakEntryInserter.
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
        /// <summary>
        /// TEMPORARY TEST SWITCH - see the doc comment where this is used in EnsureSlotsCore.
        /// When true, every new slot reuses the existing template texture directly (no
        /// _Livery.bff insertion at all); when false (the prior default), spares are preferred
        /// and insertion is the fallback. Flip back to false once the in-game test result is in.
        /// </summary>
        private const bool TestReuseExistingLiveryTextureDirectly = true;

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

        public SlotPatchOutcome EnsureSlots(string ams2InstallFolder, string carModel, int requiredSlotCount, IReadOnlyList<string> siblingModelsForTextureReuse)
        {
            try
            {
                return EnsureSlotsCore(ams2InstallFolder, carModel, requiredSlotCount, siblingModelsForTextureReuse);
            }
            catch (Exception ex)
            {
                return new SlotPatchOutcome { Status = SlotPatchStatus.Failed, Message = ex.Message };
            }
        }

        private SlotPatchOutcome EnsureSlotsCore(string ams2InstallFolder, string carModel, int requiredSlotCount, IReadOnlyList<string> siblingModelsForTextureReuse)
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

            // ---- Resolve this model's livery-texture paks and pick one fresh, non-colliding
            // relative texture path per new slot, probed against the model's own base livery
            // pak's TOC (grounded in what's actually in the pak, not parsed from .rcf text). ----
            var liveryPakPaths = PakPathResolver.GetLiveryPakPaths(ams2InstallFolder, carModel, File.Exists);
            if (liveryPakPaths.Count == 0)
                return Unrecognized($"No _Livery.bff pak found for model '{carModel}' - cannot provision new slot textures.");

            var liveryPakSnapshots = new List<BffPakSnapshot>(liveryPakPaths.Count);
            foreach (var liveryPakPath in liveryPakPaths)
            {
                try
                {
                    liveryPakSnapshots.Add(BffPakReader.Read(liveryPakPath));
                }
                catch (Exception ex)
                {
                    return Unrecognized($"Could not read {liveryPakPath}: {ex.Message}");
                }
            }
            BffPakSnapshot baseLiverySnapshot = liveryPakSnapshots[0];

            string? templatePath = RcfLiverySlotPatcher.TryGetReusableTexturePath(firstRcfXml);
            if (templatePath == null)
                return Unrecognized($"No plain-TEXTURE CONDITION found in {targets[0].AbsolutePath} to base a new texture name/content on.");

            var newTexturePathsByLiveryId = new Dictionary<int, string>();
            var idsNeedingInsertion = new List<int>();

            if (TestReuseExistingLiveryTextureDirectly)
            {
                // ---- TEMPORARY TEST SWITCH: point every new slot's NEWTEXTURE straight at the
                // existing, already-referenced template texture, with no _Livery.bff insertion or
                // spare-searching at all. Now that the real root cause of "renders empty" turned
                // out to be the _hr.rcf naming bug (every _Livery.bff-side fix - ext-info,
                // UnknownFlag, mSectionInfoPos, folder convention - was validated in isolation
                // while _hr.rcf silently stayed unpatched), the original "duplicate NEWTEXTURE
                // across slots" approach may have been unfairly ruled out: it was tested before
                // _hr.rcf was known to be broken. If this test confirms in-game that reusing an
                // existing texture directly renders correctly, the entire insertion machinery
                // below (BffPakEntryInserter/BffExtInfoCodec/ScribeCipher/spare-finding) becomes
                // unnecessary complexity for the common case - see AMS2-livery-modding-knowledge.md.
                // Intentionally left as a switch rather than deleting the insertion path, since
                // this hasn't been confirmed yet and a car that's fully out of distinct textures to
                // reuse would still need it.
                foreach (int id in newIds)
                    newTexturePathsByLiveryId[id] = templatePath;
            }
            else
            {
                // ---- Prefer genuine spare textures: files Reiza already shipped inside this
                // model's own _Livery.bff pak(s) but that no CONDITION currently references. Only
                // the slots left over once spares run out fall back to inserting a genuinely new
                // pak entry - see AMS2-livery-modding-knowledge.md. ----
                var spareTextures = FindSpareTextures(liveryPakSnapshots, firstRcfXml, newIds.Count);
                for (int i = 0; i < newIds.Count; i++)
                {
                    if (i < spareTextures.Count)
                        newTexturePathsByLiveryId[newIds[i]] = spareTextures[i];
                    else
                        idsNeedingInsertion.Add(newIds[i]);
                }

                // ---- New texture paths for slots still needing insertion: stay inside the
                // model's OWN, already-shipped texture folder (e.g.
                // "vehicles\textures\Formula_Hitech_g1m3\"), same prefix/case/numbering convention
                // as its existing CONDITIONs (e.g. "f_hitech_g1m3_livery09_1.dds") instead of
                // inventing a brand-new folder the engine has never indexed. ----
                if (!TryParseLiveryTexturePattern(templatePath, out string prefix, out string originalDigits, out string extension))
                    return Unrecognized($"Could not parse a numbered texture filename out of '{templatePath}'.");

                int suffixCounter = 1;
                foreach (int id in idsNeedingInsertion)
                {
                    string candidate;
                    do
                    {
                        candidate = $"{prefix}{originalDigits}_{suffixCounter}{extension}";
                        suffixCounter++;
                    } while (BffPakReader.TryFindEntryByPath(baseLiverySnapshot, candidate) != null);
                    newTexturePathsByLiveryId[id] = candidate;
                }
            }

            // ---- Source texture bytes for each livery pak variant from the SAME model's own
            // template texture (the one templatePath points at) - only needed for slots that
            // didn't get a spare above. Falls back to a sibling model's texture (the old approach)
            // only if this model's own variant is somehow unreadable, as a defensive last resort -
            // see AMS2-livery-modding-knowledge.md. A variant with no usable source texture is
            // simply left untouched - base should essentially always succeed; HD/LD degrade
            // gracefully rather than failing the whole operation. ----
            var sourceBytesByLiveryPak = new Dictionary<string, byte[]>();
            if (idsNeedingInsertion.Count > 0)
            {
                for (int i = 0; i < liveryPakPaths.Count; i++)
                {
                    string liveryPakPath = liveryPakPaths[i];
                    byte[]? bytes = TryGetOwnModelTexture(liveryPakSnapshots[i], templatePath);

                    if (bytes == null)
                    {
                        string variantSuffix = ExtractVariantSuffix(liveryPakPath);
                        foreach (var sibling in siblingModelsForTextureReuse)
                        {
                            bytes = TryGetSiblingTexture(ams2InstallFolder, sibling, variantSuffix);
                            if (bytes != null) break;
                        }
                    }

                    if (bytes != null)
                        sourceBytesByLiveryPak[liveryPakPath] = bytes;
                }
            }

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

            // ---- Backup every target pak up front (rcf-bearing + livery, changed or not - a
            // currently-untouched variant might need patching next race, and we want its original
            // saved now). ----
            foreach (var pakPath in allPakPaths.Concat(liveryPakPaths))
                _backupStore.EnsureBackedUp(ams2InstallFolder, pakPath);

            // ---- Repack each changed pak to a temp file, validating before commit. Livery paks
            // chain one BffPakEntryInserter.AddEntry call per new slot through successive temp
            // files, since each call only appends a single entry. ----
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

                foreach (var liveryPakPath in liveryPakPaths)
                {
                    if (idsNeedingInsertion.Count == 0)
                        continue; // every new slot was covered by a genuine spare texture - livery pak itself is untouched

                    if (!sourceBytesByLiveryPak.TryGetValue(liveryPakPath, out byte[]? sourceBytes))
                        continue; // no sibling had a usable texture for this variant - leave it untouched

                    var snapshot = liveryPakPath == liveryPakPaths[0] ? baseLiverySnapshot : BffPakReader.Read(liveryPakPath);
                    string workingPath = liveryPakPath;

                    foreach (int id in idsNeedingInsertion)
                    {
                        // DIAGNOSTIC: every prior attempt duplicated a source texture's bytes
                        // EXACTLY (byte-for-byte identical to an already-loaded entry). The one
                        // confirmed-working manual insertion did NOT - its new texture was
                        // distinct from every existing entry, even though same-content
                        // duplication passed every structural/offline check. Testing whether
                        // content-based texture deduplication in the engine is the actual
                        // blocker by perturbing one byte deep in the pixel payload (leaves the
                        // DDS header and format completely intact) - unique per new slot so two
                        // new slots don't collide with each other either. Cosmetically invisible;
                        // not a real fix, just isolating this variable. See
                        // AMS2-livery-modding-knowledge.md.
                        byte[] uniqueBytes = (byte[])sourceBytes.Clone();
                        uniqueBytes[^1] ^= (byte)(0xA5 ^ id);

                        string outputPath = $"{liveryPakPath}.{id}.rewgp_tmp";
                        BffPakEntryInserter.AddEntry(snapshot, newTexturePathsByLiveryId[id], uniqueBytes, compressionType: 1, outputPath);
                        snapshot = BffPakReader.Read(outputPath); // must see this slot's entry before adding the next

                        if (workingPath != liveryPakPath)
                            TryDelete(workingPath); // clean up the prior chain link, now superseded

                        workingPath = outputPath;
                    }

                    tempFilesByOriginal[liveryPakPath] = workingPath;
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
        private static List<string> GetCandidateRcfPaths(string carModel) => new()
        {
            Path.Combine("vehicles", carModel, $"{carModel}.rcf"),
            Path.Combine("vehicles", carModel, $"{carModel}_hr.rcf"),
        };

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

        /// <summary>
        /// Finds texture entries already present inside this model's own livery pak(s) that no
        /// CONDITION currently references - "spares". Cheaper and safer than inserting a new pak
        /// entry when one happens to exist, since it needs zero pak-structure changes at all - but
        /// confirmed against a real install this is NOT guaranteed: formula_hitech_g1m3's true
        /// pristine pak has no spares (every numbered livery texture it ships is already
        /// referenced) - see AMS2-livery-modding-knowledge.md. Requires the candidate to exist in
        /// *every* resolved livery pak variant (base/HD/LD) so a spare is never used for only some
        /// of them.
        /// </summary>
        private static List<string> FindSpareTextures(IReadOnlyList<BffPakSnapshot> liveryPakSnapshots, string rcfXml, int maxNeeded)
        {
            var spares = new List<string>();
            if (maxNeeded == 0 || liveryPakSnapshots.Count == 0)
                return spares;

            string? template = RcfLiverySlotPatcher.TryGetReusableTexturePath(rcfXml);
            if (template == null)
                return spares;

            var match = System.Text.RegularExpressions.Regex.Match(template, @"^(.*?)(\d+)(\.[A-Za-z0-9]+)$");
            if (!match.Success)
                return spares;

            string prefix = match.Groups[1].Value;
            int digitWidth = match.Groups[2].Value.Length;
            string extension = match.Groups[3].Value;

            var used = RcfLiverySlotPatcher.GetUsedTexturePaths(rcfXml);

            for (int n = 1; spares.Count < maxNeeded && n <= 99; n++)
            {
                string candidate = prefix + n.ToString("D" + digitWidth) + extension;
                if (used.Contains(candidate))
                    continue;
                if (liveryPakSnapshots.All(s => BffPakReader.TryFindEntryByPath(s, candidate) != null))
                    spares.Add(candidate);
            }

            return spares;
        }

        private static string ExtractVariantSuffix(string liveryPakPath)
        {
            string fileName = Path.GetFileName(liveryPakPath);
            if (fileName.EndsWith("_HD_Livery.bff", StringComparison.OrdinalIgnoreCase)) return "_HD";
            if (fileName.EndsWith("_LD_Livery.bff", StringComparison.OrdinalIgnoreCase)) return "_LD";
            return "";
        }

        /// <summary>
        /// Extracts a known-working texture's raw plaintext bytes from a sibling model, to reuse
        /// for a new slot on the target model. Fails closed (returns null) on any problem - missing
        /// files, unreadable paks, no plain-TEXTURE slot to borrow from - so the caller can simply
        /// try the next sibling in priority order rather than aborting the whole patch attempt.
        /// </summary>
        private static byte[]? TryGetSiblingTexture(string ams2InstallFolder, string siblingModel, string variantSuffix)
        {
            try
            {
                string vehiclesFolder = Path.Combine(ams2InstallFolder, "Pakfiles", "Vehicles");
                string siblingRcfPakPath = Path.Combine(vehiclesFolder, $"{siblingModel}{variantSuffix}.bff");
                string siblingLiveryPakPath = Path.Combine(vehiclesFolder, $"{siblingModel}{variantSuffix}_Livery.bff");
                if (!File.Exists(siblingRcfPakPath) || !File.Exists(siblingLiveryPakPath))
                    return null;

                var rcfSnapshot = BffPakReader.Read(siblingRcfPakPath);
                var rcfEntry = BffPakReader.TryFindEntryByPath(rcfSnapshot, Path.Combine("vehicles", siblingModel, $"{siblingModel}.rcf"));
                if (rcfEntry == null)
                    return null;

                var (rcfXml, _) = DecodeRcfEntry(rcfSnapshot, rcfEntry);
                string? texturePath = RcfLiverySlotPatcher.TryGetReusableTexturePath(rcfXml);
                if (texturePath == null)
                    return null;

                var librarySnapshot = BffPakReader.Read(siblingLiveryPakPath);
                var textureEntry = BffPakReader.TryFindEntryByPath(librarySnapshot, texturePath);
                if (textureEntry == null)
                    return null;

                return BffEntryExtractor.ExtractPlaintext(librarySnapshot, textureEntry);
            }
            catch
            {
                return null; // fail closed for this sibling - caller tries the next one in priority order
            }
        }

        /// <summary>
        /// Extracts a known-working texture's raw plaintext bytes from THIS SAME model's own
        /// livery pak - the preferred texture source (see the "New texture paths" comment in
        /// EnsureSlotsCore). Fails closed (returns null) if the path isn't in this snapshot, so the
        /// caller can fall back to a sibling model instead of aborting.
        /// </summary>
        private static byte[]? TryGetOwnModelTexture(BffPakSnapshot liverySnapshot, string relativePath)
        {
            try
            {
                var entry = BffPakReader.TryFindEntryByPath(liverySnapshot, relativePath);
                return entry == null ? null : BffEntryExtractor.ExtractPlaintext(liverySnapshot, entry);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Splits a real livery texture path like "vehicles\textures\Formula_Hitech_g1m3\
        /// f_hitech_g1m3_livery09.dds" into a prefix ("...\f_hitech_g1m3_livery"), the ORIGINAL
        /// numeric suffix preserved verbatim ("09" - not reformatted/re-padded, unlike
        /// FindSpareTextures's use of this same shape), and the extension (".dds"). Used to name a
        /// newly-inserted texture as "{prefix}{originalDigits}_{N}{extension}" - staying inside the
        /// model's own real, already-shipped folder instead of inventing a new one.
        /// </summary>
        private static bool TryParseLiveryTexturePattern(string templatePath, out string prefix, out string originalDigits, out string extension)
        {
            var match = System.Text.RegularExpressions.Regex.Match(templatePath, @"^(.*?)(\d+)(\.[A-Za-z0-9]+)$");
            prefix = match.Success ? match.Groups[1].Value : string.Empty;
            originalDigits = match.Success ? match.Groups[2].Value : string.Empty;
            extension = match.Success ? match.Groups[3].Value : string.Empty;
            return match.Success;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }
}
