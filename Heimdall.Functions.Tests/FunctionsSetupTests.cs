using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Heimdall.Functions.Tests;

public sealed partial class FunctionsIntegrationTests
{
    [Fact]
    public async Task DirectWorkerRequestsKeepAmbientContextIsolatedAndRestoreIt()
    {
        var services = new ServiceCollection();
        services.AddHeimdallFunctions(assemblies: [typeof(Invocations).Assembly]);
        await using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        Assert.Null(accessor.HttpContext);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            await using var scope = provider.CreateAsyncScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider, TraceIdentifier = $"request-{index}" };
            context.Request.Method = "POST";
            context.Request.ContentType = "application/json";
            context.Request.Headers["X-Heimdall-Content-Invocation"] = "test.ambient";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            context.Response.Body = new MemoryStream();
            var result = await provider.GetRequiredService<HeimdallFunctions>().HandleAsync(context.Request, "content/invocations");
            Assert.Null(accessor.HttpContext);
            await result.ExecuteResultAsync(new ActionContext { HttpContext = context });
            Assert.Null(accessor.HttpContext);
            context.Response.Body.Position = 0;
            Assert.Contains($"request-{index}", await new StreamReader(context.Response.Body).ReadToEndAsync());
        }));
        Assert.Null(accessor.HttpContext);
    }

    [Theory]
    [InlineData("/protected", null, 401)]
    [InlineData("/protected", "alice", 200)]
    [InlineData("/admin-page", "alice", 403)]
    [InlineData("/admin-page", "admin", 200)]
    public async Task PageAuthorizationRunsBeforeRendering(string path, string? user, int status)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (user is not null) request.Headers.Add("X-User", user);
        using var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        if (status != 200) Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApplicationAssetSupportsRangeAndOnlyServesKnownFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "heimdall-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "site.css"), "body{color:red}");
            await File.WriteAllTextAsync(Path.Combine(root, "private.config"), "secret");
            await using var app = await StartAsync(root);
            using var client = app.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/assets/site.css");
            request.Headers.Range = new(0, 3);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
            Assert.Equal("body", await response.Content.ReadAsStringAsync());
            Assert.Equal("text/css", response.Content.Headers.ContentType!.MediaType);
            foreach (var path in new[] { "private.config", ".env", "missing.css", "a/../../private.config", "a%5cb.css" })
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/assets/" + path)).StatusCode);
            using var rejected = await client.PostAsync("/assets/site.css", null);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, rejected.StatusCode);
        }
        finally
        {
            // Only files created by this test, with a fixed generated directory name.
            File.Delete(Path.Combine(root, "site.css"));
            File.Delete(Path.Combine(root, "private.config"));
            Directory.Delete(root);
        }
    }
}
