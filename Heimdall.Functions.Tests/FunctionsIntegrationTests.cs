using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Functions.Tests;

public sealed partial class FunctionsIntegrationTests
{
    private static async Task<WebApplication> StartAsync(string? assetRoot = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAuthentication("fixture")
            .AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("fixture", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("Admin", policy => policy.RequireRole("Admin")));
        if (assetRoot is not null) builder.Services.Configure<HeimdallFunctionAssetOptions>(options => options.RootPath = assetRoot);
        builder.Services.AddScoped<ScopedValue>();
        builder.Services.AddHeimdallFunctions(options =>
        {
            options.BifrostHeartbeatInterval = TimeSpan.FromMilliseconds(50);
            options.AuthorizeBifrostTopic = (_, topic) => ValueTask.FromResult(topic == "test");
        }, typeof(Invocations).Assembly);
        builder.Services.AddHeimdallFunctionPages(pages =>
        {
            pages.MapPage("/", _ => Html.Span("root page"));
            pages.MapPage("/orders", _ => Html.Span("orders page"));
            pages.MapPage("/orders/{id:int}", context => Html.Span($"order {context.Request.RouteValues["id"]}"));
            pages.MapPage("/orders/{id}", context => Html.Span($"slug {context.Request.RouteValues["id"]}"));
            pages.MapPage("/files/{**path}", context => Html.Span($"file {context.Request.RouteValues["path"]}"));
            pages.MapPage("/optional/{name?}", context => Html.Span($"name {context.Request.RouteValues["name"]}"));
            pages.MapPage("/broken", (Func<HttpContext, IHtmlContent>)(_ =>
                throw new InvalidOperationException("render failed")));
            pages.MapAuthorizedPage("/private", context => Html.Span(context.User.Identity!.Name ?? "member"));
            pages.MapPage("/configured/{id:int}", context => Html.Span($"{context.GetEndpoint()?.DisplayName}:{context.Request.RouteValues["id"]}"))
                .WithDisplayName("configured page").RequireAuthorization();
        });
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        Func<IServiceProvider, HttpContext, Task<IHtmlContent>> render = (sp, _) =>
            Task.FromResult<IHtmlContent>(Html.Span(sp.GetRequiredService<ScopedValue>().Id.ToString()));
        // Deliberately no ASP.NET routing, authentication, authorization, or antiforgery middleware.
        app.Run(async context =>
        {
            var host = context.RequestServices.GetRequiredService<HeimdallFunctions>();
            if (context.Request.Path.StartsWithSegments("/router", out var routedPath))
            {
                var routedResult = await context.RequestServices.GetRequiredService<HeimdallFunctionRouter>()
                    .HandleAsync(context.Request, routedPath.Value?.TrimStart('/'));
                await routedResult.ExecuteResultAsync(new ActionContext { HttpContext = context });
                return;
            }
            if (context.Request.Path.StartsWithSegments("/assets", out var assetPath))
            {
                await context.RequestServices.GetRequiredService<HeimdallFunctionAssets>()
                    .Serve(context.Request, assetPath.Value!.TrimStart('/')).ExecuteResultAsync(new ActionContext { HttpContext = context });
                return;
            }
            if (context.Request.Path == "/protected" || context.Request.Path == "/admin-page")
            {
                var protectedResult = await host.RenderAuthorizedPageAsync(context.Request, render,
                    context.Request.Path == "/admin-page" ? "Admin" : null);
                await protectedResult.ExecuteResultAsync(new ActionContext { HttpContext = context });
                return;
            }
            if (context.Request.Path.StartsWithSegments("/gated", out var gatedPath) ||
                context.Request.Path.StartsWithSegments("/gated-admin", out gatedPath))
            {
                var policy = context.Request.Path.StartsWithSegments("/gated-admin") ? "Admin" : null;
                var denied = await host.AuthorizeAsync(context.Request, policy);
                var gatedResult = denied ?? await host.HandleAsync(context.Request, gatedPath.Value?.Trim('/'));
                await gatedResult.ExecuteResultAsync(new ActionContext { HttpContext = context });
                return;
            }
            var result = context.Request.Path == "/page"
                ? await host.RenderPageAsync(context.Request, render)
                : await host.HandleAsync(context.Request, context.Request.Path.Value![1..]);
            await result.ExecuteResultAsync(new ActionContext { HttpContext = context });
        });
        await app.StartAsync();
        return app;
    }

    private static async Task<(string Token, string Cookie)> CsrfAsync(HttpClient client, string? user = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/csrf");
        if (user is not null) request.Headers.Add("X-User", user);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("requestToken").GetString()!, response.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
    }

    private static HttpRequestMessage Invocation(string? id, HttpContent? body = null,
        (string Token, string Cookie)? csrf = null, string? user = null, bool legacy = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, legacy ? "/content/actions" : "/content/invocations")
        { Content = body ?? JsonContent.Create(new { }) };
        if (id is not null) request.Headers.Add(legacy ? "X-Heimdall-Content-Action" : "X-Heimdall-Content-Invocation", id);
        if (csrf is { } tokens)
        {
            request.Headers.Add("Cookie", tokens.Cookie);
            request.Headers.Add("RequestVerificationToken", tokens.Token);
        }
        if (user is not null) request.Headers.Add("X-User", user);
        return request;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JsonAndLegacyProtocolUseSharedPipeline(bool legacy)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var csrf = await CsrfAsync(client);
        using var request = Invocation("test.echo", JsonContent.Create(new { text = "<script>" }), csrf, legacy: legacy);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("&lt;script&gt;", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AntiforgeryCannotBeBypassedAndExplicitOptOutWorks()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var denied = await client.SendAsync(Invocation("test.echo"));
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal("invalid", denied.Headers.GetValues(HeimdallRequestPipeline.AntiforgeryFailureHeader).Single());
        using var allowed = await client.SendAsync(Invocation("test.public"));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.False(allowed.Headers.Contains(HeimdallRequestPipeline.AntiforgeryFailureHeader));
    }

    [Fact]
    public async Task TokensAreBoundToAuthenticatedIdentityWithoutMiddleware()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var alice = await CsrfAsync(client, "alice");
        using var accepted = await client.SendAsync(Invocation("test.private", csrf: alice, user: "alice"));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Contains("alice", await accepted.Content.ReadAsStringAsync());
        using var rejected = await client.SendAsync(Invocation("test.private", csrf: alice, user: "bob"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("alice", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.OK)]
    public async Task InvocationAuthorizationIsEnforced(string? user, HttpStatusCode expected)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var csrf = await CsrfAsync(client, user);
        using var response = await client.SendAsync(Invocation("test.admin", csrf: csrf, user: user));
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.BadRequest)]
    [InlineData("missing", HttpStatusCode.NotFound)]
    public async Task MissingAndUnknownInvocationAreReported(string? id, HttpStatusCode expected)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.SendAsync(Invocation(id, csrf: await CsrfAsync(client)));
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/content/invocations", "POST")]
    [InlineData("GET", "/content/actions", "POST")]
    [InlineData("POST", "/csrf", "GET")]
    [InlineData("POST", "/bifrost/token", "GET")]
    [InlineData("POST", "/bifrost", "GET")]
    public async Task WrongMethodReturnsAllowHeader(string method, string path, string allow)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(allow, response.Content.Headers.Allow.Single());
    }

    [Fact]
    public async Task UnknownRouteIsNotFound()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/unknown")).StatusCode);
    }

    [Fact]
    public async Task InvalidJsonIsBadRequest()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.SendAsync(Invocation("test.echo", new StringContent("{", Encoding.UTF8, "application/json"), await CsrfAsync(client)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MultipartBindsFilesAndEnforcesBodyLimit()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var csrf = await CsrfAsync(client);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "sample.bin");
        using var accepted = await client.SendAsync(Invocation("test.upload", form, csrf));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Contains("3", await accepted.Content.ReadAsStringAsync());
        using var large = new MultipartFormDataContent();
        large.Add(new ByteArrayContent(new byte[8192]), "file", "large.bin");
        using var rejected = await client.SendAsync(Invocation("test.upload", large, csrf));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, rejected.StatusCode);
    }

    [Fact]
    public async Task TimeoutMetadataCancelsInvocation()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.SendAsync(Invocation("test.timeout", csrf: await CsrfAsync(client)));
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact]
    public async Task EndpointRateLimitingFailsClosed()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.SendAsync(Invocation("test.limited", csrf: await CsrfAsync(client)));
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task PageUsesRequestScopeAndHtmlContentType()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var first = await client.GetAsync("/page");
        using var second = await client.GetAsync("/page");
        Assert.Equal("text/html", first.Content.Headers.ContentType!.MediaType);
        Assert.NotEqual(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PageRenderFailureUsesServerErrorResponse()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/router/broken");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Heimdall page render failed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DirectivesSurviveResultAdaptation()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.SendAsync(Invocation("test.directives", csrf: await CsrfAsync(client)));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<abort", html);
        Assert.Contains("<redirect", html);
        Assert.Contains("/next", html);
    }

    [Fact]
    public async Task TopicAuthorizationAndStreamTokensAreEnforced()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/bifrost/token?topic=test")).StatusCode);
        var csrf = await CsrfAsync(client);
        using var mint = new HttpRequestMessage(HttpMethod.Get, "/bifrost/token?topic=denied");
        mint.Headers.Add("Cookie", csrf.Cookie);
        mint.Headers.Add("RequestVerificationToken", csrf.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(mint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bifrost?topic=test&st=bad")).StatusCode);
    }

    [Fact]
    public async Task SseFlushesHeartbeatsAndEventsAndCleansUpAfterDisconnect()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var csrf = await CsrfAsync(client);
        using var mint = new HttpRequestMessage(HttpMethod.Get, "/bifrost/token?topic=test");
        mint.Headers.Add("Cookie", csrf.Cookie);
        mint.Headers.Add("RequestVerificationToken", csrf.Token);
        using var minted = await client.SendAsync(mint);
        var token = (await minted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stream = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/bifrost?topic=test&st={Uri.EscapeDataString(token!)}"), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal("text/event-stream", stream.Content.Headers.ContentType!.MediaType);
        using var reader = new StreamReader(await stream.Content.ReadAsStreamAsync(timeout.Token));
        Assert.Equal("event: heimdall:connected", await reader.ReadLineAsync(timeout.Token));
        string? line;
        do { line = await reader.ReadLineAsync(timeout.Token); } while (line != ": ping");
        var bifrost = app.Services.GetRequiredService<Bifrost>();
        await bifrost.PublishAsync("test", Html.Span("event payload"), TimeSpan.FromSeconds(10));
        do { line = await reader.ReadLineAsync(timeout.Token); } while (line?.Contains("event payload") != true);
        stream.Dispose();
        for (var i = 0; i < 100 && bifrost.HasSubscribers("test"); i++) await Task.Delay(10, timeout.Token);
        Assert.False(bifrost.HasSubscribers("test"));
        Assert.Empty(bifrost.Connections.GetConnections());
    }

    public sealed class ScopedValue { public Guid Id { get; } = Guid.NewGuid(); }
    public sealed record Payload(string Text);

    public static class Invocations
    {
        [ContentInvocation("test.ambient"), RequireAntiforgeryToken(false)]
        public static async Task<IHtmlContent> Ambient(HttpContext context, [FromServices] IHttpContextAccessor accessor)
        {
            await Task.Delay(10, context.RequestAborted);
            return Html.Span(ReferenceEquals(context, accessor.HttpContext) ? context.TraceIdentifier : "wrong context");
        }

        [ContentInvocation("test.echo")]
        public static IHtmlContent Echo(Payload payload) => Html.Span(payload.Text);
        [ContentInvocation("test.public"), RequireAntiforgeryToken(false)]
        public static IHtmlContent Public() => Html.Span("Public");
        [ContentInvocation("test.private"), Authorize]
        public static IHtmlContent Private(HttpContext context) => Html.Span(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        [ContentInvocation("test.admin"), Authorize(Roles = "Admin")]
        public static IHtmlContent Admin() => Html.Span("Admin");
        [ContentInvocation("test.upload"), RequestSizeLimit(4096)]
        public static IHtmlContent Upload(IFormFile file) => Html.Span(file.Length.ToString());
        [ContentInvocation("test.timeout"), RequestTimeout(20)]
        public static async Task<IHtmlContent> Timeout(CancellationToken ct)
        { await Task.Delay(10000, ct); return HtmlString.Empty; }
        [ContentInvocation("test.limited"), EnableRateLimiting("unavailable")]
        public static IHtmlContent Limited() => throw new InvalidOperationException("Must not execute");
        [ContentInvocation("test.directives")]
        public static IHtmlContent Directives() => Html.Fragment(HeimdallHtml.Abort(), HeimdallHtml.Redirect("/next"));
    }

    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-User", out var name)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, name.ToString()) };
            if (name == "admin") claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
