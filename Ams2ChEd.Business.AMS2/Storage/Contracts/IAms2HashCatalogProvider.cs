namespace AMS2ChEd.Business.AMS2.Storage.Contracts
{
    /// <summary>
    /// Provides the name -> internal-AMS2-hash dictionaries that Ams2Interop.Ams2RaceConfigurator's
    /// constructor requires to resolve car/track selections. Ships empty (see
    /// Tracks/ams2_track_hashes.json, CarModels/ams2_car_hashes.json) - this data is sourced/
    /// authored outside of Rewind GP's own codebase and populated separately.
    /// </summary>
    public interface IAms2HashCatalogProvider
    {
        /// <summary>Keyed by track display name/slug, e.g. from Ams2Interop's circuits_ref.psv source.</summary>
        IReadOnlyDictionary<string, int> TrackHashes { get; }

        /// <summary>
        /// Keyed by the exact same string as Ams2TeamEntry.Ams2Car (the AMS2 vehicle model
        /// folder/id already used to write livery files) - not a separate cosmetic display name.
        /// </summary>
        IReadOnlyDictionary<string, int> CarHashes { get; }
    }
}
