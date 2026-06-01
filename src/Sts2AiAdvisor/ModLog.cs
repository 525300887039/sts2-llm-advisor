using System;
using MegaCrit.Sts2.Core.Logging;

namespace Sts2AiAdvisor;

/// <summary>Thin wrapper over the game logger so every line is prefixed consistently.</summary>
internal static class ModLog
{
    private const string Prefix = "[Sts2AiAdvisor]";

    public static void Info(string message) => Log.Info($"{Prefix} {message}");
    public static void Warn(string message) => Log.Warn($"{Prefix} {message}");
    public static void Error(string message) => Log.Error($"{Prefix} {message}");
    public static void Error(string message, Exception ex) => Log.Error($"{Prefix} {message}: {ex}");
}
