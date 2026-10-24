using AMS2ChEd.Business.AMS2.Models;

namespace AMS2ChEd.Business.AMS2.Storage.Contracts
{
    /// <summary>The template kits, number styles and tyre sets that ship with the app.</summary>
    public interface ILiveryKitLoader
    {
        /// <summary>The kit listing this Ams2Car, or null when no kit fits it.</summary>
        LiveryKit FindKit(string ams2Car);

        /// <summary>The outfit set by name, or null.</summary>
        OutfitSet FindOutfitSet(string name);

        /// <summary>The number style by name, or null.</summary>
        NumberStyle FindNumberStyle(string name);

        /// <summary>Full path of a number style's master strip, or null.</summary>
        string NumberStylePath(string name);

        /// <summary>
        /// The tyre set by id: AMS2 texture name (TIREDIFF, TIRESPEC, ...) to the full path of its
        /// file. Null when the library has no such set.
        /// </summary>
        IReadOnlyDictionary<string, string> FindTyreSet(string id);
    }
}
