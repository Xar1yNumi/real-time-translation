namespace RealTimeTranslation.Core;

public sealed record AppSettings(
    string Endpoint,
    AudioSourceKind AudioSource,
    TargetLanguage TargetLanguage,
    double SubtitleLeft,
    double SubtitleTop);

public interface IAppSettingsStore
{
    ValueTask<AppSettings?> LoadAsync(CancellationToken cancellationToken);

    ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}

public interface ICredentialStore
{
    ValueTask<string?> ReadApiKeyAsync(CancellationToken cancellationToken);

    ValueTask WriteApiKeyAsync(string apiKey, CancellationToken cancellationToken);

    ValueTask DeleteApiKeyAsync(CancellationToken cancellationToken);
}
