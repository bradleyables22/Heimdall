using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Tests;

public sealed partial class RenderingHelperTests
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
}
