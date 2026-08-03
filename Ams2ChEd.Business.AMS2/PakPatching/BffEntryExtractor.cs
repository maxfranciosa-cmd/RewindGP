using System.IO.Compression;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Extracts a single TOC entry's plaintext bytes from a pak's raw file bytes: RC4-decrypt the
    /// on-disk bytes, then zlib-decompress (on-disk entry data is "zlib-compress first, then
    /// RC4-encrypt" - see AMS2-livery-modding-knowledge.md's "Entry data" section, so reading
    /// reverses that: RC4-decrypt first, then zlib-decompress).
    /// </summary>
    public static class BffEntryExtractor
    {
        private const byte CompressionNone = 0;
        private const byte CompressionZLib = 1;

        public static byte[] ExtractPlaintext(BffPakSnapshot snapshot, BffTocEntry entry)
        {
            if (entry.DataOffset < 0 || entry.DataOffset + entry.PakSize > snapshot.FullFileBytes.Length)
                throw new InvalidDataException($"Entry {entry.Index} data range is outside the pak file.");

            byte[] onDiskBytes = snapshot.FullFileBytes.AsSpan((int)entry.DataOffset, (int)entry.PakSize).ToArray();

            if (snapshot.KeyIndex >= 0)
            {
                byte[] key = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[snapshot.KeyIndex]);
                Rc4Cipher.Transform(onDiskBytes, key);
            }

            return entry.CompressionType switch
            {
                CompressionNone => onDiskBytes,
                CompressionZLib => Inflate(onDiskBytes, (int)entry.OriginalSize),
                _ => throw new NotSupportedException(
                    $"Entry {entry.Index} uses unsupported compression type {entry.CompressionType} " +
                    "(only None/ZLib are supported - .rcf entries are always ZLib per the format notes)."),
            };
        }

        private static byte[] Inflate(byte[] zlibBytes, int originalSize)
        {
            using var input = new MemoryStream(zlibBytes);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream(originalSize);
            zlib.CopyTo(output);

            byte[] result = output.ToArray();
            if (result.Length != originalSize)
                throw new InvalidDataException($"Decompressed size {result.Length} did not match expected size {originalSize}.");

            return result;
        }

        /// <summary>
        /// Compresses (if the entry uses ZLib) then RC4-encrypts plaintext, producing the exact
        /// on-disk bytes a new/changed entry needs - the inverse of <see cref="ExtractPlaintext"/>.
        /// Mirrors that method's compression-type branching so an entry's on-disk bytes always
        /// stay consistent with its own <see cref="BffTocEntry.CompressionType"/>.
        /// </summary>
        public static byte[] EncodePlaintext(byte[] plaintext, byte compressionType, BffPakSnapshot snapshot)
        {
            byte[] onDiskBytes = compressionType switch
            {
                CompressionNone => (byte[])plaintext.Clone(), // avoid mutating the caller's buffer in place below
                CompressionZLib => Deflate(plaintext),
                _ => throw new NotSupportedException($"Unsupported compression type {compressionType} (only None/ZLib are supported)."),
            };

            if (snapshot.KeyIndex >= 0)
            {
                byte[] key = Rc4Cipher.Descramble(Rc4KeySet.Pc2AndAboveRawKeys[snapshot.KeyIndex]);
                Rc4Cipher.Transform(onDiskBytes, key);
            }

            return onDiskBytes;
        }

        private static byte[] Deflate(byte[] plaintext)
        {
            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(plaintext, 0, plaintext.Length);
            }

            return compressed.ToArray();
        }
    }
}
