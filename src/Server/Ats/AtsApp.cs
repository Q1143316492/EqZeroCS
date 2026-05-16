using EqZero.Server.Framework;
using EqZero.Shared.Logging;
using EqZero.Shared.Net;

namespace EqZero.Server.Ats;

/// <summary>Placeholder ats process.</summary>
public sealed class AtsApp : ServerAppBase
{
    public AtsApp(string name) : base(name) { }

    protected override void OnMessage(TcpConnection conn, Message msg)
        => Log.Info($"{Tag} recv {msg}");
}
