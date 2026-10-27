namespace RealTimeTranslation.Core.Tests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void CoreAssemblyIsLoadable()
    {
        Assert.NotNull(typeof(SolutionSmokeTests).Assembly);
    }
}
