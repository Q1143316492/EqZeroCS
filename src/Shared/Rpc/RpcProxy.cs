using System;
using System.Collections.Generic;
using System.Dynamic;
using EqZero.Shared.Net;

namespace EqZero.Shared.Rpc;

/// <summary>
/// Dynamic proxy that mirrors framwork/rpc_wrapper.py:RpcWrapperBase.
///
/// Usage:
///     dynamic gcc = new RpcProxy(conn, define);
///     gcc.RRpcGetGcc(1).RRpcGetGccPlayer(playerId).OnHello("hi from client");
///
/// Each <c>RRpcGet*</c> call returns a new proxy carrying the accumulated chain.
/// The first non-<c>RRpcGet</c> call is terminal: it packages the whole chain
/// into a single message and sends it through the underlying connection.
/// </summary>
public sealed class RpcProxy : DynamicObject
{
    private readonly TcpConnection _conn;
    private readonly object?[] _rpcDefine;
    private readonly IReadOnlyList<object?[]> _chain;

    public RpcProxy(TcpConnection conn,
                    string fromProcess = "", string fromGlobalId = "",
                    string toProcess = "",   string toGlobalId = "")
        : this(conn, new object?[] { fromProcess, fromGlobalId, toProcess, toGlobalId }, Array.Empty<object?[]>())
    { }

    private RpcProxy(TcpConnection conn, object?[] rpcDefine, IReadOnlyList<object?[]> chain)
    {
        _conn = conn;
        _rpcDefine = rpcDefine;
        _chain = chain;
    }

    /// <summary>Create a proxy with an updated rpc-define 4-tuple but the same connection.</summary>
    public RpcProxy WithDefine(string fromProcess, string fromGlobalId, string toProcess, string toGlobalId)
        => new(_conn, new object?[] { fromProcess, fromGlobalId, toProcess, toGlobalId }, Array.Empty<object?[]>());

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        var entry = new object?[] { binder.Name, args ?? Array.Empty<object>() };
        var newChain = new List<object?[]>(_chain.Count + 1);
        newChain.AddRange(_chain);
        newChain.Add(entry);

        if (binder.Name.StartsWith(RpcProtocol.RRpcGetPrefix, StringComparison.Ordinal))
        {
            result = new RpcProxy(_conn, _rpcDefine, newChain);
            return true;
        }

        // Terminal call — fire and forget; awaiting would change call-site semantics.
        var payload = RpcMessage.Build(_rpcDefine, newChain);
        _ = _conn.SendAsync(payload, MessageFlag.RRpcCall);
        result = null;
        return true;
    }
}
