using System;
using System.Collections.Generic;
using System.IO;
using BizHawk.Client.Common;

namespace Re3Explorer;

public abstract class GameVersion(IMemoryApi memory) {
    public record Character(float X, float Y, float Z, short Health);

    private const int MaxEnemies = 40;
    
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
    
    protected abstract uint PlayerXAddress { get; }
    protected abstract uint PlayerYAddress { get; }
    protected abstract uint PlayerZAddress { get; }
    protected abstract uint PlayerHealthAddress { get; }
    
    protected abstract uint EnemyListAddress { get; }
    protected abstract uint EnemyListEndAddress { get; }

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
    
    private float PlayerX => Memory.ReadS32(PlayerXAddress) / 4096f;
    
    private float PlayerY => Memory.ReadS32(PlayerYAddress) / 4096f;
    
    private float PlayerZ => Memory.ReadS32(PlayerZAddress) / 4096f;
    
    private short PlayerHealth => (short)Memory.ReadS16(PlayerHealthAddress);

    public void ApplyRandTrackingPatch() {
        _randTrackingOverwriteValue = ApplyTrackingPatch(RandFunctionAddress, RandTrackerPatchAddress, RandTrackerPatch, RandTrackerDataAddress, RandTrackerDataSize);
    }
    
    public Character Player => new(PlayerX, PlayerY, PlayerZ, PlayerHealth);

    public IList<Character> Enemies {
        get {
            var enemies = new List<Character>();

            var enemyListEnd = Memory.ReadU32(EnemyListEndAddress);
            for (var i = 0; i < MaxEnemies; i++) {
                var enemyPtr = Memory.ReadU32(EnemyListAddress + i * 4);
                if (enemyPtr == enemyListEnd) {
                    continue;
                }

                enemyPtr &= 0x3fffff;
                var x = Memory.ReadS32(enemyPtr + 0x88) / 4096f;
                var y = Memory.ReadS32(enemyPtr + 0x8C) / 4096f;
                var z = Memory.ReadS32(enemyPtr + 0x90) / 4096f;
                var health = (short)Memory.ReadS16(enemyPtr + 0xCC);
                
                enemies.Add(new Character(x, y, z, health));
            }

            return enemies;
        }
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