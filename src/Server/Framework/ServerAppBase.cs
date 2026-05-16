using System;
using System.Threading.Tasks;
using EqZero.Shared.Logging;
using EqZero.Shared.Net;

namespace EqZero.Server.Framework;

/// <summary>
/// Base class for all server-process apps. Owns the listening acceptor for the
/// process's own port; subclasses bring up outbound peer connections in
/// <see cref="OnInitAsync"/> and react to incoming traffic via <see cref="OnMessage"/>.
/// </summary>
public abstract class ServerAppBase
{
    protected TcpAcceptor? Acceptor { get; private set; }

    public string Name { get; }
    public string Tag => $"[{Name}]";

    protected ServerAppBase(string name) => Name = name;

    public async Task InitAsync()
    {
        var ep = ServerRegistry.Get(Name);
        Log.Info($"{Tag} init {ep.Ip}:{ep.Port}");
        Acceptor = new TcpAcceptor(ep.Ip, ep.Port);
        Acceptor.MessageReceived += OnMessage;
        Acceptor.ConnectionClosed += OnConnectionClosed;
        Acceptor.Start();
        await OnInitAsync();
    }

    public void Unit()
    {
        Acceptor?.Stop();
        OnUnit();
        Log.Info($"{Tag} shutdown");
    }

    /// <summary>Hook for subclasses to dial peer servers, prime state, etc.</summary>
    protected virtual Task OnInitAsync() => Task.CompletedTask;

    protected virtual void OnUnit() { }

    /// <summary>Called for every inbound message from any accepted connection.</summary>
    protected abstract void OnMessage(TcpConnection conn, Message msg);

    protected virtual void OnConnectionClosed(TcpConnection conn) { }
}
