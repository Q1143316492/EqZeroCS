using System;
using EqZero.Server.Framework;
using EqZero.Shared.Logging;
using EqZero.Shared.Net;
using EqZero.Shared.Rpc;

namespace EqZero.Server.Gcc;

/// <summary>
/// gcc process. Hosts <see cref="GccPlayer"/> objects and accepts RPC chains
/// such as <c>RRpcGetGcc(1).RRpcGetGccPlayer(id).OnHello("...")</c>.
/// </summary>
public sealed class GccApp : ServerAppBase, IRpcRoute
{
    public GccPlayerMgr PlayerMgr { get; } = new();

    private readonly RpcDispatcher _dispatcher;

    public GccApp(string name) : base(name)
    {
        _dispatcher = new RpcDispatcher(() => this);
    }

    protected override void OnMessage(TcpConnection conn, Message msg)
    {
        if (msg.Flag == MessageFlag.RRpcCall)
            _dispatcher.Dispatch(conn, msg);
        else
            Log.Info($"{Tag} recv non-rpc: {msg}");
    }

    public object? RRpcGet(string name, object?[] args) => name switch
    {
        "RRpcGetGcc"          => this,
        "RRpcGetGccPlayerMgr" => PlayerMgr,
        "RRpcGetGccPlayer"    => args.Length > 0 ? PlayerMgr.Get(Convert.ToInt64(args[0])) : null,
        _ => null,
    };
}
