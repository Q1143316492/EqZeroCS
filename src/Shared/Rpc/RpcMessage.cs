using System;
using System.Collections;
using System.Collections.Generic;
using EqZero.Shared.Net;

namespace EqZero.Shared.Rpc;

/// <summary>Helpers to build/parse the RPC message body shape.</summary>
public static class RpcMessage
{
    /// <summary>
    /// Build the on-wire dict for an RPC call.
    /// <paramref name="rpcDefine"/> is the 4-tuple (fromProcess, fromGlobalId, toProcess, toGlobalId).
    /// <paramref name="callChain"/> is a list of [funcName, argsArray] entries.
    /// </summary>
    public static Dictionary<string, object?> Build(object?[] rpcDefine, IReadOnlyList<object?[]> callChain)
    {
        var chainArray = new object?[callChain.Count];
        for (int i = 0; i < callChain.Count; i++) chainArray[i] = callChain[i];
        return new Dictionary<string, object?>
        {
            [RpcProtocol.RpcDefineKey] = rpcDefine,
            [RpcProtocol.CallChainKey] = chainArray,
        };
    }

    /// <summary>Extract the 4-tuple rpc define from a parsed message.</summary>
    public static object?[] ExtractDefine(Message msg)
    {
        if (!msg.Data.TryGetValue(RpcProtocol.RpcDefineKey, out var v) || v is null)
            return Array.Empty<object?>();
        return ToObjectArray(v);
    }

    /// <summary>Extract the call chain entries (each entry = [name, args]).</summary>
    public static List<(string Name, object?[] Args)> ExtractChain(Message msg)
    {
        var result = new List<(string, object?[])>();
        if (!msg.Data.TryGetValue(RpcProtocol.CallChainKey, out var raw) || raw is null) return result;
        foreach (var item in (IEnumerable)raw)
        {
            var entry = ToObjectArray(item);
            if (entry.Length < 1) continue;
            var name = entry[0]?.ToString() ?? string.Empty;
            var args = entry.Length >= 2 && entry[1] is not null
                ? ToObjectArray(entry[1]!)
                : Array.Empty<object?>();
            result.Add((name, args));
        }
        return result;
    }

    private static object?[] ToObjectArray(object value)
    {
        if (value is object?[] arr) return arr;
        if (value is IEnumerable e)
        {
            var list = new List<object?>();
            foreach (var x in e) list.Add(x);
            return list.ToArray();
        }
        return new object?[] { value };
    }
}
