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
    public async Task BifrostConnectionHandler_TracksLifecycleAndMutableMetadata()
    {
        var probe = new TestBifrostConnectionProbe();
        await using var app = await CreateAppAsync(
            services =>
            {
                services.AddSingleton(probe);
                services.AddBifrostConnectionHandler<TestBifrostConnectionHandler>();
            },
            options => options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true));
        using var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IBifrostConnectionStore>();
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

        await WaitUntilAsync(() => store.Connections.Count == 1, TimeSpan.FromSeconds(2));
        var connection = Assert.Single(store.Connections);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("event: heimdall:connected", connectedBody);
        Assert.Equal("orders", connection.Topic);
        Assert.Equal("alice", connection.Subject);
        Assert.NotEqual(Guid.Empty, connection.ConnectionId);
        Assert.Equal("acme", connection.Metadata["tenant"]);
        Assert.True(store.TryGet(connection.ConnectionId, out var lookedUp));
        Assert.Equal(connection.ConnectionId, lookedUp?.ConnectionId);
        Assert.Contains(
            probe.Authenticated,
            snapshot => snapshot.ConnectionId == connection.ConnectionId &&
                snapshot.Metadata["tenant"] == "acme");

        Assert.True(store.TrySetMetadata(connection.ConnectionId, "region", "east"));
        var selected = store.GetConnections(new BifrostConnectionSelector
        {
            Topic = "ORDERS",
            Subject = "alice",
            Metadata = new Dictionary<string, string>
            {
                ["tenant"] = "acme",
                ["region"] = "east"
            }
        });
        Assert.Single(selected);
        Assert.True(store.TryRemoveMetadata(connection.ConnectionId, "region"));
        Assert.Empty(store.GetConnections(new BifrostConnectionSelector
        {
            Metadata = new Dictionary<string, string> { ["region"] = "east" }
        }));

        await streamCts.CancelAsync();
        stream.Dispose();
        await WaitUntilAsync(
            () => store.Connections.Count == 0 && probe.Disconnected.Count == 1,
            TimeSpan.FromSeconds(2));

        var disconnected = Assert.Single(probe.Disconnected);
        Assert.Equal(connection.ConnectionId, disconnected.ConnectionId);
        Assert.Equal("client-disconnected", disconnected.Reason);
        Assert.Equal("acme", disconnected.Metadata["tenant"]);
    }

    [Fact]
    public async Task BifrostConnectionSettings_InvokesInlineCallbacksWithScopedServices()
    {
        var probe = new TestBifrostConnectionProbe();
        await using var app = await CreateAppAsync(
            services =>
            {
                services.AddSingleton(probe);
                services.AddScoped<TestBifrostScopedMarker>();
            },
            options =>
            {
                options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true);
                options.OnBifrostAuthenticated = context =>
                {
                    context.TrySetMetadata("source", "inline");
                    probe.InlineAuthenticatedServices.Enqueue(
                        context.Services.GetRequiredService<TestBifrostScopedMarker>().Value);
                    return ValueTask.CompletedTask;
                };
                options.OnBifrostDisconnected = context =>
                {
                    probe.InlineDisconnectedServices.Enqueue(
                        context.Services.GetRequiredService<TestBifrostScopedMarker>().Value);
                    return ValueTask.CompletedTask;
                };
            });
        using var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IBifrostConnectionStore>();
        var tokenResponse = await GetBifrostTokenAsync(client, "inline", "alice");
        var token = await tokenResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        using var streamCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost?topic=inline&st={Uri.EscapeDataString(token!.Token!)}");
        request.Headers.Add(TestAuthHandler.UserHeaderName, "alice");

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            streamCts.Token);
        using var stream = await response.Content.ReadAsStreamAsync(streamCts.Token);
        await ReadUntilAsync(stream, "data: topic:inline", streamCts.Token);
        await WaitUntilAsync(() => store.Connections.Count == 1, TimeSpan.FromSeconds(2));

        var connection = Assert.Single(store.Connections);
        Assert.Equal("inline", connection.Metadata["source"]);
        Assert.Equal(new[] { "scope" }, probe.InlineAuthenticatedServices.ToArray());

        await streamCts.CancelAsync();
        stream.Dispose();
        await WaitUntilAsync(
            () => store.Connections.Count == 0 && probe.InlineDisconnectedServices.Count == 1,
            TimeSpan.FromSeconds(2));

        Assert.Equal(new[] { "scope" }, probe.InlineDisconnectedServices.ToArray());
    }

    [Fact]
    public async Task BifrostConnectionHandler_DisconnectFailureDoesNotLeaveARegistryEntry()
    {
        await using var app = await CreateAppAsync(
            services => services.AddBifrostConnectionHandler<ThrowingBifrostDisconnectHandler>(),
            options => options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true));
        using var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IBifrostConnectionStore>();
        var tokenResponse = await GetBifrostTokenAsync(client, "cleanup", "alice");
        var token = await tokenResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        using var streamCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = CreateBifrostStreamRequest("cleanup", token!.Token!, "alice");

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            streamCts.Token);
        using var stream = await response.Content.ReadAsStreamAsync(streamCts.Token);
        await ReadUntilAsync(stream, "data: topic:cleanup", streamCts.Token);
        await WaitUntilAsync(() => store.Connections.Count == 1, TimeSpan.FromSeconds(2));

        await streamCts.CancelAsync();
        stream.Dispose();

        await WaitUntilAsync(() => store.Connections.Count == 0, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task BifrostConnectionStore_SelectsAndDisconnectsOneConnectionWithoutAffectingAnother()
    {
        await using var app = await CreateAppAsync(
            configureHeimdall: options =>
                options.AuthorizeBifrostTopic = (_, _) => ValueTask.FromResult(true));
        using var client = app.GetTestClient();
        var bifrost = app.Services.GetRequiredService<Bifrost>();
        var store = app.Services.GetRequiredService<IBifrostConnectionStore>();

        var aliceTokenResponse = await GetBifrostTokenAsync(client, "orders", "alice");
        var aliceToken = await aliceTokenResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        var bobTokenResponse = await GetBifrostTokenAsync(client, "orders", "bob");
        var bobToken = await bobTokenResponse.Content.ReadFromJsonAsync<BifrostTokenResponse>();
        using var aliceCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var bobCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var aliceRequest = CreateBifrostStreamRequest("orders", aliceToken!.Token!, "alice");
        using var bobRequest = CreateBifrostStreamRequest("orders", bobToken!.Token!, "bob");
        using var aliceResponse = await client.SendAsync(
            aliceRequest,
            HttpCompletionOption.ResponseHeadersRead,
            aliceCts.Token);
        using var bobResponse = await client.SendAsync(
            bobRequest,
            HttpCompletionOption.ResponseHeadersRead,
            bobCts.Token);
        using var aliceStream = await aliceResponse.Content.ReadAsStreamAsync(aliceCts.Token);
        using var bobStream = await bobResponse.Content.ReadAsStreamAsync(bobCts.Token);
        await ReadUntilAsync(aliceStream, "data: topic:orders", aliceCts.Token);
        await ReadUntilAsync(bobStream, "data: topic:orders", bobCts.Token);

        await WaitUntilAsync(() => store.Connections.Count == 2, TimeSpan.FromSeconds(2));
        var alice = Assert.Single(store.GetConnections(BifrostConnectionSelector.ForSubject("alice")));
        var bob = Assert.Single(store.GetConnections(BifrostConnectionSelector.ForSubject("bob")));

        var aliceDisconnected = store.Disconnect(
            new BifrostConnectionSelector { Topic = "orders", Subject = "alice" },
            "authorization-revoked");
        var aliceBody = await ReadUntilAsync(
            aliceStream,
            "data: authorization-revoked",
            aliceCts.Token);

        Assert.Equal(1, aliceDisconnected);
        Assert.Contains("event: heimdall:disconnect", aliceBody);
        await WaitUntilAsync(
            () => store.GetConnections(BifrostConnectionSelector.ForSubject("alice")).Count == 0,
            TimeSpan.FromSeconds(2));
        Assert.Single(store.GetConnections(BifrostConnectionSelector.ForSubject("bob")));

        var bobMessage = ReadUntilAsync(bobStream, "data: <span>still-here</span>", bobCts.Token);
        await bifrost.PublishAsync(
            "orders",
            Html.Span("still-here"),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        Assert.Contains("data: <span>still-here</span>", await bobMessage);

        var bobDisconnected = bifrost.DisconnectSubscribers(
            BifrostConnectionSelector.ForConnection(bob.ConnectionId),
            "server-shutdown");
        var bobBody = await ReadUntilAsync(
            bobStream,
            "data: server-shutdown",
            bobCts.Token);

        Assert.Equal(1, bobDisconnected);
        Assert.Contains("event: heimdall:disconnect", bobBody);
        await WaitUntilAsync(() => store.Connections.Count == 0, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AddBifrostConnectionHandler_IsFluentAndRegistersAScopedHandler()
    {
        var services = new ServiceCollection();

        var result = services
            .AddHeimdall()
            .AddBifrostConnectionHandler<TestBifrostConnectionHandler>();

        Assert.Same(services, result);
        var descriptor = Assert.Single(
            services,
            service =>
                service.ServiceType == typeof(IBifrostConnectionHandler) &&
                service.ImplementationType == typeof(TestBifrostConnectionHandler));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IBifrostConnectionStore>();
        Assert.Same(provider.GetRequiredService<BifrostConnectionStore>(), store);
        Assert.Same(store, provider.GetRequiredService<Bifrost>().Connections);
    }

    [Fact]
    public void BifrostConnectionStore_RejectsUnboundedDisconnectAndInvalidMetadata()
    {
        var store = new BifrostConnectionStore();

        Assert.Throws<ArgumentException>(() => store.Disconnect(new BifrostConnectionSelector()));
        Assert.Throws<ArgumentException>(() => store.TrySetMetadata(Guid.NewGuid(), " ", "value"));
        Assert.Throws<ArgumentNullException>(() => store.TrySetMetadata(Guid.NewGuid(), "key", null!));
        Assert.Throws<ArgumentException>(() => store.TryRemoveMetadata(Guid.NewGuid(), "\t"));
        Assert.Throws<ArgumentException>(() => store.Disconnect(BifrostConnectionSelector.ForTopic("orders"), "bad\nreason"));
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

    private static HttpRequestMessage CreateBifrostStreamRequest(
        string topic,
        string token,
        string userName)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/__heimdall/v1/bifrost?topic={Uri.EscapeDataString(topic)}&st={Uri.EscapeDataString(token)}");
        request.Headers.Add(TestAuthHandler.UserHeaderName, userName);
        return request;
    }
}
