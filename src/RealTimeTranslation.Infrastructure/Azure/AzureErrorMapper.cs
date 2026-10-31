using Microsoft.CognitiveServices.Speech;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Azure;

internal static class AzureErrorMapper
{
    public static AppErrorCode Map(CancellationErrorCode code) => code switch
    {
        CancellationErrorCode.AuthenticationFailure => AppErrorCode.AuthenticationFailed,
        CancellationErrorCode.ConnectionFailure => AppErrorCode.NetworkUnavailable,
        CancellationErrorCode.ServiceTimeout or CancellationErrorCode.ServiceError
            or CancellationErrorCode.ServiceUnavailable => AppErrorCode.ServiceUnavailable,
        CancellationErrorCode.TooManyRequests or CancellationErrorCode.Forbidden =>
            AppErrorCode.RateLimited,
        _ => AppErrorCode.Unknown
    };
}
