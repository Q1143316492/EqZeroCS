using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EqZero.Shared.Logging;

namespace EqZero.Shared.Net;

/// <summary>Outbound TCP helpers: connect once, or connect with retry until the target is up.</summary>
public static class TcpDialer
{
    public static async Task<TcpConnection> ConnectAsync(string host, int port)
    {
        var client = new TcpClient();
        await client.ConnectAsync(host, port).ConfigureAwait(false);
        var conn = new TcpConnection(client);
        _ = conn.StartAsync();
        return conn;
    }

    /// <summary>
    /// Retry-connect loop, used at server boot when peer processes may not yet be listening.
    /// Logs every failure but never throws unless cancelled.
    /// </summary>
    public static async Task<TcpConnection> ConnectWithRetryAsync(
        string host, int port, string? label = null,
        int delayMs = 500, CancellationToken ct = default)
    {
        for (int attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var conn = await ConnectAsync(host, port).ConfigureAwait(false);
                Log.Info($"dialed {label ?? $"{host}:{port}"} (attempt {attempt}) → conn#{conn.Id}");
                return conn;
            }
            catch (Exception ex)
            {
                if (attempt == 1 || attempt % 10 == 0)
                    Log.Warn($"dial {label ?? $"{host}:{port}"} failed (attempt {attempt}): {ex.Message}");
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
        }
    }
}
