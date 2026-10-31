using System.Diagnostics;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class MyMemoryLiveTests
{
    [Trait("Category", "MyMemoryLive")]
    [Fact]
    public async Task TranslatesSixDirectionsWithoutCredentials()
    {
        if (Environment.GetEnvironmentVariable("RUN_MYMEMORY_LIVE_TESTS") != "1")
        {
            Assert.Skip("Set RUN_MYMEMORY_LIVE_TESTS=1 for six harmless anonymous HTTPS requests.");
        }

        using var client = new HttpClient();
        var translator = new MyMemoryTranslator(client);
        var samples = new[]
        {
            (Text: "Good morning.", Source: "en", Target: "zh"),
            (Text: "Good morning.", Source: "en", Target: "ru"),
            (Text: "早上好。", Source: "zh", Target: "en"),
            (Text: "早上好。", Source: "zh", Target: "ru"),
            (Text: "Доброе утро.", Source: "ru", Target: "en"),
            (Text: "Доброе утро.", Source: "ru", Target: "zh")
        };

        foreach (var sample in samples)
        {
            var timer = Stopwatch.StartNew();
            var translated = await translator.TranslateAsync(
                sample.Text, sample.Source, sample.Target,
                TestContext.Current.CancellationToken);
            timer.Stop();

            Assert.False(string.IsNullOrWhiteSpace(translated));
            Assert.NotEqual(sample.Text, translated);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"{sample.Source}->{sample.Target}: nonempty, changed, {timer.Elapsed.TotalMilliseconds:F0} ms");
        }
    }
}
