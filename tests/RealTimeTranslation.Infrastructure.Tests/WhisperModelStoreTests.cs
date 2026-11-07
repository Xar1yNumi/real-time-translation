using System.Net;
using System.Security.Cryptography;
using System.Text;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class WhisperModelStoreTests
{
    private const string ModelFileName = "ggml-small-q5_1.bin";

    [Fact]
    public async Task ValidCachedModelDoesNotCallNetwork()
    {
        using var directory = new TemporaryDirectory();
        var model = Encoding.UTF8.GetBytes("verified model");
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, ModelFileName), model,
            TestContext.Current.CancellationToken);
        var handler = new StubHandler(_ => throw new InvalidOperationException("Network was called."));
        using var client = new HttpClient(handler);
        var store = new WhisperModelStore(client, directory.Path, Descriptor(model));

        var path = await store.EnsureAvailableAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(directory.Path, ModelFileName), path);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CacheCheckNeverDownloadsAndRejectsCorruptModel()
    {
        using var directory = new TemporaryDirectory();
        var expected = Encoding.UTF8.GetBytes("verified model");
        var handler = new StubHandler(_ => throw new InvalidOperationException("Network was called."));
        using var client = new HttpClient(handler);
        using var store = new WhisperModelStore(client, directory.Path, Descriptor(expected));

        Assert.Null(await store.VerifyCachedAsync(TestContext.Current.CancellationToken));
        var path = Path.Combine(directory.Path, ModelFileName);
        await File.WriteAllBytesAsync(path, expected, TestContext.Current.CancellationToken);
        Assert.Equal(path, await store.VerifyCachedAsync(TestContext.Current.CancellationToken));
        await File.WriteAllTextAsync(path, "corrupt", TestContext.Current.CancellationToken);
        Assert.Null(await store.VerifyCachedAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ValidDownloadIsInstalledWithoutPartialFile()
    {
        using var directory = new TemporaryDirectory();
        var model = Encoding.UTF8.GetBytes("verified model");
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(model)
        });
        using var client = new HttpClient(handler);
        var store = new WhisperModelStore(client, directory.Path, Descriptor(model));

        var path = await store.EnsureAvailableAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(model, await File.ReadAllBytesAsync(path,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.RequestCount);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task WrongDigestDoesNotInstallModel()
    {
        using var directory = new TemporaryDirectory();
        var expected = Encoding.UTF8.GetBytes("verified model");
        var response = expected.ToArray();
        response[0] ^= 1;
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(response)
        }));
        var store = new WhisperModelStore(client, directory.Path, Descriptor(expected));

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await store.EnsureAvailableAsync(null, TestContext.Current.CancellationToken));

        Assert.False(File.Exists(Path.Combine(directory.Path, ModelFileName)));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task TruncatedResponseDoesNotInstallModel()
    {
        using var directory = new TemporaryDirectory();
        var expected = Encoding.UTF8.GetBytes("verified model");
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(expected[..4])
        }));
        var store = new WhisperModelStore(client, directory.Path, Descriptor(expected));

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await store.EnsureAvailableAsync(null, TestContext.Current.CancellationToken));

        Assert.False(File.Exists(Path.Combine(directory.Path, ModelFileName)));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task InterruptedDownloadDoesNotInstallModel()
    {
        using var directory = new TemporaryDirectory();
        var model = Encoding.UTF8.GetBytes("verified model");
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new InterruptingStream(model))
        }));
        var store = new WhisperModelStore(client, directory.Path, Descriptor(model));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await store.EnsureAvailableAsync(null, TestContext.Current.CancellationToken));

        Assert.False(File.Exists(Path.Combine(directory.Path, ModelFileName)));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task StalledResponseBodyTimesOutAndRemovesPartialFile()
    {
        using var directory = new TemporaryDirectory();
        var model = Encoding.UTF8.GetBytes("verified model");
        using var stream = new BlockingReadStream(model);
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        }));
        using var store = new WhisperModelStore(client, directory.Path, Descriptor(model),
            TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await store.EnsureAvailableAsync(null, TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));

        Assert.False(File.Exists(Path.Combine(directory.Path, ModelFileName)));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task DisposeCancelsActiveAndWaitingDownloads()
    {
        using var directory = new TemporaryDirectory();
        var model = Encoding.UTF8.GetBytes("verified model");
        using var stream = new BlockingReadStream(model);
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        }));
        var store = new WhisperModelStore(client, directory.Path, Descriptor(model),
            TimeSpan.FromSeconds(10));
        var first = store.EnsureAvailableAsync(null, TestContext.Current.CancellationToken);
        await stream.SecondReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);
        var waiting = store.EnsureAvailableAsync(null, TestContext.Current.CancellationToken);

        await Task.Run(store.Dispose, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await first.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await waiting.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(directory.Path, ModelFileName)));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    private static WhisperModelDescriptor Descriptor(byte[] expected) => new(
        new Uri("https://example.test/model.bin"),
        expected.LongLength,
        Convert.ToHexString(SHA256.HashData(expected)));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "RealTimeTranslation.ModelStoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class InterruptingStream(byte[] bytes) : MemoryStream(bytes)
    {
        private bool readOnce;

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (readOnce)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            readOnce = true;
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, 4)], cancellationToken);
        }
    }

    private sealed class BlockingReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        private bool readOnce;

        public TaskCompletionSource<bool> SecondReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (!readOnce)
            {
                readOnce = true;
                return await base.ReadAsync(buffer[..Math.Min(buffer.Length, 4)], cancellationToken);
            }

            SecondReadStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
