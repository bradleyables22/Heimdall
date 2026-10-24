using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    [Fact]
    public void ToHtmlString_RendersContentWithDefaultEncoding()
    {
        var content = Html.Div(
            Html.Id("preview"),
            "<script>alert('unsafe')</script>");

        var html = content.ToHtmlString();

        Assert.Equal(
            "<div id=\"preview\">&lt;script&gt;alert(&#x27;unsafe&#x27;)&lt;/script&gt;</div>",
            html);
    }

    [Fact]
    public void ToHtmlString_RejectsNullContent()
    {
        IHtmlContent? content = null;

        Assert.Throws<ArgumentNullException>(() => content!.ToHtmlString());
    }

    [Fact]
    public async Task ToHtmlStringAsync_UsesAsyncHtmlContentWriter()
    {
        var content = new AsyncTestHtmlContent(
            "<span>sync</span>",
            "<span>async</span>");

        var html = await content.ToHtmlStringAsync();

        Assert.Equal("<span>async</span>", html);
    }

    [Fact]
    public async Task ToHtmlStringAsync_FallsBackToSynchronousHtmlContentWriter()
    {
        var content = Html.Div("<script>alert('unsafe')</script>");

        var html = await content.ToHtmlStringAsync();

        Assert.Equal(
            "<div>&lt;script&gt;alert(&#x27;unsafe&#x27;)&lt;/script&gt;</div>",
            html);
    }

    [Fact]
    public async Task ToHtmlStringAsync_RendersNestedAsyncContentInsideElementsAndCollections()
    {
        var content = Html.Div(
            new object?[]
            {
                Html.Text("before"),
                new AsyncTestHtmlContent("sync", "async"),
                Html.Text("after")
            });

        Assert.Equal("<div>beforeasyncafter</div>", await content.ToHtmlStringAsync());
        Assert.Equal("<div>beforesyncafter</div>", content.ToHtmlString());
    }

    [Fact]
    public async Task ToHtmlStringAsync_RejectsNullContent()
    {
        IHtmlContent? content = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => content!.ToHtmlStringAsync().AsTask());
    }

    [Fact]
    public async Task ToHtmlStringAsync_HonorsCancellationBeforeRendering()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new AsyncTestHtmlContent("sync", "async")
                .ToHtmlStringAsync(cancellation.Token)
                .AsTask());
    }
}
