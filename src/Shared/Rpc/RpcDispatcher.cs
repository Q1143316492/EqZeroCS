using System;
using System.Reflection;
using EqZero.Shared.Logging;
using EqZero.Shared.Net;

namespace EqZero.Shared.Rpc;

/// <summary>
/// Walks an RPC call-chain on the receiver side, resolving each <c>RRpcGet*</c>
/// step into a new object (via <see cref="IRpcRoute"/> or reflection) and finally
/// invoking the terminal method via reflection. While the terminal handler runs,
/// <see cref="RpcCallContext.Current"/> is populated so the handler can reply.
/// </summary>
public sealed class RpcDispatcher
{
    private readonly Func<object?> _rootFactory;

    public RpcDispatcher(Func<object?> rootFactory) => _rootFactory = rootFactory;

    public void Dispatch(TcpConnection conn, Message msg)
    {
        var chain = RpcMessage.ExtractChain(msg);
        if (chain.Count == 0) { Log.Warn("rpc dispatch: empty chain"); return; }
        var define = RpcMessage.ExtractDefine(msg);

        object? current = _rootFactory();
        for (int i = 0; i < chain.Count; i++)
        {
            var (name, args) = chain[i];
            bool isRoute = name.StartsWith(RpcProtocol.RRpcGetPrefix, StringComparison.Ordinal);

            if (isRoute)
            {
                current = ResolveRoute(current, name, args);
                if (current is null)
                {
                    Log.Warn($"rpc dispatch: route {name} returned null at step {i}");
                    return;
                }
            }
            else
            {
                var prev = RpcCallContext.Current;
                RpcCallContext.Current = new RpcCallContext(conn, define);
                try { Invoke(current, name, args); }
                finally { RpcCallContext.Current = prev; }
                return;
            }
        }
        Log.Warn("rpc dispatch: chain ended without terminal call");
    }

    private static object? ResolveRoute(object? target, string name, object?[] args)
    {
        if (target is null) return null;
        if (target is IRpcRoute route)
        {
            var next = route.RRpcGet(name, args);
            if (next is not null) return next;
        }
        // Fall back to reflection so a plain object with RRpcGet* methods also works.
        return InvokeMember(target, name, args);
    }

    private static void Invoke(object? target, string name, object?[] args)
    {
        if (target is null) { Log.Warn($"rpc dispatch: terminal {name} on null target"); return; }
        try { InvokeMember(target, name, args); }
        catch (Exception ex) { Log.Error($"rpc dispatch: invoking {target.GetType().Name}.{name} failed: {ex}"); }
    }

    private static object? InvokeMember(object target, string name, object?[] args)
    {
        var mi = target.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (mi is null)
        {
            Log.Warn($"rpc dispatch: method {name} not found on {target.GetType().Name}");
            return null;
        }
        var parms = mi.GetParameters();
        var coerced = new object?[parms.Length];
        for (int i = 0; i < parms.Length; i++)
        {
            var src = i < args.Length ? args[i] : null;
            coerced[i] = Coerce(src, parms[i].ParameterType);
        }
        return mi.Invoke(target, coerced);
    }

    private static object? Coerce(object? value, Type target)
    {
        if (value is null) return null;
        var srcType = value.GetType();
        if (target.IsAssignableFrom(srcType)) return value;
        try { return Convert.ChangeType(value, target); }
        catch { return value; }
    }
}
