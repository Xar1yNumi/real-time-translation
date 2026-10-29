using System.Diagnostics;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Azure;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] arguments)
{
    if (!TryParseArguments(arguments, out var wavePath, out var targetLanguage))
    {
        Console.Error.WriteLine(
            "Usage: RealTimeTranslation.Feasibility --wav <path> --target zh-Hans|en|ru");
        return 1;
    }

    var endpoint = Environment.GetEnvironmentVariable("AZURE_SPEECH_ENDPOINT");
    var apiKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine(
            "Set AZURE_SPEECH_ENDPOINT and AZURE_SPEECH_KEY before running this command.");
        return 1;
    }

    if (!File.Exists(wavePath))
    {
        Console.Error.WriteLine("The specified WAV file does not exist.");
        return 1;
    }

    try
    {
        var stopwatch = Stopwatch.StartNew();
        var firstPartialSeen = 0;
        var finalResult = new TaskCompletionSource<FinalUtteranceUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new AzureTranslationProvider();
        await using var session = await provider.CreateSessionAsync(
            new TranslationSessionOptions(endpoint, apiKey, targetLanguage),
            CancellationToken.None).ConfigureAwait(false);
        session.UpdateReceived += (_, eventArgs) =>
        {
            if (eventArgs.Update is PartialTranscriptUpdate partial
                && Interlocked.CompareExchange(ref firstPartialSeen, 1, 0) == 0)
            {
                Console.WriteLine(
                    $"first-partial-ms={stopwatch.ElapsedMilliseconds} text={partial.Text}");
            }
            else if (eventArgs.Update is FinalUtteranceUpdate final)
            {
                finalResult.TrySetResult(final);
            }
            else if (eventArgs.Update is TranslationCanceledUpdate canceled)
            {
                finalResult.TrySetException(
                    new InvalidOperationException($"Azure session canceled: {canceled.ErrorCode}"));
            }
        };

        await session.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await WavePcmStreamer.StreamAsync(
            wavePath,
            session,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None).ConfigureAwait(false);
        var result = await finalResult.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        Console.WriteLine(
            $"final-ms={stopwatch.ElapsedMilliseconds} source={result.OriginalText} "
            + $"translation={result.TranslatedText} detected={result.DetectedLanguage}");
        await session.StopAsync(CancellationToken.None).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(result.TranslatedText) ? 1 : 0;
    }
    catch (Exception exception)
    {
        var message = exception.Message
            .Replace(apiKey, "[redacted]", StringComparison.Ordinal)
            .Replace(endpoint, "[endpoint]", StringComparison.Ordinal);
        Console.Error.WriteLine($"{exception.GetType().Name}: {message}");
        return 1;
    }
}

static bool TryParseArguments(
    string[] arguments,
    out string wavePath,
    out TargetLanguage targetLanguage)
{
    wavePath = string.Empty;
    targetLanguage = default;
    string? targetCode = null;

    for (var index = 0; index < arguments.Length; index++)
    {
        if (string.Equals(arguments[index], "--wav", StringComparison.OrdinalIgnoreCase)
            && index + 1 < arguments.Length)
        {
            wavePath = arguments[++index];
        }
        else if (string.Equals(arguments[index], "--target", StringComparison.OrdinalIgnoreCase)
                 && index + 1 < arguments.Length)
        {
            targetCode = arguments[++index];
        }
        else
        {
            return false;
        }
    }

    if (string.IsNullOrWhiteSpace(wavePath))
    {
        return false;
    }

    targetLanguage = targetCode?.ToLowerInvariant() switch
    {
        "zh-hans" => TargetLanguage.SimplifiedChinese,
        "en" => TargetLanguage.English,
        "ru" => TargetLanguage.Russian,
        _ => default
    };
    return targetCode is not null
        && (targetCode.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
            || targetCode.Equals("en", StringComparison.OrdinalIgnoreCase)
            || targetCode.Equals("ru", StringComparison.OrdinalIgnoreCase));
}
