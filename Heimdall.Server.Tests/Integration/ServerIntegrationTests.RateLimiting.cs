using System.Net;
using Microsoft.AspNetCore.TestHost;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    [Fact]
    public async Task NativeRateLimitPolicy_AppliesToContentInvocation()
    {
        await using var app = await CreateRateLimitedAppAsync();
        using var client = app.GetTestClient();

        var first = await PostContentActionAsync(client, "tests.rate.limited");
        var second = await PostContentActionAsync(client, "tests.rate.limited");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task NativeDisableRateLimiting_OverridesTypePolicyForContentInvocation()
    {
        await using var app = await CreateRateLimitedAppAsync();
        using var client = app.GetTestClient();

        var first = await PostContentActionAsync(client, "tests.rate.disabled");
        var second = await PostContentActionAsync(client, "tests.rate.disabled");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }
}
