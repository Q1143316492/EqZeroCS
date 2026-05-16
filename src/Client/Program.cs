using System;
using System.Threading;
using System.Threading.Tasks;
using EqZero.Shared.Logging;

namespace EqZero.Client;

internal static class Program
{
    private static async Task<int> Main()
    {
        Console.Title = "EqZero client";
        var app = new ClientApp();
        try { await app.RunAsync(); }
        catch (Exception ex)
        {
            Log.Error($"client run failed: {ex}");
            return 1;
        }

        using var exit = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };
        Log.Info("press Ctrl+C to exit");
        exit.Wait();

        app.Shutdown();
        return 0;
    }
}
