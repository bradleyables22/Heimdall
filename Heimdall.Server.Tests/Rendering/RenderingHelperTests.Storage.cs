using Heimdall.Server.Rendering;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    [Fact]
    public void StaticHelpers_RenderStorageDirectivesAndEncodeValues()
    {
        var html = Render(Html.Fragment(
            HeimdallHtml.SetStorage(
                HeimdallHtml.StorageArea.Local,
                "ui.theme",
                "<dark>&\""),
            HeimdallHtml.RemoveStorage(
                HeimdallHtml.StorageArea.Session,
                "checkout.state")));

        Assert.Contains(
            "<storage area=\"local\" operation=\"set\" key=\"ui.theme\">&lt;dark&gt;&amp;&quot;"
                + "</storage>",
            html);
        Assert.Contains(
            "<storage area=\"session\" operation=\"remove\" key=\"checkout.state\"></storage>",
            html);
    }

    [Fact]
    public void FluentHelpers_RenderStoragePayloadSourceAndDirectives()
    {
        var html = Render(FluentHtml.Fragment(fragment =>
        {
            fragment.Add(FluentHtml.Button(button => button
                .Heimdall(heimdall => heimdall
                    .Click("checkout.next")
                    .PayloadFromStorage(HeimdallHtml.StorageArea.Session, "checkout.state"))));
            fragment.Heimdall()
                .SetStorage(HeimdallHtml.StorageArea.Local, "ui.theme", "dark")
                .RemoveStorage(HeimdallHtml.StorageArea.Session, "checkout.state");
        }));

        Assert.Contains(
            "heimdall-payload-from=\"session-storage:checkout.state\"",
            html);
        Assert.Contains(
            "<storage area=\"local\" operation=\"set\" key=\"ui.theme\">dark</storage>",
            html);
        Assert.Contains(
            "<storage area=\"session\" operation=\"remove\" key=\"checkout.state\"></storage>",
            html);
    }

    [Fact]
    public void StorageHelpers_RejectMissingKeysAndValues()
    {
        Assert.Throws<ArgumentException>(() =>
            HeimdallHtml.SetStorage(HeimdallHtml.StorageArea.Local, " ", "value"));
        Assert.Throws<ArgumentException>(() =>
            HeimdallHtml.RemoveStorage(HeimdallHtml.StorageArea.Session, ""));
        Assert.Throws<ArgumentNullException>(() =>
            HeimdallHtml.SetStorage(HeimdallHtml.StorageArea.Local, "key", null!));
    }

    [Fact]
    public void StorageHelpers_RejectUnsupportedAreas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HeimdallHtml.SetStorage((HeimdallHtml.StorageArea)99, "key", "value"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HeimdallHtml.PayloadFromStorage((HeimdallHtml.StorageArea)99, "key"));
    }
}
