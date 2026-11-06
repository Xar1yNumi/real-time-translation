using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.LocalTranslation;

internal interface ILocalTranslatorWorker : ILocalTextTranslator, IAsyncDisposable;

public sealed class LocalTranslatorHost : ILocalTextTranslator, IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<ILocalTranslatorWorker>> createWorker;
    private readonly SemaphoreSlim gate = new(1, 1);
    private ILocalTranslatorWorker? worker;
    private bool disposed;

    public LocalTranslatorHost(string pythonExecutable, string workerScript, string modelsDirectory)
        : this(async cancellationToken => await LocalTranslationClient.StartAsync(
            pythonExecutable, workerScript, modelsDirectory, cancellationToken).ConfigureAwait(false))
    {
    }

    internal LocalTranslatorHost(Func<CancellationToken, Task<ILocalTranslatorWorker>> createWorker)
    {
        this.createWorker = createWorker ?? throw new ArgumentNullException(nameof(createWorker));
    }

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            worker ??= await createWorker(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> TranslateAsync(string text, string sourceCode,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            await PrepareAsync(cancellationToken).ConfigureAwait(false);
            var current = worker ?? throw new ObjectDisposedException(nameof(LocalTranslatorHost));
            try
            {
                return await current.TranslateAsync(text, sourceCode, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (AppFailureException failure) when (
                failure.Error.Code == AppErrorCode.ServiceUnavailable &&
                !cancellationToken.IsCancellationRequested)
            {
                await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    if (ReferenceEquals(worker, current))
                    {
                        worker = null;
                        await current.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    gate.Release();
                }
                if (attempt > 0) throw;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            disposed = true;
            if (worker is not null)
            {
                await worker.DisposeAsync().ConfigureAwait(false);
                worker = null;
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
