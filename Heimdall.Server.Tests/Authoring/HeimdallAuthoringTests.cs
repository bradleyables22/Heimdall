using System.Reflection;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Heimdall.Server.Tests.Authoring;

using Heimdall = global::Heimdall.Server.Rendering.Heimdall;

public sealed class HeimdallAuthoringTests
{
    [Fact]
    public void StandaloneChainsAreImmutableAndKeepCanonicalNames()
    {
        var shared = Heimdall.OnClick("orders.refresh").Target("#shared");
        var first = shared.Target("#first").Payload(new { Id = 1 });
        var second = shared.Target("#second").Payload(new { Id = 2 });

        Assert.Equal("#shared", shared[HeimdallHtml.Attrs.Target]);
        Assert.False(shared.ContainsKey(HeimdallHtml.Attrs.Payload));
        Assert.Equal("#first", first[HeimdallHtml.Attrs.Target]);
        Assert.Equal("#second", second[HeimdallHtml.Attrs.Target]);
        Assert.Equal(1, JsonDocument.Parse((string)first[HeimdallHtml.Attrs.Payload]).RootElement.GetProperty("Id").GetInt32());
        Assert.All(first.Keys, key => Assert.StartsWith("heimdall-", key));
        Assert.Empty(Heimdall.Attributes);
    }

    [Fact]
    public void EveryFluentMethodHasAMatchingStaticEntryPoint()
    {
        foreach (var method in typeof(FluentHeimdall.HeimdallBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(method => method.ReturnType == typeof(FluentHeimdall.HeimdallBuilder)))
        {
            var counterpart = typeof(Heimdall).GetMethod(method.Name, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
            Assert.NotNull(counterpart);
            Assert.Equal(method.ReturnType, counterpart.ReturnType);
            Assert.Equal(method.GetParameters().Select(parameter => parameter.DefaultValue), counterpart.GetParameters().Select(parameter => parameter.DefaultValue));
        }
    }

    [Fact]
    public void EveryFragmentMethodHasAMatchingRenderableEntryPoint()
    {
        foreach (var method in typeof(FluentHeimdall.HeimdallFragmentBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var counterpart = typeof(Heimdall).GetMethod(method.Name, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
            Assert.NotNull(counterpart);
            Assert.Equal(typeof(Microsoft.AspNetCore.Html.IHtmlContent), counterpart.ReturnType);
        }
    }

    [Fact]
    public void ConfigurationCoversTheEntireCanonicalElementAttributeCatalog()
    {
        var configuration = Heimdall.Attributes;
        foreach (var trigger in Enum.GetValues<HeimdallHtml.Trigger>()) configuration = configuration.On(trigger, "test.action");
        configuration = configuration.Target("#target").SwapOuter().Disable().PreventDefault().IgnoreAll().ScopeSelf()
            .SyncReplace("group").Payload(new { Id = 1 }).PayloadFromClosestForm().PayloadRef("app.payload")
            .DebounceMs(300).Key("Enter").HoverDelayMs(50).VisibleOnce().ScrollThresholdPx(120).PollMs(1000)
            .SseTopic("orders").SseTopicAlias("orders").SseTarget("#stream").SseSwap(HeimdallHtml.Swap.Inner)
            .SseEvent("update").SseDisable().State(new { Count = 0 }).State("cart", new { Count = 1 })
            .LocalizeTime(new DateTimeOffset(2026, 8, 26, 18, 30, 5, TimeSpan.Zero), "yyyy-MM-dd");
        var expected = typeof(HeimdallHtml.Attrs).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!)
            .Select(name => name == "data-heimdall-state-" ? name + "cart" : name)
            .Concat(["heimdall-time", "heimdall-time-format"]);
        Assert.Equal(expected.Order(), configuration.Keys.Order());
        Assert.Equal("replace", configuration[HeimdallHtml.Attrs.Sync]);
        Assert.Equal("group", configuration[HeimdallHtml.Attrs.SyncGroup]);
        Assert.IsType<bool>(configuration[HeimdallHtml.Attrs.PreventDefault]);
    }

    [Fact]
    public void ExplicitFalseValuesOverrideEarlierTrueValues()
    {
        var configuration = Heimdall.Disable().Disable(false).PreventDefault().PreventDefault(false)
            .VisibleOnce().VisibleOnce(false).SseDisable().SseDisable(false);
        Assert.All(configuration.Values, value => Assert.Equal("false", value));
        Assert.Equal(4, configuration.Count);
        Assert.Equal("false", HeimdallHtml.PreventDefault(false).Value);
        Assert.Equal("false", HeimdallHtml.VisibleOnce(false).Value);
    }

    [Fact]
    public void PayloadsStateAndPluginHelpersKeepTheirCanonicalEncodingAndValues()
    {
        const string untrusted = "\"><script>alert(1)</script>&";
        var configuration = Heimdall.OnPluginTrigger("company.editor.commit", "orders.save")
            .Payload(new { Text = untrusted }).State("cart", new { Text = untrusted })
            .PayloadFromPlugin("company.editor.document");
        Assert.Equal("orders.save", configuration["heimdall-content-company-editor-commit"]);
        Assert.Equal("plugin:company.editor.document", configuration[HeimdallHtml.Attrs.PayloadFrom]);
        Assert.Contains("script", (string)configuration[HeimdallHtml.Attrs.Payload]);
        Assert.DoesNotContain("<script>", (string)configuration[HeimdallHtml.Attrs.Payload]);
        Assert.Equal(untrusted, JsonDocument.Parse((string)configuration[HeimdallHtml.Attrs.Payload]).RootElement.GetProperty("Text").GetString());
        Assert.Throws<ArgumentException>(() => Heimdall.OnPluginTrigger("bad name", "orders.save"));
        Assert.Throws<ArgumentException>(() => Heimdall.SyncGroup(" "));
    }

    [Fact]
    public void LocalTimeCarriesValidatedAttributesAndSeparateFallbackText()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var time = Heimdall.LocalizeTime(new DateTimeOffset(2026, 8, 26, 18, 30, 5, TimeSpan.FromHours(2)), "MMM d, yyyy HH:mm");
            Assert.Equal("2026-08-26T16:30:05.000Z", time["heimdall-time"]);
            Assert.Equal("Aug 26, 2026 18:30", time.FallbackText);
            Assert.Equal(time.FallbackText, time.Target("#time").FallbackText);
            Assert.Throws<ArgumentException>(() => Heimdall.LocalizeTime(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)));
            Assert.Throws<ArgumentException>(() => Heimdall.LocalizeTime(DateTimeOffset.UtcNow, "O"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task TagHelperAppliesAttributesOnceAndPreservesExplicitMarkupAndContent()
    {
        var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var attributes = new TagHelperAttributeList { { "heimdall", "authoring" }, { HeimdallHtml.Attrs.Target, "#explicit" }, { "class", "button" } };
        var output = new TagHelperOutput("button", attributes, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetContent("Keep me")));
        var helper = new HeimdallTagHelper { Behavior = Heimdall.OnClick("orders.save").Target("#generated").Disable(false) };
        await helper.ProcessAsync(context, output);
        Assert.False(output.Attributes.ContainsName("heimdall"));
        Assert.Equal("#explicit", output.Attributes[HeimdallHtml.Attrs.Target].Value);
        Assert.Equal("false", output.Attributes[HeimdallHtml.Attrs.Disable].Value);
        Assert.Equal("button", output.Attributes["class"].Value);
        Assert.Equal(1, output.Attributes.Count(attribute => attribute.Name == HeimdallHtml.Attrs.Target));
    }

    [Fact]
    public async Task TagHelperEncodesTimeFallbackAndAcceptsNullConfigurations()
    {
        var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "time");
        var output = new TagHelperOutput("span", new TagHelperAttributeList { { "heimdall", "authoring" } },
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetContent(" ")));
        await new HeimdallTagHelper { Behavior = Heimdall.LocalizeTime(DateTimeOffset.UtcNow, "'<script>' yyyy") }.ProcessAsync(context, output);
        var html = output.ToHtmlString();
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("><script>", html);
        Assert.Contains("<script>", WebUtility.HtmlDecode(html));

        var empty = new TagHelperOutput("div", new TagHelperAttributeList { { "heimdall", null } },
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await new HeimdallTagHelper().ProcessAsync(context, empty);
        Assert.Empty(empty.Attributes);
    }

    [Fact]
    public async Task ContentAdaptersRenderFragmentsAndAsyncContentWithoutDoubleEncoding()
    {
        var fragment = Heimdall.Fragment(content => content
            .Invocation("#status", payload: Html.Text("<status>"))
            .Mutate("#state", mutation => mutation.State(new { Count = 1 }))
            .HistoryPush("/orders").SetStorage(HeimdallHtml.StorageArea.Session, "cart", "ready")
            .PluginCommand("company.editor.update", new { Text = "<safe>" })
            .JsInvokeVoidAfter("window.app.done", 42).Abort("<reason>"));
        var html = fragment.ToHtmlString();
        Assert.Equal(html, Heimdall.Markup(fragment).Value);
        Assert.Contains("&lt;status&gt;", html);
        Assert.Contains("<mutation ", html);
        Assert.Contains("<history ", html);
        Assert.Contains("<storage ", html);
        Assert.Contains("<heimdall-command ", html);
        Assert.Contains("<javascript ", html);
        Assert.Contains("&lt;reason&gt;", html);
        var asyncContent = new AsyncTestHtmlContent("<strong>Sync</strong>", "<strong>Async</strong>");
        Assert.Equal("<strong>Async</strong>", (await Heimdall.MarkupAsync(asyncContent)).Value);
        var asyncFragment = await Heimdall.Fragment(async content =>
        {
            await Task.Yield();
            content.Add(Html.Text("<async>"));
        });
        Assert.Equal("&lt;async&gt;", asyncFragment.ToHtmlString());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Heimdall.MarkupAsync(asyncContent, cancellation.Token));
    }
}
