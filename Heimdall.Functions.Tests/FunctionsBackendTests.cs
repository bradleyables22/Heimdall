using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Functions.Tests;

public sealed partial class FunctionsIntegrationTests
{
    [Fact]
    public async Task PublicInvocationRunsWithoutAnAuthenticationSchemeOrAspNetMiddleware()
    {
        var services = new ServiceCollection();
        services.AddHeimdallFunctions(assemblies: [typeof(Invocations).Assembly]);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Headers["X-Heimdall-Content-Invocation"] = "test.public";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Response.Body = new MemoryStream();

        var result = await provider.GetRequiredService<HeimdallFunctions>()
            .HandleAsync(context.Request, "content/invocations");
        await result.ExecuteResultAsync(new ActionContext { HttpContext = context });

        Assert.Equal(200, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        Assert.Contains("Public", await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Theory]
    [InlineData("/gated/csrf", null, HttpStatusCode.Unauthorized)]
    [InlineData("/gated/csrf", "alice", HttpStatusCode.OK)]
    [InlineData("/gated-admin/csrf", "alice", HttpStatusCode.Forbidden)]
    [InlineData("/gated-admin/csrf", "admin", HttpStatusCode.OK)]
    public async Task CompleteProtocolTriggerCanUseDefaultOrNamedAuthorizationPolicy(
        string path, string? user, HttpStatusCode expected)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (user is not null) request.Headers.Add("X-User", user);
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrEmpty(json.GetProperty("requestToken").GetString()));
        }
    }

    [Fact]
    public async Task SubscriptionTokenCannotBeUsedByAnotherIdentity()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var csrf = await CsrfAsync(client, "alice");
        using var mintRequest = new HttpRequestMessage(HttpMethod.Get, "/bifrost/token?topic=test");
        mintRequest.Headers.Add("Cookie", csrf.Cookie);
        mintRequest.Headers.Add("RequestVerificationToken", csrf.Token);
        mintRequest.Headers.Add("X-User", "alice");
        using var minted = await client.SendAsync(mintRequest);
        Assert.Equal(HttpStatusCode.OK, minted.StatusCode);
        var token = (await minted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        using var streamRequest = new HttpRequestMessage(HttpMethod.Get,
            "/bifrost?topic=test&st=" + Uri.EscapeDataString(token!));
        streamRequest.Headers.Add("X-User", "bob");
        using var denied = await client.SendAsync(streamRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.False(app.Services.GetRequiredService<Heimdall.Server.Bifrost>().HasSubscribers("test"));
    }

    [Fact]
    public async Task ChangedApplicationAssetInvalidatesItsEtag()
    {
        var root = Path.Combine(Path.GetTempPath(), "heimdall-asset-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "site.css");
        try
        {
            await File.WriteAllTextAsync(file, "body{color:red}");
            await using var app = await StartAsync(root);
            using var client = app.GetTestClient();
            using var first = await client.GetAsync("/assets/site.css");
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Contains("max-age=0", first.Headers.CacheControl!.ToString());
            var oldEtag = first.Headers.ETag;
            Assert.NotNull(oldEtag);

            await File.WriteAllTextAsync(file, "body{color:blue}");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(5));
            using var revalidate = new HttpRequestMessage(HttpMethod.Get, "/assets/site.css");
            revalidate.Headers.IfNoneMatch.Add(oldEtag);
            using var changed = await client.SendAsync(revalidate);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            Assert.Contains("blue", await changed.Content.ReadAsStringAsync());
            Assert.NotEqual(oldEtag, changed.Headers.ETag);
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(root);
        }
    }

}
