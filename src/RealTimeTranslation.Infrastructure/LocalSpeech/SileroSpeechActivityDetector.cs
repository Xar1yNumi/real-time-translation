using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace RealTimeTranslation.Infrastructure.LocalSpeech;

internal sealed class SileroSpeechActivityDetector : ISpeechActivityDetector
{
    private const string ModelSha256 =
        "1A153A22F4509E292A94E67D6F9B85E8DEB25B4988682B7E174C65279D8788E3";
    private readonly InferenceSession session;
    private readonly float[] context = new float[64];
    private readonly float[] state = new float[256];
    private bool disposed;

    public SileroSpeechActivityDetector(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        using var file = File.OpenRead(modelPath);
        var hash = Convert.ToHexString(SHA256.HashData(file));
        if (hash != ModelSha256)
        {
            throw new InvalidDataException("Speech activity model hash mismatch.");
        }
        using var options = new SessionOptions
        {
            InterOpNumThreads = 1,
            IntraOpNumThreads = 1
        };
        session = new InferenceSession(modelPath, options);
    }

    public bool IsSpeech(ReadOnlySpan<byte> pcm16Mono)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (pcm16Mono.Length != 1_024)
        {
            throw new ArgumentException("Silero requires a 32 ms PCM16 frame at 16 kHz.", nameof(pcm16Mono));
        }

        var input = new float[576];
        context.CopyTo(input, 0);
        for (var index = 0; index < 512; index++)
        {
            input[64 + index] = BinaryPrimitives.ReadInt16LittleEndian(
                pcm16Mono.Slice(index * 2, 2)) / 32768f;
        }
        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(input, [1, 576])),
            NamedOnnxValue.CreateFromTensor("sr", new DenseTensor<long>(new long[] { 16_000 }, [1])),
            NamedOnnxValue.CreateFromTensor("state", new DenseTensor<float>(state, [2, 1, 128]))
        };
        using var results = session.Run(inputs);
        var probability = results.Single(item => item.Name == "output").AsTensor<float>().ToArray()[0];
        var nextState = results.Single(item => item.Name == "stateN").AsTensor<float>().ToArray();
        if (nextState.Length != state.Length)
        {
            throw new InvalidDataException("Unexpected Silero state tensor.");
        }
        nextState.CopyTo(state, 0);
        input.AsSpan(512, 64).CopyTo(context);
        return probability >= 0.5f;
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Array.Clear(context);
        Array.Clear(state);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        session.Dispose();
    }
}
