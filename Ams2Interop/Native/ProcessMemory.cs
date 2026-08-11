using System.Runtime.InteropServices;

namespace Ams2Interop.Native;

/// <summary>
/// Thin wrapper around ReadProcessMemory/WriteProcessMemory plus a "safe read with a
/// plausibility check" convention: a pointer-shaped value outside a sane heap range is treated
/// as invalid rather than trusted, since the objects this resolves can legitimately be absent
/// (e.g. the player hasn't opened Custom Race yet).
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    public IntPtr Handle { get; }

    private ProcessMemory(IntPtr handle)
    {
        Handle = handle;
    }

    public static ProcessMemory Open(int processId)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.ProcessAccess.All, false, processId);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"OpenProcess failed for pid {processId} (error {Marshal.GetLastWin32Error()}). " +
                "Running elevated, or a mismatched process bitness, are the usual causes.");
        return new ProcessMemory(handle);
    }

    public bool TryReadInt64(long address, out long value)
    {
        if (!ReadRaw(address, 8, out var buf)) { value = 0; return false; }
        value = BitConverter.ToInt64(buf, 0);
        return true;
    }

    public bool TryReadInt32(long address, out int value)
    {
        if (!ReadRaw(address, 4, out var buf)) { value = 0; return false; }
        value = BitConverter.ToInt32(buf, 0);
        return true;
    }

    /// <summary>Raw ReadProcessMemory into a caller-sized buffer - used by the region scanner.</summary>
    public bool ReadRaw(long address, int length, out byte[] buffer)
    {
        buffer = new byte[length];
        if (!NativeMethods.ReadProcessMemory(Handle, (IntPtr)address, buffer, length, out var read))
            return false;
        return (long)read == length;
    }

    /// <summary>
    /// Plausibility check: value - 0x100000000 &lt; 0x700000000 (unsigned). This tells "a
    /// plausible pointer" apart from "garbage/zero," not a strict validity guarantee.
    /// </summary>
    public bool TryReadPointerSafe(long address, out long pointer)
    {
        if (!TryReadInt64(address, out pointer)) return false;
        var biased = unchecked((ulong)pointer) - 0x100000000UL;
        return biased < 0x700000000UL;
    }

    /// <summary>
    /// Resolves a VM498/VM550 slot's int value: node = *(vmBase + slot*8); validates the type
    /// tag at node-0x18 == IntPropertyTypeTag; returns the int at node+0x18c.
    /// </summary>
    public bool TryReadSlot(long vmBase, int slot, out int value)
    {
        value = 0;
        if (!TryReadPointerSafe(vmBase + slot * 8L, out var node)) return false;
        if (!TryReadInt64(node - 0x18, out var typeTag)) return false;
        if (typeTag != Ams2Constants.IntPropertyTypeTag) return false;
        return TryReadInt32(node + 0x18c, out value);
    }

    public void Dispose() => NativeMethods.CloseHandle(Handle);
}
