using Ams2Interop;
using Ams2Interop.Native;

namespace AMS2ChEd.Tests.Ams2Interop
{
    /// <summary>
    /// Synthetic process memory: mapped regions of bytes. Reads outside a mapped region fail, like
    /// ReadProcessMemory on an unmapped page. Writes made through IMemoryWriter are recorded (the
    /// "fake writer"); setup writes through Put* aren't.
    /// </summary>
    internal sealed class FakeMemory : IScannableMemory, IMemoryWriter
    {
        private readonly List<(long Base, byte[] Bytes)> _regions = new();

        public List<(long Address, byte[] Bytes)> RawWrites { get; } = new();

        public void Map(long address, int size) => _regions.Add((address, new byte[size]));

        public IEnumerable<(long Base, long Size)> ReadableRegions() => _regions.Select(r => (r.Base, (long)r.Bytes.Length)).OrderBy(r => r.Base).ToList();

        public void PutInt64(long address, long value) => Put(address, BitConverter.GetBytes(value));
        public void PutInt32(long address, int value) => Put(address, BitConverter.GetBytes(value));

        public void Put(long address, byte[] bytes)
        {
            if (!TryFind(address, bytes.Length, out var region, out var offset))
                throw new InvalidOperationException($"0x{address:X} isn't mapped");
            bytes.CopyTo(region, offset);
        }

        public bool ReadRaw(long address, int length, out byte[] buffer)
        {
            buffer = new byte[length];
            if (!TryFind(address, length, out var region, out var offset)) return false;
            Array.Copy(region, offset, buffer, 0, length);
            return true;
        }

        public bool TryReadInt64(long address, out long value)
        {
            value = 0;
            if (!ReadRaw(address, 8, out var b)) return false;
            value = BitConverter.ToInt64(b, 0);
            return true;
        }

        public bool TryReadInt32(long address, out int value)
        {
            value = 0;
            if (!ReadRaw(address, 4, out var b)) return false;
            value = BitConverter.ToInt32(b, 0);
            return true;
        }

        public bool TryWriteInt32(long address, int value) => WriteRaw(address, BitConverter.GetBytes(value));

        public bool WriteRaw(long address, byte[] buffer)
        {
            RawWrites.Add((address, buffer));
            if (!TryFind(address, buffer.Length, out var region, out var offset)) return false;
            buffer.CopyTo(region, offset);
            return true;
        }

        private bool TryFind(long address, int length, out byte[] region, out int offset)
        {
            foreach (var (start, bytes) in _regions)
            {
                if (address >= start && address + length <= start + bytes.Length)
                {
                    region = bytes;
                    offset = (int)(address - start);
                    return true;
                }
            }
            region = Array.Empty<byte>();
            offset = 0;
            return false;
        }
    }

    /// <summary>Builds Custom Race objects in a FakeMemory: an object is an array of node pointers; each node carries the tag at -0x18 and its value at +0x18c.</summary>
    internal sealed class FakeCustomRace
    {
        public const long Tag = 0x141FF1658;
        private long _nextObject = 0x2_0000_0000;
        private long _nextNode = 0x3_0000_0000;

        public FakeMemory Mem { get; } = new();

        public long NewObject()
        {
            long address = _nextObject;
            _nextObject += 0x10000;
            Mem.Map(address, 0x1000);
            return address;
        }

        public long SetSlot(long obj, int slot, int value, long tag = Tag)
        {
            long region = _nextNode;
            _nextNode += 0x1000;
            Mem.Map(region, 0x400);
            long node = region + 0x40;
            Mem.PutInt64(obj + slot * 8L, node);
            Mem.PutInt64(node - 0x18, tag);
            Mem.PutInt32(node + 0x18c, value);
            return node;
        }

        public void RemoveSlot(long obj, int slot) => Mem.PutInt64(obj + slot * 8L, 0);

        public long NewVm550(int hour = 12, int day = 15, int month = 6, int year = 2026)
        {
            var vm = NewObject();
            SetSlot(vm, Ams2Constants.Vm550Slot.WeatherSlotCount, 1);
            SetSlot(vm, Ams2Constants.Vm550Slot.WeatherSlot1, 0);
            SetSlot(vm, Ams2Constants.Vm550Slot.WeatherSlot2, 0);
            SetSlot(vm, Ams2Constants.Vm550Slot.WeatherSlot3, 0);
            SetSlot(vm, Ams2Constants.Vm550Slot.WeatherSlot4, 17);
            SetSlot(vm, Ams2Constants.Vm550Slot.Hour, hour);
            SetSlot(vm, Ams2Constants.Vm550Slot.Day, day);
            SetSlot(vm, Ams2Constants.Vm550Slot.Month, month);
            SetSlot(vm, Ams2Constants.Vm550Slot.Year, year);
            SetSlot(vm, Ams2Constants.Vm550Slot.DurationTypeFlag, 1);
            SetSlot(vm, Ams2Constants.Vm550Slot.RollingStart, 0);
            SetSlot(vm, Ams2Constants.Vm550Slot.MinTyres, -1);
            SetSlot(vm, Ams2Constants.Vm550Slot.MinFuel, 4);
            SetSlot(vm, Ams2Constants.Vm550Slot.Fcy, 0);
            return vm;
        }

        /// <param name="packedDate">The raw VM498 packed date at +0x188 (default: 2026-06-15 with one flag bit set).</param>
        public long NewVm498(int skill = 100, int rivalCount = 20, uint? packedDate = null)
        {
            var vm = NewObject();
            SetSlot(vm, Ams2Constants.Vm498Slot.NumOpponentsType, 1);
            SetSlot(vm, Ams2Constants.Vm498Slot.OpponentsTypeKind, 1);
            SetSlot(vm, Ams2Constants.Vm498Slot.Skill, skill);
            SetSlot(vm, Ams2Constants.Vm498Slot.WetWeatherSkill, 50);
            SetSlot(vm, Ams2Constants.Vm498Slot.MistakeFrequency, 2);
            SetSlot(vm, Ams2Constants.Vm498Slot.RivalCount, rivalCount);
            Mem.PutInt32(vm + Ams2Constants.Vm498DatePackedOffset,
                unchecked((int)(packedDate ?? Vm498PackedDate.Encode(2026, 6, 15, 0x02000000u))));
            return vm;
        }

        public long NewMaster()
        {
            var master = NewObject();
            SetSlot(master, Ams2Constants.MasterSlot.Track, 123456);
            SetSlot(master, Ams2Constants.MasterSlot.Car, 654321);
            SetSlot(master, Ams2Constants.MasterSlot.Livery, 3);
            return master;
        }
    }

    /// <summary>Records every setter call instead of calling into a game; reports each as landed.</summary>
    internal sealed class FakeSlotSetter : ISlotSetter
    {
        public List<(long Vm, int Slot, int Value)> Calls { get; } = new();

        public bool TrySetSlot(long vmBase, int slot, int value, out int actualValue)
        {
            Calls.Add((vmBase, slot, value));
            actualValue = value;
            return true;
        }
    }
}
