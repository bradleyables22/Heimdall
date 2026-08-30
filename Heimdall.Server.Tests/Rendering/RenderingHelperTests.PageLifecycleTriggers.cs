using Heimdall.Server.Rendering;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    [Theory]
    [InlineData(HeimdallHtml.Trigger.DocumentVisible, "heimdall-content-document-visible", "page.refresh")]
    [InlineData(HeimdallHtml.Trigger.Online, "heimdall-content-online", "connection.restore")]
    public void GenericOn_RendersPageLifecycleTrigger(
        HeimdallHtml.Trigger trigger,
        string expectedAttribute,
        string action)
    {
        var html = Render(Html.Div(HeimdallHtml.On(trigger, action), "Status"));

        Assert.Equal($"<div {expectedAttribute}=\"{action}\">Status</div>", html);
    }

    [Fact]
    public void StaticNamedHelpers_RenderPageLifecycleTriggers()
    {
        var documentVisible = Render(Html.Div(
            HeimdallHtml.OnDocumentVisible("dashboard.refresh"),
            "Dashboard"));
        var online = Render(Html.Div(
            HeimdallHtml.OnOnline("connection.restore"),
            "Connection"));

        Assert.Equal(
            "<div heimdall-content-document-visible=\"dashboard.refresh\">Dashboard</div>",
            documentVisible);
        Assert.Equal(
            "<div heimdall-content-online=\"connection.restore\">Connection</div>",
            online);
    }

    [Fact]
    public void FluentNamedHelpers_RenderBothPageLifecycleNamingStyles()
    {
        var documentVisible = Render(FluentHtml.Fragment(fragment => fragment
            .Div(element => element.Heimdall().OnDocumentVisible("dashboard.explicit"))
            .Div(element => element.Heimdall().DocumentVisible("dashboard.concise"))));
        var online = Render(FluentHtml.Fragment(fragment => fragment
            .Div(element => element.Heimdall().OnOnline("connection.explicit"))
            .Div(element => element.Heimdall().Online("connection.concise"))));

        Assert.Equal(
            "<div heimdall-content-document-visible=\"dashboard.explicit\"></div>" +
            "<div heimdall-content-document-visible=\"dashboard.concise\"></div>",
            documentVisible);
        Assert.Equal(
            "<div heimdall-content-online=\"connection.explicit\"></div>" +
            "<div heimdall-content-online=\"connection.concise\"></div>",
            online);
    }

    [Fact]
    public void PageLifecycleAttributeConstants_AreStable()
    {
        Assert.Equal("heimdall-content-document-visible", HeimdallHtml.Attrs.DocumentVisible);
        Assert.Equal("heimdall-content-online", HeimdallHtml.Attrs.Online);
    }

    [Fact]
    public void PageLifecycleTriggers_AppendWithoutRenumberingExistingTriggerValues()
    {
        Assert.Equal(9, (int)HeimdallHtml.Trigger.Scroll);
        Assert.Equal(10, (int)HeimdallHtml.Trigger.DocumentVisible);
        Assert.Equal(11, (int)HeimdallHtml.Trigger.Online);
    }

    [Fact]
    public void IgnoreHelper_RendersPageLifecycleTriggerTokens()
    {
        var html = Render(Html.Div(
            HeimdallHtml.Ignore(
                HeimdallHtml.Trigger.DocumentVisible,
                HeimdallHtml.Trigger.Online),
            "Boundary"));

        Assert.Equal("<div heimdall-ignore=\"document-visible online\">Boundary</div>", html);
    }
}
