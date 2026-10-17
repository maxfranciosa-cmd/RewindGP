using Ams2ChEd.Business.AMS2.Services;

namespace AMS2ChEd.Business.AMS2.Storage.Contracts
{
    public interface ICarModelCapacityLoader
    {
        /// <summary>
        /// Returns the ordered (model, slots) list as declared in the registry for this class,
        /// or null if the class has no entry (caller must treat null/empty as "uncapped").
        /// </summary>
        IReadOnlyList<(string Model, int Slots)> GetModelsForClass(string ams2Class);

        /// <summary>
        /// The models of this class that declare their own helmet model (helmet_era), keyed by
        /// model. Models not in the result use the helmet the season's year implies. Never null.
        /// </summary>
        IReadOnlyDictionary<string, HelmetEra> GetHelmetErasForClass(string ams2Class);
    }
}
