using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EqZero.Shared.Logging;

namespace EqZero.Shared.Net;

/// <summary>
/// Bidirectional, length-prefixed TCP connection. Used by both server-accepted
/// sockets and client-dialed sockets. Equivalent to framwork/iostream.py:IOStream.
/// </summary>
public sealed class TcpConnection : IDisposable
{
    private static int s_nextId;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private int _disposed;

    public int Id { get; }
    public bool IsConnected => _client.Connected && _disposed == 0;

    /// <summary>Arbitrary tag attached by the owning app (e.g. a Player id).</summary>
    public object? Tag { get; set; }

    public event Action<TcpConnection, Message>? MessageReceived;
    public event Action<TcpConnection>? Closed;

    public TcpConnection(TcpClient client)
    {
        Id = Interlocked.Increment(ref s_nextId);
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
    }

    public Task StartAsync() => Task.Run(ReadLoopAsync);

    public async Task SendAsync(Dictionary<string, object?> data, int flag = MessageFlag.Base)
    {
        var bytes = Message.Create(data, flag).Pack();
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(bytes, 0, bytes.Length, _cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[4096];
        var pending = new List<byte>(4096);
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                int n = await _stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token).ConfigureAwait(false);
                if (n <= 0) break;

                pending.AddRange(new ArraySegment<byte>(buffer, 0, n));

                while (true)
                {
                    int consumed = Message.TryParse(pending.ToArray().AsSpan(), out var msg);
                    if (consumed == 0 || msg is null) break;
                    pending.RemoveRange(0, consumed);
                    msg.FromConnection = this;
                    try { MessageReceived?.Invoke(this, msg); }
                    catch (Exception ex) { Log.Error($"conn#{Id} handler error: {ex}"); }
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            Log.Warn($"conn#{Id} read loop ended: {ex.Message}");
        }
        finally
        {
            Closed?.Invoke(this);
            Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _cts.Cancel(); } catch { /* ignore */ }
        try { _stream.Dispose(); } catch { /* ignore */ }
        try { _client.Close(); } catch { /* ignore */ }
        _cts.Dispose();
        _writeLock.Dispose();
    }
}
