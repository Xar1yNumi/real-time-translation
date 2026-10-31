using System.Buffers.Binary;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class WhisperTranscriberTests
{
    [Fact]
    public async Task EmptyInputDoesNotRunInference()
    {
        var engine = new FakeInference();
        await using var transcriber = await WhisperTranscriber.CreateAsync(
            new FakeModelStore(), engine, TestContext.Current.CancellationToken);

        var result = await transcriber.TranscribeAsync(
            ReadOnlyMemory<byte>.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(TranscriptionStatus.Empty, result.Status);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public async Task UnsupportedDetectedLanguageIsClassified()
    {
        var engine = new FakeInference([new("bonjour", "fr")]);
        await using var transcriber = await WhisperTranscriber.CreateAsync(
            new FakeModelStore(), engine, TestContext.Current.CancellationToken);

        var result = await transcriber.TranscribeAsync(
            new byte[32_000], TestContext.Current.CancellationToken);

        Assert.Equal(TranscriptionStatus.UnsupportedLanguage, result.Status);
        Assert.Equal("fr", result.LanguageCode);
        Assert.Null(result.Text);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    [InlineData("ru")]
    public async Task SupportedLanguagesReturnRecognizedText(string languageCode)
    {
        var engine = new FakeInference([new(" hello ", languageCode)]);
        await using var transcriber = await WhisperTranscriber.CreateAsync(
            new FakeModelStore(), engine, TestContext.Current.CancellationToken);

        var result = await transcriber.TranscribeAsync(
            new byte[32_000], TestContext.Current.CancellationToken);

        Assert.Equal(TranscriptionStatus.Recognized, result.Status);
        Assert.Equal("hello", result.Text);
        Assert.Equal(languageCode, result.LanguageCode);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(engine.LastWav!, 0, 4));
        Assert.Equal(32_000, BinaryPrimitives.ReadInt32LittleEndian(engine.LastWav.AsSpan(40, 4)));
    }

    [Fact]
    public async Task CancellationDoesNotRunInference()
    {
        var engine = new FakeInference();
        await using var transcriber = await WhisperTranscriber.CreateAsync(
            new FakeModelStore(), engine, TestContext.Current.CancellationToken);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            transcriber.TranscribeAsync(new byte[32_000], canceled.Token));
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public async Task ConcurrentDisposalDoesNotFaultWaiters()
    {
        var engine = new BlockingInference();
        var transcriber = await WhisperTranscriber.CreateAsync(
            new FakeModelStore(), engine, TestContext.Current.CancellationToken);
        var running = transcriber.TranscribeAsync(
            new byte[32_000], TestContext.Current.CancellationToken);
        await engine.Started.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var firstDispose = transcriber.DisposeAsync().AsTask();
        var secondDispose = transcriber.DisposeAsync().AsTask();

        engine.Release.SetResult();

        await Task.WhenAll(running, firstDispose, secondDispose).WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RunningInferenceCanBeCanceled()
    {
        var engine = new BlockingInference();
        await using var transcriber = await WhisperTranscriber.CreateAsync(
            new FakeModelStore(), engine, TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var running = transcriber.TranscribeAsync(new byte[32_000], cancellation.Token);
        await engine.Started.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    private sealed class FakeModelStore : IWhisperModelStore
    {
        public Task<string?> VerifyCachedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>("validated-model-path");

        public Task<string> EnsureAvailableAsync(IProgress<double>? progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("validated-model-path");
        }

        public void Dispose() { }
    }

    private sealed class FakeInference(IReadOnlyList<InferenceSegment>? segments = null) : IWhisperInference
    {
        public int Calls { get; private set; }
        public byte[]? LastWav { get; private set; }

        public Task<IReadOnlyList<InferenceSegment>> InferAsync(
            string modelPath, ReadOnlyMemory<byte> wav, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastWav = wav.ToArray();
            return Task.FromResult(segments ?? (IReadOnlyList<InferenceSegment>)[]);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingInference : IWhisperInference
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<InferenceSegment>> InferAsync(
            string modelPath, ReadOnlyMemory<byte> wav, CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return [];
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
