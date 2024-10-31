using System;
using System.Collections.Generic;
using BizHawk.Client.Common;

namespace Re3Explorer;

public class GameVersions {
    private readonly Dictionary<string, Func<IMemoryApi, GameVersion>> _games = new() {
        { GameVersionSlpm87224.Hash, memory => new GameVersionSlpm87224(memory) },
    };

    public GameVersion Get(string hash, IMemoryApi mem) {
        return _games[hash].Invoke(mem);
    }
}