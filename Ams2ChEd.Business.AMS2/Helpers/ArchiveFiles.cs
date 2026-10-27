using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>The zip / rar / 7z archives skin packs come in.</summary>
    public static class ArchiveFiles
    {
        public static readonly string[] Extensions = { ".zip", ".rar", ".7z" };

        public static bool IsArchive(string path) =>
            Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        /// <summary>Every file in the archive, as a relative path with forward slashes, without extracting anything.</summary>
        public static List<string> ListFiles(string archivePath)
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath);
            return archive.Entries.Where(e => !e.IsDirectory && e.Key != null).Select(e => Normalize(e.Key)).ToList();
        }

        public static void ExtractAll(string archivePath, string destinationDir)
        {
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, destinationDir, overwriteFiles: true);
                return;
            }

            var options = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };
            using var archive = ArchiveFactory.OpenArchive(archivePath);

            // Solid archives (the usual 7z, solid rar) need one sequential pass: extracting entry
            // by entry restarts decompression from the start of the block for every file, which
            // turns a minute's work into most of an hour on a large livery pack. SharpCompress only
            // allows that pass on solid or 7z archives, so anything else goes entry by entry.
            if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
            {
                using var reader = archive.ExtractAllEntries();
                reader.WriteAllToDirectory(destinationDir, options);
            }
            else
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                    entry.WriteToDirectory(destinationDir, options);
            }
        }

        public static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

        /// <summary>
        /// Finds a file named by a path the archive was authored with, among the archive's files:
        /// the exact path, else the one ending the same way once the top folder is dropped (an
        /// archive re-packed under another name, or a different version's folder).
        /// </summary>
        public static string Find(IReadOnlyCollection<string> files, string wanted)
        {
            wanted = Normalize(wanted);
            var exact = files.FirstOrDefault(f => string.Equals(f, wanted, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            int slash = wanted.IndexOf('/');
            if (slash < 0)
                return files.FirstOrDefault(f => f.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase));

            var tail = wanted[slash..];
            return files.FirstOrDefault(f => f.EndsWith(tail, StringComparison.OrdinalIgnoreCase));
        }
    }
}
