using Ams2Interop;
using Ams2Interop.Native;

namespace AMS2ChEd.Tests.Ams2Interop
{
    [TestClass]
    public class Ams2InteropSettingsBlockTests
    {
        private const uint Italy = 0x11111111, Brazil = 0x22222222, Germany = 0x33333333;
        private static readonly IReadOnlySet<uint> Known = new HashSet<uint> { Italy, Brazil, Germany };
        private const int Shift = 0x48;

        [TestMethod]
        public void Resolve_ConstantsValid_UsedDirectly()
        {
            var game = new FakeSettingsBlock();
            game.PlaceIdentity(Ams2Constants.PlayerIdentityOffset.Country, Italy);
            game.PlaceWear(Ams2Constants.GameplaySettingsOffset.TyreWear, 6, Ams2Constants.GameplaySettingsOffset.FuelUsage, 6);
            var log = new List<string>();

            var layout = SettingsBlockLayout.Resolve(game.Mem, FakeSettingsBlock.Block, log.Add);

            Assert.AreEqual(Ams2Constants.PlayerIdentityOffset.Country, layout.CountryOffset);
            Assert.AreEqual(Ams2Constants.PlayerIdentityOffset.FullName, layout.FullNameOffset);
            Assert.AreEqual(Ams2Constants.GameplaySettingsOffset.TyreWear, layout.TyreWearOffset);
            Assert.AreEqual(Ams2Constants.GameplaySettingsOffset.FuelUsage, layout.FuelUsageOffset);
            Assert.IsTrue(layout.WearResolved);
            Assert.IsFalse(log.Any(l => l.Contains("scanning")), string.Join("\n", log));
        }

        [TestMethod]
        public void Resolve_BlockShifted_ScanFindsIdentity_AndWearFollowsIt()
        {
            var game = new FakeSettingsBlock();
            int country = Ams2Constants.PlayerIdentityOffset.Country + Shift;
            game.PlaceIdentity(country, Italy);
            game.PlaceWear(Ams2Constants.GameplaySettingsOffset.TyreWear + Shift, 6, Ams2Constants.GameplaySettingsOffset.FuelUsage + Shift, 5);
            var log = new List<string>();

            var layout = SettingsBlockLayout.Resolve(game.Mem, FakeSettingsBlock.Block, log.Add);

            Assert.AreEqual(country, layout.CountryOffset);
            Assert.AreEqual(Ams2Constants.GameplaySettingsOffset.TyreWear + Shift, layout.TyreWearOffset);
            Assert.AreEqual(Ams2Constants.GameplaySettingsOffset.FuelUsage + Shift, layout.FuelUsageOffset);
            Assert.IsTrue(layout.WearResolved);
            Assert.IsTrue(log.Any(l => l.Contains("settings block moved: identity +0x48")), string.Join("\n", log));

            // and the writers use the moved offsets
            var settings = new GameplaySettings(game.Mem, game.Mem, game.Access());
            Assert.IsTrue(settings.TryApply(3, 3, out var original));
            Assert.AreEqual((6, 5), (original.TyreWearIndex, original.FuelUsageIndex));
            CollectionAssert.AreEquivalent(
                new[] { FakeSettingsBlock.Block + layout.TyreWearOffset, FakeSettingsBlock.Block + layout.FuelUsageOffset },
                game.Mem.RawWrites.Select(w => w.Address).ToArray());
        }

        [TestMethod]
        public void Resolve_TwoCandidateRuns_Unresolved()
        {
            var game = new FakeSettingsBlock();
            game.PlaceIdentity(Ams2Constants.PlayerIdentityOffset.Country + Shift, Italy);
            game.PlaceIdentity(0x1000, Brazil);

            var layout = SettingsBlockLayout.Resolve(game.Mem, FakeSettingsBlock.Block);

            Assert.IsFalse(layout.IdentityResolved);
            Assert.IsFalse(layout.WearResolved);
            Assert.IsFalse(new PlayerIdentityWriter(game.Mem, game.Mem, game.Access(), game.Allocate).IsAvailable);
        }

        [TestMethod]
        public void Resolve_WearOutOfRangeAtDerivedOffsets_WearOff_IdentityStillOn()
        {
            var game = new FakeSettingsBlock();
            game.PlaceIdentity(Ams2Constants.PlayerIdentityOffset.Country, Italy);
            game.PlaceWear(Ams2Constants.GameplaySettingsOffset.TyreWear, 9, Ams2Constants.GameplaySettingsOffset.FuelUsage, 6);

            var access = game.Access();
            var settings = new GameplaySettings(game.Mem, game.Mem, access);
            var identity = new PlayerIdentityWriter(game.Mem, game.Mem, access, game.Allocate);

            Assert.IsFalse(settings.IsAvailable);
            Assert.IsFalse(settings.TryApply(3, 3, out _));
            Assert.AreEqual(0, game.Mem.RawWrites.Count);
            Assert.IsTrue(identity.IsAvailable);
            Assert.IsTrue(identity.TryApply(new PlayerIdentity("Ayrton", "Senna", "SEN", Brazil), out _, Known));
        }

        [TestMethod]
        public void ApplyIdentity_UnknownStoredCountry_Refused()
        {
            var game = new FakeSettingsBlock();
            game.PlaceIdentity(Ams2Constants.PlayerIdentityOffset.Country, 0x7EADBEEF);
            var writer = new PlayerIdentityWriter(game.Mem, game.Mem, game.Access(), game.Allocate);

            Assert.IsFalse(writer.TryApply(new PlayerIdentity("Ayrton", "Senna", "SEN", Brazil), out _, Known));
            Assert.AreEqual(0, game.Mem.RawWrites.Count);
            Assert.AreEqual(0, game.Allocations);
        }

        [TestMethod]
        public void ApplyIdentity_ZeroStoredCountry_RefusedEvenWithoutAKnownSet()
        {
            var game = new FakeSettingsBlock();
            game.PlaceIdentity(Ams2Constants.PlayerIdentityOffset.Country, 0);
            var writer = new PlayerIdentityWriter(game.Mem, game.Mem, game.Access(), game.Allocate);

            Assert.IsFalse(writer.TryApply(new PlayerIdentity("Ayrton", "Senna", "SEN", Brazil), out _));
            Assert.AreEqual(0, game.Mem.RawWrites.Count);
        }

        [TestMethod]
        public void ApplyIdentity_ThenRestore_UsesTheApplyTimeOffsets()
        {
            var game = new FakeSettingsBlock();
            int country = Ams2Constants.PlayerIdentityOffset.Country + Shift;
            game.PlaceIdentity(country, Italy, "Max", "Player", "PLA");

            var applier = new PlayerIdentityWriter(game.Mem, game.Mem, game.Access(), game.Allocate);
            Assert.IsTrue(applier.TryApply(new PlayerIdentity("Ayrton", "Senna", "SEN", Brazil), out var original, Known));
            Assert.AreEqual(country, original.LayoutAnchor);
            game.Mem.RawWrites.Clear();

            // a later attach restores through a fresh writer, as Ams2RaceLaunchAssistant does
            var restorer = new PlayerIdentityWriter(game.Mem, game.Mem, game.Access(), game.Allocate);
            Assert.IsTrue(restorer.TryRestore(original, Known));

            var block = FakeSettingsBlock.Block;
            var addresses = game.Mem.RawWrites.Select(w => w.Address).ToHashSet();
            Assert.IsTrue(addresses.Contains(block + country), "Country at the apply-time offset");
            for (int i = 1; i <= 4; i++)
                Assert.IsTrue(addresses.Contains(block + country + i * 8), $"name field {i} at the apply-time offset");
            Assert.IsFalse(addresses.Contains(block + Ams2Constants.PlayerIdentityOffset.Country));
            var now = restorer.Read()!;
            Assert.IsTrue(now.SameValues(new PlayerIdentity("Max", "Player", "PLA", Italy)));
        }

        [TestMethod]
        public void Restore_LayoutNowResolvesElsewhere_Refused()
        {
            var game = new FakeSettingsBlock();
            game.PlaceIdentity(Ams2Constants.PlayerIdentityOffset.Country + Shift, Italy);
            game.PlaceWear(Ams2Constants.GameplaySettingsOffset.TyreWear + Shift, 6, Ams2Constants.GameplaySettingsOffset.FuelUsage + Shift, 6);
            var stale = new PlayerIdentity("Max", "Player", "PLA", Italy) { LayoutAnchor = Ams2Constants.PlayerIdentityOffset.Country };
            var staleWear = new WearSettingsSnapshot(4, 4) { LayoutAnchor = Ams2Constants.PlayerIdentityOffset.Country };

            var access = game.Access();
            Assert.IsFalse(new PlayerIdentityWriter(game.Mem, game.Mem, access, game.Allocate).TryRestore(stale, Known));
            Assert.IsFalse(new GameplaySettings(game.Mem, game.Mem, access).TryRestore(staleWear));
            Assert.AreEqual(0, game.Mem.RawWrites.Count);
        }

        [TestMethod]
        public void BlockPointerChangedAfterResolve_NothingTouched()
        {
            var game = new FakeSettingsBlock();
            game.PlaceIdentity(Ams2Constants.PlayerIdentityOffset.Country, Italy);
            game.PlaceWear(Ams2Constants.GameplaySettingsOffset.TyreWear, 6, Ams2Constants.GameplaySettingsOffset.FuelUsage, 6);
            var access = game.Access();
            var settings = new GameplaySettings(game.Mem, game.Mem, access);
            Assert.IsTrue(settings.IsAvailable);

            game.Mem.PutInt64(FakeSettingsBlock.PointerAddress, FakeSettingsBlock.Block + 0x10);

            Assert.IsFalse(settings.TryApply(3, 3, out _));
            Assert.AreEqual(0, game.Mem.RawWrites.Count);
        }

        /// <summary>A settings block in fake memory, with AMS2-layout strings it can point at.</summary>
        private sealed class FakeSettingsBlock
        {
            public const long PointerAddress = 0x1_4000_0000;
            public const long Block = 0x5_0000_0000;
            private const long Strings = 0x6_0000_0000;
            private const long Heap = 0x6_8000_0000;
            private long _nextString = Strings;
            private long _nextAllocation = Heap;

            public FakeMemory Mem { get; } = new();
            public int Allocations { get; private set; }

            public FakeSettingsBlock()
            {
                Mem.Map(PointerAddress, 0x100);
                Mem.PutInt64(PointerAddress, Block);
                Mem.Map(Block, SettingsBlockLayout.ScanWindow + 0x1000);
                Mem.Map(Strings, 0x10000);
                Mem.Map(Heap, 0x10000);
            }

            public SettingsBlockAccess Access() => new(Mem, PointerAddress);

            public long? Allocate(int size)
            {
                Allocations++;
                var address = _nextAllocation;
                _nextAllocation += (size + 0xFF) & ~0xFF;
                return address;
            }

            public void PlaceIdentity(int countryOffset, uint country, string forename = "Max", string surname = "Player", string handle = "PLA")
            {
                Mem.PutInt32(Block + countryOffset, unchecked((int)country));
                var names = new[] { forename, surname, handle, $"{forename} {surname}" };
                for (int i = 0; i < names.Length; i++)
                    Mem.PutInt64(Block + countryOffset + 8 + i * 8, NewString(names[i]));
            }

            public void PlaceWear(int tyreOffset, int tyre, int fuelOffset, int fuel)
            {
                Mem.PutInt32(Block + tyreOffset, tyre);
                Mem.PutInt32(Block + fuelOffset, fuel);
            }

            private long NewString(string text)
            {
                var bytes = Ams2String.Build(0x1_4000_0080, text);
                var at = _nextString;
                _nextString += (bytes.Length + 0xF) & ~0xF;
                Mem.Put(at, bytes);
                return at + Ams2String.HeaderSize;
            }
        }
    }
}
