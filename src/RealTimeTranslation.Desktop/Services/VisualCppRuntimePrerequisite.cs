using System.Runtime.InteropServices;

namespace RealTimeTranslation.Desktop.Services;

public static class VisualCppRuntimePrerequisite
{
    public const string DownloadUrl = "https://aka.ms/vc14/vc_redist.x64.exe";

    private static readonly string[] RequiredLibraries =
    [
        "MSVCP140.dll", "MSVCP140_1.dll", "VCOMP140.dll"
    ];

    public static IReadOnlyList<string> FindMissing(Func<string, bool>? isAvailable = null)
    {
        isAvailable ??= TryLoad;
        return RequiredLibraries.Where(name => !isAvailable(name)).ToArray();
    }

    private static bool TryLoad(string name)
    {
        if (!NativeLibrary.TryLoad(name, out var handle)) return false;
        NativeLibrary.Free(handle);
        return true;
    }
}
