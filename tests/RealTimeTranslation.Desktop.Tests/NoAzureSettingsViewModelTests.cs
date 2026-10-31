using RealTimeTranslation.Core;
using RealTimeTranslation.Desktop.Services;
using RealTimeTranslation.Desktop.ViewModels;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Desktop.Tests;

public sealed class NoAzureSettingsViewModelTests
{
    [Fact]
    public async Task StartRequiresValidatedModelAndTextSharingConsentButNoCredentials()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        await model.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.False(model.CanStart);
        Assert.False(model.ModelReady);

        await model.DownloadModelAsync(TestContext.Current.CancellationToken);
        Assert.True(model.ModelReady);
        Assert.False(model.CanStart);
        model.HasTextSharingConsent = true;
        Assert.True(model.CanStart);

        await model.StartTranslationAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, fixture.StartCount);
        Assert.Equal(string.Empty, fixture.StartedOptions?.Endpoint);
        Assert.Equal(string.Empty, fixture.StartedOptions?.ApiKey);
        Assert.Equal(string.Empty, fixture.Settings.Saved?.Endpoint);
        Assert.Equal(1, fixture.Windows.ShowCount);
        model.MarkStopped();
        Assert.Null(model.ActiveOptions);
        Assert.True(model.CanStart);
    }

    [Fact]
    public async Task ValidCachedModelIsFoundWithoutDownload()
    {
        var fixture = new Fixture();
        fixture.Models.CachedPath = "verified-path";
        var model = fixture.Create();

        await model.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.True(model.ModelReady);
        Assert.Equal(0, fixture.Models.DownloadCount);
        model.HasTextSharingConsent = true;
        Assert.True(model.CanStart);
    }

    [Fact]
    public async Task ModelDownloadFailureIsVisibleAndKeepsStartDisabled()
    {
        var fixture = new Fixture();
        fixture.Models.DownloadFailure = new IOException("private path details");
        var model = fixture.Create();
        await model.InitializeAsync(TestContext.Current.CancellationToken);
        model.HasTextSharingConsent = true;

        await model.DownloadModelAsync(TestContext.Current.CancellationToken);

        Assert.False(model.ModelReady);
        Assert.False(model.CanStart);
        Assert.Contains("失败", model.StatusMessage);
        Assert.DoesNotContain("private path details", model.StatusMessage);
    }

    private sealed class Fixture
    {
        public FakeSettingsStore Settings { get; } = new();
        public FakeModelStore Models { get; } = new();
        public FakeWindowOrchestrator Windows { get; } = new();
        public StartTranslationOptions? StartedOptions { get; private set; }
        public int StartCount { get; private set; }

        public SettingsViewModel Create() => new(
            Settings, Models,
            (options, _) =>
            {
                StartedOptions = options;
                StartCount++;
                return ValueTask.CompletedTask;
            },
            Windows,
            _ => "Default device");
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public AppSettings? Saved { get; private set; }

        public ValueTask<AppSettings?> LoadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<AppSettings?>(null);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            Saved = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeModelStore : IWhisperModelStore
    {
        public string? CachedPath { get; set; }
        public Exception? DownloadFailure { get; set; }
        public int DownloadCount { get; private set; }

        public Task<string?> VerifyCachedAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CachedPath);

        public Task<string> EnsureAvailableAsync(
            IProgress<double>? progress, CancellationToken cancellationToken)
        {
            DownloadCount++;
            if (DownloadFailure is not null)
            {
                throw DownloadFailure;
            }

            progress?.Report(1);
            CachedPath = "verified-path";
            return Task.FromResult(CachedPath);
        }

        public void Dispose() { }
    }

    private sealed class FakeWindowOrchestrator : IWindowOrchestrator
    {
        public bool CanShowSubtitles { get; set; } = true;
        public int ShowCount { get; private set; }

        public void ShowSubtitles() => ShowCount++;
        public void ShowSettings() { }
        public void ShutdownApplication() { }
    }
}
