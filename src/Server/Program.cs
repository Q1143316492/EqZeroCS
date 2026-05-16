using System;
using System.Threading;
using System.Threading.Tasks;
using EqZero.Server.Ats;
using EqZero.Server.Framework;
using EqZero.Server.Gas;
using EqZero.Server.Gate;
using EqZero.Server.Gcc;
using EqZero.Server.Login;
using EqZero.Shared.Logging;

namespace EqZero.Server;

internal static class Program
{
    /// <summary>
    /// Usage: EqZero.Server --name &lt;login|gate|gas1|gcc1|ats1&gt;
    /// The factory below dispatches on the process-type prefix.
    /// </summary>
    private static async Task<int> Main(string[] args)
    {
        var name = ParseName(args) ?? "login";
        var app = CreateApp(name);
        if (app is null) { Log.Error($"unknown process name '{name}'"); return 2; }

        Console.Title = $"EqZero {name}";

        try { await app.InitAsync(); }
        catch (Exception ex)
        {
            Log.Error($"init failed: {ex}");
            return 1;
        }

        using var exit = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };
        Log.Info("press Ctrl+C to exit");
        exit.Wait();

        app.Unit();
        return 0;
    }

    private static ServerAppBase? CreateApp(string name) => ServerRegistry.ProcessType(name) switch
    {
        "login" => new LoginApp(name),
        "gate"  => new GateApp(name),
        "gas"   => new GasApp(name),
        "gcc"   => new GccApp(name),
        "ats"   => new AtsApp(name),
        _ => null,
    };

    private static string? ParseName(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--name") return args[i + 1];
        return null;
    }
}
