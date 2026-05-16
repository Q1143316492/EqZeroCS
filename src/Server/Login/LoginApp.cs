using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EqZero.Server.Framework;
using EqZero.Shared.Logging;
using EqZero.Shared.Net;
using EqZero.Shared.Rpc;

namespace EqZero.Server.Login;

/// <summary>
/// Entry point for clients: validates a hello, allocates a player id, asks gcc
/// to create the corresponding GccPlayer, and round-robin assigns one of the
/// configured gate processes for follow-up traffic.
/// </summary>
public sealed class LoginApp : ServerAppBase
{
    private TcpConnection? _gccConn;
    private RpcProxy? _gccRpc;
    private long _nextPlayerId = 1000;

    private List<string> _gateNames = new();
    private int _gateCursor;

    public LoginApp(string name) : base(name) { }

    protected override async Task OnInitAsync()
    {
        var gccEp = ServerRegistry.Get("gcc1");
        _gccConn = await TcpDialer.ConnectWithRetryAsync(gccEp.Ip, gccEp.Port, "gcc1");
        _gccRpc = new RpcProxy(_gccConn, fromProcess: Name, fromGlobalId: Name,
                                          toProcess: "gcc1", toGlobalId: "gcc1");

        // Collect all gate* entries from server_config (order-stable).
        _gateNames = ServerRegistry.All.Keys
            .Where(n => ServerRegistry.ProcessType(n) == "gate")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        if (_gateNames.Count == 0)
            throw new InvalidOperationException("no gate* entries in server_config.json");
        Log.Info($"{Tag} gate pool = [{string.Join(", ", _gateNames)}]");
    }

    private string PickGate()
    {
        var idx = Interlocked.Increment(ref _gateCursor) - 1;
        return _gateNames[((idx % _gateNames.Count) + _gateNames.Count) % _gateNames.Count];
    }

    protected override void OnMessage(TcpConnection conn, Message msg)
    {
        if (msg.Flag != MessageFlag.Base)
        {
            Log.Warn($"{Tag} unexpected flag={msg.Flag} from conn#{conn.Id}");
            return;
        }

        var op = msg.Get<string>("op");
        if (op == "login")
        {
            var user = msg.Get<string>("user") ?? "anon";
            var pid  = Interlocked.Increment(ref _nextPlayerId);
            Log.Info($"{Tag} login user={user} → player_id={pid}");

            // Ask gcc to create the player object server-side.
            dynamic gcc = _gccRpc!;
            gcc.RRpcGetGccPlayerMgr().CreatePlayer(pid, user);

            // Round-robin a gate for the client to follow up on.
            var gateName = PickGate();
            var gate = ServerRegistry.Get(gateName);
            Log.Info($"{Tag} assign {gateName} ({gate.Ip}:{gate.Port}) to player_id={pid}");

            _ = conn.SendAsync(new Dictionary<string, object?>
            {
                ["op"] = "login_ok",
                ["player_id"] = pid,
                ["gate_name"] = gateName,
                ["gate_ip"] = gate.Ip,
                ["gate_port"] = gate.Port,
            });
        }
        else
        {
            Log.Warn($"{Tag} unknown op={op}");
        }
    }

    protected override void OnUnit() => _gccConn?.Dispose();
}
