using System.Security.Cryptography;

namespace RealTimeTranslation.Infrastructure.LocalSpeech;

public sealed class BundledWhisperModelStore : IWhisperModelStore
{
    private const string SpeechModel = "models/ggml-small-q5_1.bin";
    private static readonly IReadOnlyDictionary<string, string> ProductionHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SpeechModel] = "AE85E4A935D7A567BD102FE55AFC16BB595BDB618E11B2FC7591BC08120411BB",
            ["models/silero_vad.onnx"] = "1A153A22F4509E292A94E67D6F9B85E8DEB25B4988682B7E174C65279D8788E3",
            ["models/opus-mt-en-zh/model.bin"] = "F24C2BB82368F7DE0196882DE1D8B644D2AA54AE2439C3142F263DE8A64EA2A9",
            ["models/opus-mt-zh-en/model.bin"] = "A188BC45BCE24635A1EB0CC42AD0B43AFDAD806B86AF9BEB3065BBA11F3B212C"
        };
    private static readonly string[] ProductionRequired =
    [
        "python/python.exe", "python/python311.dll", "python/python3.dll",
        "python/vcruntime140.dll", "python/vcruntime140_1.dll", "worker.py",
        "python/Lib/site-packages/ctranslate2/__init__.py",
        "python/Lib/site-packages/ctranslate2/ctranslate2.dll",
        "python/Lib/site-packages/ctranslate2/_ext.cp311-win_amd64.pyd",
        "python/Lib/site-packages/sentencepiece/__init__.py",
        "python/Lib/site-packages/sentencepiece/_sentencepiece.cp311-win_amd64.pyd",
        "python/Lib/site-packages/opencc/__init__.py",
        "python/Lib/site-packages/opencc/config/t2s.json",
        "python/Lib/site-packages/opencc/dictionary/TSCharacters.txt",
        "python/Lib/site-packages/opencc/dictionary/TSPhrases.txt",
        "models/opus-mt-en-zh/config.json", "models/opus-mt-en-zh/opus_mt.json",
        "models/opus-mt-en-zh/source.spm", "models/opus-mt-en-zh/target.spm",
        "models/opus-mt-en-zh/shared_vocabulary.json", "models/opus-mt-en-zh/LICENCE.txt",
        "models/opus-mt-zh-en/config.json", "models/opus-mt-zh-en/opus_mt.json",
        "models/opus-mt-zh-en/source.spm", "models/opus-mt-zh-en/target.spm",
        "models/opus-mt-zh-en/shared_vocabulary.json", "models/opus-mt-zh-en/LICENCE.txt"
    ];

    private readonly string root;
    private readonly IReadOnlyDictionary<string, string> hashes;
    private readonly IReadOnlyList<string> required;
    private readonly string speechModel;

    public BundledWhisperModelStore(string bundleDirectory)
        : this(bundleDirectory, ProductionHashes, ProductionRequired, SpeechModel)
    {
    }

    internal BundledWhisperModelStore(string bundleDirectory,
        IReadOnlyDictionary<string, string> hashes, IReadOnlyList<string> required,
        string speechModel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        root = Path.GetFullPath(bundleDirectory);
        this.hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
        this.required = required ?? throw new ArgumentNullException(nameof(required));
        this.speechModel = speechModel;
    }

    public async Task<string?> VerifyCachedAsync(CancellationToken cancellationToken)
    {
        foreach (var item in required)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(Resolve(item))) return null;
        }

        foreach (var (relativePath, expected) in hashes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Resolve(relativePath);
            if (!File.Exists(path)) return null;
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, 81_920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(
                file, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                return null;
        }
        return Resolve(speechModel);
    }

    public async Task<string> EnsureAvailableAsync(IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var path = await VerifyCachedAsync(cancellationToken).ConfigureAwait(false);
        if (path is null)
            throw new FileNotFoundException("The bundled local translation components are missing or modified.");
        progress?.Report(1);
        return path;
    }

    public void Dispose() { }

    private string Resolve(string relativePath) => Path.Combine(root,
        relativePath.Replace('/', Path.DirectorySeparatorChar));
}
