using System;
using System.Collections.Generic;
using EqZero.Shared.Config;

namespace EqZero.Server.Framework;

/// <summary>Process-wide cache of all server endpoints declared in server_config.json.</summary>
public static class ServerRegistry
{
    private static Dictionary<string, EndPointConfig>? s_all;

    public static IReadOnlyDictionary<string, EndPointConfig> All
    {
        get
        {
            s_all ??= ConfigLoader.LoadAllServers();
            return s_all;
        }
    }

    public static EndPointConfig Get(string name)
    {
        if (!All.TryGetValue(name, out var ep))
            throw new KeyNotFoundException($"unknown server '{name}'");
        return ep;
    }

    /// <summary>Strip trailing digits from a process name: "gas1" → "gas", "login" → "login".</summary>
    public static string ProcessType(string processName)
    {
        int i = processName.Length;
        while (i > 0 && char.IsDigit(processName[i - 1])) i--;
        return processName.Substring(0, i);
    }
}
