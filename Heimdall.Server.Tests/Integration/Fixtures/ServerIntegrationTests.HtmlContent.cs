using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Server.Tests;

public static class TestHtmlContentActions
{
    [ContentInvocation("tests.html.concrete")]
    public static HtmlString Concrete()
        => new("<span>concrete</span>");

    [ContentInvocation("tests.html.task-concrete")]
    public static Task<HtmlString> TaskConcrete()
        => Task.FromResult(new HtmlString("<span>task-concrete</span>"));

    [ContentInvocation("tests.html.valuetask-concrete")]
    public static ValueTask<HtmlContentBuilder> ValueTaskConcrete()
    {
        var content = new HtmlContentBuilder();
        content.AppendHtml("<span>valuetask-concrete</span>");
        return ValueTask.FromResult(content);
    }

    [ContentInvocation("tests.html.async")]
    public static IHtmlAsyncContent Async()
        => new AsyncTestHtmlContent(
            "<span>sync-async-content</span>",
            "<span>async-content</span>");

    [ContentInvocation("tests.html.task-async")]
    public static Task<IHtmlAsyncContent> TaskAsync()
        => Task.FromResult<IHtmlAsyncContent>(new AsyncTestHtmlContent(
            "<span>sync-task-async-content</span>",
            "<span>task-async-content</span>"));
}

public static class TestBlazorContentActions
{
    [ContentInvocation("tests.blazor.component")]
    public static Task<IHtmlContent> Component(
        [FromServices] IHeimdallBlazorRenderer renderer,
        BlazorComponentPayload payload,
        CancellationToken cancellationToken)
        => renderer.RenderAsync<TestStaticBlazorComponent>(
            new { Name = payload.Name },
            cancellationToken);
}

public sealed class BlazorComponentPayload
{
    public string? Name { get; set; }
}

internal sealed class TestStaticBlazorComponent : ComponentBase
{
    [Parameter]
    public string? Name { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "data-name", Name);
        builder.AddContent(2, Name);
        builder.CloseElement();
    }
}
