using System.Collections.Concurrent;
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
    private sealed record CsrfToken(string RequestToken, string CookieHeader);

    private sealed class CsrfResponse
    {
        public string? RequestToken { get; set; }
    }

    private sealed class BifrostTokenResponse
    {
        public string? Token { get; set; }
    }

    private sealed class TestBifrostConnectionProbe
    {
        public ConcurrentQueue<BifrostConnectionSnapshot> Authenticated { get; } = new();

        public ConcurrentQueue<BifrostConnectionSnapshot> Disconnected { get; } = new();

        public ConcurrentQueue<string> InlineAuthenticatedServices { get; } = new();

        public ConcurrentQueue<string> InlineDisconnectedServices { get; } = new();
    }

    private sealed class TestBifrostScopedMarker
    {
        public string Value => "scope";
    }

    private sealed record BifrostConnectionSnapshot(
        Guid ConnectionId,
        string Topic,
        string? Subject,
        string Reason,
        IReadOnlyDictionary<string, string> Metadata);

    private sealed class TestBifrostConnectionHandler(
        TestBifrostConnectionProbe probe)
        : IBifrostConnectionHandler
    {
        public ValueTask OnBifrostAuthenticatedAsync(BifrostAuthenticatedContext context)
        {
            Assert.True(context.TrySetMetadata("tenant", "acme"));
            Assert.True(context.Connections.TryGet(context.Connection.ConnectionId, out var connection));

            probe.Authenticated.Enqueue(CreateSnapshot(
                connection ?? context.Connection,
                reason: "authenticated"));

            return ValueTask.CompletedTask;
        }

        public ValueTask OnBifrostDisconnectedAsync(BifrostDisconnectedContext context)
        {
            probe.Disconnected.Enqueue(CreateSnapshot(context.Connection, context.Reason));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingBifrostDisconnectHandler : IBifrostConnectionHandler
    {
        public ValueTask OnBifrostDisconnectedAsync(BifrostDisconnectedContext context)
            => ValueTask.FromException(
                new InvalidOperationException("Test disconnect cleanup failure."));
    }

    private static BifrostConnectionSnapshot CreateSnapshot(
        BifrostConnectionInfo connection,
        string reason)
        => new(
            connection.ConnectionId,
            connection.Topic,
            connection.Subject,
            reason,
            new Dictionary<string, string>(connection.Metadata, StringComparer.Ordinal));

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

    private sealed class TestBifrostTopicAccessStore
    {
        public HashSet<string> AllowedTopics { get; } = new(StringComparer.Ordinal);

        public List<string> AuthorizationCalls { get; } = new();

        public bool CanRead(ClaimsPrincipal user, string topic)
        {
            AuthorizationCalls.Add(topic);

            return user.Identity?.IsAuthenticated == true &&
                AllowedTopics.Contains(topic);
        }
    }

    private sealed class UserNotificationsTopicAuthHandler(
        TestBifrostTopicAccessStore access)
        : IBifrostTopicAuthHandler
    {
        public bool CanHandle(string topic)
        {
            var parts = topic.Split(':');
            return parts.Length == 3 &&
                parts[0] == "user" &&
                parts[2] == "notifications";
        }

        public ValueTask<bool> AuthorizeAsync(BifrostTopicAuthorizationContext context)
        {
            var parts = context.Topic.Split(':');
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return ValueTask.FromResult(
                string.Equals(userId, parts[1], StringComparison.Ordinal) &&
                access.CanRead(context.User, context.Topic));
        }
    }

    private sealed class TenantOrdersTopicAuthHandler(
        TestBifrostTopicAccessStore access)
        : IBifrostTopicAuthHandler
    {
        public bool CanHandle(string topic)
        {
            var parts = topic.Split(':');
            return parts.Length == 3 &&
                parts[0] == "tenant" &&
                parts[2] == "orders";
        }

        public ValueTask<bool> AuthorizeAsync(BifrostTopicAuthorizationContext context)
            => ValueTask.FromResult(access.CanRead(context.User, context.Topic));
    }

    private sealed class FirstAmbiguousTopicAuthHandler : IBifrostTopicAuthHandler
    {
        public bool CanHandle(string topic) => topic == "ambiguous";

        public ValueTask<bool> AuthorizeAsync(BifrostTopicAuthorizationContext context)
            => ValueTask.FromResult(true);
    }

    private sealed class SecondAmbiguousTopicAuthHandler : IBifrostTopicAuthHandler
    {
        public bool CanHandle(string topic) => topic == "ambiguous";

        public ValueTask<bool> AuthorizeAsync(BifrostTopicAuthorizationContext context)
            => ValueTask.FromResult(true);
    }
}
