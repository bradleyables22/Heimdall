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
    public async Task ContentAction_MalformedJsonPayloadLogsWarning()
    {
        var logs = new TestLoggerProvider();
        await using var app = await CreateAppAsync(services =>
        {
            services.AddLogging(builder => builder.AddProvider(logs));
        });
        using var client = app.GetTestClient();
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", "tests.payload.complex");
        request.Headers.Add("RequestVerificationToken", csrfToken.RequestToken);
        request.Headers.Add("Cookie", csrfToken.CookieHeader);
        request.Content = new StringContent("{ bad json", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            logs.Entries,
            entry => entry.Category == "Heimdall.Server.ContentEndpoints" &&
                entry.Level == LogLevel.Warning &&
                entry.Message.Contains("tests.payload.complex", StringComparison.Ordinal) &&
                entry.Exception is not null);
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
    public async Task FromFormAttribute_ForcesRegisteredTypeToBindFromFormAndHonorsNameAliases()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton(new ServiceLikePayload { Value = "from-service" });
        });
        using var client = app.GetTestClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("from-form"), "model.Value");
        content.Add(new ByteArrayContent([1, 2, 3]), "file", "aliased.bin");

        var response = await PostMultipartContentActionAsync(
            client,
            "tests.upload.from-form-aliases",
            content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Equal("<span>from-form|aliased.bin|3</span>", html);
        Assert.DoesNotContain("from-service", html);
    }

    [Fact]
    public async Task FromFormAttribute_RejectsJsonRequest()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton(new ServiceLikePayload { Value = "from-service" });
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(
            client,
            "tests.upload.from-form-aliases",
            new { Value = "from-json" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Contains("requires a form content type", body);
    }
}
