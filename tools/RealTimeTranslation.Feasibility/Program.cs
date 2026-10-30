using System.Collections.Concurrent;
using System.Diagnostics;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Audio;
using RealTimeTranslation.Infrastructure.Azure;
using RealTimeTranslation.Feasibility;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] arguments)
{
    if (!FeasibilityOptions.TryParse(arguments, out var options))
    {
        Console.Error.WriteLine(
            "Usage: --wav <path> --target zh-Hans|en|ru [--show-text] "
            + "OR --source system|microphone --target zh-Hans|en|ru "
            + "--duration 00:05:00 [--show-text]");
        return 1;
    }

    var endpoint = Environment.GetEnvironmentVariable("AZURE_SPEECH_ENDPOINT");
    var apiKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine("Set AZURE_SPEECH_ENDPOINT and AZURE_SPEECH_KEY.");
        return 1;
    }

    if (options.Source == FeasibilitySource.Wave && !File.Exists(options.WavePath))
    {
        Console.Error.WriteLine("The specified WAV file does not exist.");
        return 1;
    }

    try
    {
        return options.Source == FeasibilitySource.Wave
            ? await RunWaveAsync(options, endpoint, apiKey).ConfigureAwait(false)
            : await RunLiveAsync(options, endpoint, apiKey).ConfigureAwait(false);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"error-category={exception.GetType().Name}");
        return 1;
    }
}

static async Task<int> RunWaveAsync(
    FeasibilityOptions options,
    string endpoint,
    string apiKey)
{
    var stopwatch = Stopwatch.StartNew();
    var firstPartialSeen = 0;
    var partialTimes = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
    var finalResult = new TaskCompletionSource<FinalUtteranceUpdate>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var provider = new AzureTranslationProvider();
    await using var session = await provider.CreateSessionAsync(
        new TranslationSessionOptions(endpoint, apiKey, options.TargetLanguage),
        CancellationToken.None).ConfigureAwait(false);
    session.UpdateReceived += (_, eventArgs) =>
    {
        switch (eventArgs.Update)
        {
            case PartialTranscriptUpdate partial:
                partialTimes.TryAdd(partial.UtteranceId, stopwatch.ElapsedMilliseconds);
                if (Interlocked.CompareExchange(ref firstPartialSeen, 1, 0) == 0)
                {
                    Log("partial", stopwatch.ElapsedMilliseconds, null, options.ShowText, partial.Text, null);
                }

                break;
            case FinalUtteranceUpdate final:
                var hasPartial = partialTimes.TryRemove(final.UtteranceId, out var partialTime);
                Log("final", stopwatch.ElapsedMilliseconds, final.DetectedLanguage,
                    options.ShowText, final.OriginalText, final.TranslatedText,
                    hasPartial ? stopwatch.ElapsedMilliseconds - partialTime : null);
                finalResult.TrySetResult(final);
                break;
            case TranslationCanceledUpdate:
                Log("canceled", stopwatch.ElapsedMilliseconds, null, false, null, null);
                finalResult.TrySetException(new InvalidOperationException("Azure session canceled."));
                break;
        }
    };

    await session.StartAsync(CancellationToken.None).ConfigureAwait(false);
    await WavePcmStreamer.StreamAsync(
        options.WavePath!,
        session,
        TimeSpan.FromMilliseconds(100),
        CancellationToken.None).ConfigureAwait(false);
    var result = await finalResult.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
    await session.StopAsync(CancellationToken.None).ConfigureAwait(false);
    return string.IsNullOrWhiteSpace(result.TranslatedText) ? 1 : 0;
}

static async Task<int> RunLiveAsync(
    FeasibilityOptions options,
    string endpoint,
    string apiKey)
{
    var stopwatch = Stopwatch.StartNew();
    var partialTimes = new ConcurrentDictionary<long, long>();
    var finalCount = 0;
    var reconnectCount = 0;
    var failed = 0;
    using var process = Process.GetCurrentProcess();
    process.Refresh();
    var peakPrivateBytes = process.PrivateMemorySize64;
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;

    try
    {
        await using var coordinator = new TranslationCoordinator(
            new AzureTranslationProvider(),
            kind => kind switch
            {
                AudioSourceKind.SystemAudio => new SystemLoopbackAudioSource(),
                AudioSourceKind.Microphone => new MicrophoneAudioSource(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            },
            static (delay, token) => Task.Delay(delay, token));
        coordinator.StateChanged += (_, eventArgs) =>
        {
            Console.WriteLine(
                $"{DateTimeOffset.UtcNow:O} event=state-{eventArgs.State} "
                + $"session-elapsed-ms={stopwatch.ElapsedMilliseconds}");
            if (eventArgs.State == SessionState.Failed)
            {
                Interlocked.Exchange(ref failed, 1);
            }
            else if (eventArgs.State == SessionState.Reconnecting)
            {
                Interlocked.Increment(ref reconnectCount);
            }
        };
        coordinator.SubtitleChanged += (_, eventArgs) =>
        {
            var state = eventArgs.State;
            if (state.IsOriginalPartial)
            {
                partialTimes.TryAdd(state.Sequence, stopwatch.ElapsedMilliseconds);
                Log("partial", stopwatch.ElapsedMilliseconds, null,
                    options.ShowText, state.OriginalText, null);
            }
            else
            {
                var hasPartial = partialTimes.TryRemove(state.Sequence, out var partialTime);
                if (!string.IsNullOrWhiteSpace(state.TranslatedText))
                {
                    Interlocked.Increment(ref finalCount);
                }

                Log("final", stopwatch.ElapsedMilliseconds, state.DetectedLanguage,
                    options.ShowText, state.OriginalText, state.TranslatedText,
                    hasPartial ? stopwatch.ElapsedMilliseconds - partialTime : null);
            }
        };

        await coordinator.StartAsync(
            new StartTranslationOptions(endpoint, apiKey, options.AudioSource!.Value,
                options.TargetLanguage),
            cancellation.Token).ConfigureAwait(false);
        var runTimer = Stopwatch.StartNew();
        while (runTimer.Elapsed < options.Duration
            && !cancellation.IsCancellationRequested
            && Volatile.Read(ref failed) == 0)
        {
            var remaining = options.Duration - runTimer.Elapsed;
            var interval = remaining < TimeSpan.FromMinutes(1)
                ? remaining : TimeSpan.FromMinutes(1);
            try
            {
                await Task.Delay(interval, cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }

            process.Refresh();
            var privateBytes = process.PrivateMemorySize64;
            peakPrivateBytes = Math.Max(peakPrivateBytes, privateBytes);
            Console.WriteLine(
                $"{DateTimeOffset.UtcNow:O} event=heartbeat "
                + $"run-elapsed-ms={runTimer.ElapsedMilliseconds} "
                + $"final-count={Volatile.Read(ref finalCount)} "
                + $"reconnect-count={Volatile.Read(ref reconnectCount)} "
                + $"private-mib={privateBytes / 1_048_576.0:F1}");
        }

        await coordinator.StopAsync(CancellationToken.None).ConfigureAwait(false);
        process.Refresh();
        peakPrivateBytes = Math.Max(peakPrivateBytes, process.PrivateMemorySize64);
        Console.WriteLine(
            $"{DateTimeOffset.UtcNow:O} event=summary "
            + $"run-elapsed-ms={runTimer.ElapsedMilliseconds} "
            + $"final-count={Volatile.Read(ref finalCount)} "
            + $"reconnect-count={Volatile.Read(ref reconnectCount)} "
            + $"peak-private-mib={peakPrivateBytes / 1_048_576.0:F1} "
            + $"failed={Volatile.Read(ref failed)}");
        return runTimer.Elapsed >= options.Duration
            && Volatile.Read(ref failed) == 0
            && Volatile.Read(ref finalCount) > 0 ? 0 : 1;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }
}

static void Log(
    string kind,
    long sessionElapsedMilliseconds,
    string? detectedLanguage,
    bool showText,
    string? original,
    string? translation,
    long? partialToFinalMilliseconds = null)
{
    var line = $"{DateTimeOffset.UtcNow:O} event={kind} "
        + $"session-elapsed-ms={sessionElapsedMilliseconds} "
        + $"detected={detectedLanguage ?? "unknown"}";
    if (partialToFinalMilliseconds is not null)
    {
        line += $" partial-to-final-ms={partialToFinalMilliseconds.Value}";
    }

    if (showText)
    {
        line += $" original={original ?? string.Empty} translation={translation ?? string.Empty}";
    }

    Console.WriteLine(line);
}
