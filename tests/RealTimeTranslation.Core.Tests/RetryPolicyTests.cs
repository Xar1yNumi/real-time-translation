using RealTimeTranslation.Core;

namespace RealTimeTranslation.Core.Tests;

public sealed class RetryPolicyTests
{
    [Fact]
    public void NetworkDefaultUsesOneTwoAndFourSecondDelays()
    {
        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)],
            RetryPolicy.NetworkDefault.Delays);
    }
}
