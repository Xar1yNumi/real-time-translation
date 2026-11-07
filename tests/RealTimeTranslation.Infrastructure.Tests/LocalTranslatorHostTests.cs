using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalTranslation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class LocalTranslatorHostTests
{
    [Fact]
    public async Task MultipleSessionsReuseOneWorkerAndDisposeIt()
    {
        var starts = 0;
        var worker = new FakeWorker();
        var host = new LocalTranslatorHost(_ =>
        {
            starts++;
            return Task.FromResult<ILocalTranslatorWorker>(worker);
        });

        await host.PrepareAsync(TestContext.Current.CancellationToken);
        await host.PrepareAsync(TestContext.Current.CancellationToken);
        Assert.Equal("translated", await host.TranslateAsync("Hello", "en",
            TestContext.Current.CancellationToken));
        Assert.Equal(1, starts);

        await host.DisposeAsync();
        Assert.True(worker.Disposed);
    }

    [Fact]
    public async Task ReplacesFailedWorkerAndRetriesTranslationOnce()
    {
        var failed = new FakeWorker { FailWithUnavailable = true };
        var replacement = new FakeWorker();
        var starts = 0;
        await using var host = new LocalTranslatorHost(_ =>
            Task.FromResult<ILocalTranslatorWorker>(++starts == 1 ? failed : replacement));

        Assert.Equal("translated", await host.TranslateAsync("Hello", "en",
            TestContext.Current.CancellationToken));
        Assert.Equal(2, starts);
        Assert.True(failed.Disposed);
    }

    private sealed class FakeWorker : ILocalTranslatorWorker
    {
        public bool Disposed { get; private set; }
        public bool FailWithUnavailable { get; init; }

        public Task<string> TranslateAsync(string text, string sourceCode,
            CancellationToken cancellationToken) => FailWithUnavailable
                ? Task.FromException<string>(new AppFailureException(
                    AppError.For(AppErrorCode.ServiceUnavailable)))
                : Task.FromResult("translated");

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
