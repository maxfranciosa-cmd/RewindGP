using Ams2ChEd.Business.AMS2.PakPatching.Contracts;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Given how many driver entries each model needs for one race, raises any overflowing
    /// model's *effective* capacity (a copy of the declared car_model_capacities.json values,
    /// never the file itself) by asking the slot patcher to expand that model's own AMS2 game
    /// files. A model the patcher can't help (install/pak not found, unrecognized format, patch
    /// failure) is left at its declared capacity, so SlotCapacityAllocator's existing cross-model
    /// redirection remains the fallback for it - see Services\Ams2LiveryService.cs.
    /// Kept as a small, pure(-ish) class separate from Ams2LiveryService so the decision logic is
    /// unit-testable without needing a real season/race file-generation pipeline.
    /// </summary>
    public static class EffectiveCapacityResolver
    {
        public static List<(string Model, int Slots)> Resolve(
            IReadOnlyDictionary<string, int> requiredCountsByModel,
            IReadOnlyList<(string Model, int Slots)> declaredCapacities,
            IVehicleLiverySlotPatcher slotPatcher,
            string ams2RootDirectory,
            Action<string> logWarning)
        {
            var effectiveCapacities = declaredCapacities?.ToList();

            if (slotPatcher == null || effectiveCapacities == null || effectiveCapacities.Count == 0)
                return effectiveCapacities;

            foreach (var (model, required) in requiredCountsByModel)
            {
                int declaredIdx = effectiveCapacities.FindIndex(m => m.Model == model);
                if (declaredIdx < 0 || required <= effectiveCapacities[declaredIdx].Slots)
                    continue; // no overflow for this model this race - nothing to patch

                // Any other model declared for this class/season is a candidate to borrow a
                // working texture from for the new slot(s) - same priority order
                // SlotCapacityAllocator already uses for redirection, so texture-reuse and
                // overflow-redirection preferences stay consistent with each other.
                var siblingModels = effectiveCapacities
                    .Where(m => m.Model != model)
                    .Select(m => m.Model)
                    .ToList();

                var outcome = slotPatcher.EnsureSlots(ams2RootDirectory, model, required, siblingModels);
                if (outcome.Status is SlotPatchStatus.Patched or SlotPatchStatus.AlreadySufficient)
                {
                    effectiveCapacities[declaredIdx] = (model, Math.Max(effectiveCapacities[declaredIdx].Slots, required));
                }
                else
                {
                    logWarning?.Invoke(
                        $"Warning: could not expand livery slots for model '{model}' ({outcome.Status}" +
                        $"{(string.IsNullOrEmpty(outcome.Message) ? "" : $": {outcome.Message}")}) - falling back to cross-model redirection.");
                }
            }

            return effectiveCapacities;
        }
    }
}
