namespace AMS2ChEd.Business.Updater
{
    /// <summary>
    /// Brings a community skin pack straight into the app for a season the app ships the data of:
    /// the player downloads the pack and hands over the archive, no season pack needed. The
    /// game module knows which files of which pack each season uses.
    /// </summary>
    public interface ISkinPackImporter
    {
        /// <summary>Whether the app knows a skin pack for the season.</summary>
        bool HasSkinPack(int seasonYear);

        /// <summary>The page the season's skin pack is downloaded from; null when there is none.</summary>
        string SkinPackUrl(int seasonYear);

        /// <summary>The name of the season's skin pack; null when there is none.</summary>
        string SkinPackName(int seasonYear);

        /// <summary>Whether the season's skin pack has been imported.</summary>
        bool IsImported(int seasonYear);

        /// <summary>The archive file types the importer reads (".zip", ".rar", ".7z").</summary>
        IReadOnlyList<string> ArchiveExtensions { get; }

        /// <summary>The seasons whose skin pack this archive is, best match first; empty when none.</summary>
        IReadOnlyList<int> DetectSeasons(string archivePath);

        Task<SkinPackImportResult> ImportAsync(int seasonYear, string archivePath, IProgress<string> progress = null, CancellationToken cancellationToken = default);

        /// <summary>Removes what an import wrote for the season.</summary>
        void Remove(int seasonYear);
    }

    public class SkinPackImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int FilesCopied { get; set; }
        public int FilesMissing { get; set; }

        /// <summary>Per team, what the helmet and visor sponsor layers came to.</summary>
        public List<string> Notes { get; set; } = new();
    }
}
