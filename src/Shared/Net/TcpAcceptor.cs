using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EqZero.Shared.Logging;

namespace EqZero.Shared.Net;

/// <summary>Accepts TCP clients and forwards messages from each connection.</summary>
public sealed class TcpAcceptor
{
    private readonly IPEndPoint _bind;
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<int, TcpConnection> _conns = new();
    private readonly CancellationTokenSource _cts = new();

    public event Action<TcpConnection>? ConnectionOpened;
    public event Action<TcpConnection, Message>? MessageReceived;
    public event Action<TcpConnection>? ConnectionClosed;

    public TcpAcceptor(string ip, int port)
    {
        _bind = new IPEndPoint(IPAddress.Parse(ip), port);
        _listener = new TcpListener(_bind);
    }

    public void Start()
    {
        _listener.Start();
        Log.Info($"listening on {_bind}");
        _ = Task.Run(AcceptLoopAsync);
    }

    public void Stop()
    {
        if (_cts.IsCancellationRequested) return;
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* ignore */ }
        foreach (var c in _conns.Values) c.Dispose();
        _conns.Clear();
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                var conn = new TcpConnection(client);
                _conns[conn.Id] = conn;
                Log.Info($"accepted conn#{conn.Id} from {client.Client.RemoteEndPoint}");

                conn.MessageReceived += (c, m) => MessageReceived?.Invoke(c, m);
                conn.Closed += c =>
                {
                    _conns.TryRemove(c.Id, out _);
                    ConnectionClosed?.Invoke(c);
                };
                ConnectionOpened?.Invoke(conn);
                _ = conn.StartAsync();
            }
        }
        catch (ObjectDisposedException) { /* listener stopped */ }
        catch (Exception ex) when (!_cts.IsCancellationRequested)
        {
            Log.Error($"accept loop error: {ex}");
        }
    }
}
