using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Heimdall.Functions.E2E;

public static class DemoInvocations
{
    [ContentInvocation("functions.mvc")]
    public static Task<IHtmlContent> Mvc([FromServices] IHeimdallMvcRenderer renderer)
        => renderer.PartialAsync("~/Views/Shared/_Greeting.cshtml", "MVC <Functions>");

    [ContentInvocation("functions.blazor")]
    public static Task<IHtmlContent> Blazor([FromServices] IHeimdallBlazorRenderer renderer)
        => renderer.RenderAsync<DemoGreeting>(new { Name = "Blazor <Functions>" });

    [ContentInvocation("functions.drop-stream")]
    public static IHtmlContent DropStream([FromServices] DemoStreamControl streams)
    {
        streams.Interrupt();
        return HtmlString.Empty;
    }

    public sealed record CounterPayload(int Count);

    [ContentInvocation("functions.increment")]
    public static IHtmlContent Increment(CounterPayload payload) => Html.Span((payload.Count + 1).ToString());

    [ContentInvocation("functions.upload")]
    [RequestSizeLimit(4096)]
    public static IHtmlContent Upload(IFormFile file) => Html.Span($"Received {file.Length} bytes");

    [ContentInvocation("functions.publish")]
    public static async Task<IHtmlContent> Publish([FromServices] Bifrost bifrost)
    {
        await bifrost.PublishAsync("functions-e2e", Html.Span("Live from Functions"), TimeSpan.FromSeconds(30));
        return HtmlString.Empty;
    }

    [ContentInvocation("functions.directives")]
    public static IHtmlContent Directives() => FluentHtml.Fragment(fragment =>
    {
        fragment.Add(Html.Span("Updated"));
        fragment.Heimdall().Invocation("#notice", HeimdallHtml.Swap.Inner, Html.Span("Notice"));
    });

    [Authorize]
    [ContentInvocation("functions.private")]
    public static IHtmlContent Private() => Html.Span("Private");

    [RequestTimeout(milliseconds: 30)]
    [ContentInvocation("functions.timeout")]
    public static async Task<IHtmlContent> Timeout(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        return HtmlString.Empty;
    }

    [EnableRateLimiting("host-only")]
    [ContentInvocation("functions.rate-limited")]
    public static IHtmlContent RateLimited() => Html.Span("Must not execute");
}
