using RealTimeTranslation.Desktop.Services;

namespace RealTimeTranslation.Desktop.Tests;

public sealed class VisualCppRuntimePrerequisiteTests
{
    [Fact]
    public void ReportsAllMissingNativeLibraries()
    {
        var missing = VisualCppRuntimePrerequisite.FindMissing(_ => false);

        Assert.Equal(["MSVCP140.dll", "MSVCP140_1.dll", "VCOMP140.dll"], missing);
    }

    [Fact]
    public void AcceptsInstalledNativeLibraries()
    {
        var missing = VisualCppRuntimePrerequisite.FindMissing(_ => true);

        Assert.Empty(missing);
    }

    [Fact]
    public void ReportsOnlyTheAbsentLibrary()
    {
        var missing = VisualCppRuntimePrerequisite.FindMissing(
            name => name != "MSVCP140_1.dll");

        Assert.Equal(["MSVCP140_1.dll"], missing);
    }
}
