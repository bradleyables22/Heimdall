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
    public async Task UseHeimdall_ThrowsClearErrorWhenAddHeimdallWasNotCalled()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();

        await using var app = builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => app.UseHeimdall());

        Assert.Contains("UseHeimdall() requires Heimdall runtime services", ex.Message);
        Assert.Contains("AddHeimdall", ex.Message);
    }

    [Fact]
    public async Task IApplicationBuilderUseHeimdall_ThrowsClearErrorWhenAddHeimdallWasNotCalled()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();

        await using var app = builder.Build();
        app.UseRouting();

        var ex = Assert.Throws<InvalidOperationException>(() => ((IApplicationBuilder)app).UseHeimdall());

        Assert.Contains("UseHeimdall() requires Heimdall runtime services", ex.Message);
        Assert.Contains("AddHeimdall", ex.Message);
    }

    [Fact]
    public async Task AuthorizedContentAction_RejectsAnonymousRequests()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.auth.secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizedContentAction_AllowsAuthenticatedRequests()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.auth.secure", userName: "alice");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("alice", html);
    }

    [Fact]
    public async Task TypeAuthorizedContentAction_AllowsAnonymousOverride()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.auth.allow-anonymous");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("public", html);
    }

    [Fact]
    public async Task PolicyAuthorizedContentAction_ForbidsAuthenticatedUserWithoutRequiredClaim()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy("tests.admin", policy => policy.RequireClaim(ClaimTypes.Role, "admin"));
            });
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.auth.admin", userName: "alice");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PolicyAuthorizedContentAction_AllowsAuthenticatedUserWithRequiredClaim()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy("tests.admin", policy => policy.RequireClaim(ClaimTypes.Role, "admin"));
            });
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(
            client,
            "tests.auth.admin",
            userName: "alice",
            role: "admin");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("admin", html);
    }

    [Fact]
    public async Task RequestTimeoutAttribute_CancelsLongRunningContentAction()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.timeout.slow");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact]
    public async Task NamedRequestTimeoutPolicy_UsesConfiguredStatusCode()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddRequestTimeouts(options =>
            {
                options.AddPolicy("tests.fast-teapot", new RequestTimeoutPolicy
                {
                    Timeout = TimeSpan.FromMilliseconds(50),
                    TimeoutStatusCode = StatusCodes.Status418ImATeapot
                });
            });
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.timeout.named-policy");

        Assert.Equal((HttpStatusCode)StatusCodes.Status418ImATeapot, response.StatusCode);
    }

    [Fact]
    public async Task DisableRequestTimeoutAttribute_OverridesTypeLevelTimeout()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.timeout.disabled");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("not timed out", html);
    }
}
