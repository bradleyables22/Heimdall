using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    [Fact]
    public async Task ContentAction_InvocationFailureLogsHandledException()
    {
        var logs = new TestLoggerProvider();
        await using var app = await CreateAppAsync(services =>
        {
            services.AddLogging(builder => builder.AddProvider(logs));
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.logging.failure");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(
            logs.Entries,
            entry => entry.Category == "Heimdall.Server.ContentEndpoints" &&
                entry.Level == LogLevel.Error &&
                entry.Message.Contains("tests.logging.failure", StringComparison.Ordinal) &&
                entry.Exception is InvalidOperationException exception &&
                exception.Message == "Expected logged action failure.");
    }

    [Fact]
    public async Task ContentAction_InvocationPrefix_PrefixesExplicitMethodInvocation()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.prefix.static.refresh");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("prefixed static", html);
    }

    [Fact]
    public async Task ContentAction_InvocationPrefix_PrefixesDefaultMethodInvocation()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton(new GreetingService("hello prefixed instance"));
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.prefix.instance.Render");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("hello prefixed instance", html);
    }

    [Fact]
    public async Task ContentAction_ActionIdLookup_IsCaseInsensitive()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "TESTS.PREFIX.STATIC.REFRESH");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("prefixed static", html);
    }

    [Fact]
    public async Task ContentAction_InvocationPrefix_NormalizesPrefixAndInvocationSegments()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.prefix.normalized.refresh");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("normalized prefix", html);
    }

    [Fact]
    public async Task ContentAction_WithoutInvocationPrefix_UsesTypeNameAndMethodNameDefault()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "DefaultContentActions.Ping");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("default action id", html);
    }

    [Fact]
    public void ContentRegistry_InvocationPrefix_DetectsDuplicateResolvedActionIds()
    {
        var assembly = CreateDuplicateInvocationAssembly();

        var ex = Assert.Throws<InvalidOperationException>(() => AddAssemblyToContentRegistry(assembly));

        Assert.Contains("Duplicate ContentInvocation id 'tests.collision.refresh'", ex.Message);
        Assert.Contains("globally unique", ex.Message);
    }

    [Fact]
    public async Task ContentAction_MissingActionHeaderReturnsBadRequest()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Content = JsonContent.Create(new { });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ContentAction_InvalidAntiforgeryTokenReturnsBadRequestInsteadOfThrowing()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client, "alice");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", "tests.auth.allow-anonymous");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Headers.Add(TestAuthHandler.UserHeaderName, "bob");
        request.Content = JsonContent.Create(new { });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("antiforgery", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ContentAction_InvalidAntiforgeryTokenLogsWarning()
    {
        var logs = new TestLoggerProvider();
        await using var app = await CreateAppAsync(services =>
        {
            services.AddLogging(builder => builder.AddProvider(logs));
        });
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client, "alice");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", "tests.auth.allow-anonymous");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Headers.Add(TestAuthHandler.UserHeaderName, "bob");
        request.Content = JsonContent.Create(new { });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            logs.Entries,
            entry => entry.Category == "Heimdall.Server.ContentEndpoints" &&
                entry.Level == LogLevel.Warning &&
                entry.Message.Contains("antiforgery", StringComparison.OrdinalIgnoreCase) &&
                entry.Exception is AntiforgeryValidationException);
    }

    [Fact]
    public async Task CookieAuthContentAction_AnonymousAuthorizedActionRedirectsToConfiguredSignIn()
    {
        await using var app = await CreateCookieAuthAppAsync();
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", "tests.auth.secure");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Content = JsonContent.Create(new { });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/signin", response.Headers.Location!.AbsolutePath);
        Assert.Contains("ReturnUrl=", response.Headers.Location.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CookieAuthBifrostToken_AnonymousProtectedTopicRedirectsToConfiguredSignIn()
    {
        await using var app = await CreateCookieAuthAppAsync();
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__heimdall/v1/bifrost/token?topic=secure-topic");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/signin", response.Headers.Location!.AbsolutePath);
        Assert.Contains("ReturnUrl=", response.Headers.Location.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CsrfEndpoint_IssuesRequestTokenWithNoStoreHeaders()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/__heimdall/v1/csrf");
        var token = await response.Content.ReadFromJsonAsync<CsrfResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(token?.RequestToken));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task CsrfEndpoint_IssuanceFailureLogsHandledException()
    {
        var logs = new TestLoggerProvider();
        await using var app = await CreateAppAsync(services =>
        {
            services.RemoveAll<IAntiforgery>();
            services.AddSingleton<IAntiforgery, ThrowingAntiforgery>();
            services.AddLogging(builder => builder.AddProvider(logs));
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/__heimdall/v1/csrf");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(
            logs.Entries,
            entry => entry.Category == "Heimdall.Server.SecurityEndpoints" &&
                entry.Level == LogLevel.Error &&
                entry.Exception is InvalidOperationException exception &&
                exception.Message == "Expected CSRF issuance failure.");
    }

    [Fact]
    public async Task HeimdallPage_RenderFailureLogsHandledException()
    {
        var logs = new TestLoggerProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging(logging => logging.AddProvider(logs));

        await using var app = builder.Build();
        app.MapHeimdallPage("/broken", () =>
            throw new InvalidOperationException("Expected page render failure."));
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/broken");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(
            logs.Entries,
            entry => entry.Category == "Heimdall.Server.PageEndpoints" &&
                entry.Level == LogLevel.Error &&
                entry.Message.Contains("/broken", StringComparison.Ordinal) &&
                entry.Exception is InvalidOperationException exception &&
                exception.Message == "Expected page render failure.");
    }

    [Fact]
    public async Task UseHeimdall_IApplicationBuilder_RegistersHeimdallEndpoints()
    {
        await using var app = await CreateAppUsingIApplicationBuilderUseHeimdallAsync();
        using var client = app.GetTestClient();

        var csrfToken = await GetCsrfTokenAsync(client);
        Assert.False(string.IsNullOrWhiteSpace(csrfToken.RequestToken));

        var actionResponse = await PostContentActionAsync(client, "tests.auth.allow-anonymous");
        var actionHtml = await actionResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, actionResponse.StatusCode);
        Assert.Contains("public", actionHtml);

        var bifrostResponse = await GetBifrostTokenAsync(client, "test-topic", "alice");
        var bifrostToken = await bifrostResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, bifrostResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(bifrostToken?.Token));
    }
}
