using System.Threading.Channels;
using System.Text.Json;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalTranslation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class LocalTranslationClientTests
{
    [Fact]
    public async Task CorrelatesOutOfOrderResponsesByRequestId()
    {
        var input = Channel.CreateUnbounded<string>();
        var output = Channel.CreateUnbounded<string>();
        await using var client = CreateClient(input, output);
        var first = client.TranslateAsync("Hello", "en", TestContext.Current.CancellationToken);
        var second = client.TranslateAsync("你好", "zh", TestContext.Current.CancellationToken);
        var firstRequest = JsonDocument.Parse(await output.Reader.ReadAsync(TestContext.Current.CancellationToken));
        var secondRequest = JsonDocument.Parse(await output.Reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("en", firstRequest.RootElement.GetProperty("source").GetString());
        Assert.Equal("zh", secondRequest.RootElement.GetProperty("source").GetString());
        var firstId = firstRequest.RootElement.GetProperty("id").GetString();
        var secondId = secondRequest.RootElement.GetProperty("id").GetString();
        await input.Writer.WriteAsync(JsonSerializer.Serialize(new { id = secondId, translation = "Hi" }), TestContext.Current.CancellationToken);
        await input.Writer.WriteAsync(JsonSerializer.Serialize(new { id = firstId, translation = "你好" }), TestContext.Current.CancellationToken);
        Assert.Equal("你好", await first);
        Assert.Equal("Hi", await second);
    }

    [Fact]
    public async Task RejectsEmptyInputBeforeSending()
    {
        var input = Channel.CreateUnbounded<string>();
        var output = Channel.CreateUnbounded<string>();
        await using var client = CreateClient(input, output);
        await Assert.ThrowsAsync<AppFailureException>(() =>
            client.TranslateAsync(" ", "en", TestContext.Current.CancellationToken));
        Assert.False(output.Reader.TryRead(out _));
    }

    [Fact]
    public async Task TimeoutAndCancellationRemovePendingRequests()
    {
        var input = Channel.CreateUnbounded<string>();
        var output = Channel.CreateUnbounded<string>();
        await using var client = CreateClient(input, output, TimeSpan.FromMilliseconds(40));
        var timedOut = client.TranslateAsync("Hello", "en", TestContext.Current.CancellationToken);
        await output.Reader.ReadAsync(TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<AppFailureException>(() => timedOut);
        Assert.Equal(AppErrorCode.ServiceUnavailable, failure.Error.Code);
        using var canceled = new CancellationTokenSource();
        var canceledTask = client.TranslateAsync("你好", "zh", canceled.Token);
        await output.Reader.ReadAsync(TestContext.Current.CancellationToken);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledTask);
        Assert.Equal(0, client.PendingCount);
    }

    [Fact]
    public async Task ProcessExitFailsPendingRequest()
    {
        var input = Channel.CreateUnbounded<string>();
        var output = Channel.CreateUnbounded<string>();
        await using var client = CreateClient(input, output);
        var pending = client.TranslateAsync("Hello", "en", TestContext.Current.CancellationToken);
        await output.Reader.ReadAsync(TestContext.Current.CancellationToken);
        input.Writer.TryComplete();
        var failure = await Assert.ThrowsAsync<AppFailureException>(() => pending);
        Assert.Equal(AppErrorCode.ServiceUnavailable, failure.Error.Code);
        var next = client.TranslateAsync("Again", "en", TestContext.Current.CancellationToken);
        var nextFailure = await Assert.ThrowsAsync<AppFailureException>(() =>
            next.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        Assert.Equal(AppErrorCode.ServiceUnavailable, nextFailure.Error.Code);
    }

    private static LocalTranslationClient CreateClient(
        Channel<string> input, Channel<string> output, TimeSpan? timeout = null) =>
        new(async token =>
            {
                try { return await input.Reader.ReadAsync(token); }
                catch (ChannelClosedException) { return null; }
            },
            (line, token) => output.Writer.WriteAsync(line, token).AsTask(),
            timeout ?? TimeSpan.FromSeconds(5));
}
