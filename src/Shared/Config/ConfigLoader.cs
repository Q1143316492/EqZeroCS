using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EqZero.Shared.Config;

public sealed class EndPointConfig
{
    public string Ip { get; set; } = "127.0.0.1";
    public int Port { get; set; }
}

/// <summary>
/// Loads the JSON config files under /config (same files as the Python version).
/// Resolves the config directory by walking upward from the executing assembly.
/// </summary>
public static class ConfigLoader
{
    public const string ServerConfigFile = "server_config.json";
    public const string ClientConfigFile = "client_config.json";

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string ResolveConfigDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            var candidate = Path.Combine(dir, "config");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("Could not locate /config directory.");
    }

    /// <summary>Load the whole server_list → name → endpoint map.</summary>
    public static Dictionary<string, EndPointConfig> LoadAllServers()
    {
        var path = Path.Combine(ResolveConfigDir(), ServerConfigFile);
        using var fs = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, EndPointConfig>>>(fs, s_jsonOpts)
                  ?? throw new InvalidDataException($"Invalid {ServerConfigFile}");
        if (!doc.TryGetValue("server_list", out var list))
            throw new KeyNotFoundException($"server_list missing in {ServerConfigFile}");
        return list;
    }

    /// <summary>Load server_config.json → { server_list: { name: {ip,port} } }.</summary>
    public static EndPointConfig LoadServer(string name)
    {
        var list = LoadAllServers();
        if (!list.TryGetValue(name, out var ep))
            throw new KeyNotFoundException($"server_list.{name} not found in {ServerConfigFile}");
        return ep;
    }

    /// <summary>Load client_config.json → { login: {ip,port} }.</summary>
    public static EndPointConfig LoadClientLogin()
    {
        var path = Path.Combine(ResolveConfigDir(), ClientConfigFile);
        using var fs = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<Dictionary<string, EndPointConfig>>(fs, s_jsonOpts)
                  ?? throw new InvalidDataException($"Invalid {ClientConfigFile}");
        if (!doc.TryGetValue("login", out var ep))
            throw new KeyNotFoundException($"login not found in {ClientConfigFile}");
        return ep;
    }
}
