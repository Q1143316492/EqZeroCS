using System;
using EqZero.Shared.Logging;
using EqZero.Shared.Rpc;

namespace EqZero.Client;

/// <summary>
/// Client-side RPC root. Server (gcc) calls back via
/// <c>RRpcGetClient().OnEnterOk(...)</c> and <c>OnPong(...)</c>.
/// </summary>
public sealed class ClientObj : IRpcRoute
{
    private readonly ClientApp _app;

    public ClientObj(ClientApp app) => _app = app;

    public object? RRpcGet(string name, object?[] args) => name switch
    {
        "RRpcGetClient" => this,
        _ => null,
    };

    public void OnEnterOk(long playerId, string echo)
    {
        Log.Info($"[client] OnEnterOk player_id={playerId} echo=\"{echo}\"");
        _app.NotifyEntered(playerId);
    }

    public void OnPong(long seq, string text)
    {
        Log.Info($"[client] OnPong seq={seq} text=\"{text}\"");
        _app.NotifyPong(seq);
    }
}
