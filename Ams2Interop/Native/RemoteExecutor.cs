using System.Runtime.InteropServices;

namespace Ams2Interop.Native;

/// <summary>
/// Executes a function inside the target process with two integer/pointer arguments, using a
/// small hand-written x64 stub run once via CreateRemoteThread per call. This exists because
/// several of AMS2's own setters are genuine function calls, not memory writes - there is no way
/// to trigger them from outside the process without executing code inside it. This is a one-shot
/// call per invocation, not a persistent hook.
///
/// Stub (22 bytes), invoked by CreateRemoteThread with RCX = pointer to a
/// { long Arg1; long Arg2; long Target; } parameter block written fresh for each call:
///
///   sub  rsp, 0x28            ; shadow space + 16-byte alignment before CALL
///   mov  rax, [rcx+0x10]      ; rax = Target
///   mov  rdx, [rcx+0x08]      ; rdx = Arg2
///   mov  rcx, [rcx]           ; rcx = Arg1 (overwrites rcx last, since we're done reading through it)
///   call rax
///   add  rsp, 0x28
///   ret
/// </summary>
public sealed class RemoteExecutor : IDisposable
{
    private static readonly byte[] StubBytes =
    {
        0x48, 0x83, 0xEC, 0x28,   // sub rsp, 0x28
        0x48, 0x8B, 0x41, 0x10,   // mov rax, [rcx+0x10]
        0x48, 0x8B, 0x51, 0x08,   // mov rdx, [rcx+0x08]
        0x48, 0x8B, 0x09,         // mov rcx, [rcx]
        0xFF, 0xD0,               // call rax
        0x48, 0x83, 0xC4, 0x28,   // add rsp, 0x28
        0xC3,                     // ret
    };

    /// <summary>
    /// Second stub for calling AMS2's SetCar (see Ams2Constants.SetCarRva's doc comment):
    /// target(context, track, car, livery, flag) - the first four args are __fastcall register
    /// args (RCX/RDX/R8/R9), the 5th is a single byte spilled to the stack at [rsp+0x20] per the
    /// Windows x64 ABI.
    ///
    /// Invoked with RCX = pointer to a 48-byte parameter block:
    ///   { long Context; long Track; long Car; long Livery; long Flag; long Target; }
    ///
    ///   push rbx
    ///   mov  rbx, rcx
    ///   sub  rsp, 0x30            ; 0x20 shadow space + 0x8 for the 5th arg slot + 0x8 alignment
    ///   mov  rax, [rbx+0x28]      ; Target
    ///   mov  r9,  [rbx+0x18]      ; Livery  (4th register arg)
    ///   mov  r8,  [rbx+0x10]      ; Car     (3rd register arg)
    ///   mov  rdx, [rbx+0x08]      ; Track   (2nd register arg)
    ///   mov  rcx, [rbx]           ; Context (1st register arg, overwrites rbx's source last)
    ///   mov  r10, [rbx+0x20]      ; Flag
    ///   mov  [rsp+0x20], r10      ; spilled 5th arg
    ///   call rax
    ///   add  rsp, 0x30
    ///   pop  rbx
    ///   ret
    ///
    /// The "mov r9, [rbx+0x18]" line's REX prefix must be 0x4C (R=1 for r9's reg field, B=0 -
    /// rbx needs no extension) - matching the same R=1/B=0 pattern used two lines below for
    /// "mov r8, [rbx+0x10]". A REX.B of 1 here would redirect the base register from rbx to r11
    /// (REX.B extends ModRM.rm the same way REX.R extends ModRM.reg) and silently load the Livery
    /// argument from garbage instead of the real value written to the param block.
    /// </summary>
    private static readonly byte[] SetCarStubBytes =
    {
        0x53,                                     // push rbx
        0x48, 0x89, 0xCB,                         // mov rbx, rcx
        0x48, 0x83, 0xEC, 0x30,                   // sub rsp, 0x30
        0x48, 0x8B, 0x43, 0x28,                   // mov rax, [rbx+0x28]
        0x4C, 0x8B, 0x4B, 0x18,                   // mov r9,  [rbx+0x18]  (REX=0x4C: R=1 for r9's reg field, B=0 - rbx needs no extension)
        0x4C, 0x8B, 0x43, 0x10,                   // mov r8,  [rbx+0x10]
        0x48, 0x8B, 0x53, 0x08,                   // mov rdx, [rbx+0x08]
        0x48, 0x8B, 0x0B,                         // mov rcx, [rbx]
        0x4C, 0x8B, 0x53, 0x20,                   // mov r10, [rbx+0x20]
        0x4C, 0x89, 0x54, 0x24, 0x20,             // mov [rsp+0x20], r10
        0xFF, 0xD0,                               // call rax
        0x48, 0x83, 0xC4, 0x30,                   // add rsp, 0x30
        0x5B,                                     // pop rbx
        0xC3,                                     // ret
    };

    private readonly ProcessMemory _mem;
    private IntPtr _stubAddress;
    private IntPtr _setCarStubAddress;

    public RemoteExecutor(ProcessMemory mem) => _mem = mem;

    public void EnsureStubInstalled()
    {
        if (_stubAddress != IntPtr.Zero) return;
        _stubAddress = InstallStub(StubBytes, "stub");
    }

    private void EnsureSetCarStubInstalled()
    {
        if (_setCarStubAddress != IntPtr.Zero) return;
        _setCarStubAddress = InstallStub(SetCarStubBytes, "SetCar stub");
    }

    /// <summary>
    /// Allocates the stub's page as write-only (no execute), writes the code, then flips the page
    /// to execute+read (no write) before it's ever run - the page is write-once/execute-many and
    /// so never needs W and X at the same time. Avoids leaving an RWX page sitting in the target
    /// process, which is one of the more reliable AV/EDR heuristics for this kind of technique;
    /// the stub's actual arguments travel in a separate, always-non-executable PAGE_READWRITE
    /// allocation (see Call/CallSetCar's paramBlock), so this doesn't affect what data can be
    /// written per-call - only the fixed code bytes' page protection.
    /// </summary>
    private IntPtr InstallStub(byte[] bytes, string label)
    {
        var address = NativeMethods.VirtualAllocEx(_mem.Handle, IntPtr.Zero, (uint)bytes.Length,
            NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
        if (address == IntPtr.Zero)
            throw new InvalidOperationException($"VirtualAllocEx ({label}) failed (error {Marshal.GetLastWin32Error()})");

        if (!NativeMethods.WriteProcessMemory(_mem.Handle, address, bytes, bytes.Length, out _))
            throw new InvalidOperationException($"WriteProcessMemory ({label}) failed (error {Marshal.GetLastWin32Error()})");

        if (!NativeMethods.VirtualProtectEx(_mem.Handle, address, (uint)bytes.Length, NativeMethods.PAGE_EXECUTE_READ, out _))
            throw new InvalidOperationException($"VirtualProtectEx ({label}) failed (error {Marshal.GetLastWin32Error()})");

        return address;
    }

    /// <summary>
    /// Calls targetAbsoluteAddress(arg1, arg2) inside the target process (standard x64
    /// __fastcall: RCX, RDX) and waits for it to return. Returns false on any failure - callers
    /// should treat that as "the write didn't happen" and verify via a subsequent read.
    /// </summary>
    public bool Call(long targetAbsoluteAddress, long arg1, long arg2, TimeSpan? timeout = null)
    {
        EnsureStubInstalled();

        var paramBlock = new byte[24];
        BitConverter.GetBytes(arg1).CopyTo(paramBlock, 0);
        BitConverter.GetBytes(arg2).CopyTo(paramBlock, 8);
        BitConverter.GetBytes(targetAbsoluteAddress).CopyTo(paramBlock, 16);

        var paramAddress = NativeMethods.VirtualAllocEx(_mem.Handle, IntPtr.Zero, (uint)paramBlock.Length,
            NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
        if (paramAddress == IntPtr.Zero) return false;

        try
        {
            if (!NativeMethods.WriteProcessMemory(_mem.Handle, paramAddress, paramBlock, paramBlock.Length, out _))
                return false;

            var thread = NativeMethods.CreateRemoteThread(_mem.Handle, IntPtr.Zero, 0, _stubAddress, paramAddress, 0, out _);
            if (thread == IntPtr.Zero) return false;

            try
            {
                var waitMs = (uint)(timeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds;
                return NativeMethods.WaitForSingleObject(thread, waitMs) == NativeMethods.WAIT_OBJECT_0;
            }
            finally
            {
                NativeMethods.CloseHandle(thread);
            }
        }
        finally
        {
            NativeMethods.VirtualFreeEx(_mem.Handle, paramAddress, 0, NativeMethods.MEM_RELEASE);
        }
    }

    /// <summary>
    /// Calls target(context, track, car, livery, flag) inside the target process - AMS2's
    /// SetCar. `context` is the master pointer (see Ams2Constants.SetCarRva's doc comment).
    /// `flag`'s exact meaning is unconfirmed; 0 is a conservative default, not a
    /// confirmed-correct one.
    /// </summary>
    public bool CallSetCar(long targetAbsoluteAddress, long context, long track, long car, long livery,
        byte flag = 0, TimeSpan? timeout = null)
    {
        EnsureSetCarStubInstalled();

        var paramBlock = new byte[48];
        BitConverter.GetBytes(context).CopyTo(paramBlock, 0);
        BitConverter.GetBytes(track).CopyTo(paramBlock, 8);
        BitConverter.GetBytes(car).CopyTo(paramBlock, 16);
        BitConverter.GetBytes(livery).CopyTo(paramBlock, 24);
        BitConverter.GetBytes((long)flag).CopyTo(paramBlock, 32);
        BitConverter.GetBytes(targetAbsoluteAddress).CopyTo(paramBlock, 40);

        var paramAddress = NativeMethods.VirtualAllocEx(_mem.Handle, IntPtr.Zero, (uint)paramBlock.Length,
            NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
        if (paramAddress == IntPtr.Zero) return false;

        try
        {
            if (!NativeMethods.WriteProcessMemory(_mem.Handle, paramAddress, paramBlock, paramBlock.Length, out _))
                return false;

            var thread = NativeMethods.CreateRemoteThread(_mem.Handle, IntPtr.Zero, 0, _setCarStubAddress, paramAddress, 0, out _);
            if (thread == IntPtr.Zero) return false;

            try
            {
                var waitMs = (uint)(timeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds;
                return NativeMethods.WaitForSingleObject(thread, waitMs) == NativeMethods.WAIT_OBJECT_0;
            }
            finally
            {
                NativeMethods.CloseHandle(thread);
            }
        }
        finally
        {
            NativeMethods.VirtualFreeEx(_mem.Handle, paramAddress, 0, NativeMethods.MEM_RELEASE);
        }
    }

    public void Dispose()
    {
        if (_stubAddress != IntPtr.Zero)
        {
            NativeMethods.VirtualFreeEx(_mem.Handle, _stubAddress, 0, NativeMethods.MEM_RELEASE);
            _stubAddress = IntPtr.Zero;
        }
        if (_setCarStubAddress != IntPtr.Zero)
        {
            NativeMethods.VirtualFreeEx(_mem.Handle, _setCarStubAddress, 0, NativeMethods.MEM_RELEASE);
            _setCarStubAddress = IntPtr.Zero;
        }
    }
}
