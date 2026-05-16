using System.Collections.Concurrent;
using EqZero.Shared.Logging;
using EqZero.Shared.Rpc;

namespace EqZero.Server.Gcc;

/// <summary>Manages every <see cref="GccPlayer"/> alive inside this gcc process.</summary>
public sealed class GccPlayerMgr : IRpcRoute
{
    private readonly ConcurrentDictionary<long, GccPlayer> _players = new();

    /// <summary>Called via RPC from login when a new user signs in.</summary>
    public void CreatePlayer(long playerId, string userName)
    {
        var p = new GccPlayer(playerId, userName);
        if (_players.TryAdd(playerId, p))
            Log.Info($"[gcc] CreatePlayer id={playerId} user={userName}");
        else
            Log.Warn($"[gcc] CreatePlayer id={playerId} already exists");
    }

    public GccPlayer? Get(long playerId) => _players.TryGetValue(playerId, out var p) ? p : null;

    public object? RRpcGet(string name, object?[] args) => name switch
    {
        "RRpcGetPlayer"    => Get(System.Convert.ToInt64(args[0])),
        "RRpcGetGccPlayer" => Get(System.Convert.ToInt64(args[0])),
        _ => null,
    };
}
