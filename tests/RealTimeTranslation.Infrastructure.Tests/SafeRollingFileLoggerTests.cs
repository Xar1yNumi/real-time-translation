using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Logging;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class SafeRollingFileLoggerTests
{
    [Fact]
    public void LoggerRejectsKeysQueryStringsTranscriptsAndTranslations()
    {
        var directory = NewTemporaryDirectory();
        try
        {
            var logger = new SafeRollingFileLogger(directory);
            logger.Log(
                "session.failed?KEY=secret-key&TOKEN=sensitive-original " +
                "authorization=sensitive-translation",
                AppErrorCode.NetworkUnavailable,
                new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero),
                new Dictionary<string, long>
                {
                    ["sensitive-original"] = 7,
                    ["attempt"] = 2
                });

            var content = string.Join("\n", Directory.GetFiles(directory)
                .Select(File.ReadAllText));
            Assert.DoesNotContain("secret-key", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sensitive-original", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sensitive-translation", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("KEY=", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TOKEN=", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("authorization=", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("attempt=2", content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoggerRotatesWithinConfiguredByteAndFileLimits()
    {
        var directory = NewTemporaryDirectory();
        try
        {
            var logger = new SafeRollingFileLogger(directory, maxBytes: 128, maxFiles: 3);
            for (var attempt = 0; attempt < 30; attempt++)
            {
                logger.Log("session.reconnecting", AppErrorCode.NetworkUnavailable,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, long> { ["attempt"] = attempt });
            }

            var files = Directory.GetFiles(directory, "events.*.log");
            Assert.InRange(files.Length, 1, 3);
            Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1, 128));
            Assert.Contains("attempt=29", File.ReadAllText(Path.Combine(directory, "events.1.log")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string NewTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "RealTimeTranslation-logs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
