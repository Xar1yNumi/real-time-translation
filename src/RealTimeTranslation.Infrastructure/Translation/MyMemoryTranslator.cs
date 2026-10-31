using System.Net;
using System.Text;
using System.Text.Json;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Translation;

public interface IMyMemoryTranslator
{
    Task<string> TranslateAsync(
        string text, string source, string target, CancellationToken cancellationToken);
}

public sealed class MyMemoryTranslator : IMyMemoryTranslator
{
    private const int MaximumRequestBytes = 500;
    private const int MaximumResponseBytes = 1_000_000;
    private readonly HttpClient client;
    private readonly TimeSpan requestTimeout;

    public MyMemoryTranslator(HttpClient client)
        : this(client, TimeSpan.FromSeconds(12))
    {
    }

    internal MyMemoryTranslator(HttpClient client, TimeSpan requestTimeout)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestTimeout, TimeSpan.Zero);
        this.requestTimeout = requestTimeout;
    }

    public async Task<string> TranslateAsync(
        string text, string source, string target, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw Failure(AppErrorCode.InvalidTranslationRequest,
                "没有可翻译的文字。", false);
        }

        if (!IsSupported(source) || !IsSupported(target))
        {
            throw Failure(AppErrorCode.InvalidTranslationRequest,
                "仅支持中文、英语和俄语之间的翻译。", false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (source == target)
        {
            return text;
        }

        if (Encoding.UTF8.GetByteCount(text) > MaximumRequestBytes)
        {
            throw Failure(AppErrorCode.InvalidTranslationRequest,
                "单句文字超过 MyMemory 的 500 字节限制，请缩短后重试。", false);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);
        var query = Uri.EscapeDataString(text);
        var pair = Uri.EscapeDataString($"{source}|{target}");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.mymemory.translated.net/get?q={query}&langpair={pair}");

        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw QuotaFailure();
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Forbidden)
            {
                throw ServiceFailure();
            }

            await response.Content.LoadIntoBufferAsync(
                MaximumResponseBytes, timeout.Token).ConfigureAwait(false);
            await using var body = await response.Content.ReadAsStreamAsync(
                timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(
                body, cancellationToken: timeout.Token).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw ServiceFailure();
            }

            if (IsQuotaResponse(root))
            {
                throw QuotaFailure();
            }

            if (!response.IsSuccessStatusCode)
            {
                throw ServiceFailure();
            }

            if (!root.TryGetProperty("responseStatus", out var status) ||
                status.ValueKind != JsonValueKind.Number || !status.TryGetInt32(out var code) || code != 200 ||
                !root.TryGetProperty("responseData", out var data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("translatedText", out var translated) ||
                translated.ValueKind != JsonValueKind.String)
            {
                throw ServiceFailure();
            }

            var result = WebUtility.HtmlDecode(translated.GetString())?.Trim();
            return !string.IsNullOrWhiteSpace(result) ? result : throw ServiceFailure();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Failure(AppErrorCode.ServiceUnavailable,
                "MyMemory 翻译请求超时，请稍后重试。", true);
        }
        catch (HttpRequestException)
        {
            throw Failure(AppErrorCode.NetworkUnavailable,
                "无法连接 MyMemory，请检查网络后重试。", true);
        }
        catch (IOException)
        {
            throw Failure(AppErrorCode.NetworkUnavailable,
                "MyMemory 连接中断，请检查网络后重试。", true);
        }
        catch (JsonException)
        {
            throw ServiceFailure();
        }
    }

    private static bool IsSupported(string language) => language is "zh" or "en" or "ru";

    private static bool IsQuotaResponse(JsonElement root)
    {
        if (root.TryGetProperty("responseStatus", out var status) &&
            status.ValueKind == JsonValueKind.Number && status.TryGetInt32(out var code) && code == 429)
        {
            return true;
        }

        if (root.TryGetProperty("responseDetails", out var details) &&
            details.ValueKind == JsonValueKind.String)
        {
            var message = details.GetString();
            return message?.Contains("QUOTA", StringComparison.OrdinalIgnoreCase) == true ||
                message?.Contains("AVAILABLE FREE TRANSLATIONS", StringComparison.OrdinalIgnoreCase) == true;
        }

        return false;
    }

    private static AppFailureException QuotaFailure() => Failure(AppErrorCode.RateLimited,
        "MyMemory 匿名翻译额度已用完或请求过于频繁，请稍后再试。", false);

    private static AppFailureException ServiceFailure() => Failure(AppErrorCode.ServiceUnavailable,
        "MyMemory 暂时无法返回有效译文，请稍后重试。", true);

    private static AppFailureException Failure(
        AppErrorCode code, string message, bool transient) =>
        new(new AppError(code, message, transient));
}
