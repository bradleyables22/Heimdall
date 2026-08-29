using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
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
    public void StaticHelpers_RenderJsInvokeVoidDirective()
    {
        var html = Render(Html.Fragment(
            HeimdallHtml.JsInvokeVoid("window.App.toast", "Saved", new { Id = 7 }),
            HeimdallHtml.JsInvokeVoidBefore("document.body.focus")));

        Assert.Contains("<javascript", html);
        Assert.Contains("function=\"window.App.toast\"", html);
        Assert.Contains("args=\"[&quot;Saved&quot;,{&quot;Id&quot;:7}]\"", html);
        Assert.Contains("timing=\"after\"", html);
        Assert.Contains("function=\"document.body.focus\"", html);
        Assert.Contains("args=\"[]\"", html);
        Assert.Contains("timing=\"before\"", html);
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
    public void DisableFalse_RendersExplicitOverrideForClientTriggerDefaults()
    {
        var html = Render(Html.Button(
            HeimdallHtml.OnClick("tests.repeat"),
            HeimdallHtml.Disable(false),
            "Repeat"));

        Assert.Contains("heimdall-content-disable=\"false\"", html);
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
}
