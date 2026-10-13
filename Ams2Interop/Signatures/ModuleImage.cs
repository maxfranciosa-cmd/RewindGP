using System.Buffers.Binary;
using System.Globalization;
using Ams2Interop.Native;

namespace Ams2Interop.Signatures;

/// <summary>
/// The parts of a loaded AMS2AVX.exe that signature work needs: which build it is, and the bytes
/// of its code section. Read from the running process (the PE headers are mapped at the module
/// base), so no file path is needed.
/// </summary>
public sealed class ModuleImage
{
    private const uint ImageScnMemExecute = 0x20000000;
    private const int ReadChunkSize = 1 << 20;

    /// <summary>
    /// Identifies one AMS2AVX.exe build: link timestamp, image size and PE checksum, all from the
    /// headers. Every Steam update of the exe changes at least one of them.
    /// </summary>
    public string BuildId { get; }

    /// <summary>RVA of the code section's first byte.</summary>
    public long TextRva { get; }

    public byte[] Text { get; }

    public long SizeOfImage { get; }

    public ModuleImage(string buildId, long textRva, byte[] text, long sizeOfImage)
    {
        BuildId = buildId;
        TextRva = textRva;
        Text = text;
        SizeOfImage = sizeOfImage;
    }

    /// <summary>Reads the PE headers and the code section of the module loaded at <paramref name="moduleBase"/>; null when the headers don't look like a PE image or the section can't be read.</summary>
    public static ModuleImage? TryRead(ProcessMemory mem, long moduleBase, Action<string>? log = null)
    {
        if (!mem.ReadRaw(moduleBase, 0x1000, out var headers))
        {
            log?.Invoke("ModuleImage: could not read the PE headers");
            return null;
        }

        if (headers[0] != (byte)'M' || headers[1] != (byte)'Z')
        {
            log?.Invoke("ModuleImage: no MZ signature at the module base");
            return null;
        }

        int nt = BinaryPrimitives.ReadInt32LittleEndian(headers.AsSpan(0x3C));
        if (nt <= 0 || nt + 0x108 > headers.Length || BinaryPrimitives.ReadUInt32LittleEndian(headers.AsSpan(nt)) != 0x00004550)
        {
            log?.Invoke("ModuleImage: no PE signature");
            return null;
        }

        int fileHeader = nt + 4;
        int sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(headers.AsSpan(fileHeader + 2));
        uint timeDateStamp = BinaryPrimitives.ReadUInt32LittleEndian(headers.AsSpan(fileHeader + 4));
        int optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(headers.AsSpan(fileHeader + 16));
        int optionalHeader = fileHeader + 20;
        uint sizeOfImage = BinaryPrimitives.ReadUInt32LittleEndian(headers.AsSpan(optionalHeader + 56));
        uint checkSum = BinaryPrimitives.ReadUInt32LittleEndian(headers.AsSpan(optionalHeader + 64));

        // the code section: ".text" if present, otherwise the largest executable section
        int sectionTable = optionalHeader + optionalHeaderSize;
        long textRva = -1;
        int textSize = 0;
        bool foundNamedText = false;
        for (int s = 0; s < sectionCount; s++)
        {
            int header = sectionTable + s * 40;
            if (header + 40 > headers.Length) break;

            uint characteristics = BinaryPrimitives.ReadUInt32LittleEndian(headers.AsSpan(header + 36));
            if ((characteristics & ImageScnMemExecute) == 0) continue;

            string name = System.Text.Encoding.ASCII.GetString(headers, header, 8).TrimEnd('\0');
            int virtualSize = BinaryPrimitives.ReadInt32LittleEndian(headers.AsSpan(header + 8));
            uint virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(headers.AsSpan(header + 12));

            bool isText = name == ".text";
            if ((isText && !foundNamedText) || (!foundNamedText && virtualSize > textSize))
            {
                textRva = virtualAddress;
                textSize = virtualSize;
                foundNamedText = isText;
            }
        }

        if (textRva < 0 || textSize <= 0)
        {
            log?.Invoke("ModuleImage: no executable section found");
            return null;
        }

        var text = new byte[textSize];
        for (int offset = 0; offset < textSize; offset += ReadChunkSize)
        {
            int length = Math.Min(ReadChunkSize, textSize - offset);
            if (!mem.ReadRaw(moduleBase + textRva + offset, length, out var chunk))
            {
                log?.Invoke($"ModuleImage: could not read the code section at +0x{offset:X}");
                return null;
            }
            Buffer.BlockCopy(chunk, 0, text, offset, length);
        }

        string buildId = string.Format(CultureInfo.InvariantCulture, "{0:X8}-{1:X8}-{2:X8}", timeDateStamp, sizeOfImage, checkSum);
        return new ModuleImage(buildId, textRva, text, sizeOfImage);
    }
}
