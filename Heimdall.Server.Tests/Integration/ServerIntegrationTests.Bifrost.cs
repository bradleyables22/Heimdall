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
    public async Task BifrostTokenEndpoint_UsesRegisteredTopicAuthHandlersWithDependencyInjection()
    {
        var access = new TestBifrostTopicAccessStore();
        access.AllowedTopics.Add("user:alice:notifications");
        access.AllowedTopics.Add("tenant:acme:orders");

        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton(access);
            services.AddBifrostTopicAuthHandler<UserNotificationsTopicAuthHandler>();
            services.AddBifrostTopicAuthHandler<TenantOrdersTopicAuthHandler>();
        });
        using var client = app.GetTestClient();

        var userAllowed = await GetBifrostTokenAsync(client, "user:alice:notifications", "alice");
        var tenantAllowed = await GetBifrostTokenAsync(client, "tenant:acme:orders", "alice");
        var userForbidden = await GetBifrostTokenAsync(client, "user:bob:notifications", "alice");
        var unknown = await GetBifrostTokenAsync(client, "unregistered:topic", "alice");

        Assert.Equal(HttpStatusCode.OK, userAllowed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tenantAllowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, userForbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unknown.StatusCode);
        Assert.Equal(
            new[] { "user:alice:notifications", "tenant:acme:orders" },
            access.AuthorizationCalls);
    }

    [Fact]
    public async Task BifrostTokenEndpoint_DeniesTopicsWithAmbiguousAuthHandlers()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddBifrostTopicAuthHandler<FirstAmbiguousTopicAuthHandler>();
            services.AddBifrostTopicAuthHandler<SecondAmbiguousTopicAuthHandler>();
        });
        using var client = app.GetTestClient();

        var response = await GetBifrostTokenAsync(client, "ambiguous", "alice");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BifrostTokenEndpoint_AppliesLegacyTopicCallbackAfterRegisteredHandler()
    {
        var access = new TestBifrostTopicAccessStore();
        access.AllowedTopics.Add("tenant:acme:orders");

        await using var app = await CreateAppAsync(
            services =>
            {
                services.AddSingleton(access);
                services.AddBifrostTopicAuthHandler<TenantOrdersTopicAuthHandler>();
            },
            options =>
            {
                options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(false);
            });
        using var client = app.GetTestClient();

        var response = await GetBifrostTokenAsync(client, "tenant:acme:orders", "alice");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(new[] { "tenant:acme:orders" }, access.AuthorizationCalls);
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

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BifrostTokenEndpoint_InvalidAntiforgeryTokenReturnsBadRequestInsteadOfThrowing()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
        });
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client, "alice");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__heimdall/v1/bifrost/token?topic=news");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Headers.Add(TestAuthHandler.UserHeaderName, "bob");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("antiforgery", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BifrostTokenEndpoint_InvalidAntiforgeryTokenLogsWarning()
    {
        var logs = new TestLoggerProvider();
        await using var app = await CreateAppAsync(
            services =>
            {
                services.AddLogging(builder => builder.AddProvider(logs));
            },
            options =>
            {
                options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
            });
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client, "alice");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__heimdall/v1/bifrost/token?topic=news");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Headers.Add(TestAuthHandler.UserHeaderName, "bob");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            logs.Entries,
            entry => entry.Category == "Heimdall.Server.BifrostEndpoints" &&
                entry.Level == LogLevel.Warning &&
                entry.Message.Contains("news", StringComparison.Ordinal) &&
                entry.Exception is AntiforgeryValidationException);
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
    public void Bifrost_HasSubscribers_ReturnsFalseWhenTopicHasNeverHadSubscribers()
    {
        var bifrost = new Bifrost();

        Assert.False(bifrost.HasSubscribers("orders"));
        Assert.Empty(bifrost.SubscribedTopics);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Bifrost_HasSubscribers_RejectsMissingTopics(string? topic)
    {
        var bifrost = new Bifrost();

        var exception = Assert.Throws<ArgumentException>(() => bifrost.HasSubscribers(topic!));

        Assert.Equal("topic", exception.ParamName);
    }

    [Fact]
    public async Task Bifrost_HasSubscribers_TracksLocalStreamLifecycleCaseInsensitively()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
        });
        using var client = app.GetTestClient();
        var bifrost = app.Services.GetRequiredService<Bifrost>();

        Assert.False(bifrost.HasSubscribers("orders"));

        var tokenResponse = await GetBifrostTokenAsync(client, "Orders", "alice");
        var token = await tokenResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        using var streamCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost?topic=Orders&st={Uri.EscapeDataString(token!.Token!)}");
        request.Headers.Add(TestAuthHandler.UserHeaderName, "alice");

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            streamCts.Token);
        using var stream = await response.Content.ReadAsStreamAsync(streamCts.Token);
        var connectedBody = await ReadUntilAsync(stream, "data: topic:Orders", streamCts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data: topic:Orders", connectedBody);
        Assert.True(bifrost.HasSubscribers("orders"));
        Assert.True(bifrost.HasSubscribers("ORDERS"));
        Assert.False(bifrost.HasSubscribers("other-topic"));

        var topics = bifrost.SubscribedTopics;
        Assert.Equal(new[] { "Orders" }, topics);

        await streamCts.CancelAsync();
        stream.Dispose();
        response.Dispose();

        await WaitUntilAsync(() => !bifrost.HasSubscribers("orders"), TimeSpan.FromSeconds(2));
        Assert.False(bifrost.HasSubscribers("orders"));
        Assert.Empty(bifrost.SubscribedTopics);
        Assert.Equal(new[] { "Orders" }, topics);
    }

    [Fact]
    public async Task Bifrost_DisconnectSubscribers_SendsTerminalEventAndStopsStream()
    {
        await using var app = await CreateAppAsync(configureHeimdall: options =>
        {
            options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
        });
        using var client = app.GetTestClient();
        var bifrost = app.Services.GetRequiredService<Bifrost>();
        var tokenResponse = await GetBifrostTokenAsync(client, "orders", "alice");
        var token = await tokenResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        using var streamCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost?topic=orders&st={Uri.EscapeDataString(token!.Token!)}");
        request.Headers.Add(TestAuthHandler.UserHeaderName, "alice");

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            streamCts.Token);
        using var stream = await response.Content.ReadAsStreamAsync(streamCts.Token);
        var connectedBody = await ReadUntilAsync(stream, "data: topic:orders", streamCts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data: topic:orders", connectedBody);
        Assert.True(bifrost.HasSubscribers("orders"));

        var disconnected = bifrost.DisconnectSubscribers("ORDERS", "operator-kick");
        var disconnectedBody = await ReadUntilAsync(stream, "data: operator-kick", streamCts.Token);

        Assert.Equal(1, disconnected);
        Assert.Contains("event: heimdall:disconnect", disconnectedBody);
        Assert.Contains("data: operator-kick", disconnectedBody);

        await WaitUntilAsync(() => !bifrost.HasSubscribers("orders"), TimeSpan.FromSeconds(2));
        Assert.Empty(bifrost.SubscribedTopics);
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
}
