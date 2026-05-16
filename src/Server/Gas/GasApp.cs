using EqZero.Server.Framework;
using EqZero.Shared.Logging;
using EqZero.Shared.Net;

namespace EqZero.Server.Gas;

/// <summary>Placeholder gas (game server) process — accepts connections, logs traffic.</summary>
public sealed class GasApp : ServerAppBase
{
    public GasApp(string name) : base(name) { }

    protected override void OnMessage(TcpConnection conn, Message msg)
        => Log.Info($"{Tag} recv {msg}");
}
