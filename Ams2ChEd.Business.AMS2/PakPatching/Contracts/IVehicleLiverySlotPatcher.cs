namespace Ams2ChEd.Business.AMS2.PakPatching.Contracts
{
    public enum SlotPatchStatus
    {
        /// <summary>The model's .rcf files already declare at least the required slot count - nothing was written.</summary>
        AlreadySufficient,

        /// <summary>One or more pak files were backed up and repacked with a higher slot count.</summary>
        Patched,

        /// <summary>The given AMS2 install folder doesn't exist.</summary>
        SkippedInstallNotFound,

        /// <summary>None of the model's candidate pak files exist on disk.</summary>
        SkippedPakNotFound,

        /// <summary>A target pak couldn't be parsed, or didn't contain the .rcf entry this model expects (see AMS2-livery-modding-knowledge.md - every candidate pak/entry combination is verified before any file is written).</summary>
        SkippedUnrecognizedFormat,

        /// <summary>Patching was attempted but failed (disk/lock error, partial-commit rollback, etc). Any files already committed in this attempt were rolled back to their prior state.</summary>
        Failed,
    }

    public sealed class SlotPatchOutcome
    {
        public required SlotPatchStatus Status { get; init; }
        public string? Message { get; init; }
    }

    public sealed class RestoreResult
    {
        public required bool Success { get; init; }
        public required int FilesRestored { get; init; }
        public string? Message { get; init; }
    }

    /// <summary>
    /// Ensures an AMS2 car model's .bff pak files declare at least a given number of livery
    /// slots, patching them in place (backing up originals first) if they currently declare
    /// fewer. Shared by race prep (Services\Ams2LiveryService.cs, called whenever a race needs
    /// more slots than a model currently has) and the Options-window "restore original vehicle
    /// files" action.
    /// </summary>
    public interface IVehicleLiverySlotPatcher
    {
        /// <param name="siblingModelsForTextureReuse">
        /// Other models (in priority order) whose existing livery textures can be reused to
        /// provision any new slot's texture entry - a new slot's NEWTEXTURE must point at a
        /// genuinely new, distinct pak entry to render in-game, not a reused reference to an
        /// existing slot's own texture. Pass an empty list if no reuse source is available; new
        /// slots will still be declared but may not have a working texture.
        /// </param>
        SlotPatchOutcome EnsureSlots(string ams2InstallFolder, string carModel, int requiredSlotCount, IReadOnlyList<string> siblingModelsForTextureReuse);

        bool HasBackups(string ams2InstallFolder);

        RestoreResult RestoreAll(string ams2InstallFolder);
    }
}
