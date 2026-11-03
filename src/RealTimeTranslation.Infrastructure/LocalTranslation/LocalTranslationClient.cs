using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.LocalTranslation;

public sealed class LocalTranslationClient : ILocalTextTranslator, IAsyncDisposable
{
    private const int MaxRequestBytes = 16_384;
    private const int MaxResponseCharacters = 16_384;
    private readonly Func<CancellationToken, Task<string?>> readLine;
    private readonly Func<string, CancellationToken, Task> writeLine;
    private readonly TimeSpan requestTimeout;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> pending = new();
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task reader;
    private readonly Process? process;
    private long nextId;
    private volatile bool terminalFailure;
    private bool disposed;

    internal LocalTranslationClient(
        Func<CancellationToken, Task<string?>> readLine,
        Func<string, CancellationToken, Task> writeLine,
        TimeSpan requestTimeout,
        Process? process = null)
    {
        this.readLine = readLine ?? throw new ArgumentNullException(nameof(readLine));
        this.writeLine = writeLine ?? throw new ArgumentNullException(nameof(writeLine));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestTimeout, TimeSpan.Zero);
        this.requestTimeout = requestTimeout;
        this.process = process;
        reader = Task.Run(ReadResponsesAsync);
    }

    internal int PendingCount => pending.Count;

    public static async Task<LocalTranslationClient> StartAsync(
        string pythonExecutable, string workerScript, string modelsDirectory,
        CancellationToken cancellationToken)
    {
        foreach (var path in new[] { pythonExecutable, workerScript })
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Local translation runtime is incomplete.", path);
            }
        }
        if (!Directory.Exists(modelsDirectory))
        {
            throw new DirectoryNotFoundException("Local translation models are missing.");
        }

        var info = new ProcessStartInfo(pythonExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
        };
        info.ArgumentList.Add("-u");
        info.ArgumentList.Add(workerScript);
        info.ArgumentList.Add("--models");
        info.ArgumentList.Add(modelsDirectory);
        info.Environment["HF_HUB_OFFLINE"] = "1";
        info.Environment["TRANSFORMERS_OFFLINE"] = "1";
        var process = Process.Start(info) ?? throw new InvalidOperationException(
            "Could not start local translation worker.");
        var client = new LocalTranslationClient(
            token => process.StandardOutput.ReadLineAsync(token).AsTask(),
            async (line, token) =>
            {
                await process.StandardInput.WriteLineAsync(line.AsMemory(), token).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(token).ConfigureAwait(false);
            },
            TimeSpan.FromSeconds(8), process);
        try
        {
            await client.ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken)
                .ConfigureAwait(false);
            return client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<string> TranslateAsync(
        string text, string sourceCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || sourceCode is not ("zh" or "en") ||
            Encoding.UTF8.GetByteCount(text) > 12_000)
        {
            throw Failure(AppErrorCode.InvalidTranslationRequest);
        }
        ObjectDisposedException.ThrowIf(disposed, this);
        if (terminalFailure)
        {
            throw Failure(AppErrorCode.ServiceUnavailable);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var id = Interlocked.Increment(ref nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(id, completion))
        {
            throw new InvalidOperationException("Duplicate translation request ID.");
        }
        if (terminalFailure)
        {
            pending.TryRemove(id, out _);
            throw Failure(AppErrorCode.ServiceUnavailable);
        }
        try
        {
            var line = JsonSerializer.Serialize(new { id, source = sourceCode, text });
            if (Encoding.UTF8.GetByteCount(line) > MaxRequestBytes)
            {
                throw Failure(AppErrorCode.InvalidTranslationRequest);
            }
            await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await writeLine(line, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                writeGate.Release();
            }
            try
            {
                return await completion.Task.WaitAsync(requestTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                throw new AppFailureException(AppError.For(AppErrorCode.ServiceUnavailable), exception);
            }
        }
        catch (IOException exception)
        {
            throw new AppFailureException(AppError.For(AppErrorCode.ServiceUnavailable), exception);
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
    }

    private async Task ReadResponsesAsync()
    {
        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                var line = await readLine(shutdown.Token).ConfigureAwait(false);
                if (line is null)
                {
                    throw Failure(AppErrorCode.ServiceUnavailable);
                }
                if (line.Length > MaxResponseCharacters)
                {
                    throw Failure(AppErrorCode.ServiceUnavailable);
                }
                using var message = JsonDocument.Parse(line);
                var root = message.RootElement;
                if (root.TryGetProperty("type", out var type) && type.GetString() == "ready")
                {
                    if (root.TryGetProperty("version", out var version) && version.GetInt32() == 1)
                    {
                        ready.TrySetResult();
                        continue;
                    }
                    throw Failure(AppErrorCode.ServiceUnavailable);
                }
                if (!root.TryGetProperty("id", out var idValue) ||
                    idValue.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                var id = idValue.GetString();
                if (id is null || !pending.TryRemove(id, out var completion))
                {
                    continue;
                }
                if (root.TryGetProperty("translation", out var translation) &&
                    translation.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(translation.GetString()))
                {
                    completion.TrySetResult(translation.GetString()!);
                }
                else
                {
                    var code = root.TryGetProperty("error", out var error) &&
                        error.GetString() == "invalid_request"
                        ? AppErrorCode.InvalidTranslationRequest
                        : AppErrorCode.ServiceUnavailable;
                    completion.TrySetException(Failure(code));
                }
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            terminalFailure = true;
            ready.TrySetException(exception);
            foreach (var item in pending)
            {
                if (pending.TryRemove(item.Key, out var completion))
                {
                    completion.TrySetException(Failure(AppErrorCode.ServiceUnavailable));
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        shutdown.Cancel();
        foreach (var item in pending)
        {
            if (pending.TryRemove(item.Key, out var completion))
            {
                completion.TrySetException(Failure(AppErrorCode.ServiceUnavailable));
            }
        }
        if (process is not null)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            process.Dispose();
        }
        await reader.ConfigureAwait(false);
        writeGate.Dispose();
        shutdown.Dispose();
    }

    private static AppFailureException Failure(AppErrorCode code) =>
        new(AppError.For(code));
}
