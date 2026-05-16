using System.Threading;
using EqZero.Shared.Net;

namespace EqZero.Shared.Rpc;

/// <summary>
/// Per-call context made available to terminal RPC handlers (via <see cref="Current"/>)
/// so they can issue a reply back to the original caller.
/// Set by <see cref="RpcDispatcher"/> around the terminal invocation.
/// </summary>
public sealed class RpcCallContext
{
    private static readonly AsyncLocal<RpcCallContext?> s_current = new();

    /// <summary>Current call's context, or null if not inside an RPC terminal handler.</summary>
    public static RpcCallContext? Current
    {
        get => s_current.Value;
        set => s_current.Value = value;
    }

    /// <summary>The inbound TCP connection the RPC arrived on.</summary>
    public TcpConnection Conn { get; }

    /// <summary>The 4-tuple [fromProcess, fromGlobalId, toProcess, toGlobalId].</summary>
    public object?[] Define { get; }

    public RpcCallContext(TcpConnection conn, object?[] define)
    {
        Conn = conn;
        Define = define;
    }

    /// <summary>
    /// Build a proxy that fires RPCs back to the original caller on the same connection,
    /// with the from/to fields of the rpcDefine flipped.
    /// </summary>
    public RpcProxy ReplyProxy()
    {
        var fromP = Define.Length > 0 ? Define[0]?.ToString() ?? string.Empty : string.Empty;
        var fromG = Define.Length > 1 ? Define[1]?.ToString() ?? string.Empty : string.Empty;
        var toP   = Define.Length > 2 ? Define[2]?.ToString() ?? string.Empty : string.Empty;
        var toG   = Define.Length > 3 ? Define[3]?.ToString() ?? string.Empty : string.Empty;
        return new RpcProxy(Conn,
            fromProcess: toP, fromGlobalId: toG,
            toProcess:   fromP, toGlobalId:  fromG);
    }
}
