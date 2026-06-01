// Main-thread marshalling helper. Independently written for this project against the
// game's threading model (game/Godot objects are only valid on the thread the mod's
// Init() ran on). The pattern — capture a SynchronizationContext, then Post work back to
// it — is a standard .NET idiom; no third-party source was copied.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sts2AiAdvisor.Game;

/// <summary>
/// Hops work back onto the game's main thread. Anything that reads or mutates a game model
/// (RunManager, Player, Deck, CardModel) or a Godot node must run there; the LLM HTTP call
/// must NOT. Capture the context once from Init() via <see cref="Initialize"/>.
/// </summary>
internal static class GameThread
{
    private static SynchronizationContext? _mainContext;
    private static int _mainThreadId = -1;

    /// <summary>Records the calling thread as "the game thread". Must be invoked from Init().</summary>
    public static void Initialize()
    {
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        _mainContext = SynchronizationContext.Current;

        if (_mainContext is null)
            ModLog.Error("GameThread: SynchronizationContext.Current was null at Init() — marshalling will not work.");
        else
            ModLog.Info($"GameThread: bound to game thread #{_mainThreadId}.");
    }

    public static bool IsInitialized => _mainContext is not null;

    /// <summary>True when the caller is already executing on the captured game thread.</summary>
    private static bool OnGameThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

    /// <summary>
    /// Evaluates <paramref name="func"/> on the game thread and returns its result. Runs
    /// synchronously when the caller is already on that thread; otherwise queues it.
    /// </summary>
    public static Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (_mainContext is null)
            return Task.FromException<T>(new InvalidOperationException("GameThread.Initialize() was never called."));

        if (OnGameThread)
        {
            try { return Task.FromResult(func()); }
            catch (Exception ex) { return Task.FromException<T>(ex); }
        }

        var promise = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mainContext.Post(static state =>
        {
            var (f, p) = ((Func<T>, TaskCompletionSource<T>))state!;
            try { p.TrySetResult(f()); }
            catch (Exception ex) { p.TrySetException(ex); }
        }, (func, promise));
        return promise.Task;
    }

    /// <summary>Runs <paramref name="work"/> on the game thread (no result).</summary>
    public static Task InvokeAsync(Action work)
        => InvokeAsync(() => { work(); return 0; });
}
