using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    [Fact]
    public void StaticHelpers_ValidateRequiredDirectiveArguments()
    {
        Assert.Throws<ArgumentException>(() => HeimdallHtml.Redirect(" "));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.Invocation(" "));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.JsInvokeVoid(" "));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.JsInvokeVoid("App.toast"));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.JsInvokeVoid("window.App['toast']"));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.SyncGroup(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeimdallHtml.Sync((HeimdallHtml.RequestSync)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => Html.Command((Html.CommandType)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => Html.Capture((Html.CaptureMode)999));
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
                .SyncReplace("save")
                .PayloadFromClosestState("row")
                .DebounceMs(-20)
                .Sse("alerts", "#feed");
        }));
        var fragment = Render(FluentHtml.Fragment(f =>
        {
            f.Heimdall()
                .Abort()
                .Redirect("/next")
                .Invocation("#status", payload: Html.Span("Done"))
                .JsInvokeVoid("window.App.done", "ok");
        }));

        Assert.Contains("heimdall-content-click=\"tests.save\"", button);
        Assert.Contains("heimdall-ignore=\"*\"", button);
        Assert.Contains("heimdall-scope=\"self\"", button);
        Assert.Contains("heimdall-sync=\"replace\"", button);
        Assert.Contains("heimdall-sync-group=\"save\"", button);
        Assert.Contains("heimdall-payload-from=\"closest-state:row\"", button);
        Assert.Contains("heimdall-debounce=\"0\"", button);
        Assert.Contains("heimdall-sse=\"alerts\"", button);
        Assert.Contains("heimdall-sse-target=\"#feed\"", button);
        Assert.Contains("<abort></abort>", fragment);
        Assert.Contains("<redirect url=\"/next\"></redirect>", fragment);
        Assert.Contains("<invocation", fragment);
        Assert.Contains("<span>Done</span>", fragment);
        Assert.Contains("function=\"window.App.done\"", fragment);
        Assert.Contains("args=\"[&quot;ok&quot;]\"", fragment);
    }
}
