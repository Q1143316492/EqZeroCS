using System;

namespace EqZero.Shared.Logging;

/// <summary>Mirrors the colored 3-level logger in framwork/logger.py (info/warn/error).</summary>
public static class Log
{
    private static readonly object s_lock = new();

    public static void Info(string msg)  => Write(ConsoleColor.Green,  "INFO ", msg);
    public static void Warn(string msg)  => Write(ConsoleColor.Cyan,   "WARN ", msg);
    public static void Error(string msg) => Write(ConsoleColor.Red,    "ERROR", msg);

    private static void Write(ConsoleColor color, string tag, string msg)
    {
        lock (s_lock)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {tag} {msg}");
            Console.ForegroundColor = prev;
        }
    }
}
