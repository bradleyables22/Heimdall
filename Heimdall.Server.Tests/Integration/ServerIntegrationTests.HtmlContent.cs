using System.Net;
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.TestHost;

namespace Heimdall.Server.Tests;

public sealed partial class ServerIntegrationTests
{
    [Theory]
    [InlineData("tests.html.concrete", "<span>concrete</span>")]
    [InlineData("tests.html.task-concrete", "<span>task-concrete</span>")]
    [InlineData("tests.html.valuetask-concrete", "<span>valuetask-concrete</span>")]
    [InlineData("tests.html.async", "<span>async-content</span>")]
    [InlineData("tests.html.task-async", "<span>task-async-content</span>")]
    public async Task ContentAction_AcceptsConcreteAndAsyncHtmlContentResults(
        string actionId,
        string expectedHtml)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(client, actionId);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedHtml, html);
    }

    [Fact]
    public async Task HeimdallPage_AcceptsConcreteTaskValueTaskAndAsyncHtmlContentResults()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();

        await using var app = builder.Build();
        app.MapHeimdallPage("/task", static () =>
            Task.FromResult(new HtmlString("<span>task-page</span>")));
        app.MapHeimdallPage("/value-task", static () =>
            ValueTask.FromResult(new HtmlString("<span>value-task-page</span>")));
        app.MapHeimdallPage("/async", static () =>
            ValueTask.FromResult<IHtmlAsyncContent>(new AsyncTestHtmlContent(
                "<span>sync-page</span>",
                "<span>async-page</span>")));

        await app.StartAsync();
        using var client = app.GetTestClient();

        var taskResponse = await client.GetAsync("/task");
        var valueTaskResponse = await client.GetAsync("/value-task");
        var asyncResponse = await client.GetAsync("/async");

        Assert.Equal("<span>task-page</span>", await taskResponse.Content.ReadAsStringAsync());
        Assert.Equal("<span>value-task-page</span>", await valueTaskResponse.Content.ReadAsStringAsync());
        Assert.Equal("<span>async-page</span>", await asyncResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ContentAction_CanRenderStaticBlazorComponentThroughAdapter()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddHeimdallBlazor();
        });
        using var client = app.GetTestClient();

        var response = await PostContentActionAsync(
            client,
            "tests.blazor.component",
            new { Name = "Ada" });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<div data-name=\"Ada\">Ada</div>", html);
    }
}
