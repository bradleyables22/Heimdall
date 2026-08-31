using System.Net;
using System.Net.Http.Json;
using Heimdall.Server;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    [Fact]
    public void HeimdallServiceSettings_AntiforgeryIsEnabledByDefault()
    {
        Assert.True(new HeimdallServiceSettings().EnableAntiforgery);
    }

    [Fact]
    public async Task ContentAction_MethodAndTypeMetadataCanDisableAntiforgery()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var methodDisabled = await PostWithoutAntiforgeryAsync(
            client,
            "tests.antiforgery.method-disabled");
        using var typeDisabled = await PostWithoutAntiforgeryAsync(
            client,
            "tests.antiforgery.type-disabled");

        Assert.Equal(HttpStatusCode.OK, methodDisabled.StatusCode);
        Assert.Equal("<span>method antiforgery disabled</span>", await methodDisabled.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, typeDisabled.StatusCode);
        Assert.Equal("<span>type antiforgery disabled</span>", await typeDisabled.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ContentAction_MethodMetadataCanReenableTypeAntiforgery()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await PostWithoutAntiforgeryAsync(
            client,
            "tests.antiforgery.method-enabled");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("antiforgery", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GlobalAntiforgeryDisableOverridesActionMetadataAndSkipsValidation()
    {
        await using var app = await CreateAppAsync(
            services =>
            {
                services.RemoveAll<IAntiforgery>();
                services.AddSingleton<IAntiforgery, ThrowingAntiforgery>();
            },
            settings => settings.EnableAntiforgery = false);
        using var client = app.GetTestClient();

        using var defaultAction = await PostWithoutAntiforgeryAsync(
            client,
            "tests.antiforgery.default");
        using var explicitlyEnabledAction = await PostWithoutAntiforgeryAsync(
            client,
            "tests.antiforgery.method-enabled");

        Assert.Equal(HttpStatusCode.OK, defaultAction.StatusCode);
        Assert.Equal(HttpStatusCode.OK, explicitlyEnabledAction.StatusCode);
    }

    [Fact]
    public async Task GlobalAntiforgeryDisableAllowsBifrostTokenMintingWithoutCsrf()
    {
        await using var app = await CreateAppAsync(
            services =>
            {
                services.RemoveAll<IAntiforgery>();
                services.AddSingleton<IAntiforgery, ThrowingAntiforgery>();
            },
            settings => settings.EnableAntiforgery = false);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/__heimdall/v1/bifrost/token?topic=public-news");
        var token = await response.Content.ReadFromJsonAsync<BifrostTokenResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(token?.Token));
    }

    [Fact]
    public async Task GlobalAntiforgeryDisableDoesNotRequireAntiforgeryServices()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddHeimdall(
            settings => settings.EnableAntiforgery = false,
            typeof(ServerIntegrationTests).Assembly);

        await using var app = builder.Build();
        app.UseHeimdall();
        await app.StartAsync();
        using var client = app.GetTestClient();

        Assert.Null(app.Services.GetService<IAntiforgery>());

        using var action = await PostWithoutAntiforgeryAsync(
            client,
            "tests.antiforgery.default");
        using var bifrost = await client.GetAsync(
            "/__heimdall/v1/bifrost/token?topic=no-antiforgery-services");

        Assert.Equal(HttpStatusCode.OK, action.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bifrost.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostWithoutAntiforgeryAsync(
        HttpClient client,
        string actionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", actionId);
        request.Content = JsonContent.Create(new { });
        return await client.SendAsync(request);
    }
}
