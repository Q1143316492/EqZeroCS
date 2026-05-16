namespace EqZero.Shared.Rpc;

/// <summary>
/// Wire-compatible with framwork/const.py ERpcProtocol. Body map uses these
/// string keys so the C# and Python sides interoperate at the byte level.
/// </summary>
public static class RpcProtocol
{
    /// <summary>4-element tuple (fromProcess, fromGlobalId, toProcess, toGlobalId).</summary>
    public const string RpcDefineKey = "1";

    /// <summary>List of [funcName, args[]] entries describing the chained call.</summary>
    public const string CallChainKey = "2";

    public const string RRpcGetPrefix = "RRpcGet";
}
