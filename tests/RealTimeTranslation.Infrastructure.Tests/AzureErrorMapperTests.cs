using Microsoft.CognitiveServices.Speech;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Azure;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class AzureErrorMapperTests
{
    [Theory]
    [InlineData(CancellationErrorCode.AuthenticationFailure, AppErrorCode.AuthenticationFailed)]
    [InlineData(CancellationErrorCode.ConnectionFailure, AppErrorCode.NetworkUnavailable)]
    [InlineData(CancellationErrorCode.ServiceTimeout, AppErrorCode.ServiceUnavailable)]
    [InlineData(CancellationErrorCode.ServiceUnavailable, AppErrorCode.ServiceUnavailable)]
    [InlineData(CancellationErrorCode.TooManyRequests, AppErrorCode.RateLimited)]
    [InlineData(CancellationErrorCode.Forbidden, AppErrorCode.RateLimited)]
    [InlineData(CancellationErrorCode.BadRequest, AppErrorCode.Unknown)]
    public void MapsSdkCancellationToSafeCategory(
        CancellationErrorCode sdkCode, AppErrorCode expected)
    {
        Assert.Equal(expected, AzureErrorMapper.Map(sdkCode));
    }
}
