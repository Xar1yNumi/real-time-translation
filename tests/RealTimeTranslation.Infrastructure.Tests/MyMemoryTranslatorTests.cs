using System.Net;
using System.Text;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class MyMemoryTranslatorTests
{
    [Theory]
    [InlineData("en", "zh")]
    [InlineData("en", "ru")]
    [InlineData("zh", "en")]
    [InlineData("zh", "ru")]
    [InlineData("ru", "en")]
    [InlineData("ru", "zh")]
    public async Task SendsOnlyTheSelectedLanguagePair(string source, string target)
    {
        Uri? requestedUri = null;
        using var client = Client((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            requestedUri = request.RequestUri;
            return Task.FromResult(Json("{\"responseStatus\":200,\"responseData\":{\"translatedText\":\"translated\"}}"));
        });
        var translator = new MyMemoryTranslator(client);

        var result = await translator.TranslateAsync(
            "hello & мир", source, target, TestContext.Current.CancellationToken);

        Assert.Equal("translated", result);
        Assert.Equal("api.mymemory.translated.net", requestedUri?.Host);
        Assert.Equal("/get", requestedUri?.AbsolutePath);
        Assert.Contains("q=hello%20%26%20%D0%BC%D0%B8%D1%80", requestedUri!.Query, StringComparison.OrdinalIgnoreCase);
        var serviceSource = source == "zh" ? "zh-CN" : source;
        var serviceTarget = target == "zh" ? "zh-CN" : target;
        Assert.Contains($"langpair={serviceSource}%7C{serviceTarget}", requestedUri.Query,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("key=", requestedUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChineseTargetRequestsSimplifiedChineseLocale()
    {
        Uri? requestedUri = null;
        using var client = Client((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(Json("{\"responseStatus\":200,\"responseData\":{\"translatedText\":\"翻译软件\"}}"));
        });
        var translator = new MyMemoryTranslator(client);

        await translator.TranslateAsync("translation software", "en", "zh",
            TestContext.Current.CancellationToken);

        Assert.Contains("langpair=en%7Czh-CN", requestedUri!.Query,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChineseTranslationUsesSimplifiedCharactersEvenWhenServiceReturnsTraditional()
    {
        using var client = Client((_, _) => Task.FromResult(Json(
            "{\"responseStatus\":200,\"responseData\":{\"translatedText\":\"軟體正在處理語音\"}}")));
        var translator = new MyMemoryTranslator(client);

        var result = await translator.TranslateAsync("The software processes speech", "en", "zh",
            TestContext.Current.CancellationToken);

        Assert.Equal("软件正在处理语音", result);
    }

    [Fact]
    public async Task SameLanguageReturnsOriginalWithoutNetwork()
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Network must not be called."));
        var translator = new MyMemoryTranslator(client);

        var result = await translator.TranslateAsync(
            "原文", "zh", "zh", TestContext.Current.CancellationToken);

        Assert.Equal("原文", result);
    }

    [Fact]
    public async Task RejectsTextAboveFiveHundredUtf8BytesBeforeNetwork()
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Network must not be called."));
        var translator = new MyMemoryTranslator(client);

        var failure = await Assert.ThrowsAsync<AppFailureException>(() =>
            translator.TranslateAsync(new string('中', 167), "zh", "en", TestContext.Current.CancellationToken));

        Assert.Contains("500", failure.Message);
    }

    [Theory]
    [InlineData("", "en", "ru")]
    [InlineData("hello", "fr", "ru")]
    [InlineData("hello", "en", "fr")]
    public async Task InvalidInputIsClassifiedWithoutNetwork(string text, string source, string target)
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Network must not be called."));
        var translator = new MyMemoryTranslator(client);

        var failure = await Assert.ThrowsAsync<AppFailureException>(() =>
            translator.TranslateAsync(text, source, target, TestContext.Current.CancellationToken));

        Assert.Equal(AppErrorCode.InvalidTranslationRequest, failure.Error.Code);
        Assert.False(failure.Error.IsTransient);
    }

    [Theory]
    [InlineData(429, "{}")]
    [InlineData(200, "{\"responseStatus\":403,\"responseDetails\":\"MYMEMORY WARNING: YOU USED ALL AVAILABLE FREE TRANSLATIONS FOR TODAY\"}")]
    public async Task QuotaFailureIsNonTransient(int status, string body)
    {
        using var client = Client((_, _) => Task.FromResult(Json(body, (HttpStatusCode)status)));
        var translator = new MyMemoryTranslator(client);

        var failure = await Assert.ThrowsAsync<AppFailureException>(() =>
            translator.TranslateAsync("hello", "en", "ru", TestContext.Current.CancellationToken));

        Assert.Equal(AppErrorCode.RateLimited, failure.Error.Code);
        Assert.False(failure.Error.IsTransient);
    }

    [Fact]
    public async Task AccessDeniedWithoutQuotaEvidenceIsNotReportedAsQuota()
    {
        using var client = Client((_, _) => Task.FromResult(Json(
            "{\"responseStatus\":403,\"responseDetails\":\"Access denied\"}",
            HttpStatusCode.Forbidden)));
        var translator = new MyMemoryTranslator(client);

        var failure = await Assert.ThrowsAsync<AppFailureException>(() =>
            translator.TranslateAsync("hello", "en", "ru", TestContext.Current.CancellationToken));

        Assert.Equal(AppErrorCode.ServiceUnavailable, failure.Error.Code);
        Assert.True(failure.Error.IsTransient);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"responseStatus\":1e100}")]
    [InlineData("{\"responseStatus\":200,\"responseData\":{\"translatedText\":\"\"}}")]
    public async Task InvalidOrEmptyResponseIsClassified(string body)
    {
        using var client = Client((_, _) => Task.FromResult(Json(body)));
        var translator = new MyMemoryTranslator(client);

        var failure = await Assert.ThrowsAsync<AppFailureException>(() =>
            translator.TranslateAsync("hello", "en", "zh", TestContext.Current.CancellationToken));

        Assert.Equal(AppErrorCode.ServiceUnavailable, failure.Error.Code);
        Assert.DoesNotContain("hello", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TimeoutHasSafeMessage()
    {
        using var client = Client(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Json("{}");
        });
        var translator = new MyMemoryTranslator(client, TimeSpan.FromMilliseconds(50));

        var failure = await Assert.ThrowsAsync<AppFailureException>(() =>
            translator.TranslateAsync("secret phrase", "en", "ru", TestContext.Current.CancellationToken));

        Assert.Equal(AppErrorCode.ServiceUnavailable, failure.Error.Code);
        Assert.DoesNotContain("secret", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallerCancellationIsPropagated()
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Network must not be called."));
        var translator = new MyMemoryTranslator(client);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            translator.TranslateAsync("hello", "en", "ru", cancellation.Token));
    }

    [Fact]
    public async Task ActiveRequestCanBeCanceledByCaller()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = Client(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Json("{}");
        });
        var translator = new MyMemoryTranslator(client);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var running = translator.TranslateAsync("hello", "en", "ru", cancellation.Token);
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
        new(new StubHandler(respond));

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
