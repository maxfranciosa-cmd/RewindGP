using System.Numerics;

namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// "Scribe" cipher used to encrypt a .bff pak's ext-info entries table + filename string table
    /// (the block PCarsTools calls the pak's "extra info", distinct from the RC4-encrypted TOC -
    /// see AMS2-livery-modding-knowledge.md and BffPakEntryInserter's doc comment on why this
    /// table matters). An RC6-shaped block cipher (32-bit words, 4-word/16-byte blocks) with a
    /// fixed, non-per-pak key - ported from PCarsTools' ScribeDecrypt
    /// (github.com/Nenkai/PCarsTools, PCarsTools/Encryption/ScribeDecrypt.cs, MIT licensed), which
    /// only implements decryption. <see cref="Encrypt"/> is this class's own derivation of the
    /// inverse transform (PCarsTools has no packer), obtained by algebraically inverting each step
    /// of the ported Decrypt in reverse order - validated by round-tripping real ext-info bytes
    /// extracted from an actual AMS2 install (decrypt then re-encrypt reproduces the original
    /// on-disk bytes exactly) before ever being wired into a write path.
    /// </summary>
    public sealed class ScribeCipher
    {
        private static readonly uint[] Key =
        {
            0xbb9fcbf5, 0xb296fa98, 0xdd9ccbdb, 0x96c2e3f2,
            0x93e8dcf3, 0xbadbbc99, 0xcc9acd89, 0xaae9bc98,
            0xa8f9a8c5, 0xb6d8fbd0, 0xc6cea888, 0
        };

        private const int Rounds = 30;
        private const int KeySize = 44;
        private const int Factor = (Rounds * 2) + 4;

        private readonly uint[] _schedule = new uint[64];

        public ScribeCipher()
        {
            CreateSchedule();
        }

        private void CreateSchedule()
        {
            uint[] tmpKey = new uint[KeySize];

            uint y = 0;
            for (uint x = 0; x < KeySize; x += 4)
            {
                uint c = Key[y];
                tmpKey[x] = (byte)(c >> 8);
                tmpKey[x + 1] = (byte)(c >> 24);
                tmpKey[x + 2] = (byte)c;
                tmpKey[x + 3] = (byte)(c >> 16);
                y += 1;
            }

            _schedule[0] = 0xB7E15163;
            for (uint x = 1; x <= (Rounds * 2) + 3; x += 1)
                _schedule[x] = _schedule[x - 1] + 0x9E3779B9;

            uint a = 0, b = 0, i = 0, j = 0;
            int count = KeySize > Factor ? 3 * KeySize : 3 * Factor;

            for (int x = 1; x <= count; x += 1)
            {
                uint arg0 = _schedule[i] + a + b;
                arg0 = BitOperations.RotateLeft(arg0, 3);
                a = arg0;
                _schedule[i] = a;

                uint kr = tmpKey[j];
                if (x <= KeySize)
                {
                    arg0 = 0xAEB3F79Au >> (int)((j % 4) * 8);
                    kr ^= (byte)arg0;
                }

                arg0 = kr + a + b;
                arg0 = BitOperations.RotateLeft(arg0, (int)((a + b) & 31));
                b = arg0;
                tmpKey[j] = b;

                i = (i + 1) % Factor;
                j = (j + 1) % KeySize;
            }
        }

        /// <summary>Decrypts <paramref name="data"/> in place, 4 uint32 words (16 bytes) at a time.</summary>
        public void Decrypt(Span<uint> data)
        {
            for (int x = 0; x < data.Length; x += 4)
            {
                uint k0 = data[x], k1 = data[x + 1], k2 = data[x + 2], k3 = data[x + 3];

                k2 -= _schedule[(Rounds * 2) + 3];
                k0 -= _schedule[(Rounds * 2) + 2];

                for (int i = Rounds; i >= 1; i -= 1)
                {
                    uint kr = k3;
                    k3 = k2; k2 = k1; k1 = k0; k0 = kr; // rotate right

                    uint a = BitOperations.RotateLeft(k3 * ((k3 << 1) + 1), 2);
                    uint b = BitOperations.RotateLeft(k1 * ((k1 << 1) + 1), 2);

                    k2 = BitOperations.RotateRight(k2 - _schedule[(i << 1) + 1], (int)(b & 31)) ^ a;
                    k0 = BitOperations.RotateRight(k0 - _schedule[i << 1], (int)(a & 31)) ^ b;
                }

                k3 -= _schedule[1];
                k1 -= _schedule[0];

                data[x] = k0; data[x + 1] = k1; data[x + 2] = k2; data[x + 3] = k3;
            }
        }

        /// <summary>
        /// Encrypts <paramref name="data"/> in place - the exact inverse of <see cref="Decrypt"/>,
        /// derived by running every one of its steps backwards (see the class doc comment).
        /// </summary>
        public void Encrypt(Span<uint> data)
        {
            for (int x = 0; x < data.Length; x += 4)
            {
                uint k0 = data[x], k1 = data[x + 1], k2 = data[x + 2], k3 = data[x + 3];

                k1 += _schedule[0];
                k3 += _schedule[1];

                for (int i = 1; i <= Rounds; i += 1)
                {
                    uint t = BitOperations.RotateLeft(k1 * ((k1 << 1) + 1), 2);
                    uint u = BitOperations.RotateLeft(k3 * ((k3 << 1) + 1), 2);

                    k0 = BitOperations.RotateLeft(k0 ^ t, (int)(u & 31)) + _schedule[i << 1];
                    k2 = BitOperations.RotateLeft(k2 ^ u, (int)(t & 31)) + _schedule[(i << 1) + 1];

                    // rotate left (inverse of Decrypt's per-round rotate right)
                    uint tmp = k0;
                    k0 = k1; k1 = k2; k2 = k3; k3 = tmp;
                }

                k0 += _schedule[(Rounds * 2) + 2];
                k2 += _schedule[(Rounds * 2) + 3];

                data[x] = k0; data[x + 1] = k1; data[x + 2] = k2; data[x + 3] = k3;
            }
        }
    }
}
