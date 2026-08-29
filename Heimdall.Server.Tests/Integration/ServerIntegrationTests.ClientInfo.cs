using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heimdall.Server;
using Microsoft.AspNetCore.TestHost;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    [Fact]
    public async Task ContentAction_BindsClientInfoAlongsidePayload()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var header = JsonSerializer.Serialize(new
        {
            timeZone = "America/New_York",
            utcOffsetMinutes = -240,
            locale = "en-US",
            languages = new[] { "en-US", "es" },
            viewportWidth = 390.5,
            viewportHeight = 844.25,
            screenWidth = 390,
            screenHeight = 844,
            devicePixelRatio = 3,
            orientation = "portrait",
            deviceCategory = "mobile",
            colorScheme = "dark",
            prefersReducedMotion = true,
            prefersContrast = "more",
            forcedColors = false,
            touch = true,
            maxTouchPoints = 5,
            pointer = "coarse",
            hover = false,
            online = true
        });

        using var response = await PostClientInfoActionAsync(client, header, new { value = "payload" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "<span>payload|True|America/New_York|-240|en-US|en-US,es|390.5x844.25|" +
            "390x844|3|portrait|mobile|dark|True|more|False|True|5|coarse|False|True</span>",
            body);
    }

    [Fact]
    public async Task ContentAction_ProvidesUnavailableClientInfoWhenHeaderIsMissing()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await PostContentActionAsync(
            client,
            "tests.client-info.echo",
            new { value = "missing" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("<span>missing|False|", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContentAction_NormalizesNullLanguagesFromUntrustedHeader()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await PostClientInfoActionAsync(
            client,
            "{\"locale\":\"en-US\",\"languages\":null}",
            new { value = "null-languages" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("<span>null-languages|True||0|en-US||", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("[]")]
    public async Task ContentAction_RejectsInvalidClientInfo(string header)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        using var response = await PostClientInfoActionAsync(client, header, new { value = "invalid" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(HeimdallClientInfo.HeaderName, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContentAction_RejectsOversizedClientInfo()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var header = new string('x', HeimdallClientInfo.MaxHeaderLength + 1);

        using var response = await PostClientInfoActionAsync(client, header, new { value = "large" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("4096-character limit", body, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> PostClientInfoActionAsync(
        HttpClient client,
        string header,
        object payload)
    {
        var csrf = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__heimdall/v1/content/actions");
        request.Headers.Add("X-Heimdall-Content-Action", "tests.client-info.echo");
        request.Headers.Add("RequestVerificationToken", csrf.RequestToken);
        request.Headers.Add("Cookie", csrf.CookieHeader);
        request.Headers.TryAddWithoutValidation(HeimdallClientInfo.HeaderName, header);
        request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }
}
