using System.Net;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.TestHost;

namespace Heimdall.Functions.Tests;

public sealed partial class FunctionsIntegrationTests
{
    [Theory]
    [InlineData("/router", "root page")]
    [InlineData("/router/orders", "orders page")]
    [InlineData("/router/ORDERS?sort=new", "orders page")]
    [InlineData("/router/orders/42", "order 42")]
    [InlineData("/router/orders/widget", "slug widget")]
    [InlineData("/router/files/a/b/c.txt", "file a/b/c.txt")]
    [InlineData("/router/optional", "name ")]
    [InlineData("/router/optional/Ada", "name Ada")]
    public async Task RegisteredPagesRenderThroughOneHttpTrigger(string url, string expected)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RouterKeepsProtocolSeparateFromPages()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var csrf = await client.GetAsync("/router/__heimdall/v1/csrf");
        Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
        Assert.Contains("requestToken", await csrf.Content.ReadAsStringAsync());
        using var missing = await client.GetAsync("/router/missing");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task RouterAppliesPageAuthorizationAndMethodChecks()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var anonymous = await client.GetAsync("/router/private");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/router/private");
        request.Headers.Add("X-User", "alice");
        using var allowed = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var wrongMethod = await client.PostAsync("/router/orders", null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        Assert.Contains("GET", wrongMethod.Content.Headers.Allow);
        using var configuredAnonymous = await client.GetAsync("/router/configured/7");
        Assert.Equal(HttpStatusCode.Unauthorized, configuredAnonymous.StatusCode);
        using var configuredRequest = new HttpRequestMessage(HttpMethod.Get, "/router/configured/7");
        configuredRequest.Headers.Add("X-User", "alice");
        using var configured = await client.SendAsync(configuredRequest);
        Assert.Equal(HttpStatusCode.OK, configured.StatusCode);
        Assert.Contains("configured page:7", await configured.Content.ReadAsStringAsync());
    }

    [Fact]
    public void PageRegistryRejectsReservedDuplicateAndInvalidRoutes()
    {
        var pages = new HeimdallFunctionPages();
        pages.MapPage("/orders", _ => Html.Span("orders"));
        Assert.Throws<InvalidOperationException>(() => pages.MapPage("/ORDERS", _ => Html.Span("duplicate")));
        foreach (var path in new[] { "/__heimdall/v1", "/assets/site.css", "/_content/Example/a.css", "/../secret", "/orders/{id" })
            Assert.Throws<ArgumentException>(() => pages.MapPage(path, _ => Html.Span("invalid")));
        pages.MapPage("/orders/{id:int}", _ => Html.Span("valid"));
        pages.MapPage("/account/sign-in", _ => Html.Span("sign in"));
        pages.MapPage("/admin", _ => Html.Span("admin"));
    }
}
