namespace EqZero.Shared.Rpc;

/// <summary>
/// Implemented by any object that can resolve <c>RRpcGet*</c> chain steps.
/// Examples: GccApp resolves <c>RRpcGetGccPlayer(id)</c> → a GccPlayer instance;
/// a client-side ClientObj resolves <c>RRpcGetClient()</c> → itself.
/// </summary>
public interface IRpcRoute
{
    object? RRpcGet(string name, object?[] args);
}
