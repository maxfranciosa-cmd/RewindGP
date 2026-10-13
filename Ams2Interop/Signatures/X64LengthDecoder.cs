namespace Ams2Interop.Signatures;

/// <summary>
/// What SignatureBuilder needs to know about one x64 instruction: how long it is, and which of its
/// bytes are addresses that move when the game is rebuilt (so they become wildcards).
/// </summary>
public readonly record struct DecodedInstruction(
    int Length,
    // rel32 operand of a near call/jmp/jcc, or -1
    int Rel32Offset,
    // disp32 of a RIP-relative memory operand ([rip+disp32]), or -1
    int RipDisp32Offset,
    // 8-byte immediate/moffs (mov r64, imm64 / mov rax, [moffs64]), or -1
    int Imm64Offset);

/// <summary>
/// A length decoder for x64 machine code - NOT a disassembler. It only works out where each
/// instruction ends and where its relocation-sensitive operands sit, which is all signature
/// building and matching need. Covers the legacy one/two/three-byte opcode maps and VEX; gives up
/// (returns false) on EVEX and anything it doesn't recognise, which SignatureBuilder treats as
/// "stop the pattern here", never as a guess.
/// </summary>
public static class X64LengthDecoder
{
    private const int MaxInstructionLength = 15;

    public static bool TryDecode(ReadOnlySpan<byte> code, out DecodedInstruction instruction)
    {
        instruction = default;
        int i = 0;
        bool operandSize16 = false;
        bool addressSize32 = false;
        bool rexW = false;

        // legacy prefixes, any order
        while (i < code.Length)
        {
            byte p = code[i];
            if (p == 0x66) { operandSize16 = true; i++; }
            else if (p == 0x67) { addressSize32 = true; i++; }
            else if (p is 0xF0 or 0xF2 or 0xF3 or 0x2E or 0x36 or 0x3E or 0x26 or 0x64 or 0x65) i++;
            else break;
            if (i > MaxInstructionLength) return false;
        }

        // REX must immediately precede the opcode
        if (i < code.Length && code[i] >= 0x40 && code[i] <= 0x4F)
        {
            rexW = (code[i] & 0x08) != 0;
            i++;
        }

        if (i >= code.Length) return false;

        int rel32 = -1, rip = -1, imm64 = -1;
        bool hasModRm;
        int immSize;
        int zSize = operandSize16 ? 2 : 4;
        byte op = code[i++];

        if (op == 0xC4 || op == 0xC5)
            return TryDecodeVex(code, i, op, out instruction);

        if (op == 0x0F)
        {
            if (i >= code.Length) return false;
            byte op2 = code[i++];

            if (op2 == 0x38)
            {
                if (i >= code.Length) return false;
                i++;
                hasModRm = true;
                immSize = 0;
            }
            else if (op2 == 0x3A)
            {
                if (i >= code.Length) return false;
                i++;
                hasModRm = true;
                immSize = 1;
            }
            else if (op2 >= 0x80 && op2 <= 0x8F)
            {
                // jcc rel32
                rel32 = i;
                hasModRm = false;
                immSize = 4;
            }
            else if (op2 == 0x0F)
            {
                return false; // 3DNow!
            }
            else if (op2 is 0x05 or 0x06 or 0x07 or 0x08 or 0x09 or 0x0B or 0x0E
                     or 0x30 or 0x31 or 0x32 or 0x33 or 0x34 or 0x35 or 0x37 or 0x77
                     or 0xA0 or 0xA1 or 0xA2 or 0xA8 or 0xA9 or 0xAA
                     || (op2 >= 0xC8 && op2 <= 0xCF))
            {
                hasModRm = false;
                immSize = 0;
            }
            else
            {
                hasModRm = true;
                immSize = op2 is 0x70 or 0x71 or 0x72 or 0x73 or 0xA4 or 0xAC or 0xBA or 0xC2 or 0xC4 or 0xC5 or 0xC6 ? 1 : 0;
            }
        }
        else
        {
            if (!TryOneByteOpcode(op, zSize, rexW, addressSize32, out hasModRm, out immSize, out bool isRel32, out bool isMoffs))
                return false;
            if (isRel32) rel32 = i;
            if (isMoffs && immSize == 8) imm64 = i;

            // F6/F7 /0 and /1 (test) carry an immediate; the other forms don't
            if ((op == 0xF6 || op == 0xF7) && i < code.Length)
            {
                int reg = (code[i] >> 3) & 7;
                if (reg is 0 or 1) immSize = op == 0xF6 ? 1 : zSize;
            }

            // mov r64, imm64
            if (op >= 0xB8 && op <= 0xBF && rexW) imm64 = i;
        }

        if (hasModRm && !TryModRm(code, ref i, out rip)) return false;

        i += immSize;
        if (i > code.Length || i > MaxInstructionLength) return false;

        instruction = new DecodedInstruction(i, rel32, rip, imm64);
        return true;
    }

    private static bool TryOneByteOpcode(byte op, int zSize, bool rexW, bool addressSize32,
        out bool hasModRm, out int immSize, out bool isRel32, out bool isMoffs)
    {
        hasModRm = false;
        immSize = 0;
        isRel32 = false;
        isMoffs = false;

        // 00-3F: the ALU block, eight ops of six encodings each (plus the escape/prefix slots)
        if (op < 0x40)
        {
            int low = op & 7;
            if (low <= 3) { hasModRm = true; return true; }
            if (low == 4) { immSize = 1; return true; }
            if (low == 5) { immSize = zSize; return true; }
            return false; // 06/07/0E/16/17/1E/1F/27/2F/37/3F: invalid in 64-bit mode (0F handled by caller)
        }

        if (op >= 0x50 && op <= 0x5F) return true;          // push/pop r64
        if (op >= 0x70 && op <= 0x7F) { immSize = 1; return true; } // jcc rel8
        if (op >= 0x84 && op <= 0x8F) { hasModRm = true; return true; }
        if (op >= 0x90 && op <= 0x99) return true;           // xchg/nop, cbw/cwd
        if (op >= 0x9B && op <= 0x9F) return true;
        if (op >= 0xA0 && op <= 0xA3) { immSize = addressSize32 ? 4 : 8; isMoffs = true; return true; }
        if (op >= 0xA4 && op <= 0xA7) return true;
        if (op >= 0xAA && op <= 0xAF) return true;
        if (op >= 0xB0 && op <= 0xB7) { immSize = 1; return true; }
        if (op >= 0xB8 && op <= 0xBF) { immSize = rexW ? 8 : zSize; return true; }
        if (op >= 0xD8 && op <= 0xDF) { hasModRm = true; return true; } // x87
        if (op >= 0xE0 && op <= 0xE7) { immSize = 1; return true; }    // loop/jrcxz/in/out imm8
        if (op >= 0xEC && op <= 0xEF) return true;

        switch (op)
        {
            case 0x63: hasModRm = true; return true;                     // movsxd
            case 0x68: immSize = zSize; return true;                     // push imm
            case 0x69: hasModRm = true; immSize = zSize; return true;    // imul r, r/m, imm
            case 0x6A: immSize = 1; return true;
            case 0x6B: hasModRm = true; immSize = 1; return true;
            case 0x6C: case 0x6D: case 0x6E: case 0x6F: return true;
            case 0x80: case 0x83: hasModRm = true; immSize = 1; return true;
            case 0x81: hasModRm = true; immSize = zSize; return true;
            case 0xA8: immSize = 1; return true;
            case 0xA9: immSize = zSize; return true;
            case 0xC0: case 0xC1: hasModRm = true; immSize = 1; return true;
            case 0xC2: immSize = 2; return true;
            case 0xC3: return true;
            case 0xC6: hasModRm = true; immSize = 1; return true;
            case 0xC7: hasModRm = true; immSize = zSize; return true;
            case 0xC8: immSize = 3; return true;                         // enter imm16, imm8
            case 0xC9: return true;
            case 0xCA: immSize = 2; return true;
            case 0xCB: case 0xCC: case 0xCF: return true;
            case 0xCD: immSize = 1; return true;
            case 0xD0: case 0xD1: case 0xD2: case 0xD3: hasModRm = true; return true;
            case 0xD7: return true;
            case 0xE8: case 0xE9: immSize = 4; isRel32 = true; return true;
            case 0xEB: immSize = 1; return true;
            case 0xF1: case 0xF4: case 0xF5: return true;
            case 0xF6: case 0xF7: hasModRm = true; return true;          // imm decided by caller from /reg
            case 0xF8: case 0xF9: case 0xFA: case 0xFB: case 0xFC: case 0xFD: return true;
            case 0xFE: case 0xFF: hasModRm = true; return true;
            default: return false; // 62 (EVEX), 82, 9A, CE, D4-D6, EA, etc.
        }
    }

    private static bool TryModRm(ReadOnlySpan<byte> code, ref int i, out int ripDispOffset)
    {
        ripDispOffset = -1;
        if (i >= code.Length) return false;
        byte modrm = code[i++];
        int mod = modrm >> 6;
        int rm = modrm & 7;

        if (mod == 3) return true;

        if (rm == 4)
        {
            if (i >= code.Length) return false;
            byte sib = code[i++];
            if (mod == 0 && (sib & 7) == 5)
            {
                i += 4; // [index*scale + disp32], absolute - not RIP-relative
                return true;
            }
        }
        else if (mod == 0 && rm == 5)
        {
            ripDispOffset = i;
            i += 4;
            return true;
        }

        if (mod == 1) i += 1;
        else if (mod == 2) i += 4;
        return true;
    }

    private static bool TryDecodeVex(ReadOnlySpan<byte> code, int i, byte vexByte, out DecodedInstruction instruction)
    {
        instruction = default;
        int map;
        if (vexByte == 0xC5)
        {
            if (i >= code.Length) return false;
            i += 1;
            map = 1;
        }
        else
        {
            if (i + 1 >= code.Length) return false;
            map = code[i] & 0x1F;
            i += 2;
        }

        if (map is < 1 or > 3 || i >= code.Length) return false;
        byte op = code[i++];

        int rip = -1;
        // vzeroupper/vzeroall are the only ModRM-less VEX instructions in common use
        bool hasModRm = !(map == 1 && op == 0x77);
        if (hasModRm && !TryModRm(code, ref i, out rip)) return false;

        int immSize = map == 3 || (map == 1 && op is 0x70 or 0x71 or 0x72 or 0x73 or 0xC2 or 0xC4 or 0xC5 or 0xC6) ? 1 : 0;
        i += immSize;
        if (i > code.Length || i > MaxInstructionLength) return false;

        instruction = new DecodedInstruction(i, -1, rip, -1);
        return true;
    }
}
