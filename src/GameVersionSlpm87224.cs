using BizHawk.Client.Common;

namespace Re3Explorer;

public class GameVersionSlpm87224(IMemoryApi memory) : GameVersion(memory) {
    // FWIW this won't match the hash of the disc file because BizHawk only does a partial hash
    public const string Hash = "B37AB196";
    
    // Nymashock currently doesn't support the memory event API, so we have to get creative and make the game track
    // RNG for us
    protected override byte[] RandTrackerPatch => [
        0x01, 0x80, 0x04, 0x3C, //  lui $a0, 0x8001
        0xA8, 0x12, 0x84, 0x34, //  ori $a0, $a0, 0x12A8
                                // check:
        0x00, 0x00, 0x82, 0x8C, //  lw $v0, 0($a0)
        0x00, 0x00, 0x00, 0x00, //  nop
        0xFD, 0xFF, 0x40, 0x14, //  bnez $v0, check
        0x04, 0x00, 0x84, 0x24, //  addiu $a0, 4 ; no bounds check to limit our instructions and registers - should have enough
        0xFC, 0xFF, 0x9F, 0xAC, //  sw $ra, -4($a0)
        0x09, 0x80, 0x03, 0x3C, //  lui $v1, 0x8009
        0xBC, 0x40, 0x00, 0x08, //  j 0x800102F0
        0x28, 0xA9, 0x63, 0x34, //  ori $v1, $v1, 0xA928
    ];

    // addresses are relative to the start of main RAM
    protected override uint RandTrackerPatchAddress => 0x083510; // CdComstr

    protected override uint RandTrackerDataAddress => 0x0112A8;

    protected override uint RandTrackerDataSize => 332;

    protected override uint RandFunctionAddress => 0x0102E8;

    protected override uint RandValueAddress => 0x09A928;

    protected override uint ScriptRandOffsetIndexAddress => 0x0E142E;

    protected override uint ScriptRandValueAddress => 0x0D3222;

    protected override uint RandOffsetsAddress => 0x09A950;

    protected override uint ScriptRandAddress => 0x052F2C;

    protected override uint RoomNumberAddress => 0x0D3218;

    protected override uint StageNumberAddress => 0x0D3216;
}