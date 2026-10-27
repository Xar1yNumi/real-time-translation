namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void InfrastructureAssemblyIsLoadable()
    {
        Assert.NotNull(typeof(SolutionSmokeTests).Assembly);
    }
}
