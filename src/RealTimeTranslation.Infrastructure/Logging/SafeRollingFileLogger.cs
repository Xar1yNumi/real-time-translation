using System.Globalization;
using System.Text;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Logging;

public sealed class SafeRollingFileLogger
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly HashSet<string> AllowedEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "session.connecting", "session.listening", "session.paused", "session.reconnecting",
        "session.failed", "session.stopping", "session.idle", "audio.fault",
        "credential.test.failed"
    };
    private static readonly HashSet<string> AllowedMetrics = new(StringComparer.Ordinal)
    {
        "attempt", "frames", "bytes", "dropped_frames", "duration_ms"
    };

    private readonly object sync = new();
    private readonly string directory;
    private readonly long maxBytes;
    private readonly int maxFiles;

    public SafeRollingFileLogger(string? directory = null, long maxBytes = 1_048_576, int maxFiles = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 128);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
        this.directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RealTimeTranslation", "logs");
        this.maxBytes = maxBytes;
        this.maxFiles = maxFiles;
    }

    public void Log(string eventName, AppErrorCode errorCode, DateTimeOffset timestamp,
        IReadOnlyDictionary<string, long>? metrics = null)
    {
        var safeEvent = AllowedEvents.Contains(eventName) ? eventName.ToLowerInvariant() : "other";
        var safeCode = Enum.IsDefined(errorCode) ? errorCode : AppErrorCode.Unknown;
        var line = new StringBuilder(timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
            .Append(" event=").Append(safeEvent)
            .Append(" error=").Append(safeCode);
        if (metrics is not null)
        {
            foreach (var metric in metrics.Where(item => AllowedMetrics.Contains(item.Key)).Take(8))
            {
                line.Append(' ').Append(metric.Key).Append('=')
                    .Append(metric.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
        line.AppendLine();
        var bytes = Utf8.GetBytes(line.ToString());
        if (bytes.LongLength > maxBytes)
        {
            return;
        }

        try
        {
            lock (sync)
            {
                Directory.CreateDirectory(directory);
                var current = PathFor(1);
                if (File.Exists(current) && new FileInfo(current).Length + bytes.LongLength > maxBytes)
                {
                    Rotate();
                }
                using var stream = new FileStream(current, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(bytes);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void Rotate()
    {
        if (maxFiles == 1)
        {
            File.Delete(PathFor(1));
            return;
        }
        for (var index = maxFiles; index >= 2; index--)
        {
            var previous = PathFor(index - 1);
            if (File.Exists(previous))
            {
                File.Move(previous, PathFor(index), overwrite: true);
            }
        }
    }

    private string PathFor(int index) => Path.Combine(directory, $"events.{index}.log");
}
