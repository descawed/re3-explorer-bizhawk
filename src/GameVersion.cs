using BizHawk.Client.Common;

namespace Re3Explorer;

public abstract class GameVersion(IMemoryApi memory) {
    protected IMemoryApi Memory { get; } = memory;
    protected abstract byte[] RandTrackerPatch { get; }
    protected abstract uint RandTrackerPatchAddress { get; }
    protected abstract uint RandTrackerDataAddress { get; }
    protected abstract uint RandTrackerDataSize { get; }
    protected abstract uint RandFunctionAddress { get; }
    protected abstract uint RandValueAddress { get; }
    protected abstract uint ScriptRandOffsetIndexAddress { get; }
    protected abstract uint ScriptRandValueAddress { get; }
    protected abstract uint RandOffsetsAddress { get; }
    protected abstract uint ScriptRandAddress { get; }
    protected abstract uint RoomNumberAddress { get; }
    protected abstract uint StageNumberAddress { get; }

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
}