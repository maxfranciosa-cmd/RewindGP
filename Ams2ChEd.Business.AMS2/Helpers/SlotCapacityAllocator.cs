namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// Assigns items requesting a named "model" slot into a capacity-limited set of models
    /// belonging to the same class, deferring anything that doesn't fit its own model to
    /// whatever free slots remain on sibling models, in three deterministic passes:
    ///
    ///   1. Fit each model's own items up to its declared cap (uncapped if undeclared).
    ///   2. Collect the leftover (overflow) items in their original global order.
    ///   3. Walk the class's models in declared order, filling remaining free slots from
    ///      the front of the overflow queue.
    ///
    /// Kept generic/pure (no AMS2 domain types) so it's a simple function to unit test.
    /// </summary>
    public static class SlotCapacityAllocator
    {
        public class Item<TPayload>
        {
            public string Model { get; init; }
            public int SequenceIndex { get; init; }
            public TPayload Payload { get; init; }
        }

        public class Result<TPayload>
        {
            public Dictionary<string, List<Item<TPayload>>> AssignedByModel { get; init; }
            public List<Item<TPayload>> Unplaceable { get; init; }
        }

        public static Result<TPayload> Allocate<TPayload>(
            IReadOnlyList<Item<TPayload>> items,
            IReadOnlyList<(string Model, int Slots)> modelsInDeclaredOrder)
        {
            var capacityByModel = modelsInDeclaredOrder.ToDictionary(m => m.Model, m => m.Slots);
            var assigned = new Dictionary<string, List<Item<TPayload>>>();
            var overflowQueue = new List<Item<TPayload>>();

            // Pass 1: fit each model's own items up to its declared cap (uncapped if not declared).
            foreach (var group in items.GroupBy(i => i.Model))
            {
                var ordered = group.OrderBy(i => i.SequenceIndex).ToList();

                if (!capacityByModel.TryGetValue(group.Key, out int slots))
                {
                    assigned[group.Key] = ordered;
                    continue;
                }

                assigned[group.Key] = ordered.Take(slots).ToList();
                overflowQueue.AddRange(ordered.Skip(slots));
            }

            // Pass 2: restore original global order across all overflow, interleaved across models.
            overflowQueue = overflowQueue.OrderBy(i => i.SequenceIndex).ToList();

            // Pass 3: walk declared models in declared order, filling remaining free slots.
            foreach (var (model, slots) in modelsInDeclaredOrder)
            {
                if (overflowQueue.Count == 0)
                    break;

                int used = assigned.TryGetValue(model, out var already) ? already.Count : 0;
                int free = slots - used;
                if (free <= 0)
                    continue;

                if (!assigned.TryGetValue(model, out var list))
                    assigned[model] = list = new List<Item<TPayload>>();

                int take = Math.Min(free, overflowQueue.Count);
                list.AddRange(overflowQueue.Take(take));
                overflowQueue.RemoveRange(0, take);
            }

            return new Result<TPayload> { AssignedByModel = assigned, Unplaceable = overflowQueue };
        }
    }
}
