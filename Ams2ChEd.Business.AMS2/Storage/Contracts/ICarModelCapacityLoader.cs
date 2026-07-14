namespace AMS2ChEd.Business.AMS2.Storage.Contracts
{
    public interface ICarModelCapacityLoader
    {
        /// <summary>
        /// Returns the ordered (model, slots) list as declared in the registry for this class,
        /// or null if the class has no entry (caller must treat null/empty as "uncapped").
        /// </summary>
        IReadOnlyList<(string Model, int Slots)> GetModelsForClass(string ams2Class);
    }
}
