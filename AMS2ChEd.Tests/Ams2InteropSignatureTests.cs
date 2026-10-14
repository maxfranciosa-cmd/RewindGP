using Ams2Interop;
using Ams2Interop.Signatures;

namespace AMS2ChEd.Tests.Ams2Interop
{
    [TestClass]
    public class X64LengthDecoderTests
    {
        [TestMethod]
        [DataRow("48 89 5C 24 08", 5, -1, -1)]          // mov [rsp+8], rbx
        [DataRow("57", 1, -1, -1)]                      // push rdi
        [DataRow("48 83 EC 20", 4, -1, -1)]             // sub rsp, 20h
        [DataRow("48 8B 05 11 22 33 44", 7, -1, 3)]     // mov rax, [rip+disp32]
        [DataRow("48 8D 0D 11 22 33 44", 7, -1, 3)]     // lea rcx, [rip+disp32]
        [DataRow("E8 11 22 33 44", 5, 1, -1)]           // call rel32
        [DataRow("E9 11 22 33 44", 5, 1, -1)]           // jmp rel32
        [DataRow("0F 84 11 22 33 44", 6, 2, -1)]        // jz rel32
        [DataRow("74 05", 2, -1, -1)]                   // jz rel8
        [DataRow("C5 F8 77", 3, -1, -1)]                // vzeroupper
        [DataRow("C5 FA 10 44 24 20", 6, -1, -1)]       // vmovss xmm0, [rsp+20h]
        [DataRow("C5 FA 10 05 11 22 33 44", 8, -1, 4)]  // vmovss xmm0, [rip+disp32]
        [DataRow("C4 E3 79 16 C0 01", 6, -1, -1)]       // vpextrd eax, xmm0, 1
        [DataRow("66 0F 1F 44 00 00", 6, -1, -1)]       // nop word [rax+rax]
        [DataRow("41 B8 01 00 00 00", 6, -1, -1)]       // mov r8d, 1
        [DataRow("66 C7 44 24 08 34 12", 7, -1, -1)]    // mov word [rsp+8], 1234h
        [DataRow("F6 C1 01", 3, -1, -1)]                // test cl, 1
        [DataRow("F6 D9", 2, -1, -1)]                   // neg cl
        [DataRow("F7 C1 01 00 00 00", 6, -1, -1)]       // test ecx, 1
        [DataRow("48 8B 04 25 00 10 00 00", 8, -1, -1)] // mov rax, [abs32] - SIB form, not RIP-relative
        [DataRow("C7 05 11 22 33 44 01 00 00 00", 10, -1, 2)] // mov dword [rip+disp32], imm32
        [DataRow("80 3D 11 22 33 44 00", 7, -1, 2)]     // cmp byte [rip+disp32], 0
        [DataRow("40 53", 2, -1, -1)]                   // push rbx (with REX)
        [DataRow("C3", 1, -1, -1)]                      // ret
        [DataRow("0F 38 00 C1", 4, -1, -1)]             // pshufb mm0, mm1
        [DataRow("66 0F 3A 0F C1 08", 6, -1, -1)]       // palignr xmm0, xmm1, 8
        public void TryDecode_KnownInstruction_LengthAndRelocatableOperands(string hex, int length, int rel32Offset, int ripOffset)
        {
            Assert.IsTrue(X64LengthDecoder.TryDecode(Hex(hex), out var ins));
            Assert.AreEqual(length, ins.Length);
            Assert.AreEqual(rel32Offset, ins.Rel32Offset);
            Assert.AreEqual(ripOffset, ins.RipDisp32Offset);
        }

        [TestMethod]
        public void TryDecode_MovImm64_FlagsTheImmediate()
        {
            Assert.IsTrue(X64LengthDecoder.TryDecode(Hex("48 B8 00 10 00 40 01 00 00 00"), out var ins));
            Assert.AreEqual(10, ins.Length);
            Assert.AreEqual(2, ins.Imm64Offset);
        }

        [TestMethod]
        [DataRow("62 F1 7C 48 10 C1")] // EVEX
        [DataRow("06")]                // invalid in 64-bit mode
        [DataRow("48 8B")]             // truncated
        public void TryDecode_UnsupportedOrTruncated_ReturnsFalse(string hex)
        {
            Assert.IsFalse(X64LengthDecoder.TryDecode(Hex(hex), out _));
        }

        internal static byte[] Hex(string hex) =>
            hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(b => Convert.ToByte(b, 16)).ToArray();
    }

    [TestClass]
    public class BytePatternTests
    {
        [TestMethod]
        public void Parse_RoundTripsWithWildcards()
        {
            Assert.AreEqual("48 8B 05 ?? ?? ?? ?? C3", BytePattern.Parse("48 8b 05 ?? ?? ?? ?? c3").ToString());
        }

        [TestMethod]
        public void FindAll_MatchesThroughWildcards()
        {
            var data = X64LengthDecoderTests.Hex("90 48 8B 05 01 02 03 04 C3 48 8B 05 AA BB CC DD C3 48 8B 0D 01 02 03 04 C3");
            var hits = BytePattern.Parse("48 8B 05 ?? ?? ?? ?? C3").FindAll(data);
            CollectionAssert.AreEqual(new List<int> { 1, 9 }, hits);
        }

        [TestMethod]
        public void FindAll_WildcardAtStart_StillFindsMatchAtOffsetZero()
        {
            var data = X64LengthDecoderTests.Hex("AA 01 02 03 BB 01 02 03");
            CollectionAssert.AreEqual(new List<int> { 0, 4 }, BytePattern.Parse("?? 01 02 03").FindAll(data));
        }

        [TestMethod]
        public void FindAll_RespectsLimit()
        {
            var data = X64LengthDecoderTests.Hex("01 02 01 02 01 02");
            Assert.AreEqual(2, BytePattern.Parse("01 02").FindAll(data, limit: 2).Count);
        }
    }

    [TestClass]
    public class Ams2SignatureRoundTripTests
    {
        [TestMethod]
        public void Generate_OnKnownBuild_EverySignatureLeadsBackToTheKnownValue()
        {
            var build = FakeBuild.Create(textPadding: 0, dataShift: 0);

            var result = Ams2SignatureGenerator.Generate(build.Image, build.Rvas);

            Assert.IsTrue(result.Complete, string.Join("\n", result.Report));
            foreach (var name in Ams2OffsetNames.All)
                Assert.AreEqual(build.Rvas[name], result.File.Signatures[name].Resolve(build.Image), name);
        }

        [TestMethod]
        public void Generate_DecoyWithSamePrologue_SetterPatternGrowsPastIt()
        {
            var build = FakeBuild.Create(textPadding: 0, dataShift: 0);

            var setter = Ams2SignatureGenerator.Generate(build.Image, build.Rvas).File.Signatures[Ams2OffsetNames.Setter];

            // the decoy shares the setter's first 14 bytes, so 16 alone wouldn't be unique
            Assert.IsTrue(BytePattern.Parse(setter.Pattern).Length > 14);
        }

        [TestMethod]
        public void Resolve_AfterAPatchMovesCodeAndData_FindsEveryNewValue()
        {
            var oldBuild = FakeBuild.Create(textPadding: 0, dataShift: 0);
            var signatures = Ams2SignatureGenerator.Generate(oldBuild.Image, oldBuild.Rvas).File;

            // what the 2026-09-20 patch did: code moved by +0x120, data block shifted too
            var newBuild = FakeBuild.Create(textPadding: 0x120, dataShift: 0x1A0, buildId: "NEW-BUILD");
            var offsets = Ams2OffsetResolver.Resolve(newBuild.Image, Ams2Constants.DefaultImageBase, signatures, cacheFilePath: null);

            Assert.AreEqual(Ams2OffsetSource.Scanned, offsets.Source);
            Assert.IsFalse(offsets.IsDegraded, offsets.Describe());
            foreach (var name in Ams2OffsetNames.All)
            {
                Assert.AreEqual(newBuild.Rvas[name], offsets.Rvas[name], name);
                Assert.AreNotEqual(oldBuild.Rvas[name], offsets.Rvas[name], name);
            }
        }

        [TestMethod]
        public void Resolve_FunctionRewrittenByThePatch_OnlyItsFeatureIsSwitchedOff()
        {
            var oldBuild = FakeBuild.Create(textPadding: 0, dataShift: 0);
            var signatures = Ams2SignatureGenerator.Generate(oldBuild.Image, oldBuild.Rvas).File;

            var newBuild = FakeBuild.Create(textPadding: 0x40, dataShift: 0, rewriteSetCar: true);
            var offsets = Ams2OffsetResolver.Resolve(newBuild.Image, Ams2Constants.DefaultImageBase, signatures, cacheFilePath: null);

            CollectionAssert.AreEqual(new[] { Ams2OffsetNames.SetCar }, offsets.Missing.ToArray());
            Assert.IsFalse(offsets.CanSetCar);
            Assert.IsTrue(offsets.CanWriteSlots);
            Assert.IsTrue(offsets.CanCommit);
            Assert.IsTrue(offsets.CanChangeGameplaySettings);
        }

        [TestMethod]
        public void Resolve_CompleteScanIsCached_AndReusedForTheSameBuild()
        {
            var cachePath = Path.Combine(Path.GetTempPath(), $"ams2_offsets_{Guid.NewGuid():N}.json");
            try
            {
                var oldBuild = FakeBuild.Create(textPadding: 0, dataShift: 0);
                var signatures = Ams2SignatureGenerator.Generate(oldBuild.Image, oldBuild.Rvas).File;
                var newBuild = FakeBuild.Create(textPadding: 0x120, dataShift: 0x1A0, buildId: "NEW-BUILD");

                var first = Ams2OffsetResolver.Resolve(newBuild.Image, Ams2Constants.DefaultImageBase, signatures, cachePath);
                var second = Ams2OffsetResolver.Resolve(newBuild.Image, Ams2Constants.DefaultImageBase, signatures, cachePath);

                Assert.AreEqual(Ams2OffsetSource.Scanned, first.Source);
                Assert.AreEqual(Ams2OffsetSource.Cached, second.Source);
                foreach (var name in Ams2OffsetNames.All)
                    Assert.AreEqual(first.Rvas[name], second.Rvas[name], name);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        [TestMethod]
        public void Resolve_DegradedScan_IsNotCached()
        {
            var cachePath = Path.Combine(Path.GetTempPath(), $"ams2_offsets_{Guid.NewGuid():N}.json");
            try
            {
                var oldBuild = FakeBuild.Create(textPadding: 0, dataShift: 0);
                var signatures = Ams2SignatureGenerator.Generate(oldBuild.Image, oldBuild.Rvas).File;
                var newBuild = FakeBuild.Create(textPadding: 0x40, dataShift: 0, rewriteSetCar: true, buildId: "NEW-BUILD");

                Ams2OffsetResolver.Resolve(newBuild.Image, Ams2Constants.DefaultImageBase, signatures, cachePath);
                var second = Ams2OffsetResolver.Resolve(newBuild.Image, Ams2Constants.DefaultImageBase, signatures, cachePath);

                Assert.AreEqual(Ams2OffsetSource.Scanned, second.Source);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        [TestMethod]
        public void SignatureFile_SaveAndLoad_RoundTrips()
        {
            var path = Path.Combine(Path.GetTempPath(), $"ams2_signatures_{Guid.NewGuid():N}.json");
            try
            {
                var build = FakeBuild.Create(textPadding: 0, dataShift: 0);
                var generated = Ams2SignatureGenerator.Generate(build.Image, build.Rvas).File;
                generated.Save(path);

                var loaded = Ams2SignatureFile.Load(path)!;

                Assert.AreEqual(generated.GeneratedFromBuild, loaded.GeneratedFromBuild);
                foreach (var name in Ams2OffsetNames.All)
                    Assert.AreEqual(build.Rvas[name], loaded.Signatures[name].Resolve(build.Image), name);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void BuiltIn_MatchesTheHandDerivedConstants()
        {
            var builtIn = Ams2ResolvedOffsets.BuiltIn(Ams2Constants.DefaultImageBase);

            Assert.AreEqual(Ams2Constants.BuiltIn.IntPropertyTypeTag, builtIn.Absolute(Ams2OffsetNames.IntPropertyTypeTag));
            Assert.AreEqual(Ams2Constants.BuiltIn.Vm498ContainerTag, builtIn.Absolute(Ams2OffsetNames.Vm498ContainerTag));
            Assert.AreEqual(Ams2Constants.BuiltIn.SetterRva, builtIn.Rva(Ams2OffsetNames.Setter));
            Assert.AreEqual(Ams2Constants.BuiltIn.GameplaySettingsPointerRva, builtIn.Rva(Ams2OffsetNames.GameplaySettingsPointer));
            Assert.IsFalse(builtIn.IsDegraded);
        }

        /// <summary>
        /// A fake AMS2AVX.exe code section: the three functions whose addresses matter, a decoy that
        /// shares the setter's prologue, and the instructions referencing each data value, laid out
        /// with real x64 encodings so the decoder, builder and resolver all run on them for real.
        /// </summary>
        private sealed class FakeBuild
        {
            private const long TextRva = 0x1000;
            private const long DataRva = 0x400000;

            public ModuleImage Image { get; private init; } = null!;
            public Dictionary<string, long> Rvas { get; private init; } = null!;

            public static FakeBuild Create(int textPadding, long dataShift, bool rewriteSetCar = false, string buildId = "OLD-BUILD")
            {
                var data = new Dictionary<string, long>
                {
                    [Ams2OffsetNames.IntPropertyTypeTag] = DataRva + 0x9000 + dataShift,
                    [Ams2OffsetNames.Vm498IdentityTag] = DataRva + 0x1B28 + dataShift,
                    [Ams2OffsetNames.Vm550IdentityTag] = DataRva + 0x1BE0 + dataShift,
                    [Ams2OffsetNames.Vm498ContainerTag] = DataRva + 0x2518 + dataShift,
                    [Ams2OffsetNames.GameplaySettingsPointer] = DataRva + 0x80000 + dataShift,
                    ["Elsewhere"] = DataRva + 0x50 + dataShift,
                };

                var asm = new Assembler(TextRva, data);
                asm.Pad(0x200 + textPadding);

                // decoy: same first 14 bytes as the setter, then different
                asm.Label("Decoy");
                asm.Emit("48 89 5C 24 08 57 48 83 EC 20 48 8B D9 33 C0 48 85 C9 74 08");
                asm.EmitRip("48 8D 05", Ams2OffsetNames.IntPropertyTypeTag);
                asm.Emit("48 89 03 48 83 C4 20 5F C3");
                asm.Pad(0x30);

                asm.Label(Ams2OffsetNames.Setter);
                asm.Emit("48 89 5C 24 08 57 48 83 EC 20 48 8B D9 8B FA");
                asm.EmitRip("48 8D 05", Ams2OffsetNames.IntPropertyTypeTag);
                asm.Emit("48 39 43 E8 75 0F 89 BB 8C 01 00 00");
                asm.EmitRel("E8", "Elsewhere");
                asm.Emit("48 8B 5C 24 30 48 83 C4 20 5F C3");
                asm.Pad(0x40);

                asm.Label(Ams2OffsetNames.SetCar);
                if (rewriteSetCar)
                {
                    asm.Emit("40 55 48 8B EC 48 83 EC 60 89 55 10 C7 45 F0 00 00 00 00 45 33 C9");
                }
                else
                {
                    asm.Emit("40 53 48 81 EC 80 00 00 00 C5 F8 77 44 8B 44 24 30 66 0F 1F 44 00 00");
                }
                asm.EmitRel("E8", Ams2OffsetNames.Commit);
                asm.Emit("85 C0");
                asm.EmitRel("0F 84", "Elsewhere");
                asm.Emit("48 81 C4 80 00 00 00 5B C3");
                asm.Pad(0x50);

                asm.Label(Ams2OffsetNames.Commit);
                asm.Emit("48 89 4C 24 08 48 83 EC 28");
                asm.EmitRip("48 8D 15", Ams2OffsetNames.Vm498IdentityTag);
                asm.Emit("48 89 51 08");
                asm.EmitRip("48 8D 15", Ams2OffsetNames.Vm550IdentityTag);
                asm.Emit("48 89 51 10");
                asm.EmitRip("48 8D 05", Ams2OffsetNames.Vm498ContainerTag);
                asm.Emit("48 89 01 C7 41 20 01 00 00 00 48 83 C4 28 C3");
                asm.Pad(0x30);

                asm.Label("ReadsSettings");
                asm.Emit("48 83 EC 28");
                asm.EmitRip("48 8B 05", Ams2OffsetNames.GameplaySettingsPointer);
                asm.Emit("48 85 C0 74 0A 8B 80 C0 D3 03 00 89 41 04 48 83 C4 28 C3");
                asm.Pad(0x100);

                var text = asm.Build();
                var rvas = new Dictionary<string, long>
                {
                    [Ams2OffsetNames.Setter] = asm.LabelRva(Ams2OffsetNames.Setter),
                    [Ams2OffsetNames.SetCar] = asm.LabelRva(Ams2OffsetNames.SetCar),
                    [Ams2OffsetNames.Commit] = asm.LabelRva(Ams2OffsetNames.Commit),
                };
                foreach (var name in Ams2OffsetNames.Data)
                    rvas[name] = data[name];

                return new FakeBuild
                {
                    Image = new ModuleImage(buildId, TextRva, text, DataRva + 0x100000),
                    Rvas = rvas
                };
            }
        }

        /// <summary>Lays out bytes and patches rel32/RIP-relative displacements once every label is placed.</summary>
        private sealed class Assembler
        {
            private readonly long _textRva;
            private readonly Dictionary<string, long> _data;
            private readonly List<byte> _bytes = new();
            private readonly Dictionary<string, int> _labels = new();
            private readonly List<(int DispAt, int InstructionEnd, string Target)> _fixups = new();

            public Assembler(long textRva, Dictionary<string, long> data)
            {
                _textRva = textRva;
                _data = data;
            }

            public void Pad(int count) => _bytes.AddRange(Enumerable.Repeat((byte)0xCC, count));

            public void Label(string name) => _labels[name] = _bytes.Count;

            public long LabelRva(string name) => _textRva + _labels[name];

            public void Emit(string hex) => _bytes.AddRange(X64LengthDecoderTests.Hex(hex));

            // opcode bytes followed by a rel32/disp32 that is the instruction's last field
            public void EmitRel(string opcodeHex, string target) => EmitWithDisp(opcodeHex, target);

            public void EmitRip(string opcodeAndModRmHex, string target) => EmitWithDisp(opcodeAndModRmHex, target);

            public byte[] Build()
            {
                var bytes = _bytes.ToArray();
                foreach (var (dispAt, end, target) in _fixups)
                {
                    long targetRva = _labels.TryGetValue(target, out var label) ? _textRva + label : _data[target];
                    BitConverter.GetBytes((int)(targetRva - (_textRva + end))).CopyTo(bytes, dispAt);
                }
                return bytes;
            }

            private void EmitWithDisp(string hex, string target)
            {
                Emit(hex);
                int dispAt = _bytes.Count;
                _bytes.AddRange(new byte[4]);
                _fixups.Add((dispAt, _bytes.Count, target));
            }
        }
    }
}
