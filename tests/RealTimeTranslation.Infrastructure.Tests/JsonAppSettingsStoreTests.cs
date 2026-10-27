using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Settings;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class JsonAppSettingsStoreTests
{
    [Fact]
    public async Task SavedJsonContainsOnlyNonSensitiveValues()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new JsonAppSettingsStore(path);
            var settings = new AppSettings(
                "https://example.test",
                AudioSourceKind.SystemAudio,
                TargetLanguage.SimplifiedChinese,
                100,
                200);

            await store.SaveAsync(settings, TestContext.Current.CancellationToken);

            var json = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Contains("example.test", json, StringComparison.Ordinal);
            Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SavedSettingsRoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var expected = new AppSettings(
                "https://example.test",
                AudioSourceKind.Microphone,
                TargetLanguage.Russian,
                125.5,
                240.25);
            var store = new JsonAppSettingsStore(path);

            await store.SaveAsync(expected, TestContext.Current.CancellationToken);
            var actual = await store.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(expected, actual);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MissingSettingsFileReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        var store = new JsonAppSettingsStore(path);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(settings);
    }
}
