using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class BundledWhisperModelStoreTests
{
    [Fact]
    public void ProductionBundleRequiresTranslationRuntimeFiles()
    {
        var required = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            typeof(BundledWhisperModelStore)
                .GetField("ProductionRequired", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null));
        Assert.Contains("python/python311.dll", required);
        Assert.Contains("python/Lib/site-packages/ctranslate2/ctranslate2.dll", required);
        Assert.Contains("python/Lib/site-packages/ctranslate2/_ext.cp311-win_amd64.pyd", required);
        Assert.Contains("python/Lib/site-packages/sentencepiece/_sentencepiece.cp311-win_amd64.pyd", required);
        Assert.Contains("python/Lib/site-packages/opencc/config/t2s.json", required);
    }

    [Fact]
    public async Task OnlyCompleteAndUnmodifiedBundleCanStartWithoutNetwork()
    {
        var root = Path.Combine(Path.GetTempPath(), "local-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var expected = new Dictionary<string, string>
            {
                ["models/ggml-small-q5_1.bin"] = Hash("speech"),
                ["models/opus-mt-en-zh/model.bin"] = Hash("translation")
            };
            using var store = new BundledWhisperModelStore(root, expected,
                ["python/python.exe", "worker.py"], "models/ggml-small-q5_1.bin");
            Assert.Null(await store.VerifyCachedAsync(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                store.EnsureAvailableAsync(null, TestContext.Current.CancellationToken));

            Write(root, "models/ggml-small-q5_1.bin", "speech");
            Write(root, "models/opus-mt-en-zh/model.bin", "translation");
            Write(root, "python/python.exe", "python");
            Write(root, "worker.py", "worker");
            var path = await store.VerifyCachedAsync(TestContext.Current.CancellationToken);
            Assert.Equal(Path.Combine(root, "models", "ggml-small-q5_1.bin"), path);
            Assert.Equal(path, await store.EnsureAvailableAsync(null,
                TestContext.Current.CancellationToken));

            Write(root, "models/opus-mt-en-zh/model.bin", "corrupt");
            Assert.Null(await store.VerifyCachedAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void Write(string root, string relativePath, string value)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
    }
}
