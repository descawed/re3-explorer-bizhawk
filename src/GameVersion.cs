using System.Collections.Generic;
using System.IO;
using BizHawk.Client.Common;

namespace Re3Explorer;

public abstract class GameVersion(IMemoryApi memory) {
    private IMemoryApi Memory { get; } = memory;
    protected abstract byte[] RandTrackerPatch { get; }
    protected abstract uint RandTrackerPatchAddress { get; }
    protected abstract uint RandTrackerDataAddress { get; }
    protected abstract uint RandTrackerDataSize { get; }
    protected abstract uint RandFunctionAddress { get; }
    protected abstract uint RandValueAddress { get; }
    protected abstract uint ScriptRandOffsetIndexAddress { get; }
    protected abstract uint ScriptRandValueAddress { get; }
    protected abstract uint RandOffsetsAddress { get; }
    public abstract uint ScriptRandAddress { get; }
    protected abstract uint RoomNumberAddress { get; }
    protected abstract uint StageNumberAddress { get; }

    private uint _randTrackingOverwriteValue;

    public string RoomId {
        get {
            var stage = Memory.ReadByte(StageNumberAddress);
            var room = Memory.ReadByte(RoomNumberAddress);
            return $"{stage + 1}{room:X02}";
        }
    }

    public ushort RngState => (ushort)Memory.ReadU16(RandValueAddress);

    public byte ScriptRngOffsetIndex => (byte)Memory.ReadU8(ScriptRandOffsetIndexAddress);
    
    public byte ScriptRngOffset => (byte)Memory.ReadU8(RandOffsetsAddress + ScriptRngOffsetIndex);

    public ushort ScriptRngState => (ushort)Memory.ReadU16(ScriptRandValueAddress);

    public void ApplyRandTrackingPatch() {
        _randTrackingOverwriteValue = ApplyTrackingPatch(RandFunctionAddress, RandTrackerPatchAddress, RandTrackerPatch, RandTrackerDataAddress, RandTrackerDataSize);
    }

    public IList<uint> GetRandCalls() {
        var callers = new List<uint>();

        var dataEnd = RandTrackerDataAddress + RandTrackerDataSize;
        if (Memory.ReadU32(dataEnd) != _randTrackingOverwriteValue) {
            throw new InvalidDataException("Rand call tracker overflowed!");
        }
        
        for (var address = RandTrackerDataAddress; address < dataEnd; address += 4) {
            var caller = Memory.ReadU32(address);
            if (caller == 0) {
                break;
            }
            
            callers.Add(caller);
        }

        // reset data block
        Memory.WriteByteRange(RandTrackerDataAddress, new byte[RandTrackerDataSize]);
        
        return callers;
    }

    private uint ApplyTrackingPatch(uint jumpAddress, uint patchAddress, byte[] patch, uint dataAddress, uint dataSize) {
        Memory.WriteU32(jumpAddress, 0x08000000 | (patchAddress >> 2));
        Memory.WriteByteRange(patchAddress, patch);
        Memory.WriteByteRange(dataAddress, new byte[dataSize]);
        // return the value past the end of the data block for detection of out of bounds writes
        return Memory.ReadU32(dataAddress + dataSize);
    }
}