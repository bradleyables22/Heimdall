using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Heimdall.Functions.Tests;

public sealed class FunctionsAuthenticationTests
{
    [Fact]
    public async Task OidcCallbackCreatesSessionAndLogoutRequiresCsrfBeforeClearingIt()
    {
        using var provider = new Provider();
        await using var app = await StartAsync(provider);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://app.test");
        var cookies = new Dictionary<string, string>();
        using var login = await SendAsync(client, cookies, HttpMethod.Get, "/login");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var authorization = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        provider.Nonce = authorization["nonce"]!;
        provider.Challenge = authorization["code_challenge"]!;
        Assert.Equal("S256", authorization["code_challenge_method"]);
        using var callback = await SendAsync(client, cookies, HttpMethod.Get,
            "/auth/callback?code=test-code&state=" + Uri.EscapeDataString(authorization["state"]!));
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.True(provider.ExchangedCode);
        using var page = await SendAsync(client, cookies, HttpMethod.Get, "/private");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("alice", await page.Content.ReadAsStringAsync());

        using var wrongMethod = await SendAsync(client, cookies, HttpMethod.Get, "/logout");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        using var missingToken = await SendAsync(client, cookies, HttpMethod.Post, "/logout");
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        using var stillSignedIn = await SendAsync(client, cookies, HttpMethod.Get, "/private");
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
        using var csrfResponse = await SendAsync(client, cookies, HttpMethod.Get, "/csrf");
        var csrf = JsonDocument.Parse(await csrfResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("requestToken").GetString();
        using var logout = await SendAsync(client, cookies, HttpMethod.Post, "/logout", csrf);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.StartsWith("https://provider.test/logout?", logout.Headers.Location!.AbsoluteUri);
        var logoutQuery = QueryHelpers.ParseQuery(logout.Headers.Location.Query);
        Assert.False(string.IsNullOrEmpty(logoutQuery["id_token_hint"]));
        using var signedOut = await SendAsync(client, cookies, HttpMethod.Get,
            "/auth/signed-out?state=" + Uri.EscapeDataString(logoutQuery["state"]!));
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
        Assert.Equal("/signed-out", signedOut.Headers.Location!.OriginalString);
        using var denied = await SendAsync(client, cookies, HttpMethod.Get, "/private");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
    }

    [Fact]
    public async Task CallbackRejectsInvalidStateAndUnknownPaths()
    {
        using var provider = new Provider();
        await using var app = await StartAsync(provider);
        using var client = app.GetTestClient();
        using var invalid = await client.GetAsync("/auth/callback?code=bad&state=bad");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.False(provider.ExchangedCode);
        using var unknown = await client.GetAsync("/auth/unknown");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.test/")]
    [InlineData("//evil.test/")]
    [InlineData("/\\evil.test/")]
    [InlineData("/\r\nLocation: evil")]
    public async Task AuthenticationHelpersRejectExternalReturnUrls(string url)
    {
        var host = new HeimdallFunctions();
        var context = new DefaultHttpContext();
        await Assert.ThrowsAsync<ArgumentException>(() => host.ChallengeAsync(context.Request, "oidc", url));
        await Assert.ThrowsAsync<ArgumentException>(() => host.SignOutAsync(context.Request, url, "Cookies"));
    }

    private static async Task<WebApplication> StartAsync(Provider provider)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options => options.LoginPath = "/login")
            .AddOpenIdConnect(options =>
            {
                options.ClientId = "test-client";
                options.ClientSecret = "test-secret";
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.ResponseType = "code";
                options.ResponseMode = "query";
                options.CallbackPath = "/auth/callback";
                options.SignedOutCallbackPath = "/auth/signed-out";
                options.SaveTokens = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "sub";
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://provider.test", AuthorizationEndpoint = "https://provider.test/authorize",
                    TokenEndpoint = "https://provider.test/token", EndSessionEndpoint = "https://provider.test/logout"
                };
                options.Configuration.SigningKeys.Add(provider.Key);
                options.Backchannel = new HttpClient(provider, false);
                options.Events.OnRemoteFailure = context =>
                { context.Response.StatusCode = 400; context.HandleResponse(); return Task.CompletedTask; };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddHeimdallFunctions(assemblies: [typeof(FunctionsAuthenticationTests).Assembly]);
        var app = builder.Build();
        Func<IServiceProvider, HttpContext, Task<Microsoft.AspNetCore.Html.IHtmlContent>> render =
            (_, ctx) => Task.FromResult(Html.Span(ctx.User.Identity!.Name!));
        app.Run(async context =>
        {
            var host = context.RequestServices.GetRequiredService<HeimdallFunctions>();
            IActionResult result;
            if (context.Request.Path.StartsWithSegments("/auth")) result = await host.HandleAuthenticationAsync(context.Request);
            else if (context.Request.Path == "/login") result = await host.ChallengeAsync(context.Request, "OpenIdConnect");
            else if (context.Request.Path == "/logout") result = await host.SignOutAsync(context.Request, "/signed-out", "Cookies", "OpenIdConnect");
            else if (context.Request.Path == "/private") result = await host.RenderAuthorizedPageAsync(context.Request, render);
            else result = await host.HandleAsync(context.Request, context.Request.Path.Value!.Trim('/'));
            await result.ExecuteResultAsync(new ActionContext { HttpContext = context });
        });
        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, Dictionary<string, string> cookies, HttpMethod method, string url, string? csrf = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", cookies.Values));
        if (csrf is not null) request.Headers.Add("RequestVerificationToken", csrf);
        var response = await client.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var headers))
            foreach (var header in headers)
            {
                var cookie = header.Split(';')[0];
                var name = cookie.Split('=')[0];
                if (cookie == name + "=") cookies.Remove(name); else cookies[name] = cookie;
            }
        return response;
    }

    private sealed class Provider : HttpMessageHandler
    {
        private readonly RSA _rsa = RSA.Create(2048);
        public RsaSecurityKey Key { get; }
        public string Nonce { get; set; } = "";
        public string Challenge { get; set; } = "";
        public bool ExchangedCode { get; private set; }
        public Provider() => Key = new RsaSecurityKey(_rsa) { KeyId = "test-signing-key" };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://provider.test/token", request.RequestUri!.AbsoluteUri);
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal("test-code", form["code"]);
            Assert.Equal(Challenge, Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]!))));
            ExchangedCode = true;
            var now = DateTime.UtcNow;
            var jwt = new JwtSecurityToken("https://provider.test", "test-client",
                [new Claim("sub", "alice"), new Claim("nonce", Nonce), new Claim("iat", EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64)],
                now.AddMinutes(-1), now.AddMinutes(5), new SigningCredentials(Key, SecurityAlgorithms.RsaSha256));
            var body = JsonSerializer.Serialize(new { access_token = "test-access-token", token_type = "Bearer", expires_in = 300, id_token = new JwtSecurityTokenHandler().WriteToken(jwt) });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
        protected override void Dispose(bool disposing) { if (disposing) _rsa.Dispose(); base.Dispose(disposing); }
    }
}
