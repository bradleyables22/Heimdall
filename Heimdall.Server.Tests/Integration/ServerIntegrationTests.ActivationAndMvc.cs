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
    public async Task ContentAction_ImplicitlyResolvesRegisteredServiceParameters()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton(new GreetingService("hello from service"));
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.services.implicit");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("hello from service", html);
    }

    [Fact]
    public async Task ContentAction_InstanceMethod_UsesConstructorDependencyAndPayload()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton(new GreetingService("hello from constructor"));
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(
            client,
            "tests.instance.greeting",
            new { Name = "Ada" });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("hello from constructor:Ada", html);
    }

    [Fact]
    public async Task ContentAction_InstanceMethod_UsesRegisteredActionTypeWhenAvailable()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton(new RegisteredInstanceContentActions("from-registration"));
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.instance.registered");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("from-registration", html);
    }

    [Fact]
    public async Task ContentAction_InstanceMethod_ActivatesUnregisteredActionTypePerRequest()
    {
        CountingInstanceContentActions.Reset();

        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var first = await PostContentActionAsync(client, "tests.instance.activation-count");
        var second = await PostContentActionAsync(client, "tests.instance.activation-count");
        var firstHtml = await first.Content.ReadAsStringAsync();
        var secondHtml = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("1", firstHtml);
        Assert.Contains("2", secondHtml);
        Assert.Equal(2, CountingInstanceContentActions.ConstructionCount);
    }

    [Fact]
    public async Task ContentAction_InstanceMethod_UsesRegisteredActionTypeLifetime()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton<StatefulRegisteredInstanceContentActions>();
        });
        using var client = app.GetTestClient();

        var first = await PostContentActionAsync(client, "tests.instance.registered-state");
        var second = await PostContentActionAsync(client, "tests.instance.registered-state");
        var firstHtml = await first.Content.ReadAsStringAsync();
        var secondHtml = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("1", firstHtml);
        Assert.Contains("2", secondHtml);
    }

    [Fact]
    public async Task ContentAction_InstanceTypeAuthorization_HonorsAuthorizeAndAllowAnonymous()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var secureAnonymous = await PostContentActionAsync(client, "tests.instance.auth.secure");
        var publicAnonymous = await PostContentActionAsync(client, "tests.instance.auth.public");
        var secureAuthenticated = await PostContentActionAsync(
            client,
            "tests.instance.auth.secure",
            userName: "alice");
        var publicHtml = await publicAnonymous.Content.ReadAsStringAsync();
        var secureHtml = await secureAuthenticated.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, secureAnonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, publicAnonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secureAuthenticated.StatusCode);
        Assert.Contains("instance public", publicHtml);
        Assert.Contains("alice", secureHtml);
    }

    [Fact]
    public async Task ContentAction_InstanceTypeRequestTimeout_CancelsLongRunningAction()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.instance.timeout.slow");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact]
    public async Task AddHeimdallMvc_RegistersMvcRendererAndViewDependencies()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();

        builder.Services.AddHeimdallMvc();

        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();

        Assert.NotNull(app.Services.GetRequiredService<IHttpContextAccessor>());
        Assert.NotNull(app.Services.GetRequiredService<ICompositeViewEngine>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IHeimdallMvcRenderer>());
    }

    [Fact]
    public void AddHeimdallMvc_PreservesCustomMvcRendererRegistration()
    {
        var services = new ServiceCollection();

        services.AddScoped<IHeimdallMvcRenderer, CustomMvcRenderer>();
        services.AddHeimdallMvc();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<CustomMvcRenderer>(
            scope.ServiceProvider.GetRequiredService<IHeimdallMvcRenderer>());
    }

    [Fact]
    public async Task AddHeimdallMvc_RendersMvcPartialFromContentAction()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddHeimdallMvc();
            services.RemoveAll<ICompositeViewEngine>();
            services.AddSingleton<ICompositeViewEngine, FakeCompositeViewEngine>();
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(
            client,
            "tests.mvc.partial",
            new { ViewName = "_Greeting", Name = "Ada" });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"mvc-partial\"", html);
        Assert.Contains("data-source=\"heimdall\"", html);
        Assert.Contains("Hello Ada", html);

        var pathResponse = await PostContentActionAsync(
            client,
            "tests.mvc.partial",
            new { ViewName = "~/Views/Shared/_Greeting.cshtml", Name = "Grace" });
        var pathHtml = await pathResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, pathResponse.StatusCode);
        Assert.Contains("Hello Grace", pathHtml);
    }

    [Fact]
    public async Task AddHeimdallMvc_ReturnsDetailedErrorWhenPartialIsMissing()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddHeimdallMvc();
            services.RemoveAll<ICompositeViewEngine>();
            services.AddSingleton<ICompositeViewEngine, FakeCompositeViewEngine>();
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.mvc.missing");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Unable to find MVC partial view 'missing'", body);
        Assert.Contains("/Views/Shared/missing.cshtml", body);
        Assert.Contains("Heimdall action invocation failed", body);
    }
}
