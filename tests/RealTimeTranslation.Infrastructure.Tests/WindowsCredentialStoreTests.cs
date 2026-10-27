using RealTimeTranslation.Infrastructure.Credentials;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class WindowsCredentialStoreTests
{
    [Fact]
    [Trait("Category", "WindowsCredentialManager")]
    public async Task CredentialRoundTripsAndDeletes()
    {
        var store = new WindowsCredentialStore(
            "RealTimeTranslation.Tests/" + Guid.NewGuid().ToString("N"));
        try
        {
            await store.WriteApiKeyAsync(
                "not-a-real-secret",
                TestContext.Current.CancellationToken);

            Assert.Equal(
                "not-a-real-secret",
                await store.ReadApiKeyAsync(TestContext.Current.CancellationToken));

            await store.DeleteApiKeyAsync(TestContext.Current.CancellationToken);
            Assert.Null(await store.ReadApiKeyAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await store.DeleteApiKeyAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task EmptyApiKeyIsRejected()
    {
        var store = new WindowsCredentialStore(
            "RealTimeTranslation.Tests/" + Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await store.WriteApiKeyAsync(
                " ",
                TestContext.Current.CancellationToken));
    }
}
