using EqZero.Shared.Logging;
using EqZero.Shared.Rpc;

namespace EqZero.Server.Gcc;

/// <summary>
/// Server-side player living inside the gcc process.
/// After the first client RPC, the player caches a <see cref="ClientRpc"/>
/// proxy so it can fire calls back to the owning client at any time —
/// not only while replying inside a call.
/// </summary>
public sealed class GccPlayer
{
    public long Id { get; }
    public string UserName { get; }

    /// <summary>Cached proxy aimed back at this player's client; null until first contact.</summary>
    public RpcProxy? ClientRpc { get; private set; }

    public GccPlayer(long id, string userName)
    {
        Id = id;
        UserName = userName;
    }

    /// <summary>Refresh <see cref="ClientRpc"/> using the current RPC call context.</summary>
    private void BindClientFromContext()
    {
        var ctx = RpcCallContext.Current;
        if (ctx is null) return;
        ClientRpc = ctx.ReplyProxy();
    }

    /// <summary>First handshake from client. Establishes the back-channel.</summary>
    public void OnHello(string message)
    {
        BindClientFromContext();
        Log.Info($"[GccPlayer#{Id} {UserName}] OnHello -> \"{message}\"");

        if (ClientRpc is null)
        {
            Log.Warn($"[GccPlayer#{Id}] no client back-channel, skip OnEnterOk");
            return;
        }
        dynamic c = ClientRpc;
        c.RRpcGetClient().OnEnterOk(Id, $"welcome {UserName}, you said: {message}");
    }

    /// <summary>Demo: client → gcc ping, gcc → client pong via cached <see cref="ClientRpc"/>.</summary>
    public void Ping(long seq, string text)
    {
        BindClientFromContext();
        Log.Info($"[GccPlayer#{Id} {UserName}] Ping seq={seq} text=\"{text}\"");

        if (ClientRpc is null) return;
        dynamic c = ClientRpc;
        c.RRpcGetClient().OnPong(seq, $"pong-{seq} for \"{text}\"");
    }
}
