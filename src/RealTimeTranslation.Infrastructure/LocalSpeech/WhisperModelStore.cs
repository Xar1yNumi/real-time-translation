using System.Buffers;
using System.Security.Cryptography;

namespace RealTimeTranslation.Infrastructure.LocalSpeech;

public interface IWhisperModelStore : IDisposable
{
    Task<string?> VerifyCachedAsync(CancellationToken cancellationToken);

    Task<string> EnsureAvailableAsync(
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}

internal sealed record WhisperModelDescriptor(Uri Source, long Length, string Sha256);

public sealed class WhisperModelStore : IWhisperModelStore
{
    private const string ModelFileName = "ggml-small-q5_1.bin";
    private static readonly WhisperModelDescriptor ProductionModel = new(
        new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/" +
            "c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin"),
        190_085_487,
        "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb");

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly object lifecycleSync = new();
    private readonly TaskCompletionSource<bool> drained = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> disposalFinished = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HttpClient client;
    private readonly string modelDirectory;
    private readonly WhisperModelDescriptor descriptor;
    private readonly TimeSpan transferTimeout;
    private int activeCalls;
    private bool disposed;

    public WhisperModelStore(HttpClient client)
        : this(client,
            Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "RealTimeTranslation", "models"),
            ProductionModel,
            TimeSpan.FromMinutes(10))
    {
    }

    internal WhisperModelStore(
        HttpClient client,
        string modelDirectory,
        WhisperModelDescriptor descriptor)
        : this(client, modelDirectory, descriptor, TimeSpan.FromMinutes(10))
    {
    }

    internal WhisperModelStore(
        HttpClient client,
        string modelDirectory,
        WhisperModelDescriptor descriptor,
        TimeSpan transferTimeout)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDirectory);
        this.modelDirectory = Path.GetFullPath(modelDirectory);
        this.descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        if (descriptor.Source.Scheme != Uri.UriSchemeHttps || descriptor.Length <= 0
            || descriptor.Sha256.Length != 64)
        {
            throw new ArgumentException("Invalid model descriptor.", nameof(descriptor));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            transferTimeout, TimeSpan.Zero);
        this.transferTimeout = transferTimeout;
    }

    public async Task<string> EnsureAvailableAsync(
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        lock (lifecycleSync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            activeCalls++;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, shutdown.Token);
            timeout.CancelAfter(transferTimeout);
            var operationToken = timeout.Token;
            var acquired = false;
            try
            {
                await gate.WaitAsync(operationToken).ConfigureAwait(false);
                acquired = true;
                operationToken.ThrowIfCancellationRequested();
                return await EnsureCoreAsync(progress, operationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (
                !cancellationToken.IsCancellationRequested
                && !shutdown.IsCancellationRequested
                && timeout.IsCancellationRequested)
            {
                throw new TimeoutException("Speech model download timed out.", exception);
            }
            finally
            {
                if (acquired)
                {
                    gate.Release();
                }
            }
        }
        finally
        {
            lock (lifecycleSync)
            {
                activeCalls--;
                if (disposed && activeCalls == 0)
                {
                    drained.TrySetResult(true);
                }
            }
        }
    }

    public async Task<string?> VerifyCachedAsync(CancellationToken cancellationToken)
    {
        lock (lifecycleSync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            activeCalls++;
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, shutdown.Token);
            var acquired = false;
            try
            {
                await gate.WaitAsync(linked.Token).ConfigureAwait(false);
                acquired = true;
                var path = Path.Combine(modelDirectory, ModelFileName);
                return await MatchesDescriptorAsync(path, linked.Token).ConfigureAwait(false)
                    ? path : null;
            }
            finally
            {
                if (acquired)
                {
                    gate.Release();
                }
            }
        }
        finally
        {
            lock (lifecycleSync)
            {
                activeCalls--;
                if (disposed && activeCalls == 0)
                {
                    drained.TrySetResult(true);
                }
            }
        }
    }

    private async Task<string> EnsureCoreAsync(
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(modelDirectory);
        var destination = Path.Combine(modelDirectory, ModelFileName);
        if (await MatchesDescriptorAsync(destination, cancellationToken).ConfigureAwait(false))
        {
            progress?.Report(1);
            return destination;
        }

        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var response = await client.GetAsync(
                descriptor.Source,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(
                cancellationToken).ConfigureAwait(false);
            await using (var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = ArrayPool<byte>.Shared.Rent(81_920);
                try
                {
                    long length = 0;
                    int read;
                    while ((read = await source.ReadAsync(
                        buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
                    {
                        length += read;
                        if (length > descriptor.Length)
                        {
                            throw new InvalidDataException("Downloaded speech model is larger than expected.");
                        }

                        hasher.AppendData(buffer, 0, read);
                        await output.WriteAsync(
                            buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        progress?.Report((double)length / descriptor.Length);
                    }

                    if (length != descriptor.Length || !MatchesHash(hasher.GetHashAndReset()))
                    {
                        throw new InvalidDataException("Downloaded speech model failed verification.");
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
            progress?.Report(1);
            return destination;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private async Task<bool> MatchesDescriptorAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != descriptor.Length)
        {
            return false;
        }

        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81_920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return MatchesHash(await SHA256.HashDataAsync(
            stream, cancellationToken).ConfigureAwait(false));
    }

    private bool MatchesHash(byte[] hash) =>
        Convert.ToHexString(hash).Equals(descriptor.Sha256, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        bool disposeOwner;
        lock (lifecycleSync)
        {
            disposeOwner = !disposed;
            if (disposeOwner)
            {
                disposed = true;
                if (activeCalls == 0)
                {
                    drained.TrySetResult(true);
                }
            }
        }

        if (!disposeOwner)
        {
            disposalFinished.Task.GetAwaiter().GetResult();
            return;
        }

        try
        {
            shutdown.Cancel();
            drained.Task.GetAwaiter().GetResult();
            gate.Dispose();
            shutdown.Dispose();
        }
        finally
        {
            disposalFinished.TrySetResult(true);
        }
    }
}
