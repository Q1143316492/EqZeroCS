using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using EqZero.Shared.Config;
using EqZero.Shared.Logging;
using EqZero.Shared.Net;
using EqZero.Shared.Rpc;

namespace EqZero.Client;

/// <summary>
/// Demo client flow:
///   1. Dial login, send {op:"login"}.
///   2. Receive {op:"login_ok", player_id, gate_name, gate_ip, gate_port}.
///   3. Dial the assigned gate, fire RPC at GccPlayer.OnHello.
///   4. After server's reverse RPC OnEnterOk arrives the client is "logged in".
///   5. As a demo of ongoing two-way chat, the client then fires 2 Pings;
///      gcc replies with 2 Pongs via the cached back-channel.
/// </summary>
public sealed class ClientApp
{
    private const int PingCount = 2;

    private TcpConnection? _loginConn;
    private TcpConnection? _gateConn;
    private RpcProxy? _gateRpc;
    private long _playerId;
    private string _gateName = "(unknown)";

    private readonly ClientObj _clientObj;
    private readonly RpcDispatcher _dispatcher;

    private readonly TaskCompletionSource<bool> _gateReady = new();
    private readonly TaskCompletionSource<long> _enteredOk = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<bool>> _pongWaiters = new();

    /// <summary>Completes when the server has fully accepted us into the game.</summary>
    public Task<long> LoggedIn => _enteredOk.Task;

    public ClientApp()
    {
        _clientObj = new ClientObj(this);
        _dispatcher = new RpcDispatcher(() => _clientObj);
    }

    public async Task RunAsync()
    {
        var loginEp = ConfigLoader.LoadClientLogin();
        Log.Info($"[client] dial login {loginEp.Ip}:{loginEp.Port}");
        _loginConn = await TcpDialer.ConnectAsync(loginEp.Ip, loginEp.Port);
        _loginConn.MessageReceived += OnLoginMessage;

        await _loginConn.SendAsync(new Dictionary<string, object?>
        {
            ["op"] = "login",
            ["user"] = "cwl",
            ["pwd"] = "123",
        });

        await _gateReady.Task;

        // First chain: triggers OnEnterOk reply (entry into game).
        dynamic gate = _gateRpc!;
        gate.RRpcGetGcc(1).RRpcGetGccPlayer(_playerId).OnHello("hi from client");
        Log.Info("[client] sent OnHello via " + _gateName);

        var pid = await _enteredOk.Task;
        Log.Info($"[client] === LOGGED IN as player_id={pid} via {_gateName} ===");

        // Now demonstrate ongoing two-way chat: client → gcc Ping, gcc → client Pong.
        for (long seq = 1; seq <= PingCount; seq++)
        {
            var waiter = new TaskCompletionSource<bool>();
            _pongWaiters[seq] = waiter;

            var text = $"ping #{seq}";
            Log.Info($"[client] send Ping seq={seq} text=\"{text}\"");
            gate.RRpcGetGcc(1).RRpcGetGccPlayer(pid).Ping(seq, text);

            await waiter.Task;
        }

        Log.Info("[client] demo complete");
    }

    private void OnLoginMessage(TcpConnection conn, Message msg)
    {
        Log.Info($"[client] login reply {msg}");
        var op = msg.Get<string>("op");
        if (op != "login_ok") return;

        _playerId = Convert.ToInt64(msg.Data["player_id"]);
        _gateName = msg.Get<string>("gate_name") ?? "(unknown)";
        var gateIp = msg.Get<string>("gate_ip") ?? "127.0.0.1";
        var gatePort = Convert.ToInt32(msg.Data["gate_port"]);

        _ = Task.Run(async () =>
        {
            try
            {
                Log.Info($"[client] dial gate {_gateName} {gateIp}:{gatePort} (player_id={_playerId})");
                _gateConn = await TcpDialer.ConnectAsync(gateIp, gatePort);
                _gateConn.MessageReceived += OnGateMessage;
                _gateRpc = new RpcProxy(_gateConn,
                    fromProcess: "client", fromGlobalId: _playerId.ToString(),
                    toProcess:   "gcc1",   toGlobalId:   _playerId.ToString());
                _gateReady.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _gateReady.TrySetException(ex);
                _enteredOk.TrySetException(ex);
            }
        });
    }

    private void OnGateMessage(TcpConnection conn, Message msg)
    {
        if (msg.Flag == MessageFlag.RRpcCall)
            _dispatcher.Dispatch(conn, msg);
        else
            Log.Info($"[client] gate sent {msg}");
    }

    internal void NotifyEntered(long playerId) => _enteredOk.TrySetResult(playerId);

    internal void NotifyPong(long seq)
    {
        if (_pongWaiters.TryRemove(seq, out var w)) w.TrySetResult(true);
    }

    public void Shutdown()
    {
        _loginConn?.Dispose();
        _gateConn?.Dispose();
    }
}
