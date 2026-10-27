namespace RealTimeTranslation.Desktop.Tests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void DesktopAssemblyIsLoadable()
    {
        Assert.NotNull(typeof(SolutionSmokeTests).Assembly);
    }
}
