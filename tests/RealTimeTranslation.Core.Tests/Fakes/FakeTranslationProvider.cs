using RealTimeTranslation.Core;

namespace RealTimeTranslation.Core.Tests.Fakes;

internal sealed class FakeTranslationProvider(params FakeTranslationSession[] sessions)
    : ITranslationProvider
{
    private readonly Queue<FakeTranslationSession> queuedSessions = new(sessions);

    public int CreateCount { get; private set; }

    public List<FakeTranslationSession> CreatedSessions { get; } = [];

    public FakeTranslationSession LastSession => CreatedSessions[^1];

    public ValueTask<ITranslationSession> CreateSessionAsync(
        TranslationSessionOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CreateCount++;
        var session = queuedSessions.Count > 0
            ? queuedSessions.Dequeue()
            : new FakeTranslationSession();
        CreatedSessions.Add(session);
        return ValueTask.FromResult<ITranslationSession>(session);
    }
}

internal sealed class FakeTranslationSession : ITranslationSession
{
    private readonly SemaphoreSlim writeCompleted = new(0);
    private readonly TaskCompletionSource firstWriteStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource releaseFirstWrite = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int activeWrites;
    private int maximumConcurrentWrites;

    public Exception? StartException { get; init; }

    public Exception? StopException { get; init; }

    public bool BlockFirstWrite { get; init; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public int DisposeCount { get; private set; }

    public int MaximumConcurrentWrites => maximumConcurrentWrites;

    public List<byte> WrittenValues { get; } = [];

    public event EventHandler<TranslationUpdateEventArgs>? UpdateReceived;

    public ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StartCount++;
        return StartException is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(StartException);
    }

    public async ValueTask WriteAudioAsync(
        ReadOnlyMemory<byte> audio,
        CancellationToken cancellationToken)
    {
        var concurrent = Interlocked.Increment(ref activeWrites);
        UpdateMaximum(concurrent);
        try
        {
            if (BlockFirstWrite && WrittenValues.Count == 0)
            {
                firstWriteStarted.TrySetResult();
                await releaseFirstWrite.Task.WaitAsync(cancellationToken);
            }

            WrittenValues.Add(audio.Span[0]);
            writeCompleted.Release();
        }
        finally
        {
            Interlocked.Decrement(ref activeWrites);
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        return StopException is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(StopException);
    }

    public void Emit(TranslationUpdate update)
    {
        UpdateReceived?.Invoke(this, new TranslationUpdateEventArgs(update));
    }

    public Task WaitForFirstWriteAsync() => firstWriteStarted.Task;

    public void ReleaseFirstWrite() => releaseFirstWrite.TrySetResult();

    public async Task WaitForWriteCountAsync(int count, CancellationToken cancellationToken)
    {
        while (WrittenValues.Count < count)
        {
            await writeCompleted.WaitAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        writeCompleted.Dispose();
        return ValueTask.CompletedTask;
    }

    private void UpdateMaximum(int candidate)
    {
        int current;
        do
        {
            current = maximumConcurrentWrites;
            if (candidate <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref maximumConcurrentWrites, candidate, current) != current);
    }
}
