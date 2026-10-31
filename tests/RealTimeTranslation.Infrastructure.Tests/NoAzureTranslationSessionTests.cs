using System.Buffers.Binary;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class NoAzureTranslationSessionTests
{
    [Fact]
    public async Task WriteBeforeStartIsRejectedAndDisposeIsIdempotent()
    {
        var transcriber = new FakeTranscriber((_, _) => throw new InvalidOperationException());
        var translator = new FakeTranslator((_, _, _, _) => throw new InvalidOperationException());
        var session = new NoAzureTranslationSession(
            transcriber, translator, TargetLanguage.English);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.WriteAudioAsync(Utterance(), TestContext.Current.CancellationToken));
        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(1, transcriber.DisposeCount);
    }

    [Fact]
    public async Task EmitsPartialThenFinalWithTheSameUtteranceIdentity()
    {
        var transcriber = new FakeTranscriber((_, _) => Task.FromResult(
            new TranscriptionResult(TranscriptionStatus.Recognized, "Hello", "en")));
        var translator = new FakeTranslator((_, _, _, _) => Task.FromResult("你好"));
        await using var session = new NoAzureTranslationSession(
            transcriber, translator, TargetLanguage.SimplifiedChinese);
        var updates = new List<TranslationUpdate>();
        var finalSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.UpdateReceived += (_, args) =>
        {
            lock (updates)
            {
                updates.Add(args.Update);
            }

            if (args.Update is FinalUtteranceUpdate)
            {
                finalSeen.TrySetResult();
            }
        };

        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.WriteAudioAsync(Utterance(), TestContext.Current.CancellationToken);
        await finalSeen.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        TranslationUpdate[] snapshot;
        lock (updates) { snapshot = [.. updates]; }
        var partial = Assert.IsType<PartialTranscriptUpdate>(snapshot[0]);
        var final = Assert.IsType<FinalUtteranceUpdate>(snapshot[1]);
        Assert.Equal(partial.UtteranceId, final.UtteranceId);
        Assert.Equal(partial.Sequence, final.Sequence);
        Assert.Equal("Hello", final.OriginalText);
        Assert.Equal("你好", final.TranslatedText);
        Assert.Equal("en", final.DetectedLanguage);
        Assert.Equal(("Hello", "en", "zh"), Assert.Single(translator.Calls));
        await session.StopAsync(TestContext.Current.CancellationToken);
        lock (updates)
        {
            Assert.Single(updates.OfType<FinalUtteranceUpdate>());
        }
    }

    [Fact]
    public async Task PendingUtterancesAreBoundedToTwoAndDropsAreObservable()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transcriber = new FakeTranscriber(async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return new TranscriptionResult(TranscriptionStatus.Recognized, "Hello", "en");
        });
        var translator = new FakeTranslator((_, _, _, _) => Task.FromResult("你好"));
        await using var session = new NoAzureTranslationSession(
            transcriber, translator, TargetLanguage.SimplifiedChinese);
        var dropped = 0;
        session.UpdateReceived += (_, args) =>
        {
            if (args.Update is TranslationWarningUpdate warning &&
                warning.ErrorCode == AppErrorCode.AudioBacklog)
            {
                Interlocked.Increment(ref dropped);
            }
        };

        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.WriteAudioAsync(Utterance(), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        for (var index = 0; index < 100; index++)
        {
            await session.WriteAudioAsync(Utterance(), TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, session.PendingUtterances);
        Assert.InRange(session.BufferedPcmBytes, 0, 768_000);
        Assert.Equal(98, Volatile.Read(ref dropped));
        release.SetResult();
        await session.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StopCancelsInflightWorkAndIsIdempotent()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transcriber = new FakeTranscriber(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new TranscriptionResult(TranscriptionStatus.Empty, null, null);
        });
        var translator = new FakeTranslator((_, _, _, _) => throw new InvalidOperationException());
        await using var session = new NoAzureTranslationSession(
            transcriber, translator, TargetLanguage.SimplifiedChinese);
        var finals = 0;
        session.UpdateReceived += (_, args) =>
        {
            if (args.Update is FinalUtteranceUpdate)
            {
                Interlocked.Increment(ref finals);
            }
        };
        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.WriteAudioAsync(Utterance(), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await session.StopAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, Volatile.Read(ref finals));
        Assert.Equal(0, session.PendingUtterances);
    }

    [Theory]
    [InlineData(AppErrorCode.NetworkUnavailable)]
    [InlineData(AppErrorCode.RateLimited)]
    public async Task TranslationFailureProducesClassifiedCanceledUpdate(AppErrorCode errorCode)
    {
        var transcriber = new FakeTranscriber((_, _) => Task.FromResult(
            new TranscriptionResult(TranscriptionStatus.Recognized, "Hello", "en")));
        var translator = new FakeTranslator((_, _, _, _) =>
            throw new AppFailureException(AppError.For(errorCode)));
        await using var session = new NoAzureTranslationSession(
            transcriber, translator, TargetLanguage.SimplifiedChinese);
        var canceled = new TaskCompletionSource<TranslationCanceledUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.UpdateReceived += (_, args) =>
        {
            if (args.Update is TranslationCanceledUpdate update)
            {
                canceled.TrySetResult(update);
            }
        };

        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.WriteAudioAsync(Utterance(), TestContext.Current.CancellationToken);
        var result = await canceled.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(errorCode, result.ErrorCode);
    }

    [Fact]
    public async Task SameLanguageUsesRecognizedTextWithoutCallingTranslator()
    {
        var transcriber = new FakeTranscriber((_, _) => Task.FromResult(
            new TranscriptionResult(TranscriptionStatus.Recognized, "你好", "zh")));
        var translator = new FakeTranslator((_, _, _, _) =>
            throw new InvalidOperationException("Same-language text must stay local."));
        await using var session = new NoAzureTranslationSession(
            transcriber, translator, TargetLanguage.SimplifiedChinese);
        var finalSeen = new TaskCompletionSource<FinalUtteranceUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.UpdateReceived += (_, args) =>
        {
            if (args.Update is FinalUtteranceUpdate update)
            {
                finalSeen.TrySetResult(update);
            }
        };

        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.WriteAudioAsync(Utterance(), TestContext.Current.CancellationToken);
        var final = await finalSeen.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal("你好", final.TranslatedText);
        Assert.Empty(translator.Calls);
    }

    [Fact]
    public async Task UnsupportedLanguageProducesWarningAndCanProcessNextUtterance()
    {
        var calls = 0;
        var transcriber = new FakeTranscriber((_, _) => Task.FromResult(
            Interlocked.Increment(ref calls) == 1
                ? new TranscriptionResult(TranscriptionStatus.UnsupportedLanguage, null, "fr")
                : new TranscriptionResult(TranscriptionStatus.Recognized, "Hello", "en")));
        var translator = new FakeTranslator((_, _, _, _) => Task.FromResult("你好"));
        await using var session = new NoAzureTranslationSession(
            transcriber, translator, TargetLanguage.SimplifiedChinese);
        var warningSeen = new TaskCompletionSource<TranslationWarningUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var finalSeen = new TaskCompletionSource<FinalUtteranceUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.UpdateReceived += (_, args) =>
        {
            if (args.Update is TranslationWarningUpdate warning)
            {
                warningSeen.TrySetResult(warning);
            }

            if (args.Update is FinalUtteranceUpdate final)
            {
                finalSeen.TrySetResult(final);
            }
        };

        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.WriteAudioAsync(Utterance(), TestContext.Current.CancellationToken);
        await session.WriteAudioAsync(Utterance(), TestContext.Current.CancellationToken);

        var warning = await warningSeen.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var final = await finalSeen.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(AppErrorCode.UnsupportedLanguage, warning.ErrorCode);
        Assert.True(final.Sequence > warning.Sequence);
    }

    [Fact]
    public async Task ProviderCreatesSessionWithoutAzureCredentials()
    {
        var store = new FakeModelStore();
        var translator = new FakeTranslator((_, _, _, _) => Task.FromResult("ok"));
        var provider = new NoAzureTranslationProvider(store, translator);

        await using var session = await provider.CreateSessionAsync(
            new TranslationSessionOptions("", "", TargetLanguage.Russian),
            TestContext.Current.CancellationToken);
        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, store.Calls);
    }

    private static byte[] Utterance()
    {
        var pcm = new byte[(500 + 600) * 32];
        for (var index = 0; index < 500 * 32; index += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(index, 2), 5_000);
        }

        return pcm;
    }

    private sealed class FakeTranscriber(
        Func<ReadOnlyMemory<byte>, CancellationToken, Task<TranscriptionResult>> transcribe)
        : IWhisperTranscriber
    {
        public int DisposeCount { get; private set; }

        public Task<TranscriptionResult> TranscribeAsync(
            ReadOnlyMemory<byte> pcm16Mono, CancellationToken cancellationToken) =>
            transcribe(pcm16Mono, cancellationToken);

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTranslator(
        Func<string, string, string, CancellationToken, Task<string>> translate)
        : IMyMemoryTranslator
    {
        public List<(string Text, string Source, string Target)> Calls { get; } = [];

        public Task<string> TranslateAsync(
            string text, string source, string target, CancellationToken cancellationToken)
        {
            Calls.Add((text, source, target));
            return translate(text, source, target, cancellationToken);
        }
    }

    private sealed class FakeModelStore : IWhisperModelStore
    {
        public int Calls { get; private set; }

        public Task<string> EnsureAvailableAsync(
            IProgress<double>? progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult("validated-path");
        }

        public void Dispose() { }
    }
}
