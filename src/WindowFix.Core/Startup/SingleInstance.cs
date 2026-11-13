namespace WindowFix.Core.Startup;

/// <summary>
/// One WindowFix per user session. A second launch signals the first ("show your window") and exits.
/// <paramref name="onShowRequested"/> is called on a background thread; marshal to the UI thread yourself.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\WindowFix.SingleInstance";
    private const string EventName = @"Local\WindowFix.Show";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private readonly RegisteredWaitHandle _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent, Action onShowRequested)
    {
        _mutex = mutex;
        _showEvent = showEvent;
        _wait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => onShowRequested(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <param name="waitForPrevious">How long to wait for a previous instance that is about to exit (elevated restart).</param>
    public static SingleInstance? TryAcquire(Action onShowRequested, TimeSpan waitForPrevious = default)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew && waitForPrevious > TimeSpan.Zero)
        {
            try
            {
                createdNew = mutex.WaitOne(waitForPrevious);
            }
            catch (AbandonedMutexException)
            {
                createdNew = true; // the previous instance exited without releasing it
            }
        }
        if (!createdNew)
        {
            mutex.Dispose();
            if (EventWaitHandle.TryOpenExisting(EventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
            return null;
        }
        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        return new SingleInstance(mutex, showEvent, onShowRequested);
    }

    public void Dispose()
    {
        _wait.Unregister(null);
        _showEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
