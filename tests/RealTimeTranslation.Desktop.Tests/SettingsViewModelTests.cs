using RealTimeTranslation.Core;
using RealTimeTranslation.Desktop.Services;
using RealTimeTranslation.Desktop.ViewModels;

namespace RealTimeTranslation.Desktop.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task StartRequiresValidConfigurationAndSuccessfulConnectionTest()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        Assert.False(model.CanStart);

        model.Endpoint = "not-an-endpoint";
        model.ApiKey = "test-key";
        await model.TestConnectionAsync(CancellationToken.None);
        Assert.False(model.CanStart);
        Assert.Equal(0, fixture.Tester.CallCount);

        model.Endpoint = "https://speech.example.com";
        await model.TestConnectionAsync(CancellationToken.None);
        Assert.True(model.CanStart);

        model.SelectedAudioSource = (AudioSourceKind)99;
        Assert.False(model.CanStart);
        model.SelectedAudioSource = AudioSourceKind.SystemAudio;
        model.SelectedTargetLanguage = (TargetLanguage)99;
        Assert.False(model.CanStart);
    }

    [Fact]
    public async Task TestConnectionSavesKeyOnlyAfterSuccess()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        model.Endpoint = "https://speech.example.com";
        model.ApiKey = "test-key";
        fixture.Tester.Failure = new IOException("simulated network error");

        await model.TestConnectionAsync(CancellationToken.None);
        Assert.Null(fixture.Credentials.StoredKey);
        Assert.False(model.CanStart);

        fixture.Tester.Failure = null;
        await model.TestConnectionAsync(CancellationToken.None);
        Assert.Equal("test-key", fixture.Credentials.StoredKey);
        Assert.True(model.CanStart);
    }

    [Fact]
    public async Task EditingKeyDuringConnectionTestDoesNotSaveUntestedValue()
    {
        var fixture = new Fixture();
        fixture.Tester.Pending = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var model = fixture.Create();
        model.Endpoint = "https://speech.example.com";
        model.ApiKey = "first-key";

        var testing = model.TestConnectionAsync(CancellationToken.None);
        model.ApiKey = "second-key";
        fixture.Tester.Pending.TrySetResult();
        await testing;

        Assert.Null(fixture.Credentials.StoredKey);
        Assert.False(model.CanStart);
    }

    [Fact]
    public async Task EditingKeyWhileCredentialWriteIsPendingDoesNotVerifyUntestedValue()
    {
        var fixture = new Fixture();
        fixture.Credentials.WriteGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var model = fixture.Create();
        model.Endpoint = "https://speech.example.com";
        model.ApiKey = "first-key";

        var testing = model.TestConnectionAsync(CancellationToken.None);
        model.ApiKey = "second-key";
        fixture.Credentials.WriteGate.TrySetResult();
        await testing;

        Assert.Equal("first-key", fixture.Credentials.StoredKey);
        Assert.False(model.CanStart);
    }

    [Fact]
    public async Task StartDoesNotUploadBeforeSubtitleWindowIsReady()
    {
        var fixture = new Fixture();
        fixture.Windows.CanShowSubtitles = false;
        var model = fixture.Create();
        model.Endpoint = "https://speech.example.com";
        model.ApiKey = "test-key";
        await model.TestConnectionAsync(CancellationToken.None);

        Assert.False(model.CanStart);
        await model.StartTranslationAsync(CancellationToken.None);
        Assert.Equal(0, fixture.StartCount);
    }

    [Fact]
    public async Task EditingConfigurationDuringSaveDoesNotStartUntestedSession()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        model.Endpoint = "https://speech.example.com";
        model.ApiKey = "test-key";
        await model.TestConnectionAsync(CancellationToken.None);
        fixture.Settings.SaveGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var starting = model.StartTranslationAsync(CancellationToken.None);
        model.SelectedTargetLanguage = TargetLanguage.Russian;
        fixture.Settings.SaveGate.TrySetResult();
        await starting;

        Assert.Equal(0, fixture.StartCount);
        Assert.Equal(0, fixture.Windows.ShowCount);
    }

    [Fact]
    public async Task LoadingSettingsNeverStartsTranslation()
    {
        var fixture = new Fixture();
        fixture.Settings.Loaded = new AppSettings(
            "https://speech.example.com",
            AudioSourceKind.Microphone,
            TargetLanguage.Russian,
            10,
            20);
        fixture.Credentials.StoredKey = "saved-key";
        var model = fixture.Create();

        await model.InitializeAsync(CancellationToken.None);

        Assert.Equal("https://speech.example.com", model.Endpoint);
        Assert.Equal(AudioSourceKind.Microphone, model.SelectedAudioSource);
        Assert.Equal(TargetLanguage.Russian, model.SelectedTargetLanguage);
        Assert.Equal("saved-key", model.ApiKey);
        Assert.Equal(0, fixture.StartCount);
        Assert.True(model.CanStart);
    }

    [Fact]
    public void SelectingSystemAudioClearsMicrophoneSelection()
    {
        var model = new Fixture().Create();
        model.IsMicrophoneSelected = true;
        Assert.True(model.IsMicrophoneSelected);

        model.IsSystemAudioSelected = true;

        Assert.True(model.IsSystemAudioSelected);
        Assert.False(model.IsMicrophoneSelected);
        Assert.Equal(AudioSourceKind.SystemAudio, model.SelectedAudioSource);
    }

    [Fact]
    public async Task StartSavesOnlySettingsAndOpensSubtitleFlowOnce()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        model.Endpoint = "https://speech.example.com";
        model.ApiKey = "test-key";
        model.SelectedTargetLanguage = TargetLanguage.English;
        await model.TestConnectionAsync(CancellationToken.None);

        await model.StartTranslationAsync(CancellationToken.None);

        Assert.Equal(1, fixture.StartCount);
        Assert.Equal(1, fixture.Windows.ShowCount);
        Assert.NotNull(fixture.Settings.Saved);
        Assert.Equal("https://speech.example.com", fixture.Settings.Saved.Endpoint);
        Assert.Equal(TargetLanguage.English, fixture.Settings.Saved.TargetLanguage);
        Assert.Equal("test-key", fixture.Credentials.StoredKey);
    }

    [Fact]
    public async Task SubtitleWindowOpensBeforeAudioSessionStarts()
    {
        var fixture = new Fixture();
        fixture.RequireWindowBeforeStart = true;
        var model = fixture.Create();
        model.Endpoint = "https://speech.example.com";
        model.ApiKey = "test-key";
        await model.TestConnectionAsync(CancellationToken.None);

        await model.StartTranslationAsync(CancellationToken.None);

        Assert.Equal(1, fixture.StartCount);
        Assert.Equal(1, fixture.Windows.ShowCount);
    }

    [Fact]
    public async Task DeleteCredentialClearsStoredAndInMemoryKey()
    {
        var fixture = new Fixture();
        fixture.Credentials.StoredKey = "saved-key";
        var model = fixture.Create();
        await model.InitializeAsync(CancellationToken.None);

        await model.DeleteCredentialAsync(CancellationToken.None);

        Assert.Null(fixture.Credentials.StoredKey);
        Assert.Equal(string.Empty, model.ApiKey);
        Assert.False(model.CanStart);
    }

    [Fact]
    public async Task CredentialCannotBeDeletedWhileTranslationIsActive()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        model.Endpoint = "https://speech.example.com";
        model.ApiKey = "test-key";
        await model.TestConnectionAsync(CancellationToken.None);
        await model.StartTranslationAsync(CancellationToken.None);

        await model.DeleteCredentialAsync(CancellationToken.None);

        Assert.Equal("test-key", fixture.Credentials.StoredKey);
        Assert.Equal("test-key", model.ApiKey);
    }

    [Fact]
    public async Task DraggedSubtitlePositionIsSavedWithNonSensitiveSettings()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        model.Endpoint = "https://speech.example.com";
        model.ApiKey = "test-key";

        await model.SaveSubtitlePositionAsync(170, 640, CancellationToken.None);

        Assert.True(model.HasSavedSubtitlePosition);
        Assert.Equal(170, fixture.Settings.Saved?.SubtitleLeft);
        Assert.Equal(640, fixture.Settings.Saved?.SubtitleTop);
        Assert.Equal("https://speech.example.com", fixture.Settings.Saved?.Endpoint);
    }

    [Fact]
    public async Task TopLeftSubtitlePositionIsRestored()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        await model.SaveSubtitlePositionAsync(0, 0, CancellationToken.None);
        fixture.Settings.Loaded = fixture.Settings.Saved;

        var restarted = fixture.Create();
        await restarted.InitializeAsync(CancellationToken.None);

        Assert.True(restarted.HasSavedSubtitlePosition);
        Assert.Equal(0, restarted.SubtitleLeft);
        Assert.Equal(0, restarted.SubtitleTop);
    }

    private sealed class Fixture
    {
        public FakeSettingsStore Settings { get; } = new();
        public FakeCredentialStore Credentials { get; } = new();
        public FakeConnectionTester Tester { get; } = new();
        public FakeWindowOrchestrator Windows { get; } = new();
        public int StartCount { get; private set; }
        public bool RequireWindowBeforeStart { get; set; }

        public SettingsViewModel Create() => new(
            Settings,
            Credentials,
            Tester,
            (_, _) =>
            {
                if (RequireWindowBeforeStart && Windows.ShowCount == 0)
                {
                    throw new InvalidOperationException("Audio started without subtitle window.");
                }

                StartCount++;
                return ValueTask.CompletedTask;
            },
            Windows,
            kind => kind == AudioSourceKind.SystemAudio ? "Default speakers" : "Default microphone");
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public AppSettings? Loaded { get; set; }
        public AppSettings? Saved { get; private set; }
        public TaskCompletionSource? SaveGate { get; set; }
        public ValueTask<AppSettings?> LoadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Loaded);
        public async ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            if (SaveGate is not null)
            {
                await SaveGate.Task.WaitAsync(cancellationToken);
            }

            Saved = settings;
        }
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        public string? StoredKey { get; set; }
        public TaskCompletionSource? WriteGate { get; set; }
        public ValueTask<string?> ReadApiKeyAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(StoredKey);
        public async ValueTask WriteApiKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            if (WriteGate is not null)
            {
                await WriteGate.Task.WaitAsync(cancellationToken);
            }

            StoredKey = apiKey;
        }
        public ValueTask DeleteApiKeyAsync(CancellationToken cancellationToken)
        {
            StoredKey = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeConnectionTester : ITranslationConnectionTester
    {
        public int CallCount { get; private set; }
        public Exception? Failure { get; set; }
        public TaskCompletionSource? Pending { get; set; }
        public async Task TestAsync(string endpoint, string apiKey, CancellationToken cancellationToken)
        {
            CallCount++;
            if (Pending is not null)
            {
                await Pending.Task.WaitAsync(cancellationToken);
            }

            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }

    private sealed class FakeWindowOrchestrator : IWindowOrchestrator
    {
        public int ShowCount { get; private set; }
        public bool CanShowSubtitles { get; set; } = true;
        public void ShowSubtitles() => ShowCount++;
        public void ShowSettings() { }
        public void ShutdownApplication() { }
    }
}
