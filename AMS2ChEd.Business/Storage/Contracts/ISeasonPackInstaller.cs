namespace AMS2ChEd.Business.Storage.Contracts
{
    public interface ISeasonPackInstaller
    {
        /// <summary>File extension of this game's season/mod pack format, including the leading dot (e.g. ".rwgp").</summary>
        string PackFileExtension { get; }

        /// <summary>Human-readable label for file-picker filters (e.g. "Rewind GP Season Pack").</summary>
        string PackFileFilterLabel { get; }

        SeasonModInstallResult InstallSeasonMod(string packFilePath);
    }
}
