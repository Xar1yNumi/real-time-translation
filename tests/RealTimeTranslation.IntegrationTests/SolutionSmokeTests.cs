namespace RealTimeTranslation.IntegrationTests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void IntegrationAssemblyIsLoadable()
    {
        Assert.NotNull(typeof(SolutionSmokeTests).Assembly);
    }
}
