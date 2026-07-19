using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed class RenderingHelperTests
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
    public void StaticHelpers_RenderIgnoreAndScopeAttributes()
    {
        var html = Render(Html.Div(
            HeimdallHtml.Ignore(HeimdallHtml.Trigger.Click, HeimdallHtml.Trigger.Submit),
            HeimdallHtml.Scope(HeimdallHtml.EventScope.Self),
            HeimdallHtml.Sync(HeimdallHtml.RequestSync.QueueLatest),
            HeimdallHtml.SyncGroup("checkout"),
            "Save"));

        Assert.Contains("heimdall-ignore=\"click submit\"", html);
        Assert.Contains("heimdall-scope=\"self\"", html);
        Assert.Contains("heimdall-sync=\"queue-latest\"", html);
        Assert.Contains("heimdall-sync-group=\"checkout\"", html);
    }

    [Theory]
    [InlineData(HeimdallHtml.RequestSync.Parallel, "parallel")]
    [InlineData(HeimdallHtml.RequestSync.Replace, "replace")]
    [InlineData(HeimdallHtml.RequestSync.Drop, "drop")]
    [InlineData(HeimdallHtml.RequestSync.QueueLatest, "queue-latest")]
    public void StaticHelpers_RenderRequestSynchronizationStrategies(
        HeimdallHtml.RequestSync strategy,
        string expected)
    {
        var html = Render(Html.Button(HeimdallHtml.Sync(strategy), "Run"));

        Assert.Contains($"heimdall-sync=\"{expected}\"", html);
    }

    [Theory]
    [InlineData(Html.CommandType.toggle_popover, "toggle-popover")]
    [InlineData(Html.CommandType.show_popover, "show-popover")]
    [InlineData(Html.CommandType.hide_popover, "hide-popover")]
    [InlineData(Html.CommandType.close, "close")]
    [InlineData(Html.CommandType.request_close, "request-close")]
    [InlineData(Html.CommandType.show_modal, "show-modal")]
    public void HtmlHelpers_RenderNativeCommandAttributes(
        Html.CommandType command,
        string expected)
    {
        var html = Render(Html.Button(
            Html.CommandFor("confirmation-dialog"),
            Html.Command(command),
            "Run"));

        Assert.Contains("commandfor=\"confirmation-dialog\"", html);
        Assert.Contains($"command=\"{expected}\"", html);
    }

    [Fact]
    public void HtmlHelpers_RenderRawCustomCommandsAcrossStaticAndFluentApis()
    {
        var staticHtml = Render(Html.Button(
            FluentHtml.CommandFor("record-preview"),
            FluentHtml.Command("--archive-record"),
            "Archive"));
        var fluentHtml = Render(FluentHtml.Button(button =>
        {
            button.CommandFor("confirmation-dialog")
                .Command(Html.CommandType.request_close)
                .Text("Cancel");
        }));

        Assert.Contains("commandfor=\"record-preview\"", staticHtml);
        Assert.Contains("command=\"--archive-record\"", staticHtml);
        Assert.Contains("commandfor=\"confirmation-dialog\"", fluentHtml);
        Assert.Contains("command=\"request-close\"", fluentHtml);
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
        Assert.Throws<ArgumentException>(() => HeimdallHtml.JsInvokeVoid(" "));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.JsInvokeVoid("App.toast"));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.JsInvokeVoid("window.App['toast']"));
        Assert.Throws<ArgumentException>(() => HeimdallHtml.SyncGroup(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeimdallHtml.Sync((HeimdallHtml.RequestSync)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => Html.Command((Html.CommandType)999));
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

    private static string Render(IHtmlContent content)
        => content.ToHtmlString();
}
