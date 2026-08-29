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

    private static async Task<WebApplication> CreateCookieAuthAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();

        builder.Services.AddAntiforgery();
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/signin";
                options.AccessDeniedPath = "/denied";
            });
        builder.Services.AddAuthorization();
        builder.Services.AddHeimdall(options =>
        {
            options.EnableDetailedErrors = true;
            options.AuthorizeBifrostTopic = (context, topic) =>
                ValueTask.FromResult(topic != "secure-topic" || context.User.Identity?.IsAuthenticated == true);
        }, typeof(ServerIntegrationTests).Assembly);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseHeimdall();
        app.MapGet("/signin", () => Results.Content("<h1>Sign in</h1>", "text/html; charset=utf-8"));
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

    private static async Task<HttpResponseMessage> PostMultipartContentActionAsync(
        HttpClient client,
        string actionId,
        MultipartFormDataContent content)
    {
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", actionId);
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Content = content;
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

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);

        while (!condition())
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException("Condition was not satisfied before the timeout elapsed.");
            }
        }
    }
}
