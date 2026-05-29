using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
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

public sealed class ServerIntegrationTests
{
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

    [Fact]
    public async Task ContentRegistry_DoesNotInstantiateServicesDuringParameterClassification()
    {
        ConstructedService.Reset(throwOnConstruct: true);

        try
        {
            await using var app = await CreateAppAsync(services =>
            {
                services.AddTransient<ConstructedService>();
            });
            using var client = app.GetTestClient();

            var response = await PostContentActionAsync(client, "tests.missing.action");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(0, ConstructedService.ConstructionCount);
        }
        finally
        {
            ConstructedService.Reset(throwOnConstruct: false);
        }
    }

    [Fact]
    public async Task ContentAction_BindsComplexPayloadWithFlexibleJsonConversions()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.payload.complex", new
        {
            Name = "Ada",
            Count = "7",
            Enabled = "on",
            Mode = "beta"
        });
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("Ada|7|True|Beta", html);
    }

    [Fact]
    public async Task ContentAction_BindsSimplePayloadParameterFromMatchingProperty()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.payload.simple", new { Name = "Grace" });
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("Grace", html);
    }

    [Fact]
    public async Task ContentAction_UsesDefaultValueForMissingSimplePayloadProperty()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, "tests.payload.simple", new { });
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("fallback", html);
    }

    [Fact]
    public async Task ContentAction_MalformedJsonPayloadReturnsBadRequest()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", "tests.payload.complex");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Content = new StringContent("{ bad json", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid Heimdall action request body", body);
    }

    [Fact]
    public async Task ContentPayloadAttribute_ForcesRegisteredTypeToBindFromRequestBody()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton(new ServiceLikePayload { Value = "from-service" });
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(
            client,
            "tests.payload.explicit",
            new { Value = "from-payload" });
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("from-payload", html);
        Assert.DoesNotContain("from-service", html);
    }

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

    [Fact]
    public async Task BifrostTokenEndpoint_UsesConfiguredTopicAuthorization()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (context, topic) =>
                ValueTask.FromResult(topic == $"user:{context.User.Identity?.Name}:notifications");
        });
        using var client = app.GetTestClient();

        var forbidden = await GetBifrostTokenAsync(client, "user:bob:notifications", "alice");
        var allowed = await GetBifrostTokenAsync(client, "user:alice:notifications", "alice");
        var token = await allowed.Content.ReadFromJsonAsync<BifrostTokenResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(token?.Token));
    }

    [Fact]
    public async Task BifrostTokenEndpoint_RejectsMissingTopic()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
        });
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client, "alice");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__heimdall/v1/bifrost/token");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Headers.Add(TestAuthHandler.UserHeaderName, "alice");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BifrostTokenEndpoint_RejectsMissingCsrfToken()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
        });
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__heimdall/v1/bifrost/token?topic=news");
        request.Headers.Add(TestAuthHandler.UserHeaderName, "alice");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BifrostTokenEndpoint_CanAuthorizeTopicWithPolicyResource()
    {
        await using var app = await CreateAppAsync(
            services =>
            {
                services.AddSingleton<IAuthorizationHandler, TopicOwnerHandler>();
                services.AddAuthorization(options =>
                {
                    options.AddPolicy("tests.topic-owner", policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.AddRequirements(new TopicOwnerRequirement());
                    });
                });
            },
            options =>
            {
                options.BifrostTopicPolicy = "tests.topic-owner";
            });
        using var client = app.GetTestClient();

        var forbidden = await GetBifrostTokenAsync(client, "user:bob:notifications", "alice");
        var allowed = await GetBifrostTokenAsync(client, "user:alice:notifications", "alice");
        var token = await allowed.Content.ReadFromJsonAsync<BifrostTokenResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(token?.Token));
    }

    [Fact]
    public async Task BifrostStream_RejectsTokenMintedForDifferentUser()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (context, topic) =>
                ValueTask.FromResult(topic == $"user:{context.User.Identity?.Name}:notifications");
        });
        using var client = app.GetTestClient();

        var allowed = await GetBifrostTokenAsync(client, "user:alice:notifications", "alice");
        var token = await allowed.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost?topic=user%3Aalice%3Anotifications&st={Uri.EscapeDataString(token!.Token!)}");
        request.Headers.Add(TestAuthHandler.UserHeaderName, "bob");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BifrostStream_DeliversPublishedHtmlToAuthorizedSubscriber()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
        });
        using var client = app.GetTestClient();
        var tokenResponse = await GetBifrostTokenAsync(client, "news", "alice");
        var token = await tokenResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        using var streamCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost?topic=news&st={Uri.EscapeDataString(token!.Token!)}");
        request.Headers.Add(TestAuthHandler.UserHeaderName, "alice");

        var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            streamCts.Token);
        var stream = await response.Content.ReadAsStreamAsync(streamCts.Token);
        var readTask = ReadUntilAsync(stream, "data: <span>fresh</span>", streamCts.Token);

        await app.Services.GetRequiredService<Bifrost>()
            .PublishAsync("news", Html.Span("fresh"), TimeSpan.FromSeconds(5), streamCts.Token);
        var body = await readTask;
        await streamCts.CancelAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("event: heimdall:connected", body);
        Assert.Contains("data: <span>fresh</span>", body);
    }

    [Fact]
    public async Task BifrostStream_DeliversPublishedHtmlWithNamedEvent()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
        });
        using var client = app.GetTestClient();
        var tokenResponse = await GetBifrostTokenAsync(client, "orders", "alice");
        var token = await tokenResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        using var streamCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost?topic=orders&st={Uri.EscapeDataString(token!.Token!)}");
        request.Headers.Add(TestAuthHandler.UserHeaderName, "alice");

        var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            streamCts.Token);
        var stream = await response.Content.ReadAsStreamAsync(streamCts.Token);
        var readTask = ReadUntilAsync(stream, "data: <span>changed</span>", streamCts.Token);

        await app.Services.GetRequiredService<Bifrost>()
            .PublishAsync("orders", "order.updated", Html.Span("changed"), TimeSpan.FromSeconds(5), streamCts.Token);
        var body = await readTask;
        await streamCts.CancelAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("event: order.updated", body);
        Assert.Contains("data: <span>changed</span>", body);
    }

    [Fact]
    public async Task BifrostStream_SendsIdleHeartbeatComment()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
            options.BifrostHeartbeatInterval = TimeSpan.FromMilliseconds(100);
        });
        using var client = app.GetTestClient();
        var tokenResponse = await GetBifrostTokenAsync(client, "quiet", "alice");
        var token = await tokenResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        using var streamCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost?topic=quiet&st={Uri.EscapeDataString(token!.Token!)}");
        request.Headers.Add(TestAuthHandler.UserHeaderName, "alice");

        var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            streamCts.Token);
        var stream = await response.Content.ReadAsStreamAsync(streamCts.Token);
        var body = await ReadUntilAsync(stream, ": ping", streamCts.Token);
        await streamCts.CancelAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("event: heimdall:connected", body);
        Assert.Contains(": ping", body);
    }

    private static async Task<WebApplication> CreateAppAsync(
        Action<IServiceCollection>? configureServices = null,
        Action<HeimdallServiceSettings>? configureHeimdall = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();

        builder.Services.AddAntiforgery();
        builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        configureServices?.Invoke(builder.Services);

        builder.Services.AddHeimdall(options =>
        {
            options.EnableDetailedErrors = true;
            configureHeimdall?.Invoke(options);
        }, typeof(ServerIntegrationTests).Assembly);

        var app = builder.Build();
        app.UseAntiforgery();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHeimdall();
        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> CreateAppUsingIApplicationBuilderUseHeimdallAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddAntiforgery();
        builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHeimdall(options =>
        {
            options.EnableDetailedErrors = true;
            options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
        }, typeof(ServerIntegrationTests).Assembly);

        var app = builder.Build();
        app.UseRouting();
        app.UseAntiforgery();
        app.UseAuthentication();
        app.UseAuthorization();
        ((IApplicationBuilder)app).UseHeimdall();
        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage> PostContentActionAsync(
        HttpClient client,
        string actionId,
        object? payload = null,
        string? userName = null,
        string? role = null)
    {
        var csrfToken = await GetCsrfTokenAsync(client, userName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", actionId);
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);

        if (!string.IsNullOrWhiteSpace(userName))
        {
            request.Headers.Add(TestAuthHandler.UserHeaderName, userName);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            request.Headers.Add(TestAuthHandler.RoleHeaderName, role);
        }

        request.Content = JsonContent.Create(payload ?? new { });
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetBifrostTokenAsync(
        HttpClient client,
        string topic,
        string userName)
    {
        var csrfToken = await GetCsrfTokenAsync(client, userName);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost/token?topic={Uri.EscapeDataString(topic)}");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Headers.Add(TestAuthHandler.UserHeaderName, userName);
        return await client.SendAsync(request);
    }

    private static async Task<CsrfToken> GetCsrfTokenAsync(HttpClient client, string? userName = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__heimdall/v1/csrf");

        if (!string.IsNullOrWhiteSpace(userName))
        {
            request.Headers.Add(TestAuthHandler.UserHeaderName, userName);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<CsrfResponse>();
        var requestToken = token?.RequestToken
            ?? throw new InvalidOperationException("CSRF response did not include a request token.");

        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            throw new InvalidOperationException("CSRF response did not set an antiforgery cookie.");

        var cookieHeader = string.Join("; ", setCookies.Select(x => x.Split(';', 2)[0]));
        return new CsrfToken(requestToken, cookieHeader);
    }

    private static async Task<string> ReadUntilAsync(Stream stream, string expected, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var body = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
                break;

            body.AppendLine(line);

            if (body.ToString().Contains(expected, StringComparison.Ordinal))
                return body.ToString();
        }

        throw new TimeoutException($"SSE stream did not include expected content: {expected}");
    }

    private static void AddAssemblyToContentRegistry(Assembly assembly)
    {
        var registryType = typeof(ContentInvocationAttribute).Assembly.GetType(
            "Heimdall.Server.ContentRegistry",
            throwOnError: true)!;
        var registry = Activator.CreateInstance(registryType, nonPublic: true)!;
        using var services = new ServiceCollection().BuildServiceProvider();
        var addFromAssembly = registryType.GetMethod(
            "AddFromAssembly",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to find ContentRegistry.AddFromAssembly.");

        try
        {
            addFromAssembly.Invoke(registry, [assembly, services]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static Assembly CreateDuplicateInvocationAssembly()
    {
        var assemblyName = new AssemblyName($"HeimdallDuplicateInvocationTests{Guid.NewGuid():N}");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");

        DefineContentActionType(
            module,
            "PrefixedCollisionActions",
            prefix: "tests.collision",
            invocation: "refresh",
            methodName: "Refresh");
        DefineContentActionType(
            module,
            "ExplicitCollisionActions",
            prefix: null,
            invocation: "tests.collision.refresh",
            methodName: "Refresh");

        return assembly;
    }

    private static void DefineContentActionType(
        ModuleBuilder module,
        string typeName,
        string? prefix,
        string invocation,
        string methodName)
    {
        var type = module.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

        if (prefix is not null)
        {
            var prefixCtor = typeof(ContentInvocationPrefixAttribute).GetConstructor([typeof(string)])
                ?? throw new InvalidOperationException("Unable to find ContentInvocationPrefixAttribute constructor.");
            type.SetCustomAttribute(new CustomAttributeBuilder(prefixCtor, [prefix]));
        }

        var method = type.DefineMethod(
            methodName,
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(IHtmlContent),
            Type.EmptyTypes);
        var invocationCtor = typeof(ContentInvocationAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("Unable to find ContentInvocationAttribute constructor.");
        method.SetCustomAttribute(new CustomAttributeBuilder(invocationCtor, [invocation]));

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);

        type.CreateType();
    }

    private sealed record CsrfToken(string RequestToken, string CookieHeader);

    private sealed class CsrfResponse
    {
        public string? RequestToken { get; set; }
    }

    private sealed class BifrostTokenResponse
    {
        public string? Token { get; set; }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string UserHeaderName = "X-Test-User";
        public const string RoleHeaderName = "X-Test-Role";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeaderName, out var userName) ||
                string.IsNullOrWhiteSpace(userName))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userName!),
                new Claim(ClaimTypes.Name, userName!)
            }.ToList();

            if (Request.Headers.TryGetValue(RoleHeaderName, out var roles))
            {
                foreach (var role in roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class TopicOwnerRequirement : IAuthorizationRequirement
    {
    }

    private sealed class TopicOwnerHandler : AuthorizationHandler<TopicOwnerRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            TopicOwnerRequirement requirement)
        {
            if (context.Resource is BifrostTopicResource resource &&
                ReferenceEquals(resource.HttpContext.User, context.User) &&
                string.Equals(
                    resource.Topic,
                    $"user:{context.User.Identity?.Name}:notifications",
                    StringComparison.Ordinal))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CustomMvcRenderer : IHeimdallMvcRenderer
    {
        public Task<IHtmlContent> PartialAsync(
            string viewName,
            object? model = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IHtmlContent>(new HtmlString("custom renderer"));

        public Task<IHtmlContent> PartialAsync(
            string viewName,
            object? model,
            Action<ViewDataDictionary> configureViewData,
            CancellationToken cancellationToken = default)
        {
            var viewData = new ViewDataDictionary(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = model
            };
            configureViewData(viewData);
            return Task.FromResult<IHtmlContent>(new HtmlString("custom renderer"));
        }
    }

    private sealed class FakeCompositeViewEngine : ICompositeViewEngine
    {
        private static readonly IView FakeView = new FakeMvcPartialView();

        public IReadOnlyList<IViewEngine> ViewEngines { get; } = Array.Empty<IViewEngine>();

        public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
        {
            if (string.Equals(viewName, "missing", StringComparison.Ordinal))
            {
                return ViewEngineResult.NotFound(
                    viewName,
                    [$"/Views/{viewName}.cshtml", $"/Views/Shared/{viewName}.cshtml"]);
            }

            return ViewEngineResult.Found(viewName, FakeView);
        }

        public ViewEngineResult GetView(string? executingFilePath, string viewPath, bool isMainPage)
        {
            if (string.Equals(viewPath, "missing", StringComparison.Ordinal))
            {
                return ViewEngineResult.NotFound(
                    viewPath,
                    [$"/Views/{viewPath}.cshtml", $"/Views/Shared/{viewPath}.cshtml"]);
            }

            if (viewPath.StartsWith("~/", StringComparison.Ordinal) ||
                viewPath.StartsWith("/", StringComparison.Ordinal) ||
                viewPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            {
                return ViewEngineResult.Found(viewPath, FakeView);
            }

            return ViewEngineResult.NotFound(
                viewPath,
                [$"/Views/{viewPath}.cshtml", $"/Views/Shared/{viewPath}.cshtml"]);
        }
    }

    private sealed class FakeMvcPartialView : IView
    {
        public string Path => "/Views/Shared/_Greeting.cshtml";

        public Task RenderAsync(ViewContext context)
        {
            var payload = Assert.IsType<MvcPartialPayload>(context.ViewData.Model);
            var source = Assert.IsType<string>(context.ViewData["source"]);

            context.Writer.Write(
                $"<div id=\"mvc-partial\" data-source=\"{source}\">Hello {payload.Name}</div>");

            return Task.CompletedTask;
        }
    }

    private static class TestContentActions
    {
        [Authorize]
        [ContentInvocation("tests.auth.secure")]
        public static IHtmlContent Secure(ClaimsPrincipal user)
        {
            return Html.Span(user.Identity?.Name ?? "anonymous");
        }

        [Authorize(Policy = "tests.admin")]
        [ContentInvocation("tests.auth.admin")]
        public static IHtmlContent AdminOnly()
        {
            return Html.Span("admin");
        }

        [RequestTimeout(50)]
        [ContentInvocation("tests.timeout.slow")]
        public static async Task<IHtmlContent> Slow(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Html.Span("done");
        }

        [RequestTimeout("tests.fast-teapot")]
        [ContentInvocation("tests.timeout.named-policy")]
        public static async Task<IHtmlContent> NamedPolicySlow(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Html.Span("done");
        }

        [ContentInvocation("tests.classification.service")]
        public static IHtmlContent UsesExplicitService([FromServices] ConstructedService service)
        {
            return Html.Span(service.GetType().Name);
        }

        [ContentInvocation("tests.payload.complex")]
        public static IHtmlContent ComplexPayload(PayloadDto payload)
        {
            return Html.Span($"{payload.Name}|{payload.Count}|{payload.Enabled}|{payload.Mode}");
        }

        [ContentInvocation("tests.payload.simple")]
        public static IHtmlContent SimplePayload(string name = "fallback")
        {
            return Html.Span(name);
        }

        [ContentInvocation("tests.payload.explicit")]
        public static IHtmlContent ExplicitPayload([ContentPayload] ServiceLikePayload payload)
        {
            return Html.Span(payload.Value);
        }

        [ContentInvocation("tests.services.implicit")]
        public static IHtmlContent ImplicitService(GreetingService service)
        {
            return Html.Span(service.Message);
        }
    }

    private sealed class InstanceContentActions(GreetingService greeting)
    {
        [ContentInvocation("tests.instance.greeting")]
        public IHtmlContent Greeting(InstancePayload payload)
        {
            return Html.Span($"{greeting.Message}:{payload.Name}");
        }
    }

    private sealed class RegisteredInstanceContentActions(string message)
    {
        [ContentInvocation("tests.instance.registered")]
        public IHtmlContent Registered()
        {
            return Html.Span(message);
        }
    }

    private sealed class InstanceDiscoveryActions(ConstructedService service)
    {
        [ContentInvocation("tests.instance.discovery")]
        public IHtmlContent Render()
        {
            return Html.Span(service.GetType().Name);
        }
    }

    private sealed class CountingInstanceContentActions
    {
        private static int constructionCount;
        private readonly int instanceNumber;

        public CountingInstanceContentActions()
        {
            instanceNumber = Interlocked.Increment(ref constructionCount);
        }

        public static int ConstructionCount => constructionCount;

        public static void Reset()
        {
            Interlocked.Exchange(ref constructionCount, 0);
        }

        [ContentInvocation("tests.instance.activation-count")]
        public IHtmlContent Count()
        {
            return Html.Span(instanceNumber);
        }
    }

    private sealed class StatefulRegisteredInstanceContentActions
    {
        private int calls;

        [ContentInvocation("tests.instance.registered-state")]
        public IHtmlContent Next()
        {
            return Html.Span(Interlocked.Increment(ref calls));
        }
    }

    [Authorize]
    private sealed class InstanceAuthorizedActions
    {
        [ContentInvocation("tests.instance.auth.secure")]
        public IHtmlContent Secure(ClaimsPrincipal user)
        {
            return Html.Span(user.Identity?.Name ?? "anonymous");
        }

        [AllowAnonymous]
        [ContentInvocation("tests.instance.auth.public")]
        public IHtmlContent Public()
        {
            return Html.Span("instance public");
        }
    }

    [RequestTimeout(50)]
    private sealed class InstanceTimeoutActions
    {
        [ContentInvocation("tests.instance.timeout.slow")]
        public async Task<IHtmlContent> Slow(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Html.Span("done");
        }
    }

    [ContentInvocationPrefix("tests.mvc")]
    private sealed class MvcContentActions(IHeimdallMvcRenderer views)
    {
        [ContentInvocation("partial")]
        public Task<IHtmlContent> Partial(MvcPartialPayload payload, CancellationToken cancellationToken)
        {
            return views.PartialAsync(
                payload.ViewName ?? "_Greeting",
                payload,
                viewData => viewData["source"] = "heimdall",
                cancellationToken);
        }

        [ContentInvocation("missing")]
        public Task<IHtmlContent> Missing(CancellationToken cancellationToken)
            => views.PartialAsync("missing", cancellationToken: cancellationToken);
    }

    [ContentInvocationPrefix("tests.prefix.static")]
    private static class PrefixedStaticContentActions
    {
        [ContentInvocation("refresh")]
        public static IHtmlContent Refresh()
        {
            return Html.Span("prefixed static");
        }
    }

    [ContentInvocationPrefix("tests.prefix.instance")]
    private sealed class PrefixedInstanceContentActions(GreetingService greeting)
    {
        [ContentInvocation]
        public IHtmlContent Render()
        {
            return Html.Span(greeting.Message);
        }
    }

    [ContentInvocationPrefix(".tests.prefix.normalized.")]
    private static class NormalizedPrefixContentActions
    {
        [ContentInvocation(".refresh.")]
        public static IHtmlContent Refresh()
        {
            return Html.Span("normalized prefix");
        }
    }

    private static class DefaultContentActions
    {
        [ContentInvocation]
        public static IHtmlContent Ping()
        {
            return Html.Span("default action id");
        }
    }

    [Authorize]
    private static class TypeAuthorizedActions
    {
        [AllowAnonymous]
        [ContentInvocation("tests.auth.allow-anonymous")]
        public static IHtmlContent Public()
        {
            return Html.Span("public");
        }
    }

    [RequestTimeout(50)]
    private static class TypeTimeoutActions
    {
        [DisableRequestTimeout]
        [ContentInvocation("tests.timeout.disabled")]
        public static async Task<IHtmlContent> Disabled(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(125), cancellationToken);
            return Html.Span("not timed out");
        }
    }

    private sealed class PayloadDto
    {
        public string? Name { get; set; }

        public int Count { get; set; }

        public bool Enabled { get; set; }

        public PayloadMode Mode { get; set; }
    }

    private sealed class InstancePayload
    {
        public string? Name { get; set; }
    }

    private sealed class MvcPartialPayload
    {
        public string? ViewName { get; set; }

        public string? Name { get; set; }
    }

    private enum PayloadMode
    {
        Alpha,
        Beta
    }

    private sealed class ServiceLikePayload
    {
        public string? Value { get; set; }
    }

    private sealed class GreetingService(string message)
    {
        public string Message { get; } = message;
    }

    private sealed class ConstructedService
    {
        private static int constructionCount;
        private static bool throwOnConstruct;

        public ConstructedService()
        {
            Interlocked.Increment(ref constructionCount);

            if (throwOnConstruct)
            {
                throw new InvalidOperationException("Service construction should not happen during action discovery.");
            }
        }

        public static int ConstructionCount => constructionCount;

        public static void Reset(bool throwOnConstruct)
        {
            Interlocked.Exchange(ref constructionCount, 0);
            ConstructedService.throwOnConstruct = throwOnConstruct;
        }
    }
}
