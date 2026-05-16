using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Resolvers;

namespace EqZero.Shared.Net;

/// <summary>
/// Wire-compatible with the Python framework's iomsg.Message:
///
///   [4 bytes BE int32 : body length] [4 bytes BE int32 : flag] [N bytes : msgpack body]
///
/// Body is a msgpack-encoded map (Dictionary&lt;string, object?&gt;).
/// </summary>
public sealed class Message
{
    public const int HeaderLen = 4;
    public const int FlagLen = 4;

    private static readonly MessagePackSerializerOptions s_options =
        MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance);

    public int Flag { get; private set; }
    public Dictionary<string, object?> Data { get; private set; } = new();

    /// <summary>Optional back-reference to the connection that received this message.</summary>
    public object? FromConnection { get; set; }

    public static Message Create(Dictionary<string, object?> data, int flag = MessageFlag.Base)
    {
        return new Message { Data = data, Flag = flag };
    }

    public T? Get<T>(string key)
    {
        if (Data.TryGetValue(key, out var v) && v is T t) return t;
        return default;
    }

    public void Set(string key, object? value) => Data[key] = value;

    /// <summary>Pack to the on-wire byte sequence.</summary>
    public byte[] Pack()
    {
        var body = MessagePackSerializer.Serialize(Data, s_options);
        var buf = new byte[HeaderLen + FlagLen + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(0, HeaderLen), body.Length);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(HeaderLen, FlagLen), Flag);
        Buffer.BlockCopy(body, 0, buf, HeaderLen + FlagLen, body.Length);
        return buf;
    }

    /// <summary>
    /// Attempt to parse a single message from the start of <paramref name="buffer"/>.
    /// Returns the total number of bytes consumed (header + flag + body), or 0 if the
    /// buffer does not yet contain a full message. Throws on a corrupt frame.
    /// </summary>
    public static int TryParse(ReadOnlySpan<byte> buffer, out Message? message)
    {
        message = null;
        if (buffer.Length < HeaderLen) return 0;

        int bodyLen = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(0, HeaderLen));
        if (bodyLen <= 0) return 0;

        int total = HeaderLen + FlagLen + bodyLen;
        if (buffer.Length < total) return 0;

        int flag = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(HeaderLen, FlagLen));
        var body = buffer.Slice(HeaderLen + FlagLen, bodyLen).ToArray();

        var data = MessagePackSerializer.Deserialize<Dictionary<string, object?>>(body, s_options);
        message = new Message { Flag = flag, Data = data ?? new Dictionary<string, object?>() };
        return total;
    }

    public override string ToString()
    {
        var parts = new List<string>(Data.Count);
        foreach (var kv in Data) parts.Add($"{kv.Key}={kv.Value}");
        return $"Message(flag={Flag}, data={{{string.Join(", ", parts)}}})";
    }
}
