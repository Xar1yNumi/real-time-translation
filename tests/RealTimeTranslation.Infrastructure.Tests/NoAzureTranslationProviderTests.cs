using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class NoAzureTranslationProviderTests
{
    [Fact]
    public async Task ReusesPreparedRecognizerAcrossSessionsAndDisposesItOnce()
    {
        var recognizer = new FakeRecognizer();
        var created = 0;
        var translator = new FakeTranslator();
        await using var provider = new NoAzureTranslationProvider(
            _ =>
            {
                created++;
                return Task.FromResult<IWhisperTranscriber>(recognizer);
            }, translator);
        var options = new TranslationSessionOptions("", "", TargetLanguage.SimplifiedChinese);

        var first = await provider.CreateSessionAsync(options, TestContext.Current.CancellationToken);
        await first.DisposeAsync();
        var second = await provider.CreateSessionAsync(options, TestContext.Current.CancellationToken);
        await second.DisposeAsync();

        Assert.Equal(1, created);
        Assert.Equal(1, recognizer.WarmupCount);
        Assert.Equal(0, recognizer.DisposeCount);
        await provider.DisposeAsync();
        Assert.Equal(1, recognizer.DisposeCount);
    }

    private sealed class FakeRecognizer : IWhisperTranscriber
    {
        public int WarmupCount { get; private set; }
        public int DisposeCount { get; private set; }

        public Task<TranscriptionResult> TranscribeAsync(
            ReadOnlyMemory<byte> pcm16Mono, CancellationToken cancellationToken)
        {
            WarmupCount++;
            return Task.FromResult(new TranscriptionResult(TranscriptionStatus.Empty, null, null));
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTranslator : IMyMemoryTranslator
    {
        public Task<string> TranslateAsync(
            string text, string sourceLanguage, string targetLanguage,
            CancellationToken cancellationToken) => Task.FromResult(text);
    }
}
