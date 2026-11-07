using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Azure;

internal interface IAzureTranslationTransport : IDisposable
{
    string TargetLanguage { get; }

    event EventHandler<TranslationRecognitionEventArgs>? Recognizing;

    event EventHandler<TranslationRecognitionEventArgs>? Recognized;

    event EventHandler<TranslationRecognitionCanceledEventArgs>? Canceled;

    Task StartAsync();

    Task StopAsync();

    void Write(byte[] audio);

    void CloseInput();
}

internal sealed class AzureTranslationTransport : IAzureTranslationTransport
{
    private readonly AudioStreamFormat streamFormat;
    private readonly PushAudioInputStream pushStream;
    private readonly AudioConfig audioConfig;
    private readonly TranslationRecognizer recognizer;
    private int inputClosed;
    private int disposed;

    public AzureTranslationTransport(TranslationSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiKey);

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new ArgumentException("Azure Speech endpoint must be an absolute URI.", nameof(options));
        }

        TargetLanguage = LanguageCatalog.GetTargetCode(options.TargetLanguage);
        (streamFormat, pushStream, audioConfig, recognizer) = CreateResources(
            endpoint,
            options.ApiKey,
            TargetLanguage);
    }

    public string TargetLanguage { get; }

    public event EventHandler<TranslationRecognitionEventArgs>? Recognizing
    {
        add => recognizer.Recognizing += value;
        remove => recognizer.Recognizing -= value;
    }

    public event EventHandler<TranslationRecognitionEventArgs>? Recognized
    {
        add => recognizer.Recognized += value;
        remove => recognizer.Recognized -= value;
    }

    public event EventHandler<TranslationRecognitionCanceledEventArgs>? Canceled
    {
        add => recognizer.Canceled += value;
        remove => recognizer.Canceled -= value;
    }

    public Task StartAsync() => recognizer.StartContinuousRecognitionAsync();

    public Task StopAsync() => recognizer.StopContinuousRecognitionAsync();

    public void Write(byte[] audio) => pushStream.Write(audio);

    public void CloseInput()
    {
        if (Interlocked.Exchange(ref inputClosed, 1) == 0)
        {
            pushStream.Close();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        List<Exception>? failures = null;
        TryCleanup(CloseInput, ref failures);
        TryCleanup(recognizer.Dispose, ref failures);
        TryCleanup(audioConfig.Dispose, ref failures);
        TryCleanup(pushStream.Dispose, ref failures);
        TryCleanup(streamFormat.Dispose, ref failures);
        ThrowCleanupFailures(failures);
    }

    private static (
        AudioStreamFormat StreamFormat,
        PushAudioInputStream PushStream,
        AudioConfig AudioConfig,
        TranslationRecognizer Recognizer) CreateResources(
            Uri endpoint,
            string apiKey,
            string targetLanguage)
    {
        AudioStreamFormat? createdFormat = null;
        PushAudioInputStream? createdStream = null;
        AudioConfig? createdAudioConfig = null;
        TranslationRecognizer? createdRecognizer = null;

        try
        {
            var config = SpeechTranslationConfig.FromEndpoint(endpoint, apiKey);
            config.SetProperty(PropertyId.SpeechServiceConnection_LanguageIdMode, "Continuous");
            config.AddTargetLanguage(targetLanguage);
            var autoDetect = AutoDetectSourceLanguageConfig.FromLanguages(
                LanguageCatalog.SourceLocales.ToArray());

            createdFormat = AudioStreamFormat.GetWaveFormatPCM(16_000, 16, 1);
            createdStream = AudioInputStream.CreatePushStream(createdFormat);
            createdAudioConfig = AudioConfig.FromStreamInput(createdStream);
            createdRecognizer = new TranslationRecognizer(config, autoDetect, createdAudioConfig);
            return (createdFormat, createdStream, createdAudioConfig, createdRecognizer);
        }
        catch
        {
            TryDisposeNoThrow(createdRecognizer);
            TryDisposeNoThrow(createdAudioConfig);
            TryDisposeNoThrow(createdStream);
            TryDisposeNoThrow(createdFormat);
            throw;
        }
    }

    private static void TryDisposeNoThrow(IDisposable? resource)
    {
        try
        {
            resource?.Dispose();
        }
        catch
        {
        }
    }

    private static void TryCleanup(Action cleanup, ref List<Exception>? failures)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            failures ??= [];
            failures.Add(exception);
        }
    }

    private static void ThrowCleanupFailures(List<Exception>? failures)
    {
        if (failures is null)
        {
            return;
        }

        throw failures.Count == 1
            ? failures[0]
            : new AggregateException("Azure translation resources failed to dispose.", failures);
    }
}
