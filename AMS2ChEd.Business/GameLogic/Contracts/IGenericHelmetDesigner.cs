namespace AMS2ChEd.Business.GameLogic.Contracts
{
    /// <summary>
    /// Optional developer tool: a window to try colours on the generic helmet designs and copy
    /// the resulting generic_helmet json. Resolved via GetService (nullable); a game without
    /// generic helmets simply doesn't register one.
    /// </summary>
    public interface IGenericHelmetDesigner
    {
        /// <summary><paramref name="ownerWindow"/> is an opaque owner handle, as for <see cref="IPlayerCosmeticsEditor.ShowEditor"/>.</summary>
        void ShowDesigner(object ownerWindow);
    }
}
