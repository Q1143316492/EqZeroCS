namespace EqZero.Shared.Net;

/// <summary>
/// Mirrors framwork/const.py ERpcType. Reserved values are kept identical with the
/// Python version so the two protocols stay binary-compatible.
/// </summary>
public static class MessageFlag
{
    public const int Base = 0;
    public const int RRpcCall = 1;
}
