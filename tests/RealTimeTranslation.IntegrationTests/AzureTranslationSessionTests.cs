using NAudio.Wave;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Azure;

namespace RealTimeTranslation.IntegrationTests;

public sealed class AzureTranslationSessionTests
{
    [Fact]
    public async Task WriteBeforeStartIsRejected()
    {
        await using var session = new AzureTranslationSession(new FakeAzureTranslationTransport());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await session.WriteAudioAsync(
                new byte[] { 1, 2 },
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StopAndDisposeAreIdempotent()
    {
        var transport = new FakeAzureTranslationTransport();
        var session = new AzureTranslationSession(transport);

        await session.StopAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);
        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(1, transport.DisposeCount);
    }

    [Fact]
    public async Task StartWaitsForSdkOperationAfterCallerCancellation()
    {
        var transport = new FakeAzureTranslationTransport { BlockStart = true };
        await using var session = new AzureTranslationSession(transport);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var startTask = session.StartAsync(cancellation.Token).AsTask();
        await transport.StartEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        cancellation.Cancel();
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        Assert.False(startTask.IsCompleted);
        transport.ReleaseStart();
        await startTask;
        await session.WriteAudioAsync(
            new byte[] { 1, 2 },
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StopWaitsForSdkOperationAfterCallerCancellation()
    {
        var transport = new FakeAzureTranslationTransport { BlockStop = true };
        await using var session = new AzureTranslationSession(transport);
        await session.StartAsync(TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var stopTask = session.StopAsync(cancellation.Token).AsTask();
        await transport.StopEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        cancellation.Cancel();
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        Assert.False(stopTask.IsCompleted);
        transport.ReleaseStop();
        await stopTask;
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await session.WriteAudioAsync(
                new byte[] { 1, 2 },
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisposeCleansResourcesWhenStoppingFails()
    {
        var transport = new FakeAzureTranslationTransport
        {
            StopException = new InvalidOperationException("Expected stop failure.")
        };
        var session = new AzureTranslationSession(transport);
        await session.StartAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await session.DisposeAsync());

        Assert.Equal(1, transport.CloseCount);
        Assert.Equal(1, transport.DisposeCount);
        await session.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "AzureLive")]
    public async Task CanStartAndStopContinuousSession()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_SPEECH_ENDPOINT");
        var key = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
        {
            Assert.Skip(
                "Deferred: configure AZURE_SPEECH_ENDPOINT and AZURE_SPEECH_KEY for Azure live validation.");
        }

        var provider = new AzureTranslationProvider();
        await using var session = await provider.CreateSessionAsync(
            new TranslationSessionOptions(endpoint, key, TargetLanguage.SimplifiedChinese),
            TestContext.Current.CancellationToken);

        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WavWithExtraChunksStreamsOnlyPcmData()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            WriteWaveWithJunkChunk(path, dataLength: 6_400);
            using var reader = new WaveFileReader(path);
            var expectedDataLength = reader.Length;
            var session = new CountingTranslationSession();

            var bytesWritten = await WavePcmStreamer.StreamAsync(
                path,
                session,
                TimeSpan.Zero,
                TestContext.Current.CancellationToken);

            Assert.Equal(expectedDataLength, bytesWritten);
            Assert.Equal(expectedDataLength, session.BytesWritten);
            Assert.NotEqual(new FileInfo(path).Length - 44, session.BytesWritten);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteWaveWithJunkChunk(string path, int dataLength)
    {
        const int formatChunkSize = 16;
        const int junkChunkSize = 5;
        var paddedJunkSize = junkChunkSize + 1;
        var riffPayloadSize = 4 + 8 + formatChunkSize + 8 + paddedJunkSize + 8 + dataLength;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(riffPayloadSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(formatChunkSize);
        writer.Write((short)WaveFormatEncoding.Pcm);
        writer.Write((short)1);
        writer.Write(16_000);
        writer.Write(32_000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("JUNK"u8);
        writer.Write(junkChunkSize);
        writer.Write(new byte[junkChunkSize]);
        writer.Write((byte)0);
        writer.Write("data"u8);
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
    }

    private sealed class CountingTranslationSession : ITranslationSession
    {
        public long BytesWritten { get; private set; }

        public event EventHandler<TranslationUpdateEventArgs>? UpdateReceived
        {
            add { }
            remove { }
        }

        public ValueTask StartAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask WriteAudioAsync(
            ReadOnlyMemory<byte> audio,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BytesWritten += audio.Length;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeAzureTranslationTransport : IAzureTranslationTransport
    {
        public string TargetLanguage => "en";

        public Exception? StopException { get; init; }

        public bool BlockStart { get; init; }

        public bool BlockStop { get; init; }

        public TaskCompletionSource StartEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource StartRelease { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource StopEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource StopRelease { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int CloseCount { get; private set; }

        public int DisposeCount { get; private set; }

        public event EventHandler<Microsoft.CognitiveServices.Speech.Translation.TranslationRecognitionEventArgs>? Recognizing
        {
            add { }
            remove { }
        }

        public event EventHandler<Microsoft.CognitiveServices.Speech.Translation.TranslationRecognitionEventArgs>? Recognized
        {
            add { }
            remove { }
        }

        public event EventHandler<Microsoft.CognitiveServices.Speech.Translation.TranslationRecognitionCanceledEventArgs>? Canceled
        {
            add { }
            remove { }
        }

        public Task StartAsync()
        {
            StartEntered.TrySetResult();
            return BlockStart ? StartRelease.Task : Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopEntered.TrySetResult();
            if (StopException is not null)
            {
                return Task.FromException(StopException);
            }

            return BlockStop ? StopRelease.Task : Task.CompletedTask;
        }

        public void Write(byte[] audio)
        {
        }

        public void CloseInput()
        {
            CloseCount++;
        }

        public void Dispose()
        {
            DisposeCount++;
        }

        public void ReleaseStart()
        {
            StartRelease.TrySetResult();
        }

        public void ReleaseStop()
        {
            StopRelease.TrySetResult();
        }
    }
}
