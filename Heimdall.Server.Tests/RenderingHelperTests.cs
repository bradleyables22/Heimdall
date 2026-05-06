using System.Text.Encodings.Web;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed class RenderingHelperTests
{
    [Fact]
    public void StaticHelpers_RenderIgnoreAndScopeAttributes()
    {
        var html = Render(Html.Div(
            HeimdallHtml.Ignore(HeimdallHtml.Trigger.Click, HeimdallHtml.Trigger.Submit),
            HeimdallHtml.Scope(HeimdallHtml.EventScope.Self),
            "Save"));

        Assert.Contains("heimdall-ignore=\"click submit\"", html);
        Assert.Contains("heimdall-scope=\"self\"", html);
    }

    [Fact]
    public void StaticHelpers_RenderAbortAndRedirectDirectives()
    {
        var html = Render(Html.Fragment(
            HeimdallHtml.Abort("validation-failed"),
            HeimdallHtml.Redirect("/login")));

        Assert.Contains("<abort reason=\"validation-failed\"></abort>", html);
        Assert.Contains("<redirect url=\"/login\"></redirect>", html);
    }

    [Fact]
    public void StaticHelpers_RenderTriggerPayloadStateAndSseAttributes()
    {
        var html = Render(Html.Button(
            HeimdallHtml.OnClick("tests.save"),
            HeimdallHtml.Target("#panel"),
            HeimdallHtml.SwapMode(HeimdallHtml.Swap.Outer),
            HeimdallHtml.Disable(),
            HeimdallHtml.PreventDefault(),
            HeimdallHtml.Payload(new { Id = 7 }),
            HeimdallHtml.State("profile", new { Name = "Ada" }),
            HeimdallHtml.SseTopic("alerts"),
            HeimdallHtml.SseTarget("#feed"),
            HeimdallHtml.SseSwapMode(HeimdallHtml.Swap.BeforeEnd),
            "Save"));

        Assert.Contains("heimdall-content-click=\"tests.save\"", html);
        Assert.Contains("heimdall-content-target=\"#panel\"", html);
        Assert.Contains("heimdall-content-swap=\"outer\"", html);
        Assert.Contains("heimdall-content-disable", html);
        Assert.Contains("heimdall-prevent-default", html);
        Assert.Contains("heimdall-payload=\"{&quot;Id&quot;:7}\"", html);
        Assert.Contains("data-heimdall-state-profile=\"{&quot;Name&quot;:&quot;Ada&quot;}\"", html);
        Assert.Contains("heimdall-sse=\"alerts\"", html);
        Assert.Contains("heimdall-sse-target=\"#feed\"", html);
        Assert.Contains("heimdall-sse-swap=\"beforeend\"", html);
    }

    [Fact]
    public void StaticHelpers_RenderInvocationWithTemplateWrappedPayload()
    {
        var html = Render(HeimdallHtml.Invocation(
            "#drawer",
            HeimdallHtml.Swap.AfterBegin,
            Html.Span("Inserted"),
            wrapInTemplate: true));

        Assert.Contains("<invocation", html);
        Assert.Contains("heimdall-content-target=\"#drawer\"", html);
        Assert.Contains("heimdall-content-swap=\"afterbegin\"", html);
        Assert.Contains("<template><span>Inserted</span></template>", html);
    }

    [Fact]
    public void StaticHelpers_ValidateRequiredDirectiveArguments()
    {
        Assert.Throws<ArgumentException>(() => HeimdallHtml.Redirect(" "));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.Invocation(" "));
    }

    [Fact]
    public void FluentHelpers_RenderElementAndFragmentExtensions()
    {
        var button = Render(FluentHtml.Button(b =>
        {
            b.Text("Save");
            b.Heimdall()
                .OnClick("tests.save")
                .Target("#panel")
                .SwapOuter()
                .Disable()
                .PreventDefault()
                .IgnoreAll()
                .ScopeSelf()
                .PayloadFromClosestState("row")
                .DebounceMs(-20)
                .Sse("alerts", "#feed");
        }));
        var fragment = Render(FluentHtml.Fragment(f =>
        {
            f.Heimdall()
                .Abort()
                .Redirect("/next")
                .Invocation("#status", payload: Html.Span("Done"));
        }));

        Assert.Contains("heimdall-content-click=\"tests.save\"", button);
        Assert.Contains("heimdall-ignore=\"*\"", button);
        Assert.Contains("heimdall-scope=\"self\"", button);
        Assert.Contains("heimdall-payload-from=\"closest-state:row\"", button);
        Assert.Contains("heimdall-debounce=\"0\"", button);
        Assert.Contains("heimdall-sse=\"alerts\"", button);
        Assert.Contains("heimdall-sse-target=\"#feed\"", button);
        Assert.Contains("<abort></abort>", fragment);
        Assert.Contains("<redirect url=\"/next\"></redirect>", fragment);
        Assert.Contains("<invocation", fragment);
        Assert.Contains("<span>Done</span>", fragment);
    }

    private static string Render(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
