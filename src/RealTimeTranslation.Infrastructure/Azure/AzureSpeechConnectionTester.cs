using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Azure;

public sealed class AzureSpeechConnectionTester : ITranslationConnectionTester
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(15);

    public async Task TestAsync(
        string endpoint,
        string apiKey,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new AppFailureException(AppError.For(AppErrorCode.InvalidEndpoint));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        cancellationToken.ThrowIfCancellationRequested();

        var configuration = SpeechTranslationConfig.FromEndpoint(uri, apiKey);
        configuration.SetProperty(PropertyId.SpeechServiceConnection_LanguageIdMode, "Continuous");
        configuration.AddTargetLanguage(LanguageCatalog.GetTargetCode(TargetLanguage.SimplifiedChinese));
        var languages = AutoDetectSourceLanguageConfig.FromLanguages(
            LanguageCatalog.SourceLocales.ToArray());
        using var format = AudioStreamFormat.GetWaveFormatPCM(16_000, 16, 1);
        using var stream = AudioInputStream.CreatePushStream(format);
        using var audioConfig = AudioConfig.FromStreamInput(stream);
        using var recognizer = new TranslationRecognizer(configuration, languages, audioConfig);
        var sessionStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        recognizer.SessionStarted += (_, _) => sessionStarted.TrySetResult();
        recognizer.Canceled += (_, args) => sessionStarted.TrySetException(
            new AppFailureException(AppError.For(AzureErrorMapper.Map(args.ErrorCode))));

        try
        {
            await recognizer.StartContinuousRecognitionAsync()
                .WaitAsync(ConnectionTimeout, cancellationToken).ConfigureAwait(false);
            await sessionStarted.Task.WaitAsync(ConnectionTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            stream.Close();
            await recognizer.StopContinuousRecognitionAsync()
                .WaitAsync(ConnectionTimeout, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
