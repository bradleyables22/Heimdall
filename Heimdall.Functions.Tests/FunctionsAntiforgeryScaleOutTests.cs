using System.Net;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Functions.Tests;

public sealed partial class FunctionsIntegrationTests
{
    [Fact]
    public async Task BifrostAntiforgeryRejectionHasExplicitRetryMarker()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var rejected = await client.GetAsync("/bifrost/token?topic=test");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("invalid", rejected.Headers.GetValues(Heimdall.Server.HeimdallRequestPipeline.AntiforgeryFailureHeader).Single());
        using var malformed = await client.GetAsync("/bifrost/token");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.False(malformed.Headers.Contains(Heimdall.Server.HeimdallRequestPipeline.AntiforgeryFailureHeader));
    }

    // Each host owns a separate DI container and key-ring cache. Only the repository is shared.
    private static Action<IServiceCollection> ConfigureKeys(IXmlRepository repository, string name = "scale-out-review")
        => services =>
        {
            services.AddDataProtection().SetApplicationName(name);
            services.Configure<KeyManagementOptions>(options => options.XmlRepository = repository);
        };

    [Fact]
    public async Task IndependentWorkersWithSharedKeysAcceptTheSameCsrfToken()
    {
        var repository = new ReviewKeyRepository();
        await using var first = await StartAsync(configureServices: ConfigureKeys(repository));
        await using var second = await StartAsync(configureServices: ConfigureKeys(repository));
        using var firstClient = first.GetTestClient();
        using var secondClient = second.GetTestClient();
        var tokens = await CsrfAsync(firstClient, "alice");
        using var response = await secondClient.SendAsync(Invocation("test.private", csrf: tokens, user: "alice"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var mint = new HttpRequestMessage(HttpMethod.Get, "/bifrost/token?topic=test");
        mint.Headers.Add("Cookie", tokens.Cookie);
        mint.Headers.Add("RequestVerificationToken", tokens.Token);
        mint.Headers.Add("X-User", "alice");
        using var minted = await secondClient.SendAsync(mint);
        Assert.Equal(HttpStatusCode.OK, minted.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentKeysOrApplicationNamesRejectAnotherWorkersToken(bool shareKeys)
    {
        var repository = new ReviewKeyRepository();
        await using var first = await StartAsync(configureServices: ConfigureKeys(repository));
        await using var second = await StartAsync(configureServices: ConfigureKeys(
            shareKeys ? repository : new ReviewKeyRepository(), shareKeys ? "different-app" : "scale-out-review"));
        using var firstClient = first.GetTestClient();
        using var secondClient = second.GetTestClient();
        var tokens = await CsrfAsync(firstClient, "alice");
        using var response = await secondClient.SendAsync(Invocation("test.private", csrf: tokens, user: "alice"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("antiforgery", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PersistedKeysKeepExistingTokensValidAfterRotationAndHostRestart()
    {
        var repository = new ReviewKeyRepository();
        (string Token, string Cookie) tokens;
        await using (var first = await StartAsync(configureServices: ConfigureKeys(repository)))
        {
            using var client = first.GetTestClient();
            tokens = await CsrfAsync(client, "alice");
            first.Services.GetRequiredService<IKeyManager>().CreateNewKey(
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));
        }
        await using var restarted = await StartAsync(configureServices: ConfigureKeys(repository));
        using var restartedClient = restarted.GetTestClient();
        using var response = await restartedClient.SendAsync(Invocation("test.private", csrf: tokens, user: "alice"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class ReviewKeyRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = [];
        public IReadOnlyCollection<XElement> GetAllElements()
        {
            lock (_elements) return _elements.Select(element => new XElement(element)).ToArray();
        }
        public void StoreElement(XElement element, string friendlyName)
        {
            lock (_elements) _elements.Add(new XElement(element));
        }
    }
}
