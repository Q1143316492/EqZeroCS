using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using EqZero.Server.Framework;
using EqZero.Shared.Logging;
using EqZero.Shared.Net;
using EqZero.Shared.Rpc;

namespace EqZero.Server.Gate;

/// <summary>
/// Front-door proxy for clients. Holds long-lived outbound connections to each
/// backend process type (Gas / Gcc / Ats) and forwards RPC frames in both
/// directions:
///   • client → backend: route by chain[0] = RRpcGet&lt;Type&gt; → matching backend conn.
///                       Side-effect: register clientByGid[fromGlobalId] = clientConn.
///   • backend → client: route by chain[0] = "RRpcGetClient" + rpcDefine.toGlobalId
///                       → look up clientByGid and forward to that client.
/// </summary>
public sealed class GateApp : ServerAppBase
{
    private const string ClientRouteKey = "Client";

    /// <summary>Map of route key (e.g. "Gcc") → connection to backend.</summary>
    private readonly Dictionary<string, TcpConnection> _outbound = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Process name we forward to per route key — for logging only.</summary>
    private readonly Dictionary<string, string> _outboundName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Map of client global-id (player id as string) → client TCP connection.</summary>
    private readonly ConcurrentDictionary<string, TcpConnection> _clientByGid = new();

    public GateApp(string name) : base(name) { }

    protected override async Task OnInitAsync()
    {
        await DialBackend("Gcc", "gcc1");
        await DialBackend("Gas", "gas1");
        await DialBackend("Ats", "ats1");
    }

    private async Task DialBackend(string routeKey, string processName)
    {
        var ep = ServerRegistry.Get(processName);
        var conn = await TcpDialer.ConnectWithRetryAsync(ep.Ip, ep.Port, processName);
        _outbound[routeKey] = conn;
        _outboundName[routeKey] = processName;
        // Subscribe so backend → client replies flow through the same routing logic.
        conn.MessageReceived += OnBackendMessage;
    }

    /// <summary>Inbound from a client (accepted connection).</summary>
    protected override void OnMessage(TcpConnection conn, Message msg)
    {
        if (msg.Flag != MessageFlag.RRpcCall)
        {
            Log.Info($"{Tag} non-rpc from conn#{conn.Id}: {msg}");
            return;
        }

        var chain = RpcMessage.ExtractChain(msg);
        if (chain.Count == 0)
        {
            Log.Warn($"{Tag} dropping rpc with empty chain");
            return;
        }

        var firstName = chain[0].Name;
        if (!firstName.StartsWith(RpcProtocol.RRpcGetPrefix, StringComparison.Ordinal))
        {
            Log.Warn($"{Tag} dropping rpc — first step is terminal: {firstName}");
            return;
        }

        var routeKey = firstName.Substring(RpcProtocol.RRpcGetPrefix.Length);
        if (!_outbound.TryGetValue(routeKey, out var outConn))
        {
            Log.Warn($"{Tag} no backend for route '{routeKey}'");
            return;
        }

        // Remember which client connection owns this player id so we can route
        // future backend → client replies back to the right socket.
        var define = RpcMessage.ExtractDefine(msg);
        var fromGid = define.Length > 1 ? define[1]?.ToString() : null;
        if (!string.IsNullOrEmpty(fromGid))
            _clientByGid[fromGid!] = conn;

        Log.Info($"{Tag} forward conn#{conn.Id} → {_outboundName[routeKey]} (head={firstName})");
        _ = outConn.SendAsync(msg.Data, MessageFlag.RRpcCall);
    }

    /// <summary>Inbound from a backend (outbound dialed connection).</summary>
    private void OnBackendMessage(TcpConnection backendConn, Message msg)
    {
        if (msg.Flag != MessageFlag.RRpcCall) return;

        var chain = RpcMessage.ExtractChain(msg);
        if (chain.Count == 0) return;

        var firstName = chain[0].Name;
        if (!firstName.StartsWith(RpcProtocol.RRpcGetPrefix, StringComparison.Ordinal)) return;

        var routeKey = firstName.Substring(RpcProtocol.RRpcGetPrefix.Length);
        if (!string.Equals(routeKey, ClientRouteKey, StringComparison.OrdinalIgnoreCase))
        {
            Log.Warn($"{Tag} backend conn#{backendConn.Id} sent unexpected head '{firstName}'");
            return;
        }

        var define = RpcMessage.ExtractDefine(msg);
        var toGid = define.Length > 3 ? define[3]?.ToString() : null;
        if (string.IsNullOrEmpty(toGid) || !_clientByGid.TryGetValue(toGid!, out var clientConn))
        {
            Log.Warn($"{Tag} no client conn for player_id '{toGid}'");
            return;
        }

        Log.Info($"{Tag} forward backend conn#{backendConn.Id} → client#{clientConn.Id} (player {toGid})");
        _ = clientConn.SendAsync(msg.Data, MessageFlag.RRpcCall);
    }

    protected override void OnConnectionClosed(TcpConnection conn)
    {
        // Remove any client-side registrations referencing this socket.
        foreach (var kv in _clientByGid)
        {
            if (kv.Value == conn) _clientByGid.TryRemove(kv.Key, out _);
        }
    }

    protected override void OnUnit()
    {
        foreach (var c in _outbound.Values) c.Dispose();
        _outbound.Clear();
        _clientByGid.Clear();
    }
}
