using Heimdall.Server.Rendering;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
{
    [Fact]
    public void ScopedHeimdallBuilder_ReturnsToElementBuilderAndSupportsRepeatedTransitions()
    {
        var html = Render(FluentHtml.Button(button => button
            .Id("save")
            .Heimdall(heimdall => heimdall
                .Click("orders.save")
                .Target("#order-result")
                .SwapInner())
            .Class("btn", "btn-primary")
            .Text("Save")
            .Heimdall(heimdall => heimdall.Disable())));

        Assert.Equal(
            "<button id=\"save\" " +
            "heimdall-content-click=\"orders.save\" " +
            "heimdall-content-target=\"#order-result\" " +
            "heimdall-content-swap=\"inner\" " +
            "class=\"btn btn-primary\" " +
            "heimdall-content-disable>Save</button>",
            html);
    }

    [Fact]
    public void ScopedHeimdallBuilder_ReturnsToFragmentBuilderWithoutChangingPartOrder()
    {
        var html = Render(FluentHtml.Fragment(fragment => fragment
            .Span(span => span.Text("Before"))
            .Heimdall(heimdall => heimdall.Invocation(
                "#status",
                payload: Html.Span("Updated")))
            .Span(span => span.Text("After"))));

        Assert.Equal(
            "<span>Before</span>" +
            "<invocation heimdall-content-target=\"#status\" heimdall-content-swap=\"inner\">" +
            "<span>Updated</span>" +
            "</invocation>" +
            "<span>After</span>",
            html);
    }

    [Fact]
    public void ScopedHeimdallBuilders_RejectNullCallbacks()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FluentHtml.Div(element => element.Heimdall(null!)));
        Assert.Throws<ArgumentNullException>(() =>
            FluentHtml.Fragment(fragment => fragment.Heimdall(null!)));
    }
}
