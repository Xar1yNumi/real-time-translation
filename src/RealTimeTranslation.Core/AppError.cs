namespace RealTimeTranslation.Core;

public enum AppErrorCode
{
    AuthenticationFailed,
    InvalidEndpoint,
    NetworkUnavailable,
    ServiceUnavailable,
    RateLimited,
    AudioDeviceUnavailable,
    UnsupportedAudioFormat,
    Unknown
}

public sealed record AppError(AppErrorCode Code, string UserMessage, bool IsTransient)
{
    public static AppError For(AppErrorCode code) => code switch
    {
        AppErrorCode.AuthenticationFailed => new(code, "Azure 凭据无效，请返回设置检查。", false),
        AppErrorCode.InvalidEndpoint => new(code, "Azure Endpoint 无效，请返回设置检查。", false),
        AppErrorCode.NetworkUnavailable => new(code, "网络连接中断，请检查网络。", true),
        AppErrorCode.ServiceUnavailable => new(code, "Azure 服务暂时不可用，请稍后重试。", true),
        AppErrorCode.RateLimited => new(code, "Azure 请求过于频繁或额度不足，请稍后重试。", false),
        AppErrorCode.AudioDeviceUnavailable => new(code, "音频设备不可用，请返回设置检查。", false),
        AppErrorCode.UnsupportedAudioFormat => new(code, "音频格式不受支持，请检查设备。", false),
        _ => new(AppErrorCode.Unknown, "翻译出现问题，请返回设置后重试。", false)
    };

    public static AppError FromException(Exception exception) => exception switch
    {
        AppFailureException failure => failure.Error,
        IOException => For(AppErrorCode.NetworkUnavailable),
        TimeoutException => For(AppErrorCode.ServiceUnavailable),
        _ => For(AppErrorCode.Unknown)
    };
}

public sealed class AppFailureException(AppError error, Exception? innerException = null)
    : Exception(error.UserMessage, innerException)
{
    public AppError Error { get; } = error;
}
